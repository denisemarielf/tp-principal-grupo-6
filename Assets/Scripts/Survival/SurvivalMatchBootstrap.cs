using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// En la escena de Supervivencia: espera el NavMesh y spawnea la partida en el parque.
public class SurvivalMatchBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject matchPrefab;

    private bool started;

    private void OnEnable()
    {
        EnemySceneSetup.NavMeshReady += OnNavMeshReady;
    }

    private void OnDisable()
    {
        EnemySceneSetup.NavMeshReady -= OnNavMeshReady;
    }

    private void OnNavMeshReady()
    {
        if (started)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
            return;

        if (matchPrefab == null)
        {
            Debug.LogError("SurvivalMatchBootstrap: falta el prefab SurvivalMatch.");
            return;
        }

        started = true;
        Vector3 position = RefugePosition();
        GameObject match = Instantiate(matchPrefab, position, Quaternion.identity);
        NetworkObject netObj = match.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("SurvivalMatchBootstrap: SurvivalMatch no tiene NetworkObject.");
            Destroy(match);
            started = false;
            return;
        }

        netObj.Spawn(true);
    }

    private Vector3 RefugePosition()
    {
        Vector3 origin = ParkCenter();
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, 25f, NavMesh.AllAreas))
            return hit.position;

        PlayerSpawnPoint spawn = FindAnyObjectByType<PlayerSpawnPoint>();
        if (spawn != null && NavMesh.SamplePosition(spawn.transform.position, out hit, 25f, NavMesh.AllAreas))
        {
            Debug.LogWarning("SurvivalMatchBootstrap: no hay NavMesh en el centro del parque. Uso el spawn de jugadores.");
            return hit.position;
        }

        Debug.LogWarning("SurvivalMatchBootstrap: no hay NavMesh en el parque.");
        return spawn != null ? spawn.transform.position : origin;
    }

    // Centro del grupo de arboles del parque, a ras del piso.
    private Vector3 ParkCenter()
    {
        GameObject park = GameObject.Find("Arboles");
        if (park == null)
            return transform.position;

        bool any = false;
        Bounds bounds = new Bounds(park.transform.position, Vector3.zero);
        foreach (Renderer renderer in park.GetComponentsInChildren<Renderer>())
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

        if (!any)
            return park.transform.position;

        Vector3 center = bounds.center;
        center.y = bounds.min.y + 0.2f;
        return center;
    }
}
