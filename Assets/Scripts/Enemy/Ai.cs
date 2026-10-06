using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Ai : NetworkBehaviour
{
    public NavMeshAgent navMeshAgent;
    public GameObject destination1;

    [Header("Follow")]
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private float distanceToFollowPlayer = 100f;
    [SerializeField] private float distanceToLosePlayer = 120f;

    [Header("Aggro")]
    [SerializeField] private float aggroDuration = 6f;

    private Transform player;
    private EnemyCombat enemyCombat;
    private Vector3 lastPlayerPosition;
    private float repathThreshold = 0.5f;
    private bool isFollowingPlayer;
    private float playerSearchInterval = 0.25f;
    private float playerSearchTimer;
    private Transform aggroTarget;
    private float aggroTimer;

    public Transform CurrentPlayer => (aggroTimer > 0f && aggroTarget != null) ? aggroTarget : player;

    // US 7.2: normal 3.5, fragil 6 y duro 1.5. El jugador camina a 7.
    public void Configure(EnemyVariant variant)
    {
        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
            navMeshAgent.speed = SpeedFor(variant);
    }

    public static float SpeedFor(EnemyVariant variant)
    {
        switch (variant)
        {
            case EnemyVariant.Fragil:
                return 6f;
            case EnemyVariant.Resistente:
                return 1.5f;
            default:
                return 3.5f;
        }
    }

    public override void OnNetworkSpawn()
    {
        enemyCombat = GetComponent<EnemyCombat>();

        if (!IsServer)
        {
            if (navMeshAgent != null)
                navMeshAgent.enabled = false;
            return;
        }

        RefreshNearestPlayer();
        PlaceOnNavMesh();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return;

        if (aggroTimer > 0f)
            aggroTimer -= Time.deltaTime;
        else if (aggroTarget != null)
            ClearAggro();

        if (aggroTimer <= 0f)
        {
            playerSearchTimer -= Time.deltaTime;
            if (playerSearchTimer <= 0f)
            {
                RefreshNearestPlayer();
                playerSearchTimer = playerSearchInterval;
            }
        }

        Transform target = CurrentPlayer;
        if (target == null)
        {
            if (isFollowingPlayer)
            {
                GoToDestination();
                isFollowingPlayer = false;
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
        bool isInAttackRange = enemyCombat != null && distanceToPlayer <= enemyCombat.attackRange;
        bool withinFollowRange = isFollowingPlayer
            ? distanceToPlayer < distanceToLosePlayer
            : distanceToPlayer < distanceToFollowPlayer;

        bool shouldFollow = (aggroTimer > 0f || withinFollowRange) && followPlayer && !isInAttackRange;

        if (shouldFollow)
        {
            FollowTarget(target);
            isFollowingPlayer = true;
        }
        else if (isFollowingPlayer && !isInAttackRange)
        {
            GoToDestination();
            isFollowingPlayer = false;
        }
    }

    public void SetAggroTarget(Transform attacker)
    {
        if (attacker == null) return;
        aggroTarget = attacker;
        aggroTimer = aggroDuration;
    }

    private void ClearAggro()
    {
        aggroTarget = null;
        aggroTimer = 0f;
    }

    private void RefreshNearestPlayer()
    {
        PMovement[] allPlayers = FindObjectsByType<PMovement>();
        Transform nearest = null;
        float nearestDist = Mathf.Infinity;

        foreach (PMovement candidate in allPlayers)
        {
            if (candidate == null) continue;
            if (candidate.TryGetComponent(out PlayerHealth health) && health.IsDead) continue;
            float dist = Vector3.Distance(transform.position, candidate.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = candidate.transform;
            }
        }

        player = nearest;
    }

    private void FollowTarget(Transform target)
    {
        if (target == null || !navMeshAgent.isOnNavMesh) return;
        if (Vector3.Distance(target.position, lastPlayerPosition) <= repathThreshold) return;

        navMeshAgent.isStopped = false;
        navMeshAgent.destination = target.position;
        lastPlayerPosition = target.position;
    }

    private void GoToDestination()
    {
        if (destination1 == null || navMeshAgent == null || !navMeshAgent.isOnNavMesh) return;
        navMeshAgent.destination = destination1.transform.position;
    }

    private void PlaceOnNavMesh()
    {
        if (navMeshAgent == null || navMeshAgent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            navMeshAgent.Warp(hit.position);
    }
}
