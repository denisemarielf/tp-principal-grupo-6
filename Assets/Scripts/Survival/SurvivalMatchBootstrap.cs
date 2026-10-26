using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// En la escena de Supervivencia: espera el NavMesh y spawnea la partida en red junto a la estación.
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
        GameObject station = GameObject.Find("Estacion");
        Vector3 origin = station != null ? station.transform.position : transform.position;
        origin.y = 30.35f;

        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            return hit.position;

        PlayerSpawnPoint spawn = FindAnyObjectByType<PlayerSpawnPoint>();
        if (spawn != null && NavMesh.SamplePosition(spawn.transform.position, out hit, 12f, NavMesh.AllAreas))
        {
            Debug.LogWarning("SurvivalMatchBootstrap: no hay NavMesh en la estación. Uso el spawn de jugadores.");
            return hit.position;
        }

        Debug.LogWarning("SurvivalMatchBootstrap: no hay NavMesh cerca de la estación.");
        return spawn != null ? spawn.transform.position : origin;
    }
}
