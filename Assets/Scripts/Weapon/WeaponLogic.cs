using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class WeaponLogic : NetworkBehaviour
{
    public enum ProjectileType { Physical, Hitscan }

    [Header("Weapon")]
    public ProjectileType projectileType = ProjectileType.Hitscan; // toggle por arma
    public Transform spawnPoint;
    public GameObject bullet; // solo se usa si projectileType es Physical
    public float shootForce = 1500f;
    public int damageAmount = 20;
    

    [Header("Hitscan")]
    public float hitscanRange = 100f;
    public LayerMask hitscanLayers = ~0; // qué puede golpear el rayo (configurable por arma)
    public ParticleSystem hitscanImpactEffect;
    [SerializeField] private float headshotMargin = 0.3f;

    [Header("Tracer")]
    [SerializeField] private BulletTracer bulletTracer; // solo se usa si projectileType es Hitscan
    bool killed = false;

    [Header("Effects")]
    public AudioSource audioSource;
    public AudioClip shootSound;
    public ParticleSystem muzzleFlash;
    public WeaponSway weaponSway;

    [Header("Camera Recoil (solo afecta al dueño del arma)")]
    public CameraRecoil cameraRecoil;
    [SerializeField] private CrosshairController crosshairController;
    private WeaponWorldLink worldLink;
    [SerializeField] private bool closeRangeDismember = false;

    private void Start()
    {
        bulletTracer?.initializeTracers();
    }
    private void Awake()
    {
        worldLink = GetComponent<WeaponWorldLink>();

        if (crosshairController == null)
            crosshairController = transform.root.GetComponentInChildren<CrosshairController>();
    }
    private Vector3 VisualMuzzlePosition =>
    (!IsOwner && worldLink != null && worldLink.worldMuzzle != null)
        ? worldLink.worldMuzzle.position
        : spawnPoint.position;

    private void PlayRemoteShootEffects()
    {
        if (audioSource != null && shootSound != null)
            audioSource.PlayOneShot(shootSound);

        if (worldLink != null)
            worldLink.PlayWorldMuzzle();
    }

    public void PlayShootEffectsLocal()
    {
        if (audioSource != null && shootSound != null)
            audioSource.PlayOneShot(shootSound);

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }

        if (weaponSway != null)
            weaponSway.AddRecoil();
        crosshairController?.NotifyShotFired();
    }

    public void ApplyCameraRecoil(bool isAiming = false)
    {
        cameraRecoil?.AddRecoil(isAiming);
    }

    /// <summary>Pide al servidor que dispare de verdad. Llamado por ShootLogic cuando ya validó cooldown/munición.</summary>
    public void RequestFire()
    {
        if (projectileType == ProjectileType.Hitscan)
        {
            GetAim(out Vector3 camPos, out Vector3 camDir);
            FireHitscanServerRpc(camPos, camDir);
        }
        else
        {
            GetAim(out Vector3 camPos, out Vector3 camDir);
            Vector3 target = camPos + camDir * hitscanRange;
            if (Physics.Raycast(camPos, camDir, out RaycastHit h, hitscanRange, hitscanLayers, QueryTriggerInteraction.Ignore))
                target = h.point;

            Quaternion fireRotation = Quaternion.LookRotation((target - spawnPoint.position).normalized);
            ShootServerRpc(spawnPoint.position, fireRotation);
        }
    }
    private void GetAim(out Vector3 pos, out Vector3 dir)
    {
        Camera cam = IsOwner ? Camera.main : null;
        if (cam != null)
        {
            pos = cam.transform.position;
            dir = cam.transform.forward;
        }
        else
        {
            pos = spawnPoint.position;
            dir = spawnPoint.forward;
        }
    }


    // La mira manda, no la punta del cañón. Si el rayo saliera del cañón, un zombie pegado al cuerpo queda atrás y no recibe el tiro.
    private Vector3 AimDirection()
    {
        if (IsOwner)
        {
            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform.forward;
        }

        return spawnPoint != null ? spawnPoint.forward : transform.forward;
    }
private readonly Dictionary<EnemyHealth, Transform> headCache = new Dictionary<EnemyHealth, Transform>();

private Transform FindHead(EnemyHealth health)
{
    if (headCache.TryGetValue(health, out Transform cached) && cached != null)
        return cached;

    // Evita que el cache crezca con zombies ya destruidos
    if (headCache.Count > 64)
        headCache.Clear();

    Transform head = null;

    // 1) Cualquier Animator humanoide dentro del zombie
    foreach (Animator a in health.GetComponentsInChildren<Animator>(true))
    {
        if (!a.isHuman) continue;
        head = a.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) break;
    }

    // 2) Si no hay, por nombre del hueso (el primero que aparece es "Head", antes que "HeadTop_End")
    if (head == null)
    {
        foreach (Transform t in health.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant().Contains("head"))
            {
                head = t;
                break;
            }
        }
    }

    if (head != null)
        headCache[health] = head;

    return head;
}

private bool IsHeadshot(RaycastHit hit, EnemyHealth health)
{
    Transform head = FindHead(health);
    if (head == null)
    {
        Debug.Log("[Headshot] no se encontró ningún hueso de cabeza");
        return false;
    }

    // Colliders por hueso (ragdoll): alcanza con esto.
    if (hit.collider.transform.IsChildOf(head)) return true;

    // Collider único: el impacto queda en la superficie, así que se compara solo la altura.
    float height = hit.point.y - head.position.y;
   
    return height > -headshotMargin;
}
    // ---------- Proyectil físico (Bullet.cs) ----------

    [ServerRpc]
    private void ShootServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject newBullet = Instantiate(bullet, position, rotation);

        Bullet bulletScript = newBullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.setDamageAmount(damageAmount);
            bulletScript.SetShooter(NetworkObject.transform);
        }

        NetworkObject netObj = newBullet.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        Rigidbody rb = newBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(rotation * Vector3.forward * shootForce);
        }
        Destroy(newBullet, 3);

        PlayShootEffectsClientRpc();
    }

    // ---------- Hitscan (raycast) ----------

  [ServerRpc]
private void FireHitscanServerRpc(Vector3 camPos, Vector3 direction)
{
    if (direction.sqrMagnitude < 0.0001f)
        direction = transform.forward;
    direction.Normalize();

    Vector3 rootEye = transform.root.position + Vector3.up * 1.5f;
    Vector3 eye = (camPos - rootEye).sqrMagnitude > 16f ? rootEye : camPos;

    const float pullBack = 2.5f;
    Vector3 origin = eye - direction * pullBack;

    Vector3 hitPoint;
    Vector3 hitNormal;
    bool hitEnemy = false;
    bool headshot = false;  
    NetworkObjectReference enemyRef = default;

    bool dismember = false;
    HumanBodyBones boneToRemove = HumanBodyBones.Head;
    EnemyHealth targetHealth = null;

    if (TryHitscan(origin, direction, eye, out RaycastHit hit))
    {
        hitPoint = hit.point;
        hitNormal = hit.normal;

        targetHealth = hit.collider.GetComponentInParent<EnemyHealth>();
        ZombieDismember dism = hit.collider.GetComponentInParent<ZombieDismember>();
        if (dism != null)
            dismember = dism.TryChooseBone(hit.point, hit.distance, closeRangeDismember, out boneToRemove);

       bool wasDead = targetHealth != null && targetHealth.IsDead();
headshot = targetHealth != null && !wasDead && IsHeadshot(hit, targetHealth);   // nuevo
hitEnemy = ApplyHitscanDamage(hit.collider, out enemyRef);
killed = hitEnemy && targetHealth != null && !wasDead && targetHealth.IsDead();
    }
    else
    {
        hitPoint = eye + direction * hitscanRange;
        hitNormal = -direction;
    }

    PlayHitscanEffectsClientRpc(hitPoint, hitNormal, hitEnemy, enemyRef, killed, hitEnemy && headshot);

    // Solo si este disparo mató al zombie.
    if (hitEnemy && dismember && (targetHealth == null || targetHealth.IsDead()))
    {
        Vector3 force = direction * 4f + Vector3.up * 2f;
        DismemberClientRpc(enemyRef, (int)boneToRemove, force);
    }

    PlayShootEffectsClientRpc();
}

    // Ignora al que dispara y lo que quedó detrás de su cabeza. Así el zombie que está encima entra en el tiro.
    private bool TryHitscan(Vector3 origin, Vector3 direction, Vector3 eye, out RaycastHit chosen)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, hitscanRange + 2.5f, hitscanLayers, QueryTriggerInteraction.Ignore);
        Transform shooter = transform.root;
        float best = float.PositiveInfinity;
        chosen = default;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == shooter || hit.transform.IsChildOf(shooter))
                continue;
            if (Vector3.Dot(hit.point - eye, direction) < 0.05f)
                continue;
            if (hit.distance >= best)
                continue;

            best = hit.distance;
            chosen = hit;
            found = true;
        }

        return found;
    }

    [ClientRpc]
    private void DismemberClientRpc(NetworkObjectReference enemyRef, int bone, Vector3 force)
    {
        if (!enemyRef.TryGet(out NetworkObject enemyObj)) return;

        ZombieDismember dism = enemyObj.GetComponentInChildren<ZombieDismember>();
        if (dism != null)
            dism.Dismember((HumanBodyBones)bone, force);
    }

    private bool ApplyHitscanDamage(Collider hitCollider, out NetworkObjectReference enemyRef)
    {
        enemyRef = default;

        EnemyHealth enemyHealth = hitCollider.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null)
            return false;

        // La referencia se crea ANTES de hacer daño: si el zombie muere y se despawnea,
        // crearla despues tira una excepcion.
        NetworkObject enemyObj = enemyHealth.GetComponentInParent<NetworkObject>();
        if (enemyObj != null && enemyObj.IsSpawned)
            enemyRef = enemyObj;

        float finalDamage = damageAmount;
        PlayerDamageBoost boost = GetComponentInParent<PlayerDamageBoost>();
        if (boost != null) finalDamage *= boost.Multiplier;

        enemyHealth.TakeDamage(finalDamage, transform.root);
        return true;
    }

    [ClientRpc]
private void PlayHitscanEffectsClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool hitEnemy, NetworkObjectReference enemyRef, bool killed, bool headshot)

{
    if (IsOwner && hitEnemy)
         crosshairController?.NotifyHit(killed, headshot);

    if (hitEnemy)
    {
        Transform woundParent = null;
        if (enemyRef.TryGet(out NetworkObject enemyObj))
            woundParent = enemyObj.transform;

        BloodSplatter.Play(hitPoint, hitNormal, woundParent);
    }
    else if (hitscanImpactEffect != null)
    {
        ParticleSystem sparks = Instantiate(
            hitscanImpactEffect,
            hitPoint,
            Quaternion.LookRotation(hitNormal)
        );
        sparks.Play();
        Destroy(sparks.gameObject, sparks.main.duration + sparks.main.startLifetime.constantMax);
    }

    bulletTracer?.PlayTracer(VisualMuzzlePosition, hitPoint);
    
}
    // ---------- Efectos compartidos ----------

    [ClientRpc]
    private void PlayShootEffectsClientRpc()
    {
        if (IsOwner) return;
        PlayRemoteShootEffects();
    }
}
