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


        BuildNavMesh();
        yield return null;

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

    // La capsula tiene el pivote en el centro. Con escala 2, hay que subir el doble
    // para que los pies queden sobre el NavMesh.
    private static float FeetLift(GameObject prefab)
    {
        CapsuleCollider body = prefab.GetComponent<CapsuleCollider>();
        if (body == null)
            return 1f;

        float scaleY = Mathf.Abs(prefab.transform.localScale.y);
        float bottom = (body.center.y - body.height * 0.5f) * scaleY;
        return -bottom;
    }

    private void BuildNavMesh()
    {
        Bounds bounds = new Bounds(transform.position, navMeshSize);
        List<NavMeshBuildSource> sources = useSceneColliders ? CollectSceneSources(bounds) : CollectOwnMesh();
        if (sources == null || sources.Count == 0)
        {
            Debug.LogError("EnemySceneSetup: no hay geometria para armar el NavMesh.", this);
            return;
        }

        
        foreach (var source in sources)
        {
            if (source.sourceObject is Mesh mesh)
            {
                Debug.Log(
                $"Mesh: {mesh.name} | Readable: {mesh.isReadable}");

                if (mesh.name == "COL")
                {
                    Debug.LogError(
                    $"Encontrada mesh COL en objeto: {source.component?.gameObject.name}");
                }
            }
        }

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByIndex(0),
            sources,
            bounds,
            Vector3.zero,
            Quaternion.identity);

        if (data != null)
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
            new List<NavMeshBuildMarkup>(),
            sources);

        // Los jugadores y enemigos ya spawneados se mueven: no son parte del piso.
        sources.RemoveAll(source => source.component != null
            && source.component.GetComponentInParent<NetworkObject>() != null);

        return sources;
    }
}
