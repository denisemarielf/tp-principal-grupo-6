using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public enum NpcState : byte
{
    Waiting,    // esperando que un jugador lo rescate
    Following,  // escoltado: sigue a su lider hacia la zona segura
    Safe        // ya llego a la zona segura
}

// Civil rescatable (US NPCs). El host es el unico que decide su estado:
// 1. Waiting: parado en su lugar. Un jugador cerca aprieta E y se lo pide al host.
// 2. Following: el host valida el pedido y el NPC sigue a ese jugador con un NavMeshAgent.
//    Si el lider muere o se desconecta, pasa a seguir al jugador vivo mas cercano.
//    Si hay un enemigo cerca se aleja un poco (escape corto) y despues retoma lo que hacia.
// 3. Safe: la SafeZone lo marca al entrar; queda parado donde llego y deja de ser un blanco.
// Los enemigos lo atacan (ver Ai y EnemyCombat) y si se queda sin vida desaparece.
// Va en el prefab del NPC, junto a NetworkObject, NetworkTransform (autoridad del server) y NavMeshAgent (desactivado).
[RequireComponent(typeof(NavMeshAgent))]
public class RescuableNpc : NetworkBehaviour
{
    // NPCs spawneados en este equipo (la SafeZone y la IA de los enemigos los recorren).
    public static readonly List<RescuableNpc> All = new List<RescuableNpc>();

    [Header("Vida")]
    [Tooltip("Igual o mayor que la vida del jugador (US 4.5).")]
    [SerializeField] private float maxHealth = 120f;

    [Header("Rescate")]
    [SerializeField] private float rescueRadius = 2.5f;
    [Tooltip("Margen extra que acepta el host, porque ve la posicion del cliente con un poco de retraso.")]
    [SerializeField] private float serverTolerance = 1f;

    [Header("Escolta")]
    [Tooltip("A que distancia del lider se frena. Usar valores distintos en cada NPC (2.5, 3.5, 4.5) para que no se amontonen.")]
    [SerializeField] private float followDistance = 2.5f;
    [SerializeField] private float walkSpeed = 3.5f;
    [Tooltip("Si queda mas lejos que esto, acelera para alcanzar al lider.")]
    [SerializeField] private float catchUpDistance = 8f;
    [SerializeField] private float catchUpSpeed = 6f;
    [Tooltip("Si queda mas lejos que esto (se trabo o se perdio), se teletransporta al lado del lider.")]
    [SerializeField] private float teleportDistance = 35f;
    [SerializeField] private float repathInterval = 0.25f;

    [Header("Escape")]
    [Tooltip("Si hay un enemigo vivo a menos de esta distancia, el civil se aleja.")]
    [SerializeField] private float fleeTriggerDistance = 7f;
    [Tooltip("Cuanto se aleja del enemigo. Corto a proposito: se corre un poco, no huye por el mapa.")]
    [SerializeField] private float fleeDistance = 5f;
    [SerializeField] private float fleeSpeed = 5f;
    [Tooltip("Cada cuantos segundos busca enemigos cerca (una sola consulta de NavMesh por chequeo).")]
    [SerializeField] private float fleeCheckInterval = 0.5f;
    [Tooltip("Segundos sin enemigos cerca antes de retomar lo que hacia.")]
    [SerializeField] private float fleeRecoverSeconds = 1.5f;

    [Header("Visual")]
    [Tooltip("Animator del modelo. Necesita un parametro float 'Speed' (idle <-> caminar).")]
    [SerializeField] private Animator animator;
    [Tooltip("Opcional: cartel tipo 'E - Rescatar'. Solo lo ve el jugador local cuando esta cerca de un NPC que espera.")]
    [SerializeField] private GameObject usePrompt;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private NetworkVariable<NpcState> state = new NetworkVariable<NpcState>(
        NpcState.Waiting,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<ulong> leaderId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Civil del refugio de Supervivencia: quieto, sin rescate, igual lo atacan los zombies.
    private NetworkVariable<bool> shelter = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool pendingShelter;

    private NavMeshAgent agent;
    private bool hasSpeedParameter;
    private Vector3 lastPosition;
    private float animatedSpeed;
    private float repathTimer;
    private Vector3 homePosition;
    private bool fleeing;
    private bool returningHome;
    private float fleeCheckTimer;
    private float fleeRecoverTimer;

    public NpcState State => state.Value;
    public bool IsDead => isDead.Value;
    public bool IsShelter => shelter.Value;
    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;

    // Llamar en el server antes de Spawn para que el valor viaje con el objeto.
    public void ConfigureAsShelter()
    {
        pendingShelter = true;
        shelter.Value = true;
    }

    // Los enemigos solo atacan a NPCs vivos que todavia no llegaron a la zona segura.
    public bool IsTargetable => IsSpawned && !isDead.Value && state.Value != NpcState.Safe;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // El agente se prende recien cuando el NavMesh ya existe (se arma en runtime al iniciar el host).
        agent.enabled = false;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == SpeedHash)
                    hasSpeedParameter = true;
            }

            if (!hasSpeedParameter)
                Debug.LogWarning("RescuableNpc: el Animator no tiene un parametro float 'Speed', el NPC no va a animarse.", this);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && pendingShelter)
            shelter.Value = true;

        All.Add(this);
        lastPosition = transform.position;
        homePosition = transform.position;

        if (usePrompt != null)
            usePrompt.SetActive(false);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            StartCoroutine(EnableAgentWhenNavMeshReady());
        }
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
    }

    public override void OnDestroy()
    {
        All.Remove(this);
        base.OnDestroy();
    }

    private IEnumerator EnableAgentWhenNavMeshReady()
    {
        float waited = 0f;
        bool warned = false;

        while (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            waited += 0.5f;
            if (!warned && waited >= 10f)
            {
                warned = true;
                Debug.LogWarning($"RescuableNpc: '{name}' no tiene NavMesh a menos de 5 m de {transform.position}. No se va a poder mover: agranda 'Nav Mesh Size' en EnemySceneSetup para que lo incluya, o acercalo al mapa.", this);
            }

            yield return new WaitForSeconds(0.5f);
        }

        agent.enabled = true;
        agent.speed = walkSpeed;
        agent.stoppingDistance = followDistance;

        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit placed, 5f, NavMesh.AllAreas))
            agent.Warp(placed.position);

        if (agent.isOnNavMesh)
            agent.isStopped = shelter.Value || state.Value != NpcState.Following;
    }

    private void Update()
    {
        UpdateAnimation();
        UpdatePrompt();

        if (!IsServer || !IsSpawned || isDead.Value) return;

        if (shelter.Value)
        {
            StopAgent();
            return;
        }

        if (state.Value == NpcState.Safe) return;

        UpdateFlee();

        if (state.Value == NpcState.Following && !fleeing)
            UpdateFollow();
        else if (state.Value == NpcState.Waiting && !fleeing && returningHome)
            UpdateReturnHome();
    }

    // ---------- Escape (solo host) ----------

    // Rutina simple: cada fleeCheckInterval mira si hay un enemigo cerca y, si lo hay, camina unos
    // metros en la direccion contraria. Un solo SamplePosition por chequeo, sin pathfinding propio.
    private void UpdateFlee()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        fleeCheckTimer -= Time.deltaTime;
        if (fleeCheckTimer > 0f) return;
        fleeCheckTimer = fleeCheckInterval;

        EnemyHealth threat = NearestThreat();
        if (threat != null)
        {
            Vector3 away = transform.position - threat.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;

            Vector3 target = transform.position + away.normalized * fleeDistance;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                fleeing = true;
                returningHome = state.Value == NpcState.Waiting;
                fleeRecoverTimer = fleeRecoverSeconds;

                agent.speed = fleeSpeed;
                agent.stoppingDistance = 0.3f;
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
        else if (fleeing)
        {
            fleeRecoverTimer -= fleeCheckInterval;
            if (fleeRecoverTimer <= 0f)
            {
                fleeing = false;
                repathTimer = 0f; // si lo escoltan, vuelve a seguir al lider enseguida
            }
        }
    }

    private EnemyHealth NearestThreat()
    {
        EnemyHealth nearest = null;
        float nearestSqr = fleeTriggerDistance * fleeTriggerDistance;

        foreach (EnemyHealth enemy in EnemyHealth.All)
        {
            if (enemy == null || enemy.IsDead()) continue;

            float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearest = enemy;
                nearestSqr = sqr;
            }
        }

        return nearest;
    }

    // Un civil que esperaba y tuvo que correrse vuelve a su lugar cuando se calma.
    private void UpdateReturnHome()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if ((transform.position - homePosition).sqrMagnitude < 0.5f * 0.5f)
        {
            returningHome = false;
            StopAgent();
            return;
        }

        agent.speed = walkSpeed;
        agent.stoppingDistance = 0.3f;
        agent.isStopped = false;
        agent.SetDestination(homePosition);
    }

    // ---------- Escolta (solo host) ----------

    private void UpdateFollow()
    {
        Debug.Log($"{name}: agent.enabled={agent.enabled}, onNavMesh={(agent.enabled && agent.isOnNavMesh)}, fleeing={fleeing}");
        Transform leader = ResolveLeader();
        if (leader == null)
        {
            SetWaiting();
            return;
        }

        float distance = Vector3.Distance(transform.position, leader.position);
        if (distance > teleportDistance)
        {
            TeleportNear(leader.position);
            return;
        }

        if (!agent.enabled || !agent.isOnNavMesh) return;

        agent.speed = distance > catchUpDistance ? catchUpSpeed : walkSpeed;
        agent.stoppingDistance = followDistance;

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.isStopped = false;
            agent.SetDestination(leader.position);
        }
    }

    // El lider es quien lo rescato; si ya no esta disponible, el jugador vivo mas cercano.
    private Transform ResolveLeader()
    {
        if (NetworkManager.ConnectedClients.TryGetValue(leaderId.Value, out NetworkClient current) && IsAlivePlayer(current))
            return current.PlayerObject.transform;

        Transform nearest = null;
        float nearestDistance = Mathf.Infinity;
        ulong nearestId = 0;

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (!IsAlivePlayer(client)) continue;

            float distance = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = client.PlayerObject.transform;
                nearestId = client.ClientId;
            }
        }

        if (nearest != null)
            leaderId.Value = nearestId;

        return nearest;
    }

    private static bool IsAlivePlayer(NetworkClient client)
    {
        if (client == null || client.PlayerObject == null) return false;

        PlayerHealth health = client.PlayerObject.GetComponent<PlayerHealth>();
        return health != null && !health.IsDead;
    }

    private void SetWaiting()
    {
        state.Value = NpcState.Waiting;
        StopAgent();
    }

    private void StopAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    private void TeleportNear(Vector3 target)
    {
        Vector3 around = target + Random.insideUnitSphere * followDistance;
        around.y = target.y;

        if (NavMesh.SamplePosition(around, out NavMeshHit hit, 5f, NavMesh.AllAreas) && agent.enabled)
            agent.Warp(hit.position);
    }

    // ---------- Rescate ----------

    // NPC que espera mas cercano a 'position' dentro de su radio de rescate, o null si no hay ninguno.
    public static RescuableNpc FindNearestWaiting(Vector3 position)
    {
        RescuableNpc nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (RescuableNpc npc in All)
        {
            if (npc == null || npc.isDead.Value || npc.shelter.Value || npc.state.Value != NpcState.Waiting) continue;

            float distance = Vector3.Distance(position, npc.transform.position);
            if (distance <= npc.rescueRadius && distance < nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public void RequestRescue()
    {
        Debug.Log($"RequestRescue: spawned={IsSpawned}, dead={isDead.Value}, state={state.Value}");
        if (!IsSpawned || isDead.Value || shelter.Value || state.Value != NpcState.Waiting) return;
        RequestRescueRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestRescueRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (isDead.Value || shelter.Value || state.Value != NpcState.Waiting) return;

        if (!NetworkManager.ConnectedClients.TryGetValue(senderId, out NetworkClient client) || !IsAlivePlayer(client))
            return;

        float distance = Vector3.Distance(client.PlayerObject.transform.position, transform.position);
        if (distance > rescueRadius + serverTolerance)
        {
            Debug.Log($"RescuableNpc: el host rechazo el pedido del cliente {senderId} (distancia {distance:0.0}).");
            return;
        }

        leaderId.Value = senderId;
        state.Value = NpcState.Following;
        returningHome = false;
        repathTimer = 0f;
        Debug.Log($"RescuableNpc: el cliente {senderId} rescato a {name}.");
    }

    // Lo llama la SafeZone (solo host) cuando el NPC escoltado entra a la zona.
    public void MarkSafe()
    {
        if (!IsServer || isDead.Value || state.Value == NpcState.Safe) return;

        state.Value = NpcState.Safe;
        StopAgent();
        agent.enabled = false;
        Debug.Log($"RescuableNpc: {name} llego a la zona segura.");
        AchievementService.NotifyCivilianRescued(leaderId.Value);
    }

    // ---------- Vida (solo host) ----------

    public void TakeDamage(float amount)
    {
        if (!IsServer || isDead.Value || state.Value == NpcState.Safe || amount <= 0f) return;

        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
        if (currentHealth.Value <= 0f)
            Die();
    }

    private void Die()
    {
        isDead.Value = true;
        agent.enabled = false;
        Debug.Log($"RescuableNpc: {name} murio.");

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            // Los objetos puestos en la escena no se destruyen: se despawnean y se ocultan.
            bool inScene = NetworkObject.IsSceneObject == true;
            NetworkObject.Despawn(!inScene);

            if (inScene)
                gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ---------- Visual (todos los equipos) ----------

    // La velocidad se calcula con el movimiento real del NPC, asi no hace falta sincronizar el Animator.
    private void UpdateAnimation()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        if (animator == null || !hasSpeedParameter) return;

        delta.y = 0f;
        float instantSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        animatedSpeed = Mathf.Lerp(animatedSpeed, instantSpeed, 10f * Time.deltaTime);
        animator.SetFloat(SpeedHash, animatedSpeed);
    }

    private void UpdatePrompt()
    {
        if (usePrompt == null || !IsSpawned) return;

        if (shelter.Value)
        {
            usePrompt.SetActive(false);
            return;
        }

        bool show = LocalPlayerCanRescue();
        usePrompt.SetActive(show);

        if (show && Camera.main != null)
            usePrompt.transform.rotation = Quaternion.LookRotation(usePrompt.transform.position - Camera.main.transform.position);
    }

    private bool LocalPlayerCanRescue()
    {
        if (isDead.Value || state.Value != NpcState.Waiting) return false;

        NetworkObject localPlayer = NetworkManager.LocalClient?.PlayerObject;
        if (localPlayer == null) return false;

        PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
        if (health == null || health.IsDead) return false;

        return Vector3.Distance(localPlayer.transform.position, transform.position) <= rescueRadius;
    }
}
