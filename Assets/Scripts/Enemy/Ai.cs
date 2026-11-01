using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Ai : NetworkBehaviour
{
    public NavMeshAgent navMeshAgent;
    public GameObject destination1;

    [Header("Follow")]
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private float distanceToFollowPlayer = 60f;
    [SerializeField] private float distanceToLosePlayer = 80f;

    [Header("Aggro")]
    [SerializeField] private float aggroDuration = 6f;

    [Header("Deambular")]
    [Tooltip("Mientras no persigue a nadie, camina sin rumbo cerca de donde aparecio. Si hay destination1 asignado, se usa ese comportamiento en vez de este.")]
    [SerializeField] private bool wanderWhenIdle = true;
    [SerializeField] private float wanderRadius = 12f;
    [Tooltip("Fraccion de la velocidad normal al deambular.")]
    [SerializeField, Range(0.1f, 1f)] private float wanderSpeedMultiplier = 0.35f;
    [Tooltip("Segundos quieto entre caminata y caminata.")]
    [SerializeField] private Vector2 wanderPause = new Vector2(2f, 6f);
    [Tooltip("Si no llega en este tiempo, elige otro punto.")]
    [SerializeField] private float maxWanderTravelTime = 15f;

    private Transform player;
    private EnemyCombat enemyCombat;
    private Vector3 lastPlayerPosition;
    private float repathThreshold = 0.5f;
    private bool isFollowingPlayer;
    private float playerSearchInterval = 0.25f;
    private float playerSearchTimer;
    private Transform aggroTarget;
    private float aggroTimer;

    // Hordas: persiguen sin importar la distancia (lo activa el AIDirector).
    private bool alwaysChase;

    private float baseSpeed;
    private Vector3 homePosition;
    private bool isWanderMoving;
    private float wanderTimer;

    public Transform CurrentPlayer => (aggroTimer > 0f && aggroTarget != null) ? aggroTarget : player;

    // US 7.2: normal 4, fragil 7 y duro 3. El jugador camina a 7.
    public void Configure(EnemyVariant variant)
    {
        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
        {
            baseSpeed = SpeedFor(variant);
            navMeshAgent.speed = baseSpeed;
        }
    }

    public static float SpeedFor(EnemyVariant variant)
    {
        switch (variant)
        {
            case EnemyVariant.Fragil:
                return 7f;
            case EnemyVariant.Resistente:
                return 3f;
            default:
                return 4f;
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

        if (baseSpeed <= 0f && navMeshAgent != null)
            baseSpeed = navMeshAgent.speed;

        homePosition = transform.position;
        wanderTimer = Random.Range(0f, wanderPause.y);

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

        // Mientras ataca (clip completo) o esta aturdido por un golpe no se mueve ni cambia de ruta.
        if (enemyCombat != null && enemyCombat.IsBusy)
            return;

        Transform target = CurrentPlayer;
        if (target == null)
        {
            if (isFollowingPlayer)
            {
                GoToDestination();
                isFollowingPlayer = false;
            }
            else
            {
                Wander();
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
        bool isInAttackRange = enemyCombat != null && distanceToPlayer <= enemyCombat.attackRange;
        bool withinFollowRange = alwaysChase || (isFollowingPlayer
            ? distanceToPlayer < distanceToLosePlayer
            : distanceToPlayer < distanceToFollowPlayer);

        bool shouldFollow = (aggroTimer > 0f || withinFollowRange) && followPlayer && !isInAttackRange;

        if (shouldFollow)
        {
            if (!isFollowingPlayer || isWanderMoving)
                StopWandering();

            FollowTarget(target);
            isFollowingPlayer = true;
        }
        else if (isFollowingPlayer && !isInAttackRange)
        {
            GoToDestination();
            isFollowingPlayer = false;
        }
        else if (!isFollowingPlayer && !isInAttackRange)
        {
            Wander();
        }
    }

    public void SetAggroTarget(Transform attacker)
    {
        if (attacker == null) return;
        aggroTarget = attacker;
        aggroTimer = aggroDuration;
    }

    // Lo llama el AIDirector en las hordas: persiguen sin importar la distancia.
    public void StartHorde()
    {
        alwaysChase = true;
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

        // Los civiles que esperan o son escoltados tambien son blancos.
        foreach (RescuableNpc npc in RescuableNpc.All)
        {
            if (npc == null || !npc.IsTargetable) continue;
            float dist = Vector3.Distance(transform.position, npc.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = npc.transform;
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

    // Camina sin rumbo cerca del punto donde aparecio: se mueve un rato, para, y vuelve a elegir.
    private void Wander()
    {
        if (!wanderWhenIdle || destination1 != null) return;
        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh) return;

        SetWalkingSpeed(true);
        wanderTimer -= Time.deltaTime;

        if (isWanderMoving)
        {
            bool arrived = !navMeshAgent.pathPending
                && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + 0.3f;

            if (arrived || wanderTimer <= 0f)
            {
                isWanderMoving = false;
                navMeshAgent.ResetPath();
                wanderTimer = Random.Range(wanderPause.x, wanderPause.y);
            }

            return;
        }

        if (wanderTimer > 0f)
            return;

        for (int i = 0; i < 5; i++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = homePosition + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            navMeshAgent.isStopped = false;
            navMeshAgent.destination = hit.position;
            isWanderMoving = true;
            wanderTimer = maxWanderTravelTime;
            return;
        }

        wanderTimer = 1f;
    }

    private void StopWandering()
    {
        isWanderMoving = false;
        SetWalkingSpeed(false);

        // Fuerza a FollowTarget a trazar la ruta aunque el jugador no se haya movido.
        lastPlayerPosition = Vector3.one * 1000000f;
    }

    private void SetWalkingSpeed(bool walking)
    {
        if (baseSpeed <= 0f)
            return;

        navMeshAgent.speed = walking ? baseSpeed * wanderSpeedMultiplier : baseSpeed;
    }

    private void PlaceOnNavMesh()
    {
        if (navMeshAgent == null || navMeshAgent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            navMeshAgent.Warp(hit.position);
    }
}