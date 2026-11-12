using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemyCombat : NetworkBehaviour
{
    [Header("Ataque")]
    public float attackDamage = 15f;
    public float attackRange = 1.5f;
    public float attackDuration = 1.2f; // solo de respaldo: la duracion real es la del clip de ataque elegido

    [Header("Alcance vertical")]
    [Tooltip("Diferencia maxima de altura (pies del zombie vs pies del objetivo) para poder golpear.")]
    public float maxVerticalReach = 1.0f;

    [Header("Cono frontal")]
    [Tooltip("El golpe solo conecta si el objetivo esta enfrente. 1 = exactamente de frente, 0.5 = cono de ~120 grados, 0 = semicirculo, -1 = desactivado.")]
    [Range(-1f, 1f)] public float frontDotThreshold = 0.5f;

    [Header("Mordida")]
    [Range(0f, 1f)] public float biteChance = 0.25f;
    [Tooltip("Distancia maxima a la que puede agarrar y morder. Si el jugador esta mas lejos pero dentro de attackRange, hace un golpe normal.")]
    public float biteRange = 1.0f;
    public float biteDamage = 15f;                     // dano de la mordida (una sola vez por mordida). 0 = solo inmoviliza

    private bool difficultyApplied;

    [Header("Empuje al recibir disparos")]
    public float knockbackSpeed = 5f;        // 0 = sin empuje. Distancia aprox = velocidad x duracion / 2
    public float knockbackDuration = 0.18f;
    public float knockbackCooldown = 0.4f;   // pausa entre empujes: con fuego automatico no lo clava en el lugar
    private Vector3 knockbackDirection;
    private float knockbackStart;
    private float knockbackUntil;
    private float nextKnockbackTime;

    private float attackEndTime;
    private float stunUntil;
    private bool biteCycle;
    private bool biteDamageDealt;
    private PMovement frozenTarget;
    private bool IsCommitted => Time.time < attackEndTime;


    // Aturdido por la reaccion al golpe: no se mueve mientras dura el clip.
    public bool IsStunned => Time.time < stunUntil;
    // Atacando (clip completo) o aturdido: ni Ai ni nadie lo mueve.
    public bool IsKnockedBack => Time.time < knockbackUntil;
    public bool IsBusy => IsStunned || IsCommitted || IsKnockedBack;


    [Header("Linea de vision")]
    [Tooltip("Capas que bloquean el ataque (paredes/entorno). Idealmente NO incluir jugadores ni zombies.")]
    public LayerMask obstacleMask = ~0;
    public float eyeHeight = 1.2f;

    [Header("Frenado")]
    public float hitSlowMultiplier = 1.7f;
    public float hitSlowDuration = 1.25f;

    private NavMeshAgent navMeshAgent;
    private Ai ai;
    private EnemyHealth enemyHealth;
    private EnemyAnimation enemyAnimation;
    private Transform currentTarget;

    [Header("Dano por tiempo (si el clip no tiene evento)")]
    [Tooltip("0 = solo por evento. Ej: 0.45 = pega al 45% del clip.")]
    [Range(0f, 1f)] public float timedDamageAt = 0f;
    private float attackStartTime, attackLength;
    private bool timedDamageDone;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        ai = GetComponent<Ai>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyAnimation = GetComponent<EnemyAnimation>();
    }

    // US 7.5: la dificultad del host escala el dano de golpe y mordida (una sola vez).
    public void ApplyDifficultyScale()
    {
        if (difficultyApplied)
            return;

        attackDamage *= DifficultyTuning.DamageMul;
        biteDamage *= DifficultyTuning.DamageMul;
        difficultyApplied = true;
    }

    public override void OnNetworkDespawn()
    {
        ReleaseBiteTarget();
    }

    // Suelta al jugador que estaba inmovilizado por la mordida (termino el clip, murio el zombie o se despawneo).
    public void ReleaseBiteTarget()
    {
        if (frozenTarget != null)
            frozenTarget.SetFrozen(false);

        frozenTarget = null;
        biteCycle = false;
        biteDamageDealt = false;
    }

    // Lo llama EnemyHealth cuando un jugador lo golpea y sigue vivo. No empuja a un zombie que esta atacando.
    public void ApplyKnockback(Vector3 sourcePosition)
    {
        if (!IsServer || knockbackSpeed <= 0f) return;
        if (IsCommitted || Time.time < nextKnockbackTime) return;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return;

        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        knockbackDirection = direction.normalized;
        knockbackStart = Time.time;
        knockbackUntil = Time.time + knockbackDuration;
        nextKnockbackTime = knockbackUntil + knockbackCooldown;
        navMeshAgent.velocity = Vector3.zero;
    }

    // Lo llama EnemyHealth cuando recibe dano y sigue vivo. Si esta en medio de un ataque no lo corta.
    public void TryStagger()
    {
        if (!IsServer || enemyAnimation == null) return;
        if (IsCommitted) return;

        float duration = enemyAnimation.PlayHit();
        if (duration <= 0f) return;

        stunUntil = Time.time + duration;
        enemyAnimation.SetAttacking(false);

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted) return;
        if (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished) return;

        // Termino el clip de mordida: el jugador queda libre aunque el resto del Update se corte antes.
        if (frozenTarget != null && !IsCommitted)
        {
            // Si el clip termino y el evento de dano nunca llego, la mordida igual cuenta.
            if (biteCycle && !biteDamageDealt && currentTarget != null
                && (enemyHealth == null || !enemyHealth.IsDead()))
                ApplyDamage(biteDamage, true);

            ReleaseBiteTarget();
        }

        if (enemyHealth != null && enemyHealth.IsDead()) return;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return;

        // Empuje: se desliza hacia atras con velocidad decreciente (NavMeshAgent.Move respeta los bordes del NavMesh).
        if (IsKnockedBack)
        {
            float remaining = 1f - (Time.time - knockbackStart) / Mathf.Max(knockbackDuration, 0.01f);
            navMeshAgent.isStopped = true;
            navMeshAgent.Move(knockbackDirection * (knockbackSpeed * Mathf.Clamp01(remaining) * Time.deltaTime));
            return;
        }

        if (IsStunned)
        {
            navMeshAgent.isStopped = true;
            return;
        }

        Transform player = ai != null ? ai.CurrentPlayer : null;
        if (player != null && player.TryGetComponent(out PlayerHealth targetHealth) && targetHealth.IsDead)
            player = null;
        if (player != null && player.TryGetComponent(out RescuableNpc targetNpc) && !targetNpc.IsTargetable)
            player = null;
        float distanceToPlayer = player != null
            ? DistanceTo(player)
            : Mathf.Infinity;

        if (IsCommitted)
        {
            // Si el jugador ya no esta al alcance (o se subio fuera del alcance vertical) en plena mordida, lo suelta y corta el ataque.
            if (biteCycle && frozenTarget != null
                && (currentTarget == null
                    || DistanceTo(currentTarget) > biteRange * 1.2f
                    || !WithinVerticalReach(currentTarget)))
            {
                ReleaseBiteTarget();
                attackEndTime = Time.time;
                navMeshAgent.isStopped = true;
                return;
            }

            if (timedDamageAt > 0f && !biteCycle && !timedDamageDone
                && Time.time >= attackStartTime + attackLength * timedDamageAt)
            {
                timedDamageDone = true;
                DealDamage();
            }
            navMeshAgent.isStopped = true;
            return;
        }
        if (player != null && CanAttack(player))
        {
            currentTarget = player;
            navMeshAgent.isStopped = true;
            FaceTarget(player);

            float duration = attackDuration;
            PMovement movement = player.GetComponent<PMovement>();
            bool bite = enemyAnimation != null && enemyAnimation.HasBite
                && movement != null
                && DistanceTo(player) <= biteRange
                && Random.value < biteChance;

            if (bite)
            {
                // La mordida agarra al jugador: queda totalmente quieto mientras dura el clip.
                duration = enemyAnimation.StartBite();
                biteCycle = true;
                biteDamageDealt = false;
                frozenTarget = movement;
                frozenTarget.SetFrozen(true);
            }
            else
            {
                // La ventana dura lo mismo que el clip sorteado, asi nunca se corta a la mitad.
                biteCycle = false;
                biteDamageDealt = false;
                if (enemyAnimation != null)
                    duration = enemyAnimation.StartAttackCycle();
            }

            attackEndTime = Time.time + duration;
            attackLength = duration;
            timedDamageDone = false;
        }
        else
        {
            currentTarget = null;
            navMeshAgent.isStopped = false;
            if (enemyAnimation != null)
                enemyAnimation.SetAttacking(false);
        }
    }

    // Distancia horizontal desde el zombie hasta el cuerpo del objetivo, no hasta su pivote.
    // Un civil escalado a 2x o con el pivote en otro lado dejaba al zombie parado afuera del rango de ataque.
    public float DistanceTo(Transform target)
    {
        if (target == null) return Mathf.Infinity;

        Vector3 self = transform.position;
        Collider body = target.GetComponentInChildren<Collider>();
        if (body == null || body.isTrigger)
        {
            Vector3 pivot = target.position;
            self.y = 0f;
            pivot.y = 0f;
            return Vector3.Distance(self, pivot);
        }

        Bounds bounds = body.bounds;
        Vector3 level = new Vector3(self.x, bounds.center.y, self.z);
        Vector3 closest = bounds.ClosestPoint(level);
        self.y = 0f;
        closest.y = 0f;
        return Vector3.Distance(self, closest);
    }

    // Diferencia de altura entre los pies del zombie y los del objetivo. Si el jugador esta mucho mas arriba (o abajo), no llega.
    private bool WithinVerticalReach(Transform target)
    {
        if (target == null) return false;

        Collider body = target.GetComponentInChildren<Collider>();
        float targetFeetY = (body != null && !body.isTrigger)
            ? body.bounds.min.y
            : target.position.y;

        return Mathf.Abs(targetFeetY - transform.position.y) <= maxVerticalReach;
    }

    // El objetivo tiene que estar enfrente del zombie (dentro del cono definido por frontDotThreshold).
    private bool IsInFront(Transform target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return true;   // practicamente encima: no hay direccion util

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return true;

        return Vector3.Dot(forward.normalized, toTarget.normalized) >= frontDotThreshold;
    }

    // Rango + altura + linea de vision. Lo usan Ai y EnemyCombat para decidir lo mismo.
    // No incluye el cono frontal a proposito: el zombie se gira hacia el objetivo (FaceTarget) justo al empezar el ataque.
    public bool CanAttack(Transform target)
    {
        // El && corta antes: el raycast solo se hace cuando ya esta cerca (barato en hordas).
        return target != null
            && DistanceTo(target) <= attackRange
            && WithinVerticalReach(target)
            && HasLineOfSight(target);
    }

    private bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Collider body = target.GetComponentInChildren<Collider>();
        Vector3 dest = body != null ? body.bounds.center : target.position + Vector3.up;

        Vector3 dir = dest - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir / dist, dist,
            obstacleMask, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit h in hits)
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(target)) continue;   // yo mismo / el objetivo
            // otros zombies, jugadores o civiles no cuentan como pared
            if (h.collider.GetComponentInParent<EnemyCombat>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (h.collider.GetComponentInParent<RescuableNpc>() != null) continue;
            return false;
        }
        return true;
    }

    public void DealDamage()
    {
        if (!IsServer || currentTarget == null) return;

        // Un zombie muerto no hace dano: la animacion de ataque puede seguir disparando el evento mientras cae.
        if (enemyHealth != null && enemyHealth.IsDead()) return;

        // Solo cuenta el evento que llega dentro de un ataque (o un instante despues). Un evento suelto no pega.
        if (Time.time > attackEndTime + 0.15f) return;

        // La mordida pega una sola vez (el fallback de Update cubre el caso de que el clip no tenga evento).
        if (biteCycle && biteDamageDealt) return;

        // La mordida usa su propio rango; el golpe normal sigue usando attackRange.
        float maxRange = biteCycle ? biteRange : attackRange;
        if (DistanceTo(currentTarget) > maxRange * 1.2f) return;

        // El jugador pudo subirse o moverse a un costado/espalda durante el clip.
        if (!WithinVerticalReach(currentTarget)) return;
        if (!IsInFront(currentTarget)) return;

        if (!HasLineOfSight(currentTarget)) return;

        ApplyDamage(biteCycle ? biteDamage : attackDamage, biteCycle);
    }

    private void ApplyDamage(float damage, bool bite)
    {
        if (currentTarget == null) return;
        if (bite) biteDamageDealt = true;
        if (damage <= 0f) return;

        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            if (playerHealth.IsDead) return;

            playerHealth.TakeDamage(damage);
            HitLandedClientRpc(bite);
            if (playerHealth.IsDead) return;

            PMovement movement = currentTarget.GetComponent<PMovement>();
            if (movement != null)
                movement.ApplyHitSlow(hitSlowMultiplier, hitSlowDuration);
            return;
        }

        RescuableNpc npc = currentTarget.GetComponent<RescuableNpc>();
        if (npc != null)
        {
            npc.TakeDamage(damage);
            HitLandedClientRpc(bite);
        }
    }

    // Suena en todos los clientes solo cuando el golpe conecta (no cuando falla o lo esquivan).
    [ClientRpc]
    private void HitLandedClientRpc(bool bite)
    {
        if (enemyAnimation != null)
            enemyAnimation.PlayLandedSound(bite);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 look = target.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(look);
    }
}
