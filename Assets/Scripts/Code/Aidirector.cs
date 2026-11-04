using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// AI Director estilo Left 4 Dead 2. Solo corre en el server.
//
// Idea: en vez de plantar enemigos fijos por el mapa, el Director mide la "intensidad"
// emocional de cada jugador y decide cu�ndo apretar y cu�ndo dar respiro:
//
//   BuildUp -> SustainPeak -> PeakFade -> Relax -> BuildUp ...
//
//   BuildUp     : va llenando el mapa de zombies (fuera de la vista) y cuenta para la pr�xima horda.
//   SustainPeak : la intensidad lleg� al tope; sigue spawneando 3-5 s m�s.
//   PeakFade    : deja de spawnear hasta que el grupo se calme (matan o se alejan).
//   Relax       : 30-45 s sin spawns nuevos. Respiro.
//
// Los zombies aparecen SIEMPRE fuera de la vista de todos los jugadores, a distancia
// razonable y con camino por NavMesh hasta el grupo (con preferencia a espaldas del jugador).
public class AIDirector : MonoBehaviour
{
    public enum Phase { BuildUp, SustainPeak, PeakFade, Relax }

    public static AIDirector Instance { get; private set; }

    // (enemigo, vino de horda). �til para ponerle objetivo a la horda en tu script Ai.
    public static event System.Action<EnemyHealth, bool> EnemySpawned;

    [Header("Prefab")]
    [SerializeField] private GameObject enemyPrefab;
    [Header("Variantes (peso relativo)")]
    [SerializeField] private float normalWeight = 0.7f;
    [Tooltip("Rapido (7): igual de veloz que el jugador. Mejor usarlo con poca frecuencia.")]
    [SerializeField] private float fragilWeight = 0.1f;
    [Tooltip("Lento (3) pero aguanta.")]
    [SerializeField] private float resistenteWeight = 0.2f;

    [Header("Ritmo")]
    [Tooltip("Intensidad (0-1) a partir de la cual empieza el pico.")]
    [SerializeField, Range(0.3f, 1f)] private float peakIntensity = 0.85f;
    [Tooltip("Durante PeakFade se espera a bajar de este valor para pasar a Relax.")]
    [SerializeField, Range(0f, 0.6f)] private float fadeIntensity = 0.25f;
    [SerializeField] private Vector2 sustainPeakTime = new Vector2(3f, 5f);
    [SerializeField] private float maxPeakFadeTime = 30f;
    [SerializeField] private Vector2 relaxTime = new Vector2(30f, 45f);
    [Tooltip("Respiro al empezar la partida.")]
    [SerializeField] private float initialGrace = 15f;

    [Header("Intensidad")]
    [SerializeField] private float closeEnemyRange = 6f;
    [Tooltip("Intensidad por segundo por cada zombi cerca.")]
    [SerializeField] private float closeEnemyGain = 0.05f;
    [SerializeField] private float maxCloseGainPerSecond = 0.3f;
    [Tooltip("Si no hay zombis a esta distancia, la intensidad baja.")]
    [SerializeField] private float threatRange = 15f;
    [SerializeField] private float decayPerSecond = 0.1f;
    [SerializeField] private float killGain = 0.02f;
    [Tooltip("Fraccion de vida perdida x este factor. 20 de 100 de vida -> 0.2 x 1.5 = +0.3.")]
    [SerializeField] private float damageGain = 1.5f;
    [Tooltip("Sube la intensidad de TODOS cuando cae un jugador.")]
    [SerializeField] private float playerDownGain = 0.5f;

    [Header("Poblaci�n de fondo")]
    [SerializeField] private int baseWanderers = 12;
    [SerializeField] private int extraPerPlayer = 6;
    [SerializeField] private int hardCap = 70;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private Vector2Int groupSize = new Vector2Int(1, 3);

    [Header("Hordas")]
    [SerializeField] private Vector2 mobInterval = new Vector2(90f, 150f);
    [SerializeField] private Vector2Int mobSize = new Vector2Int(20, 35);
    [Tooltip("Multiplicador extra de tama�o por cada jugador adem�s del primero.")]
    [SerializeField] private float mobPerExtraPlayer = 0.25f;
    [SerializeField] private int mobBatch = 4;
    [SerializeField] private float mobBatchInterval = 0.4f;
    [SerializeField] private float mobGiveUpTime = 10f;

    [Header("D�nde aparecen")]
    [SerializeField] private float minSpawnDistance = 20f;
    [SerializeField] private float maxSpawnDistance = 55f;
    [Tooltip("Largo m�ximo del camino por NavMesh hasta el jugador (evita spawns 'del otro lado de la pared').")]
    [SerializeField] private float maxPathLength = 150f;
    [Range(0f, 1f)]
    [SerializeField] private float behindBias = 0.6f;
    [SerializeField] private float viewHalfAngle = 75f;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private int attemptsPerSpawn = 12;
    [Tooltip("Si hay EnemySpawnPoint en la escena, probabilidad de usarlos como candidatos.")]
    [Range(0f, 1f)]
    [SerializeField] private float markerBias = 0.7f;
    [Tooltip("Si hay EnemySpawnPoint en la escena, usa SOLO esos y nunca puntos al azar. Asi no aparecen enemigos donde no pusiste marcadores (la calle, por ejemplo).")]
    [SerializeField] private bool markersOnly = false;
    [Tooltip("Qu� bloquea la vista de los jugadores. Idealmente solo geometr�a del mapa.")]
    [SerializeField] private LayerMask occlusionMask = ~0;
    [Tooltip("Zombis m�s lejos que esto y fuera de vista se guardan.")]
    [SerializeField] private float despawnDistance = 110f;

    [Header("Avance (calle recta)")]
    [Tooltip("Activado: los zombis solo aparecen ADELANTE del jugador que va primero. Nunca atras.")]
    [SerializeField] private bool onlySpawnAhead = false;
    [Tooltip("Direccion del avance en el mundo. Calle sobre el eje Z hacia adelante: (0,0,1). Hacia -X: (-1,0,0). Al seleccionar el Director se dibuja una flecha cyan para verificarla.")]
    [SerializeField] private Vector3 advanceDirection = Vector3.forward;
    [Tooltip("Opcional: meta del nivel (ej: la zona de extraccion). Si se asigna, 'adelante' = mas cerca de la meta POR CAMINO REAL de NavMesh, "
           + "asi funciona en edificios con escaleras y pasillos. Tiene que estar cerca del NavMesh. Sin meta se usa la direccion de arriba.")]
    [SerializeField] private Transform advanceTarget;
    [Tooltip("Margen en metros respecto del primero. 0 = estrictamente adelante de el.")]
    [SerializeField] private float aheadMargin = 0f;
    [Tooltip("Zombis que quedan mas atras que esto respecto del ultimo jugador, y fuera de vista, se guardan.")]
    [SerializeField] private float behindDespawnDistance = 50f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private class PlayerState
    {
        public PMovement move;
        public PlayerHealth health;
        public float intensity;
        public bool wasDead;
        public float lastHealth;
        public float lastMax;
        public bool IsDead => health != null && health.IsDead;
    }

    private readonly List<PlayerState> players = new List<PlayerState>();
    private readonly List<PlayerState> alivePlayers = new List<PlayerState>();
    private readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
    private NavMeshPath path;
    private EnemySpawnPoint[] spawnPoints = new EnemySpawnPoint[0];
    private readonly List<EnemySpawnPoint> aheadMarkers = new List<EnemySpawnPoint>();
    private Vector3 aheadDir = Vector3.forward;
    private float aheadFront;

    // Diagnostico: por que se rechazan los puntos de spawn.
    private static readonly string[] RejectNames =
    {
        "detras del primero", "muy cerca de un jugador", "a la vista", "muy lejos",
        "sin espacio libre", "jugador fuera del NavMesh", "sin camino al jugador",
        "camino demasiado largo", "punto fuera del NavMesh",
        "zona sin enemigos", "sin camino a la meta"
    };
    private readonly int[] rejects = new int[11];

    // Modo meta: "adelante" por camino real de NavMesh.
    private NavMeshPath flowPath;
    private Vector3 targetNavPos;
    private bool useFlow;
    private float aheadFlow;
    private float minPathSeen = float.PositiveInfinity;

    private bool running;
    private Phase phase = Phase.Relax;
    private float phaseTimer;
    private float phaseElapsed;
    private float spawnTimer;
    private float refreshTimer;
    private float cleanupTimer;

    private float mobTimer;
    private int mobPending;
    private float mobBatchTimer;
    private float mobFailTime;

    public Phase CurrentPhase => phase;
    public float TeamIntensity { get; private set; }

    private void Awake()
    {
        Instance = this;
        path = new NavMeshPath();
        flowPath = new NavMeshPath();
    }

    private System.Action onBaked;

    private void OnDestroy()
    {
        if (onBaked != null)
            EnemySceneSetup.NavMeshReady -= onBaked;

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        StartCoroutine(Boot());
    }

    private IEnumerator Boot()
    {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        if (!NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            yield break;
        }

        // Espera a que EnemySceneSetup termine de hornear el NavMesh de ESTA escena.
        // Tras un cambio de escena el NavMesh viejo sigue cargado, asi que mirar si hay
        // triangulos no alcanza: se espera el aviso NavMeshReady.
        bool baked = FindAnyObjectByType<EnemySceneSetup>() == null;
        onBaked = () => baked = true;
        EnemySceneSetup.NavMeshReady += onBaked;

        float waited = 0f;
        while (!baked)
        {
            yield return new WaitForSeconds(0.25f);
            waited += 0.25f;

            // Seguro: si el aviso nunca llega, sigue con lo que haya cargado.
            if (waited > 30f && NavMesh.CalculateTriangulation().vertices.Length > 0)
                break;
        }

        EnemySceneSetup.NavMeshReady -= onBaked;
        onBaked = null;

        while (NavMesh.CalculateTriangulation().vertices.Length == 0)
            yield return new WaitForSeconds(0.5f);

        if (FindAnyObjectByType<SurvivalMatchBootstrap>() != null)
        {
            Debug.Log("AIDirector: modo Supervivencia, el Director queda apagado.");
            enabled = false;
            yield break;
        }

        useFlow = false;
        if (onlySpawnAhead && advanceTarget != null)
        {
            if (NavMesh.SamplePosition(advanceTarget.position, out NavMeshHit goalHit, 8f, NavMesh.AllAreas))
            {
                targetNavPos = goalHit.position;
                useFlow = true;
            }
            else
            {
                Debug.LogWarning("AIDirector: advanceTarget no est� cerca del NavMesh; se usa advanceDirection.", this);
            }
        }

        spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Exclude);
        mobTimer = Random.Range(mobInterval.x, mobInterval.y);
        EnterPhase(Phase.Relax, initialGrace);
        running = true;
    }

    // ---------------------------------------------------------------- API p�blica

    // Para alarmas, eventos de mapa, botones, etc. size <= 0 usa el tama�o normal.
    public void TriggerMob(int size = -1)
    {
        if (!running)
            return;

        mobPending += size > 0 ? size : RollMobSize();
        mobFailTime = 0f;
    }

    public void AddIntensity(float amount)
    {
        foreach (PlayerState state in players)
            state.intensity = Mathf.Clamp01(state.intensity + amount);
    }

    // ---------------------------------------------------------------- loop

    private void Update()
    {
        if (!running)
            return;

        float dt = Time.deltaTime;

        refreshTimer -= dt;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 1f;
            RefreshPlayers();
        }

        // Un jugador puede destruirse (desconexion) entre un refresco y otro.
        players.RemoveAll(state => state == null || state.move == null);

        CleanEnemyList();
        CollectAlivePlayers();
        UpdateIntensity(dt);
        UpdatePhase(dt);
        UpdateBackgroundSpawns(dt);
        UpdateMob(dt);

        cleanupTimer -= dt;
        if (cleanupTimer <= 0f)
        {
            cleanupTimer = 2f;
            DespawnStragglers();
        }
    }

    private void RefreshPlayers()
    {
        foreach (PMovement move in FindObjectsByType<PMovement>())
        {
            if (move == null)
                continue;

            bool known = false;
            foreach (PlayerState state in players)
            {
                if (state.move == move)
                {
                    known = true;
                    break;
                }
            }

            if (!known)
            {
                PlayerHealth health = move.GetComponent<PlayerHealth>();
                players.Add(new PlayerState
                {
                    move = move,
                    health = health,
                    lastHealth = health != null ? health.CurrentHealth : 0f,
                    lastMax = health != null ? health.MaxHealth : 0f
                });
            }
        }

        players.RemoveAll(state => state.move == null);
    }

    private void CollectAlivePlayers()
    {
        alivePlayers.Clear();
        foreach (PlayerState state in players)
        {
            if (!state.IsDead)
                alivePlayers.Add(state);
        }
    }

    private void CleanEnemyList()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.IsDead())
            {
                // Matar zombis cerca sube un poco la intensidad: est�s peleando.
                foreach (PlayerState state in players)
                {
                    if (!state.IsDead && FlatDistance(state.move.transform.position, enemy.transform.position) <= threatRange)
                        state.intensity = Mathf.Clamp01(state.intensity + killGain);
                }

                enemies.RemoveAt(i);
            }
        }
    }

    // ---------------------------------------------------------------- intensidad

    private void UpdateIntensity(float dt)
    {
        float closeSqr = closeEnemyRange * closeEnemyRange;
        float threatSqr = threatRange * threatRange;
        float team = 0f;

        foreach (PlayerState state in players)
        {
            // El da�o se detecta leyendo la vida. Si cambi� la vida m�xima (boost que
            // empieza o termina) no se cuenta como da�o.
            if (state.health != null)
            {
                float max = state.health.MaxHealth;
                float current = state.health.CurrentHealth;
                if (!state.IsDead && state.lastMax > 0f && max > 0f
                    && Mathf.Approximately(max, state.lastMax)
                    && current < state.lastHealth - 0.01f)
                {
                    state.intensity = Mathf.Clamp01(
                        state.intensity + (state.lastHealth - current) / max * damageGain);
                }

                state.lastHealth = current;
                state.lastMax = max;
            }

            if (state.IsDead)
            {
                if (!state.wasDead)
                {
                    state.wasDead = true;
                    AddIntensity(playerDownGain);
                }

                state.intensity = 0f;
                continue;
            }

            state.wasDead = false;
            Vector3 pos = state.move.transform.position;
            int close = 0;
            bool threatened = false;

            foreach (EnemyHealth enemy in enemies)
            {
                float sqr = FlatSqrDistance(pos, enemy.transform.position);
                if (sqr <= closeSqr)
                    close++;
                if (sqr <= threatSqr)
                    threatened = true;
            }

            if (close > 0)
            {
                float gain = Mathf.Min(close * closeEnemyGain, maxCloseGainPerSecond);
                state.intensity += gain * dt;
            }
            else if (!threatened)
            {
                state.intensity -= decayPerSecond * dt;
            }

            state.intensity = Mathf.Clamp01(state.intensity);
            team = Mathf.Max(team, state.intensity);
        }

        // Como en L4D: manda el jugador m�s estresado.
        TeamIntensity = team;
    }

    // ---------------------------------------------------------------- fases

    private void EnterPhase(Phase next, float duration)
    {
        phase = next;
        phaseTimer = duration;
        phaseElapsed = 0f;
    }

    private void UpdatePhase(float dt)
    {
        phaseElapsed += dt;
        phaseTimer -= dt;

        switch (phase)
        {
            case Phase.BuildUp:
                mobTimer -= dt;
                if (TeamIntensity >= peakIntensity)
                    EnterPhase(Phase.SustainPeak, Random.Range(sustainPeakTime.x, sustainPeakTime.y));
                break;

            case Phase.SustainPeak:
                if (phaseTimer <= 0f)
                    EnterPhase(Phase.PeakFade, maxPeakFadeTime);
                break;

            case Phase.PeakFade:
                if (TeamIntensity <= fadeIntensity || phaseTimer <= 0f)
                    EnterPhase(Phase.Relax, Random.Range(relaxTime.x, relaxTime.y));
                break;

            case Phase.Relax:
                if (phaseTimer <= 0f)
                    EnterPhase(Phase.BuildUp, 0f);
                break;
        }
    }

    // ---------------------------------------------------------------- spawns

    private int WandererTarget()
    {
        int extra = Mathf.Max(0, alivePlayers.Count - 1) * extraPerPlayer;
        return Mathf.Min(baseWanderers + extra, hardCap);
    }

    private void UpdateBackgroundSpawns(float dt)
    {
        if (phase != Phase.BuildUp && phase != Phase.SustainPeak)
            return;
        if (alivePlayers.Count == 0)
            return;

        spawnTimer -= dt;
        if (spawnTimer > 0f)
            return;

        spawnTimer = spawnInterval;

        if (enemies.Count >= WandererTarget() || enemies.Count >= hardCap)
            return;

        int wanted = Random.Range(groupSize.x, groupSize.y + 1);
        wanted = Mathf.Min(wanted, WandererTarget() - enemies.Count);
        SpawnGroup(wanted, false);
    }

    private void UpdateMob(float dt)
    {
        // La horda toca cuando ven�a el reloj y estamos en BuildUp; una vez arrancada, termina de salir.
        if (mobPending <= 0)
        {
            if (phase == Phase.BuildUp && mobTimer <= 0f && alivePlayers.Count > 0)
            {
                TriggerMob();
                mobTimer = Random.Range(mobInterval.x, mobInterval.y);
            }

            return;
        }

        mobBatchTimer -= dt;
        if (mobBatchTimer > 0f)
            return;

        mobBatchTimer = mobBatchInterval;

        if (enemies.Count >= hardCap)
            return;

        int batch = Mathf.Min(mobBatch, mobPending, hardCap - enemies.Count);
        int spawned = SpawnGroup(batch, true);
        mobPending -= spawned;

        if (spawned == 0)
        {
            mobFailTime += mobBatchInterval;
            if (mobFailTime >= mobGiveUpTime)
            {
                Debug.LogWarning("AIDirector: no encontr� d�nde spawnear la horda, se cancela el resto ("
                    + mobPending + "). Motivos de rechazo:" + RejectSummary());
                ResetRejects();
                mobPending = 0;
            }
        }
        else
        {
            mobFailTime = 0f;
        }
    }

    private int RollMobSize()
    {
        float scale = 1f + Mathf.Max(0, alivePlayers.Count - 1) * mobPerExtraPlayer;
        return Mathf.RoundToInt(Random.Range(mobSize.x, mobSize.y + 1) * scale);
    }

    private int SpawnGroup(int count, bool isMob)
    {
        if (count <= 0 || !TryFindSpawnPoint(out Vector3 anchor))
            return 0;

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = anchor;
            if (i > 0)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.5f);
                Vector3 raw = anchor + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;
                point = hit.position;
            }

            EnemyHealth enemy = EnemySceneSetup.SpawnNetworkEnemy(enemyPrefab, point, PickVariant());
            if (enemy == null)
                continue;

            enemies.Add(enemy);
            spawned++;

            // Los de horda persiguen sin importar la distancia (requiere Ai.StartHorde, ver parche).
            if (isMob && enemy.TryGetComponent(out Ai ai))
                ai.StartHorde();

            EnemySpawned?.Invoke(enemy, isMob);
        }

        return spawned;
    }

    private EnemyVariant PickVariant()
    {
        float normal = Mathf.Max(0f, normalWeight);
        float fragil = Mathf.Max(0f, fragilWeight);
        float resistente = Mathf.Max(0f, resistenteWeight);
        float total = normal + fragil + resistente;

        if (total <= 0f)
            return EnemyVariant.Normal;

        float roll = Random.value * total;
        if (roll < normal)
            return EnemyVariant.Normal;
        if (roll < normal + fragil)
            return EnemyVariant.Fragil;

        return EnemyVariant.Resistente;
    }

    // ---------------------------------------------------------------- b�squeda de punto

    private bool TryFindSpawnPoint(out Vector3 result)
    {
        result = default;
        if (alivePlayers.Count == 0)
            return false;

        PlayerState leader = null;
        aheadMarkers.Clear();

        if (onlySpawnAhead)
        {
            if (useFlow)
            {
                leader = LeadingByFlow(out aheadFlow);
                if (leader == null)
                    return false; // ningun jugador tiene camino a la meta
            }
            else
            {
                aheadDir = AdvanceDirection();
                leader = LeadingPlayer(aheadDir, out aheadFront);
            }

            // Solo los marcadores que estan delante del que va primero y a una distancia util.
            foreach (EnemySpawnPoint marker in spawnPoints)
            {
                if (marker == null)
                    continue;

                Vector3 p = marker.transform.position;
                float d = FlatDistance(p, leader.move.transform.position);
                if (d < minSpawnDistance || d > maxSpawnDistance * 1.1f)
                    continue;

                if (useFlow)
                {
                    if (FlowDistance(p) > aheadFlow - aheadMargin)
                        continue;
                }
                else if (Vector3.Dot(p, aheadDir) < aheadFront + aheadMargin)
                {
                    continue;
                }

                aheadMarkers.Add(marker);
            }
        }

        bool hasMarkers = spawnPoints.Length > 0;
        float bias = markersOnly && hasMarkers ? 1f : markerBias;

        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            PlayerState anchor = onlySpawnAhead ? leader : alivePlayers[Random.Range(0, alivePlayers.Count)];
            Vector3 candidate;

            bool useMarker = onlySpawnAhead
                ? aheadMarkers.Count > 0 && Random.value < bias
                : hasMarkers && Random.value < bias;

            // Solo marcadores: si no hay ninguno utilizable, no se spawnea nada.
            if (markersOnly && hasMarkers && !useMarker)
                break;

            if (useMarker)
            {
                EnemySpawnPoint marker = onlySpawnAhead
                    ? aheadMarkers[Random.Range(0, aheadMarkers.Count)]
                    : spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (marker == null)
                    continue;
                candidate = marker.transform.position;
            }
            else if (onlySpawnAhead)
            {
                // Direccion recta: abanico hacia adelante. Con meta: alrededor, y IsValidSpawn filtra por camino.
                float spread = useFlow ? 180f : 50f;
                Vector3 baseDir = useFlow ? Vector3.forward : aheadDir;
                Vector3 dir = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * baseDir;
                candidate = anchor.move.transform.position + dir * Random.Range(minSpawnDistance, maxSpawnDistance);
            }
            else
            {
                Vector3 forward = anchor.move.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.forward;
                forward.Normalize();

                float angle = Random.value < behindBias ? Random.Range(110f, 250f) : Random.Range(0f, 360f);
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                candidate = anchor.move.transform.position + dir * Random.Range(minSpawnDistance, maxSpawnDistance);
            }

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                rejects[8]++;
                continue;
            }

            if (IsValidSpawn(hit.position))
            {
                result = hit.position;
                return true;
            }
        }

        return false;
    }

    // Distancia por NavMesh desde un punto hasta la meta. Infinito si no hay camino.
    private float FlowDistance(Vector3 from)
    {
        if (!NavMesh.SamplePosition(from, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return float.PositiveInfinity;
        if (!NavMesh.CalculatePath(hit.position, targetNavPos, NavMesh.AllAreas, flowPath)
            || flowPath.status != NavMeshPathStatus.PathComplete)
            return float.PositiveInfinity;

        return PathLength(flowPath);
    }

    // El jugador mas cerca de la meta por camino real.
    private PlayerState LeadingByFlow(out float flow)
    {
        PlayerState best = null;
        flow = float.PositiveInfinity;

        foreach (PlayerState state in alivePlayers)
        {
            float f = FlowDistance(state.move.transform.position);
            if (f < flow)
            {
                flow = f;
                best = state;
            }
        }

        return best;
    }

    // Direccion en la que avanza el grupo (plana). Con advanceTarget, hacia la meta.
    private Vector3 AdvanceDirection()
    {
        Vector3 dir = advanceDirection;

        if (advanceTarget != null && alivePlayers.Count > 0)
        {
            Vector3 center = Vector3.zero;
            foreach (PlayerState state in alivePlayers)
                center += state.move.transform.position;
            center /= alivePlayers.Count;
            dir = advanceTarget.position - center;
        }

        dir.y = 0f;
        return dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
    }

    // El jugador que va mas adelante segun la direccion de avance.
    private PlayerState LeadingPlayer(Vector3 dir, out float progress)
    {
        PlayerState best = alivePlayers[0];
        progress = Vector3.Dot(best.move.transform.position, dir);

        for (int i = 1; i < alivePlayers.Count; i++)
        {
            float p = Vector3.Dot(alivePlayers[i].move.transform.position, dir);
            if (p > progress)
            {
                progress = p;
                best = alivePlayers[i];
            }
        }

        return best;
    }

    private bool IsValidSpawn(Vector3 point)
    {
        // Zonas donde nunca aparecen enemigos (la calle, por ejemplo).
        if (EnemyNoSpawnZone.IsBlocked(point))
            return Reject(9);

        // Calle recta: nunca detras del que va primero.
        if (onlySpawnAhead && !useFlow && Vector3.Dot(point, aheadDir) < aheadFront + aheadMargin)
            return Reject(0);

        PlayerState nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (PlayerState state in alivePlayers)
        {
            Vector3 playerPos = state.move.transform.position;
            float distance = FlatDistance(point, playerPos);

            if (distance < minSpawnDistance)
                return Reject(1);
            if (IsVisibleFrom(state, point))
                return Reject(2);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = state;
            }
        }

        if (nearest == null || nearestDistance > maxSpawnDistance * 1.1f)
            return Reject(3);

        // Espacio libre para un zombi.
        Vector3 feet = point + Vector3.up * 0.7f;
        Vector3 head = point + Vector3.up * 1.7f;
        if (Physics.CheckCapsule(feet, head, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            return Reject(4);

        // Con meta: "adelante" = mas cerca de la meta que el que va primero, por camino real.
        if (onlySpawnAhead && useFlow)
        {
            float flow = FlowDistance(point);
            if (float.IsInfinity(flow))
                return Reject(10);
            if (flow > aheadFlow - aheadMargin)
                return Reject(0);
        }

        // Tiene que poder llegar caminando, y sin dar una vuelta enorme.
        if (!NavMesh.SamplePosition(nearest.move.transform.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
            return Reject(5);
        if (!NavMesh.CalculatePath(point, playerHit.position, NavMesh.AllAreas, path)
            || path.status != NavMeshPathStatus.PathComplete)
            return Reject(6);

        float length = PathLength(path);
        minPathSeen = Mathf.Min(minPathSeen, length);
        if (length > maxPathLength)
            return Reject(7);

        return true;
    }

    private bool Reject(int reason)
    {
        rejects[reason]++;
        return false;
    }

    private string RejectSummary()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < rejects.Length; i++)
        {
            if (rejects[i] > 0)
                sb.Append("\n  - ").Append(RejectNames[i]).Append(": ").Append(rejects[i]);
        }

        if (sb.Length == 0)
            sb.Append("\n  (ningun candidato llego a evaluarse)");

        if (!float.IsInfinity(minPathSeen))
            sb.Append("\n  Camino mas corto visto: ").Append(minPathSeen.ToString("0")).Append(" m (maxPathLength = ").Append(maxPathLength.ToString("0")).Append(")");

        return sb.ToString();
    }

    private void ResetRejects()
    {
        System.Array.Clear(rejects, 0, rejects.Length);
        minPathSeen = float.PositiveInfinity;
    }

    private bool IsVisibleFrom(PlayerState state, Vector3 point)
    {
        Transform t = state.move.transform;
        Vector3 eye = t.position + Vector3.up * eyeHeight;
        Vector3 target = point + Vector3.up * 1.2f;
        Vector3 to = target - eye;
        float distance = to.magnitude;
        if (distance < 0.1f)
            return true;

        if (Vector3.Angle(t.forward, to) > viewHalfAngle)
            return false;

        return !Physics.Raycast(eye, to / distance, distance - 0.3f, occlusionMask, QueryTriggerInteraction.Ignore);
    }

    private void DespawnStragglers()
    {
        // Sin jugadores vivos no hay referencia: no se borra nada.
        if (alivePlayers.Count == 0)
            return;

        // Modo avance: lo que quedo muy atras del ultimo jugador ya no cuenta.
        Vector3 dir = onlySpawnAhead && !useFlow ? AdvanceDirection() : Vector3.forward;
        float rearProgress = float.PositiveInfinity;   // direccion recta: el mas atrasado
        float rearFlow = float.NegativeInfinity;       // con meta: el mas lejos de la meta
        if (onlySpawnAhead)
        {
            foreach (PlayerState state in alivePlayers)
            {
                Vector3 pos = state.move.transform.position;
                if (useFlow)
                {
                    float f = FlowDistance(pos);
                    if (!float.IsInfinity(f))
                        rearFlow = Mathf.Max(rearFlow, f);
                }
                else
                {
                    rearProgress = Mathf.Min(rearProgress, Vector3.Dot(pos, dir));
                }
            }
        }

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null)
                continue;

            Vector3 pos = enemy.transform.position;
            bool far = true;
            bool seen = false;

            foreach (PlayerState state in alivePlayers)
            {
                if (FlatDistance(pos, state.move.transform.position) <= despawnDistance)
                    far = false;
                if (IsVisibleFrom(state, pos))
                    seen = true;
            }

            if (seen)
                continue;

            bool behind = false;
            if (onlySpawnAhead)
            {
                if (useFlow)
                {
                    if (!float.IsInfinity(rearFlow))
                    {
                        float ef = FlowDistance(pos);
                        behind = !float.IsInfinity(ef) && ef > rearFlow + behindDespawnDistance;
                    }
                }
                else
                {
                    behind = Vector3.Dot(pos, dir) < rearProgress - behindDespawnDistance;
                }
            }

            if (!far && !behind)
                continue;

            NetworkObject netObj = enemy.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Destroy(enemy.gameObject);

            enemies.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------- utilidades

    private static float PathLength(NavMeshPath p)
    {
        Vector3[] corners = p.corners;
        float length = 0f;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    private static float FlatSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt(FlatSqrDistance(a, b));
    }

    // Flecha cyan: hacia donde el Director considera "adelante". Se ve al seleccionarlo.
    private void OnDrawGizmosSelected()
    {
        if (!onlySpawnAhead)
            return;

        Vector3 dir = advanceDirection;
        if (advanceTarget != null)
            dir = advanceTarget.position - transform.position;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        dir.Normalize();

        Vector3 from = transform.position + Vector3.up * 1f;
        Vector3 to = from + dir * 15f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawLine(to, to - Quaternion.AngleAxis(25f, Vector3.up) * dir * 3f);
        Gizmos.DrawLine(to, to - Quaternion.AngleAxis(-25f, Vector3.up) * dir * 3f);
    }

    private void OnGUI()
    {
        if (!showDebug || !running)
            return;

        GUI.Label(new Rect(10f, Screen.height - 34f, 520f, 24f),
            "Director: " + phase
            + " | intensidad " + TeamIntensity.ToString("0.00")
            + " | zombis " + enemies.Count
            + " | horda pendiente " + mobPending
            + " | prox horda " + Mathf.Max(0f, mobTimer).ToString("0") + "s");
    }
}