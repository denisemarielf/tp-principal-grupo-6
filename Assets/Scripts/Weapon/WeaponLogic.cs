using UnityEngine;
using Unity.Netcode;

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
            Vector3 origin =  spawnPoint.position;
            Vector3 direction = spawnPoint.forward;
            FireHitscanServerRpc(origin, direction);
        }
        else
        {
            Quaternion fireRotation = spawnPoint.rotation;
            ShootServerRpc(spawnPoint.position, fireRotation);
        }
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
    private void FireHitscanServerRpc(Vector3 origin, Vector3 direction)
    {
        Vector3 hitPoint;
        Vector3 hitNormal;
        bool hitEnemy = false;
        NetworkObjectReference enemyRef = default;

        bool dismember = false;
        HumanBodyBones boneToRemove = HumanBodyBones.Head;
        EnemyHealth targetHealth = null;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, hitscanRange, hitscanLayers))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;

            // Se decide ANTES de hacer daño: al morir, el zombie puede despawnearse.
            targetHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            ZombieDismember dism = hit.collider.GetComponentInParent<ZombieDismember>();
            if (dism != null)
                dismember = dism.TryChooseBone(hit.point, hit.distance, closeRangeDismember, out boneToRemove);

            hitEnemy = ApplyHitscanDamage(hit.collider, out enemyRef);
        }
        else
        {
            hitPoint = origin + direction.normalized * hitscanRange;
            hitNormal = -direction;
        }

        PlayHitscanEffectsClientRpc(hitPoint, hitNormal, hitEnemy, enemyRef);

        // Solo si este disparo mato al zombie.
        if (hitEnemy && dismember && (targetHealth == null || targetHealth.IsDead()))
        {
            Vector3 force = direction.normalized * 4f + Vector3.up * 2f;
            DismemberClientRpc(enemyRef, (int)boneToRemove, force);
        }

        PlayShootEffectsClientRpc();
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
    private void PlayHitscanEffectsClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool hitEnemy, NetworkObjectReference enemyRef)
    {
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