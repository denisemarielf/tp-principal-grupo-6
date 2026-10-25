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
                                                       // AHORA
    [Header("Sonidos (se elige uno al azar por acción)")]
    public AudioClip[] swingSounds;
    public AudioClip[] hitFleshSounds;
    public AudioClip[] hitSurfaceSounds;
    [Tooltip("Variación aleatoria de pitch en el sonido del swing (0 = sin variación).")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;
    private int lastSwingIdx = -1, lastFleshIdx = -1, lastSurfaceIdx = -1;
    private bool isEquipped;
    private InputAction attackAction;
    public ProceduralMeleeAnimation proceduralAnimation; // opcional: armas sin animaciones
    private bool attackLocked;
    private bool attackStarted;
    private float attackLockTime;
    public override Sprite HudIcon => hudIcon;
    public override void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
        attackLocked = false;
    }

    public override void OnShoot(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        attackAction = context.action;
        if (context.performed) TryAttack();
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

    public bool CanAttack => Time.time >= nextLocalAttackTime && !attackLocked;

    // ---------- Lado del dueño ----------

    /// <summary>Llamar desde el input de ataque (solo el dueño). Devuelve true si el ataque salió.</summary>
    private void Update()
    {
        if (!IsOwner) return;
        UpdateAttackLock();

        if (!isEquipped || PauseMenuUI.IsOpen) return;
        if (isAutomatic && attackAction != null && attackAction.IsPressed())
            TryAttack();
    }
    public bool TryAttack()
    {
        if (!IsOwner || !isEquipped || PauseMenuUI.IsOpen || !CanAttack) return false;

        nextLocalAttackTime = Time.time + attackCooldown;
        if (weaponAnimator != null)
        {
            attackLocked = true;
            attackStarted = false;
            attackLockTime = Time.time;
        }

        PlaySwingLocal();

        Transform o = attackOrigin != null ? attackOrigin : transform;
        AttackServerRpc(o.position, o.forward);
        return true;
    }

    private void UpdateAttackLock()
    {
        if (!attackLocked) return;

        if (weaponAnimator == null || !weaponAnimator.isActiveAndEnabled)
        {
            attackLocked = false;
            return;
        }

        AnimatorStateInfo cur = weaponAnimator.GetCurrentAnimatorStateInfo(0);
        bool inTransition = weaponAnimator.IsInTransition(0);
        bool curIsAttack = cur.IsTag("Attack");
        bool nextIsAttack = inTransition && weaponAnimator.GetNextAnimatorStateInfo(0).IsTag("Attack");

        // Esperar a que el animator realmente entre al estado de ataque
        if (!attackStarted)
        {
            if (curIsAttack || nextIsAttack) attackStarted = true;
            else if (Time.time - attackLockTime > 0.5f) attackLocked = false; // seguro por si nunca entra
            return;
        }

        // Se libera cuando el ataque terminó o empezó la transición de salida hacia idle
        if (!curIsAttack || (inTransition && !nextIsAttack))
            attackLocked = false;
    }
    private void PlaySwingLocal()
    {
        PlaySwingSound();
        if (weaponAnimator != null)
        {
            weaponAnimator.SetInteger("AttackIndex", PickAttackIndex());
            weaponAnimator.SetTrigger(AttackHash);
        }
        if (proceduralAnimation != null) proceduralAnimation.PlaySwing(); // <-- volver a agregar
        if (weaponSway != null) weaponSway.AddRecoil();
        cameraRecoil?.AddRecoil(false);
    }
    private static readonly int[] attackGroups = { 0, 0, 0, 1, 2, 2 };
    private int lastAttackGroup = -1;

    private int PickAttackIndex()
    {
        int idx;
        int tries = 0;
        do
        {
            idx = Random.Range(0, attackGroups.Length);
            tries++;
        } while (attackGroups[idx] == lastAttackGroup && tries < 20);

        lastAttackGroup = attackGroups[idx];
        return idx;
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

            // Primero: ¿es un enemigo? Si no, ni gastamos tiempo en el resto
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null || hitThisSwing.Contains(enemy)) continue;

            Vector3 closest = SafeClosestPoint(col, origin);
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

            hitThisSwing.Add(enemy);
            enemy.TakeDamage(damageAmount, transform.root);
            hitAnything = true;

            Vector3 normal = (origin - closest).sqrMagnitude > 0.0001f ? (origin - closest).normalized : -direction;
            PlayHitClientRpc(closest, normal, true);
        }
    }
    private static Vector3 SafeClosestPoint(Collider col, Vector3 point)
    {
        if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
            return col.ClosestPoint(point);

        if (col is MeshCollider mc && mc.convex)
            return col.ClosestPoint(point);

        return col.bounds.ClosestPoint(point);
    }

    // ---------- Efectos en clientes ----------

    [ClientRpc]
    private void PlaySwingClientRpc()
    {
        if (IsOwner) return; // el dueño ya lo reprodujo local
        PlaySwingSound();
        if (worldAnimator != null) worldAnimator.SetTrigger(AttackHash);
        if (proceduralAnimation != null) proceduralAnimation.PlaySwing(); // <-- volver a agregar
    }

    [ClientRpc]
    private void PlayHitClientRpc(Vector3 point, Vector3 normal, bool hitEnemy)
    {
        if (hitEnemy)
        {
            BloodSplatter.Play(point, normal);
            PlayClipAt(PickClip(hitFleshSounds, ref lastFleshIdx), point);   // <-- cambió
        }
        else
        {
            if (surfaceImpactEffect != null)
            {
                ParticleSystem fx = Instantiate(surfaceImpactEffect, point, Quaternion.LookRotation(normal));
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }
            PlayClipAt(PickClip(hitSurfaceSounds, ref lastSurfaceIdx), point); // <-- cambió
        }
    }

    private void PlaySwingSound()
    {
        AudioClip clip = PickClip(swingSounds, ref lastSwingIdx);
        if (audioSource == null || clip == null) return;

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip);
    }

    private void PlayClipAt(AudioClip clip, Vector3 point)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, point);
    }

    /// <summary>Elige un clip al azar evitando repetir el anterior.</summary>
    private static AudioClip PickClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) { lastIndex = 0; return clips[0]; }

        int i;
        do { i = Random.Range(0, clips.Length); } while (i == lastIndex);
        lastIndex = i;
        return clips[i];
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
