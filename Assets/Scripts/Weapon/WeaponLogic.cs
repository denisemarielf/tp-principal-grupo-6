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
            FireHitscanServerRpc(AimDirection());
        else
        {
            Quaternion fireRotation = spawnPoint.rotation;
            ShootServerRpc(spawnPoint.position, fireRotation);
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
    private void FireHitscanServerRpc(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        Vector3 eye = transform.root.position + Vector3.up * 1.5f;
        const float pullBack = 2.5f;
        Vector3 origin = eye - direction * pullBack;

        Vector3 hitPoint;
        Vector3 hitNormal;
        bool hitEnemy = false;

        if (TryHitscan(origin, direction, eye, out RaycastHit hit))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            hitEnemy = ApplyHitscanDamage(hit.collider);
        }
        else
        {
            hitPoint = eye + direction * hitscanRange;
            hitNormal = -direction;
        }

        PlayHitscanEffectsClientRpc(hitPoint, hitNormal, hitEnemy);
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

    private bool ApplyHitscanDamage(Collider hitCollider)
    {
        EnemyHealth enemyHealth = hitCollider.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null)
            return false;

        float finalDamage = damageAmount;
        PlayerDamageBoost boost = GetComponentInParent<PlayerDamageBoost>();
        if (boost != null) finalDamage *= boost.Multiplier;

        enemyHealth.TakeDamage(finalDamage, transform.root);
        return true;
    }

    [ClientRpc]
    private void PlayHitscanEffectsClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool hitEnemy)
    {
        if (hitEnemy)
        {
            BloodSplatter.Play(hitPoint, hitNormal);
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