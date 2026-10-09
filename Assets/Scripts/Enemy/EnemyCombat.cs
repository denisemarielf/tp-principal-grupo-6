using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemyCombat : NetworkBehaviour
{
    [Header("Ataque")]
    public float attackDamage = 15f;
    public float attackRange = 2.5f;
    public float attackCooldown = 0.8f;

    [Header("Frenado")]
    public float hitSlowMultiplier = 0.7f;
    public float hitSlowDuration = 1.25f;

    private float lastAttackTime;
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
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return;

        Transform player = ai != null ? ai.CurrentPlayer : null;
        if (player != null && player.TryGetComponent(out PlayerHealth targetHealth) && targetHealth.IsDead)
            player = null;
        float distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : Mathf.Infinity;

        if (distanceToPlayer <= attackRange)
        {
            currentTarget = player;
            navMeshAgent.isStopped = true;
            FaceTarget(player);
            if (enemyAnimation != null)
                enemyAnimation.SetAttacking(true);

            if (Time.time >= lastAttackTime + attackCooldown)
                Attack();
        }
        else
        {
            currentTarget = null;
            navMeshAgent.isStopped = false;
            if (enemyAnimation != null)
                enemyAnimation.SetAttacking(false);
        }
    }

    private void Attack()
    {
        lastAttackTime = Time.time;
        DealDamage();
    }

    public void DealDamage()
    {
        if (!IsServer || currentTarget == null) return;

        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead) return;

        playerHealth.TakeDamage(attackDamage);
        if (playerHealth.IsDead) return;

        PMovement movement = currentTarget.GetComponent<PMovement>();
        if (movement != null)
            movement.ApplyHitSlow(hitSlowMultiplier, hitSlowDuration);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 look = target.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(look);
    }
}
