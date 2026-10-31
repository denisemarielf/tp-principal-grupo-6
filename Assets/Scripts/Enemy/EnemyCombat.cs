using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemyCombat : NetworkBehaviour
{
    [Header("Ataque")]
    public float attackDamage = 15f;
    public float attackRange = 15.5f;
    public float attackDuration = 1.2f; // duración de UN ciclo del clip de ataque (ya con la velocidad del estado)

    private float attackEndTime;
    private bool IsCommitted => Time.time < attackEndTime;

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

    private void Update()
    {
        if (!IsServer) return;
        if (enemyHealth != null && enemyHealth.IsDead()) return;
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted) return;
        if (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished) return;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return;

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
            // Termina el golpe aunque el jugador se haya movido: no apuntar ni cancelar.
            navMeshAgent.isStopped = true;
            return;
        }
        if (distanceToPlayer <= attackRange)
        {
            currentTarget = player;
            navMeshAgent.isStopped = true;
            FaceTarget(player);
            if (enemyAnimation != null)
                enemyAnimation.SetAttacking(true);
            attackEndTime = Time.time + attackDuration;
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
        if (Vector3.Distance(transform.position, currentTarget.position) > attackRange * 1.2f) return;

        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            if (playerHealth.IsDead) return;

            playerHealth.TakeDamage(attackDamage);
            if (playerHealth.IsDead) return;

            PMovement movement = currentTarget.GetComponent<PMovement>();
            if (movement != null)
                movement.ApplyHitSlow(hitSlowMultiplier, hitSlowDuration);
            return;
        }

        RescuableNpc npc = currentTarget.GetComponent<RescuableNpc>();
        if (npc != null)
            npc.TakeDamage(attackDamage);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 look = target.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(look);
    }
}
