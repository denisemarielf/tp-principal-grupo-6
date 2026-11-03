using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemyCombat : NetworkBehaviour
{
    [Header("Ataque")]
    public float attackDamage = 15f;
    public float attackRange = 15.5f;
    public float attackDuration = 1.2f; // solo de respaldo: la duracion real es la del clip de ataque elegido

    [Header("Mordida")]
    [Range(0f, 1f)] public float biteChance = 0.25f;     // probabilidad de morder en cada ciclo (solo contra jugadores)

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
    private PMovement frozenTarget;
    private bool IsCommitted => Time.time < attackEndTime;

    // Aturdido por la reaccion al golpe: no se mueve mientras dura el clip.
    public bool IsStunned => Time.time < stunUntil;
    // Atacando (clip completo) o aturdido: ni Ai ni nadie lo mueve.
    public bool IsKnockedBack => Time.time < knockbackUntil;
    public bool IsBusy => IsStunned || IsCommitted || IsKnockedBack;

    [Header("Frenado")]
    public float hitSlowMultiplier = 1.7f;
    public float hitSlowDuration = 1.25f;

    private NavMeshAgent navMeshAgent;
    private Ai ai;
    private EnemyHealth enemyHealth;
    private EnemyAnimation enemyAnimation;
    private Transform currentTarget;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        ai = GetComponent<Ai>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyAnimation = GetComponent<EnemyAnimation>();
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

        // Termino el clip de mordida: el jugador queda libre aunque el resto del Update se corte antes.
        if (frozenTarget != null && !IsCommitted)
            ReleaseBiteTarget();

        if (enemyHealth != null && enemyHealth.IsDead()) return;
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted) return;
        if (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished) return;
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
            ? Vector3.Distance(transform.position, player.position)
            : Mathf.Infinity;

        if (IsCommitted)
        {
            // Termina el golpe completo aunque el jugador se haya movido: no apuntar ni cancelar.
            navMeshAgent.isStopped = true;
            return;
        }
        if (distanceToPlayer <= attackRange)
        {
            currentTarget = player;
            navMeshAgent.isStopped = true;
            FaceTarget(player);

            float duration = attackDuration;
            PMovement movement = player.GetComponent<PMovement>();
            bool bite = enemyAnimation != null && enemyAnimation.HasBite
                && movement != null && Random.value < biteChance;

            if (bite)
            {
                // La mordida agarra al jugador: queda totalmente quieto mientras dura el clip.
                duration = enemyAnimation.StartBite();
                biteCycle = true;
                frozenTarget = movement;
                frozenTarget.SetFrozen(true);
            }
            else
            {
                // La ventana dura lo mismo que el clip sorteado, asi nunca se corta a la mitad.
                biteCycle = false;
                if (enemyAnimation != null)
                    duration = enemyAnimation.StartAttackCycle();
            }

            attackEndTime = Time.time + duration;
        }
        else
        {
            currentTarget = null;
            navMeshAgent.isStopped = false;
            if (enemyAnimation != null)
                enemyAnimation.SetAttacking(false);
        }
    }

    public void DealDamage()
    {
        if (!IsServer || currentTarget == null) return;

        // Un zombie muerto no hace dano: la animacion de ataque puede seguir disparando el evento mientras cae.
        if (enemyHealth != null && enemyHealth.IsDead()) return;

        // Solo cuenta el evento que llega dentro de un ataque (o un instante despues). Un evento suelto no pega.
        if (Time.time > attackEndTime + 0.15f) return;

        // La mordida solo inmoviliza. Ese zombie no vuelve a sacar vida mientras te tiene agarrado.
        if (biteCycle) return;
        if (Vector3.Distance(transform.position, currentTarget.position) > attackRange * 1.2f) return;

        float damage = attackDamage;

        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            if (playerHealth.IsDead) return;

            playerHealth.TakeDamage(damage);
            HitLandedClientRpc(biteCycle);
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
            HitLandedClientRpc(biteCycle);
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