using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemySceneSetup : MonoBehaviour
{
    const int NotWalkableArea = 1;

    [SerializeField] private GameObject enemyPrefab;

    [Header("NavMesh")]
    [Tooltip("Desactivado: arma el NavMesh con el MeshFilter de este objeto (piso de prueba). Activado: con los colliders de la escena (terreno, calles, edificios).")]
    [SerializeField] private bool useSceneColliders = false;
    [SerializeField] private Vector3 navMeshSize = new Vector3(80f, 20f, 80f);
    [SerializeField] private string[] mapRootNames = { "AllProps", "Road Network", "Plane", "WorlWall" };

    [Header("Enemigos")]
    [SerializeField] private int enemyCount = 80;
    [SerializeField] private float minDistanceFromSpawn = 25f;
    [SerializeField] private float minSeparation = 12f;
    [Tooltip("Aparecen cuando un jugador se acerca a esta distancia, para no crear todo el mapa de una.")]
    [SerializeField] private float activateDistance = 80f;
    [Tooltip("Si quedan más lejos que esto de todos los jugadores, se guardan hasta que alguien vuelva.")]
    [SerializeField] private float despawnDistance = 120f;
    [Tooltip("Los enemigos solo se planifican sobre este objeto, la calzada.")]
    [SerializeField] private string roadObjectName = "road_0001";
    [Tooltip("Metros hacia adentro del borde de la calle, para no aparecer en el cordón.")]
    [SerializeField] private float roadEdgeMargin = 1.5f;

    private readonly List<PlannedEnemy> planned = new List<PlannedEnemy>();
    private Bounds roadBounds;
    private bool hasRoad;
    private float streamTimer;
    private bool streaming;

    private class PlannedEnemy
    {
        public Vector3 position;
        public EnemyVariant variant;
        public EnemyHealth alive;
        public bool eliminated;
    }

    private void Start()
    {
        StartCoroutine(SetupWhenServer());
    }

    private void Update()
    {
        if (!streaming)
            return;

        streamTimer -= Time.deltaTime;
        if (streamTimer > 0f)
            return;

        streamTimer = 0.5f;
        UpdateStreaming();
    }

    private IEnumerator SetupWhenServer()
    {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        if (!NetworkManager.Singleton.IsServer)
            yield break;

        yield return BuildNavMesh();

        if (!useSceneColliders)
        {
            Spawn(new Vector3(3f, 1f, 2f), EnemyVariant.Normal);
            Spawn(new Vector3(-2.5f, 1f, 3.5f), EnemyVariant.Resistente);
            yield break;
        }

        PlanAcrossMap();
        streaming = true;
        UpdateStreaming();
    }

    private void PlanAcrossMap()
    {
        if (!TryGetRoadBounds(out roadBounds))
        {
            Debug.LogWarning("EnemySceneSetup: no está la calle, no hay puntos de enemigos.", this);
            return;
        }

        hasRoad = true;
        float margin = RoadMargin(roadBounds);
        Vector3 playerSpawn = PlayerSpawnPosition();
        var chosen = new List<Vector3>(enemyCount);
        int attempts = Mathf.Max(enemyCount * 100, 1);

        for (int i = 0; i < attempts && chosen.Count < enemyCount; i++)
        {
            // Solo sobre el asfalto. Si el punto cae en un auto, el NavMesh lo corre y se descarta si sale de la calle.
            Vector3 candidate = new Vector3(
                Random.Range(roadBounds.min.x + margin, roadBounds.max.x - margin),
                1f,
                Random.Range(roadBounds.min.z + margin, roadBounds.max.z - margin));

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                continue;

            Vector3 point = hit.position;
            if (!InsideRoad(point, roadBounds, 0.4f))
                continue;
            if (FlatDistance(point, playerSpawn) < minDistanceFromSpawn)
                continue;
            if (TooCloseToAnother(point, chosen))
                continue;
            if (BlocksSpawn(point))
                continue;

            chosen.Add(point);
            planned.Add(new PlannedEnemy
            {
                position = point,
                variant = (EnemyVariant)Random.Range(0, 3)
            });
        }

        if (planned.Count < enemyCount)
            Debug.LogWarning("EnemySceneSetup: quedaron " + planned.Count + " de " + enemyCount + " puntos de enemigos.", this);
    }

    // Solo existen los que están cerca de alguien. El resto espera a que el grupo avance.
    private void UpdateStreaming()
    {
        List<Vector3> players = LivingPlayerPositions();
        if (players.Count == 0)
            players.Add(PlayerSpawnPosition());

        foreach (PlannedEnemy slot in planned)
        {
            if (slot.eliminated)
                continue;

            Vector3 place = slot.alive != null ? slot.alive.transform.position : slot.position;
            float nearest = NearestDistance(place, players);
            if (slot.alive != null)
            {
                if (slot.alive.IsDead())
                {
                    slot.eliminated = true;
                    continue;
                }

                if (nearest > despawnDistance)
                {
                    DespawnEnemy(slot.alive);
                    slot.alive = null;
                }

                continue;
            }

            if (nearest <= activateDistance)
                slot.alive = Spawn(slot.position, slot.variant);
        }
    }

    private EnemyHealth Spawn(Vector3 position, EnemyVariant variant)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySceneSetup: falta el prefab del enemigo.");
            return null;
        }

        float sampleRadius = hasRoad ? 2f : 8f;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)
            && (!hasRoad || InsideRoad(hit.position, roadBounds, 0.4f)))
        {
            position = hit.position + Vector3.up * FeetLift(enemyPrefab);
        }
        else if (hasRoad)
        {
            position += Vector3.up * FeetLift(enemyPrefab);
        }

        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
            health.Configure(variant);

        EnemyAppearance appearance = enemy.GetComponent<EnemyAppearance>();
        if (appearance != null)
            appearance.Configure(variant);

        Ai ai = enemy.GetComponent<Ai>();
        if (ai != null)
            ai.Configure(variant);

        NetworkObject netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn(true);

        return health;
    }

    private static void DespawnEnemy(EnemyHealth health)
    {
        if (health == null)
            return;

        NetworkObject netObj = health.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(health.gameObject);
    }

    // La capsula tiene el pivote en el centro. Hay que subirla para que los pies
    // queden sobre el NavMesh.
    private static float FeetLift(GameObject prefab)
    {
        CapsuleCollider body = prefab.GetComponent<CapsuleCollider>();
        if (body == null)
            return 1f;

        float scaleY = Mathf.Abs(prefab.transform.localScale.y);
        float bottom = (body.center.y - body.height * 0.5f) * scaleY;
        return -bottom;
    }

    private IEnumerator BuildNavMesh()
    {
        if (useSceneColliders)
            EnsureVehicleColliders();

        Bounds bounds = useSceneColliders ? MapBounds() : new Bounds(transform.position, navMeshSize);
        List<NavMeshBuildSource> sources = useSceneColliders ? CollectSceneSources(bounds) : CollectOwnMesh();
        if (sources == null)
            sources = new List<NavMeshBuildSource>();

        // Una malla sin lectura (por ejemplo Col_LF00top) no puede entrar al NavMesh.
        // En el editor avisa; en el juego compilado el horneado falla.
        int unreadables = sources.RemoveAll(source =>
            source.shape == NavMeshBuildSourceShape.Mesh
            && source.sourceObject is Mesh mesh
            && !mesh.isReadable);

        if (unreadables > 0)
            Debug.LogWarning("EnemySceneSetup: se omitieron " + unreadables + " mallas sin lectura.", this);

        if (sources.Count == 0)
        {
            Debug.LogError("EnemySceneSetup: no hay geometria legible para armar el NavMesh.", this);
            yield break;
        }

        NavMeshData data = new NavMeshData();
        AsyncOperation bake = NavMeshBuilder.UpdateNavMeshDataAsync(
            data,
            NavMesh.GetSettingsByIndex(0),
            sources,
            bounds);

        while (!bake.isDone)
            yield return null;

        NavMesh.AddNavMeshData(data);
    }

    private Bounds MapBounds()
    {
        bool any = false;
        Bounds bounds = new Bounds(transform.position, navMeshSize);

        foreach (string rootName in mapRootNames)
        {
            if (string.IsNullOrEmpty(rootName))
                continue;

            GameObject root = GameObject.Find(rootName);
            if (root == null)
                continue;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                    continue;
                if (!any)
                {
                    bounds = collider.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (any)
            bounds.Expand(new Vector3(2f, 4f, 2f));

        return bounds;
    }

    private List<NavMeshBuildSource> CollectOwnMesh()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return null;

        return new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = filter.sharedMesh,
                transform = transform.localToWorldMatrix,
                area = 0
            }
        };
    }

    private static List<NavMeshBuildSource> CollectSceneSources(Bounds bounds)
    {
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(
            bounds,
            ~0,
            NavMeshCollectGeometry.PhysicsColliders,
            0,
            VehicleMarkups(),
            sources);

        // Los jugadores y enemigos ya spawneados se mueven: no son parte del piso.
        sources.RemoveAll(source => source.component != null
            && source.component.GetComponentInParent<NetworkObject>() != null);

        return sources;
    }

    // Caja del auto, no la malla visual: esa malla cortaba la calle entera.
    private static void EnsureVehicleColliders()
    {
        var vehicles = new HashSet<Transform>();
        foreach (Renderer renderer in FindObjectsByType<Renderer>())
        {
            Transform vehicle = VehicleRoot(renderer.transform);
            if (vehicle != null)
                vehicles.Add(vehicle);
        }

        foreach (Transform vehicle in vehicles)
        {
            if (HasSolidCollider(vehicle))
                continue;
            if (!TryVehicleBox(vehicle, out Vector3 center, out Vector3 size))
                continue;

            BoxCollider box = vehicle.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
    }

    // Los autos no son piso: se hornean como zona no caminable para que el NavMesh los rodee.
    private static List<NavMeshBuildMarkup> VehicleMarkups()
    {
        var markups = new List<NavMeshBuildMarkup>();
        var marked = new HashSet<Transform>();

        foreach (Collider collider in FindObjectsByType<Collider>())
        {
            Transform vehicle = VehicleRoot(collider.transform);
            if (vehicle == null || !marked.Add(vehicle))
                continue;

            markups.Add(new NavMeshBuildMarkup
            {
                root = vehicle,
                overrideArea = true,
                area = NotWalkableArea,
                ignoreFromBuild = false,
                applyToChildren = true
            });
        }

        return markups;
    }

    private static bool HasSolidCollider(Transform vehicle)
    {
        foreach (Collider collider in vehicle.GetComponentsInChildren<Collider>())
        {
            if (!collider.isTrigger)
                return true;
        }

        return false;
    }

    private static bool TryVehicleBox(Transform vehicle, out Vector3 center, out Vector3 size)
    {
        center = Vector3.zero;
        size = Vector3.zero;
        bool any = false;
        Bounds local = new Bounds();

        foreach (Renderer renderer in vehicle.GetComponentsInChildren<Renderer>())
        {
            Bounds world = renderer.bounds;
            Vector3 extents = world.extents;
            Vector3 worldCenter = world.center;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = worldCenter + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 localCorner = vehicle.InverseTransformPoint(worldCorner);
                if (!any)
                {
                    local = new Bounds(localCorner, Vector3.zero);
                    any = true;
                }
                else
                {
                    local.Encapsulate(localCorner);
                }
            }
        }

        if (!any || local.size.sqrMagnitude < 0.01f)
            return false;

        center = local.center;
        size = local.size;
        return true;
    }

    private static List<Vector3> LivingPlayerPositions()
    {
        var positions = new List<Vector3>();
        foreach (PMovement player in FindObjectsByType<PMovement>())
        {
            if (player == null)
                continue;
            if (player.TryGetComponent(out PlayerHealth health) && health.IsDead)
                continue;
            positions.Add(player.transform.position);
        }

        return positions;
    }

    private static float NearestDistance(Vector3 point, List<Vector3> players)
    {
        float nearest = float.PositiveInfinity;
        foreach (Vector3 player in players)
        {
            float distance = FlatDistance(point, player);
            if (distance < nearest)
                nearest = distance;
        }

        return nearest;
    }

    private Vector3 PlayerSpawnPosition()
    {
        PlayerSpawnPoint spawn = FindAnyObjectByType<PlayerSpawnPoint>();
        if (spawn != null)
            return spawn.transform.position;

        return transform.position;
    }

    private bool TooCloseToAnother(Vector3 point, List<Vector3> chosen)
    {
        foreach (Vector3 other in chosen)
        {
            if (FlatDistance(point, other) < minSeparation)
                return true;
        }

        return false;
    }

    // El piso se acepta. Una pared, un mueble o un auto no.
    private static bool BlocksSpawn(Vector3 point)
    {
        Vector3 feet = point + Vector3.up * 0.6f;
        Vector3 head = point + Vector3.up * 1.7f;
        Collider[] hits = Physics.OverlapCapsule(feet, head, 0.35f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.isTrigger || IsFloor(point, hit))
                continue;
            return true;
        }

        return false;
    }

    private static bool IsFloor(Vector3 point, Collider hit)
    {
        Vector3 origin = point + Vector3.up * 1.5f;
        RaycastHit[] rays = Physics.RaycastAll(origin, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit ray in rays)
        {
            if (ray.collider == hit && ray.normal.y > 0.5f)
                return true;
        }

        return false;
    }

    private bool TryGetRoadBounds(out Bounds road)
    {
        road = default;
        if (string.IsNullOrEmpty(roadObjectName))
            return false;

        GameObject roadObject = GameObject.Find(roadObjectName);
        if (roadObject == null)
            return false;

        Renderer renderer = roadObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            road = renderer.bounds;
            return road.size.x > 0.1f && road.size.z > 0.1f;
        }

        Collider collider = roadObject.GetComponent<Collider>();
        if (collider == null)
            return false;

        road = collider.bounds;
        return road.size.x > 0.1f && road.size.z > 0.1f;
    }

    private float RoadMargin(Bounds road)
    {
        float margin = Mathf.Max(0f, roadEdgeMargin);
        if (road.size.x <= margin * 2f || road.size.z <= margin * 2f)
            return 0f;

        return margin;
    }

    private static bool InsideRoad(Vector3 point, Bounds road, float margin)
    {
        return point.x >= road.min.x + margin
            && point.x <= road.max.x - margin
            && point.z >= road.min.z + margin
            && point.z <= road.max.z - margin;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static Transform VehicleRoot(Transform current)
    {
        Transform found = null;
        while (current != null)
        {
            if (current.name.Contains("Vehicle"))
                found = current;
            current = current.parent;
        }

        return found;
    }
}
