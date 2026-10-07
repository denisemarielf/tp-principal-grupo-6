using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemySceneSetup : MonoBehaviour
{
    [Serializable]
    private struct EnemySpawn
    {
        public Vector3 position;
        public EnemyVariant variant;
    }

    [SerializeField] private GameObject enemyPrefab;

    [Header("NavMesh")]
    [Tooltip("Desactivado: arma el NavMesh con el MeshFilter de este objeto (piso de prueba). Activado: con los colliders de la escena (terreno, calles, edificios) dentro de 'Nav Mesh Size'.")]
    [SerializeField] private bool useSceneColliders = false;
    [SerializeField] private Vector3 navMeshSize = new Vector3(80f, 20f, 80f);

    [Header("Enemigos")]
    [Tooltip("Posiciones relativas a este objeto. Si esta vacio se usan las posiciones de prueba.")]
    [SerializeField] private List<EnemySpawn> spawns = new List<EnemySpawn>();

    private void Start()
    {
        StartCoroutine(SetupWhenServer());
    }

    private IEnumerator SetupWhenServer()
    {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        if (!NetworkManager.Singleton.IsServer)
            yield break;


        yield return BuildNavMesh();

        if (spawns.Count == 0)
        {
            Spawn(new Vector3(3f, 1f, 2f), EnemyVariant.Normal);
            Spawn(new Vector3(-2.5f, 1f, 3.5f), EnemyVariant.Resistente);
            yield break;
        }

        foreach (EnemySpawn spawn in spawns)
            Spawn(transform.position + spawn.position, spawn.variant);
    }

    private void Spawn(Vector3 position, EnemyVariant variant)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySceneSetup: falta el prefab del enemigo.");
            return;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            position = hit.position + Vector3.up * FeetLift(enemyPrefab);

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
        Bounds bounds = new Bounds(transform.position, navMeshSize);
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

    // Los autos no son piso. Si entran al horneado, el taxi corta la calle.
    private static List<NavMeshBuildMarkup> VehicleMarkups()
    {
        var markups = new List<NavMeshBuildMarkup>();
        var ignored = new HashSet<Transform>();

        foreach (Collider collider in FindObjectsByType<Collider>())
        {
            Transform vehicle = VehicleRoot(collider.transform);
            if (vehicle == null || !ignored.Add(vehicle))
                continue;

            markups.Add(new NavMeshBuildMarkup
            {
                root = vehicle,
                ignoreFromBuild = true
            });
        }

        return markups;
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
