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
    [Tooltip("Daño TOTAL del disparo si todos los perdigones impactan. Se reparte entre pelletCount.")]
    public int damageAmount = 20;

    [Header("Pellets (escopetas)")]
    [Tooltip("1 = arma normal. >1 = escopeta: cada disparo lanza N perdigones dentro del cono de dispersión.")]
    [SerializeField] private int pelletCount = 1;

    [Header("Caída de daño por distancia")]
    [SerializeField] private bool useDamageFalloff = false;
    [SerializeField] private float falloffStart = 15f;
    [SerializeField] private float falloffEnd = 40f;
    [Range(0f, 1f)]
    [SerializeField] private float minDamageMultiplier = 0.3f;

    [Header("Hitscan")]
    public float hitscanRange = 100f;
    public LayerMask hitscanLayers = ~0; // qué puede golpear el rayo (configurable por arma)
    public ParticleSystem hitscanImpactEffect;
    [SerializeField] private float headshotMargin = 0.3f;
    [Tooltip("Multiplicador de daño por perdigón (o bala) que impacta en la cabeza.")]
    [SerializeField] private float headshotDamageMultiplier = 2f;

    [Header("Tracer")]
    [SerializeField] private BulletTracer bulletTracer; // solo se usa si projectileType es Hitscan

    [Header("Effects")]
    public AudioSource audioSource;
    public AudioClip shootSound;
    public ParticleSystem muzzleFlash;
    public WeaponSway weaponSway;

    [Header("Camera Recoil (solo afecta al dueño del arma)")]
    public CameraRecoil cameraRecoil;
    [SerializeField] private CrosshairController crosshairController;
    private WeaponWorldLink worldLink;
    private WeaponSpread weaponSpread; // opcional: sin él el arma es 100% precisa
    [SerializeField] private bool closeRangeDismember = false;

    public WeaponSpread Spread => weaponSpread;

    private void Start()
    {
        bulletTracer?.initializeTracers();
    }

    private void Awake()
    {
        worldLink = GetComponent<WeaponWorldLink>();
        weaponSpread = GetComponent<WeaponSpread>();

        if (crosshairController == null)
            crosshairController = transform.root.GetComponentInChildren<CrosshairController>();
    }

    public override void OnNetworkSpawn()
    {
        // La dispersión solo se calcula en el dueño; en los demás no hace falta.
        if (weaponSpread != null)
            weaponSpread.enabled = IsOwner;
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

    /// <summary>Pasa la dispersión actual a la mira. Lo llama ShootLogic.Update (solo arma equipada del dueño).</summary>
    public void UpdateCrosshairSpread()
    {
        if (weaponSpread != null && crosshairController != null)
            crosshairController.SetSpread(weaponSpread.CurrentSpread);
    }

    /// <summary>Pide al servidor que dispare de verdad. Llamado por ShootLogic cuando ya validó cooldown/munición.</summary>
    public void RequestFire()
    {
        // Se lee la dispersión ANTES de registrar el disparo (el bloom cuenta desde el siguiente).
        float spreadDeg = weaponSpread != null ? weaponSpread.CurrentSpread : 0f;

        GetAim(out Vector3 camPos, out Vector3 camDir);

        if (projectileType == ProjectileType.Hitscan)
        {
            FireHitscanServerRpc(camPos, camDir, spreadDeg);
        }
        else
        {
            Vector3 target = camPos + camDir * hitscanRange;
            if (Physics.Raycast(camPos, camDir, out RaycastHit h, hitscanRange, hitscanLayers, QueryTriggerInteraction.Ignore))
                target = h.point;

            Quaternion fireRotation = Quaternion.LookRotation((target - spawnPoint.position).normalized);
            ShootServerRpc(spawnPoint.position, fireRotation, spreadDeg);
        }

        if (weaponSpread != null)
            weaponSpread.RegisterShot();
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

    // El servidor no confía ciegamente en la dispersión que manda el cliente.
    private float ClampSpread(float spreadDeg)
    {
        float max = weaponSpread != null ? weaponSpread.MaxSpread : 0f;
        return Mathf.Clamp(spreadDeg, 0f, max);
    }

    // damageAmount es el daño total del disparo; cada perdigón lleva su parte.
    // Así los powerups que modifican DamageAmount siguen funcionando igual.
    private float DamagePerPellet => damageAmount / (float)Mathf.Max(1, pelletCount);

    private float DamageMultiplierAt(float distance)
    {
        if (!useDamageFalloff || falloffEnd <= falloffStart) return 1f;
        float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
        return Mathf.Lerp(1f, minDamageMultiplier, t);
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
    private void ShootServerRpc(Vector3 position, Quaternion rotation, float spreadDeg)
    {
        spreadDeg = ClampSpread(spreadDeg);
        var rng = new System.Random();
        Vector3 baseDir = rotation * Vector3.forward;
        int pellets = Mathf.Max(1, pelletCount);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = WeaponSpread.ApplySpread(baseDir, spreadDeg, rng);
            Quaternion pelletRot = Quaternion.LookRotation(dir);

            GameObject newBullet = Instantiate(bullet, position, pelletRot);

            Bullet bulletScript = newBullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.setDamageAmount(Mathf.Max(1, Mathf.RoundToInt(DamagePerPellet)));
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
                rb.AddForce(dir * shootForce);
            }
            Destroy(newBullet, 3);
        }

        PlayShootEffectsClientRpc();
    }

    // ---------- Hitscan (raycast) ----------

    private struct PelletResult
    {
        public Vector3 dir;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public bool hitEnemy;
        public bool killed;
        public bool headshot;
        public bool dismember;
        public HumanBodyBones bone;
        public NetworkObjectReference enemyRef;
    }

    [ServerRpc]
    private void FireHitscanServerRpc(Vector3 camPos, Vector3 direction, float spreadDeg)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        spreadDeg = ClampSpread(spreadDeg);

        Vector3 rootEye = transform.root.position + Vector3.up * 1.5f;
        Vector3 eye = (camPos - rootEye).sqrMagnitude > 16f ? rootEye : camPos;

        int pellets = Mathf.Max(1, pelletCount);
        var rng = new System.Random();
        var results = new PelletResult[pellets];

        bool anyKill = false, anyHead = false, anyHit = false;
        int notifyIndex = -1;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 pelletDir = WeaponSpread.ApplySpread(direction, spreadDeg, rng);
            results[i] = FirePellet(eye, pelletDir);

            anyHit |= results[i].hitEnemy;
            anyKill |= results[i].killed;
            anyHead |= results[i].hitEnemy && results[i].headshot;

            // El hitmarker lo dispara un solo perdigón: prioridad kill > headshot > hit.
            if (notifyIndex < 0 && results[i].killed) notifyIndex = i;
        }
        if (notifyIndex < 0)
        {
            for (int i = 0; i < pellets; i++)
                if (results[i].hitEnemy && results[i].headshot) { notifyIndex = i; break; }
        }
        if (notifyIndex < 0)
        {
            for (int i = 0; i < pellets; i++)
                if (results[i].hitEnemy) { notifyIndex = i; break; }
        }

        for (int i = 0; i < pellets; i++)
        {
            PelletResult r = results[i];
            bool notify = i == notifyIndex;

            PlayHitscanEffectsClientRpc(
                r.hitPoint, r.hitNormal, r.hitEnemy, r.enemyRef,
                notify, notify && anyKill, notify && anyHead);

            // Solo si este perdigón fue el que mató al zombie.
            if (r.hitEnemy && r.dismember && r.killed)
            {
                Vector3 force = r.dir * 4f + Vector3.up * 2f;
                DismemberClientRpc(r.enemyRef, (int)r.bone, force);
            }
        }

        PlayShootEffectsClientRpc();
    }

    /// <summary>Resuelve un perdigón (o la bala única): raycast, daño, headshot, desmembramiento.</summary>
    private PelletResult FirePellet(Vector3 eye, Vector3 dir)
    {
        PelletResult r = default;
        r.dir = dir;

        const float pullBack = 2.5f;
        Vector3 origin = eye - dir * pullBack;

        if (TryHitscan(origin, dir, eye, out RaycastHit hit))
        {
            r.hitPoint = hit.point;
            r.hitNormal = hit.normal;

            EnemyHealth targetHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            ZombieDismember dism = hit.collider.GetComponentInParent<ZombieDismember>();
            if (dism != null)
                r.dismember = dism.TryChooseBone(hit.point, hit.distance, closeRangeDismember, out r.bone);

            bool wasDead = targetHealth != null && targetHealth.IsDead();
            r.headshot = targetHealth != null && !wasDead && IsHeadshot(hit, targetHealth);
            r.hitEnemy = ApplyHitscanDamage(hit.collider, hit.distance, r.headshot, out r.enemyRef);
            r.killed = r.hitEnemy && targetHealth != null && !wasDead && targetHealth.IsDead();
        }
        else
        {
            r.hitPoint = eye + dir * hitscanRange;
            r.hitNormal = -dir;
        }

        return r;
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

    private bool ApplyHitscanDamage(Collider hitCollider, float distance, bool headshot, out NetworkObjectReference enemyRef)
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

        float finalDamage = DamagePerPellet * DamageMultiplierAt(distance);
        if (headshot) finalDamage *= headshotDamageMultiplier;
        PlayerDamageBoost boost = GetComponentInParent<PlayerDamageBoost>();
        if (boost != null) finalDamage *= boost.Multiplier;

        enemyHealth.TakeDamage(finalDamage, transform.root);
        return true;
    }

    [ClientRpc]
    private void PlayHitscanEffectsClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool hitEnemy, NetworkObjectReference enemyRef, bool notifyHit, bool killed, bool headshot)
    {
        if (IsOwner && hitEnemy && notifyHit)
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
