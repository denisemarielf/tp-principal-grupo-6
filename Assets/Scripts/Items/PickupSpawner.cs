using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Instancia los pickups en la red (US 1.2). Solo actua en el host: los clientes los reciben
// replicados por Netcode. Cuando alguien agarra uno, el host lo vuelve a crear despues de un rato.
public class PickupSpawner : MonoBehaviour
{
    [SerializeField] private SpeedBoostPickup pickupPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float respawnSeconds = 8f;

    private NetworkManager networkManager;

    private void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("PickupSpawner: no hay NetworkManager en la escena.", this);
            enabled = false;
            return;
        }

        if (pickupPrefab == null)
        {
            Debug.LogError("PickupSpawner: falta asignar 'Pickup Prefab' en el Inspector.", this);
            enabled = false;
            return;
        }

        networkManager.OnServerStarted += SpawnAll;
        networkManager.OnServerStopped += OnServerStopped;

        if (networkManager.IsServer)
        {
            SpawnAll();
        }
    }

    private void OnDestroy()
    {
        if (networkManager == null) return;

        networkManager.OnServerStarted -= SpawnAll;
        networkManager.OnServerStopped -= OnServerStopped;
    }

    private void SpawnAll()
    {
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                SpawnAt(point);
            }
        }
    }

    private void SpawnAt(Transform point)
    {
        if (!networkManager.IsServer) return;

        SpeedBoostPickup pickup = Instantiate(pickupPrefab, point.position, point.rotation);
        pickup.Collected += _ => StartCoroutine(RespawnAfterDelay(point));
        pickup.GetComponent<NetworkObject>().Spawn(true);
    }

    private IEnumerator RespawnAfterDelay(Transform point)
    {
        yield return new WaitForSeconds(respawnSeconds);
        SpawnAt(point);
    }

    // Evita que un respawn pendiente de una partida anterior aparezca en la siguiente.
    private void OnServerStopped(bool wasHost)
    {
        StopAllCoroutines();
    }
}
