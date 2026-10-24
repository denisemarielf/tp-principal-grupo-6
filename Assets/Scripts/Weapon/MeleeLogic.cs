using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeLogic : EquippableWeapon
{
    [Header("Input / HUD")]
    [SerializeField] private Sprite hudIcon;
    [SerializeField] private bool isAutomatic = false; // true = mantener click sigue golpeando

    private bool isEquipped;
    private InputAction attackAction;

    public override Sprite HudIcon => hudIcon;
    public override void SetEquipped(bool equipped) { isEquipped = equipped; }

    public override void OnShoot(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        attackAction = context.action;
        if (context.performed) TryAttack();
    }

    private void Update()
    {
        if (!IsOwner || !isEquipped || PauseMenuUI.IsOpen) return;
        if (isAutomatic && attackAction != null && attackAction.IsPressed())
            TryAttack();
    }
    [Header("Ataque")]
    [Tooltip("Normalmente la Main Camera del jugador (ojos). Desde acá sale el volumen de golpe.")]
    public Transform attackOrigin;
    public int damageAmount = 35;
    public float attackCooldown = 0.7f;
    [Tooltip("Segundos desde que arranca el swing hasta que el golpe 'conecta'. Sincronizalo con tu animación.")]
    public float hitDelay = 0.2f;

    [Header("Volumen de golpe")]
    public float range = 2f;
    [Range(10f, 180f)] public float arcAngle = 90f;
    public LayerMask hitLayers = ~0;       // qué puede golpear
    public LayerMask obstacleLayers = ~0;  // qué bloquea el golpe (paredes)
    public int maxTargets = 3;             // 1 = arma de un solo objetivo

    [Header("Anti-trampa (servidor)")]
    [SerializeField] private float maxOriginOffset = 3f;

    [Header("Efectos")]
    public AudioSource audioSource;
    public AudioClip swingSound;
    public AudioClip hitFleshSound;
    public AudioClip hitSurfaceSound;
    public ParticleSystem surfaceImpactEffect;
    public Animator weaponAnimator;   // animator del viewmodel (trigger "Attack")
    public Animator worldAnimator;    // opcional: lo que ven los otros jugadores
    public WeaponSway weaponSway;     // opcional: reutiliza tu recoil como "golpe"
    public CameraRecoil cameraRecoil; // opcional

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private float nextLocalAttackTime;
    private float nextServerAttackTime;
    private readonly HashSet<EnemyHealth> hitThisSwing = new HashSet<EnemyHealth>();
    private readonly Collider[] overlapBuffer = new Collider[32];

    public bool CanAttack => Time.time >= nextLocalAttackTime;

    // ---------- Lado del dueño ----------

    /// <summary>Llamar desde el input de ataque (solo el dueño). Devuelve true si el ataque salió.</summary>
    public bool TryAttack()
    {
        if (!IsOwner || !isEquipped || PauseMenuUI.IsOpen || !CanAttack) return false;

        nextLocalAttackTime = Time.time + attackCooldown;

        PlaySwingLocal();

        Transform o = attackOrigin != null ? attackOrigin : transform;
        AttackServerRpc(o.position, o.forward);
        return true;
    }

    private void PlaySwingLocal()
    {
        PlaySwingSound();
        if (weaponAnimator != null) weaponAnimator.SetTrigger(AttackHash);
        if (weaponSway != null) weaponSway.AddRecoil();
        cameraRecoil?.AddRecoil(false);
    }

    // ---------- Servidor ----------

    [ServerRpc]
    private void AttackServerRpc(Vector3 origin, Vector3 direction)
    {
        // Cooldown autoritativo (con un poco de tolerancia por latencia/jitter)
        if (Time.time < nextServerAttackTime) return;
        nextServerAttackTime = Time.time + attackCooldown * 0.9f;

        // Sanidad: el origen reportado debe estar cerca del jugador
        if ((origin - transform.root.position).sqrMagnitude > maxOriginOffset * maxOriginOffset) return;
        if (direction.sqrMagnitude < 0.001f) return;

        PlaySwingClientRpc();
        StartCoroutine(HitRoutine(origin, direction.normalized));
    }

    private IEnumerator HitRoutine(Vector3 origin, Vector3 direction)
    {
        if (hitDelay > 0f) yield return new WaitForSeconds(hitDelay);

        hitThisSwing.Clear();
        int count = Physics.OverlapSphereNonAlloc(origin, range, overlapBuffer, hitLayers, QueryTriggerInteraction.Ignore);

        float halfArc = arcAngle * 0.5f;
        bool hitAnything = false;

        for (int i = 0; i < count && hitThisSwing.Count < maxTargets; i++)
        {
            Collider col = overlapBuffer[i];
            if (col.transform.root == transform.root) continue; // no pegarse a uno mismo

            Vector3 closest = col.ClosestPoint(origin);
            Vector3 toTarget = closest - origin;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = direction;

            // Dentro del arco frontal
            if (Vector3.Angle(direction, toTarget) > halfArc) continue;

            // Línea de visión (que no haya una pared en el medio)
            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit block, toTarget.magnitude + 0.05f,
                    obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (block.collider.transform.root != col.transform.root) continue;
            }

            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null || !hitThisSwing.Add(enemy)) continue;

            enemy.TakeDamage(damageAmount, transform.root);
            hitAnything = true;

            Vector3 normal = (origin - closest).sqrMagnitude > 0.0001f ? (origin - closest).normalized : -direction;
            PlayHitClientRpc(closest, normal, true);
        }

        // Si no pegó a ningún enemigo, feedback de impacto contra la superficie
        if (!hitAnything &&
            Physics.Raycast(origin, direction, out RaycastHit surface, range, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            PlayHitClientRpc(surface.point, surface.normal, false);
        }
    }

    // ---------- Efectos en clientes ----------

    [ClientRpc]
    private void PlaySwingClientRpc()
    {
        if (IsOwner) return; // el dueño ya lo reprodujo local
        PlaySwingSound();
        if (worldAnimator != null) worldAnimator.SetTrigger(AttackHash);
    }

    [ClientRpc]
    private void PlayHitClientRpc(Vector3 point, Vector3 normal, bool hitEnemy)
    {
        if (hitEnemy)
        {
            BloodSplatter.Play(point, normal);
            PlayClipAt(hitFleshSound, point);
        }
        else
        {
            if (surfaceImpactEffect != null)
            {
                ParticleSystem fx = Instantiate(surfaceImpactEffect, point, Quaternion.LookRotation(normal));
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }
            PlayClipAt(hitSurfaceSound, point);
        }
    }

    private void PlaySwingSound()
    {
        if (audioSource != null && swingSound != null)
            audioSource.PlayOneShot(swingSound);
    }

    private void PlayClipAt(AudioClip clip, Vector3 point)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, point);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform o = attackOrigin != null ? attackOrigin : transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(o.position, range);
        Vector3 l = Quaternion.AngleAxis(-arcAngle * 0.5f, o.up) * o.forward * range;
        Vector3 r = Quaternion.AngleAxis(arcAngle * 0.5f, o.up) * o.forward * range;
        Gizmos.DrawLine(o.position, o.position + l);
        Gizmos.DrawLine(o.position, o.position + r);
    }
#endif
}
