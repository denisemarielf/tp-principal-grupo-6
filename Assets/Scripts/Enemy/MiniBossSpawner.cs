using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Hace aparecer al mini jefe cada tanto. Va en cualquier GameObject de la escena (no necesita NetworkObject).
// Solo actua en el server/host. Nunca hay mas de maxAlive vivos a la vez.
public class MiniBossSpawner : MonoBehaviour
{
    [SerializeField] private GameObject miniBossPrefab;

    [Header("Frecuencia")]
    [SerializeField] private float firstSpawnDelay = 120f;
    [SerializeField] private Vector2 spawnInterval = new Vector2(180f, 360f);
    [SerializeField] private int maxAlive = 1;

    [Header("Donde aparece")]
    [Tooltip("Distancia al jugador elegido (lejos, para que no aparezca en la cara).")]
    [SerializeField] private Vector2 spawnDistance = new Vector2(35f, 60f);
    [SerializeField] private int maxAttempts = 12;

    private readonly List<NetworkObject> alive = new List<NetworkObject>();
    private float nextSpawnTime;

#if UNITY_EDITOR
    private void Reset()
    {
        miniBossPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/MiniBoss.prefab");
    }
#endif

    private void Start()
    {
        nextSpawnTime = Time.time + firstSpawnDelay;
    }

    private void Update()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || !manager.IsServer || miniBossPrefab == null)
            return;

        alive.RemoveAll(o => o == null || !o.IsSpawned);

        if (Time.time < nextSpawnTime || alive.Count >= maxAlive)
            return;
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted)
            return;
        // En Supervivencia el jefe sale con las oleadas (cada 2 rondas), no por este temporizador.
        if (SurvivalModeController.Current != null)
            return;

        if (TrySpawn())
            nextSpawnTime = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
        else
            nextSpawnTime = Time.time + 5f; // no encontro lugar: reintenta pronto
    }

    [ContextMenu("Spawn ahora (solo en Play, como host)")]
    public void SpawnNow()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsServer)
            TrySpawn();
    }

    private bool TrySpawn()
    {
        List<PMovement> players = new List<PMovement>();
        foreach (PMovement candidate in FindObjectsByType<PMovement>())
        {
            if (candidate == null) continue;
            if (candidate.TryGetComponent(out PlayerHealth health) && health.IsDead) continue;
            players.Add(candidate);
        }

        if (players.Count == 0)
            return false;

        Transform anchor = players[Random.Range(0, players.Count)].transform;

        for (int i = 0; i < maxAttempts; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(spawnDistance.x, spawnDistance.y);
            Vector3 candidate = anchor.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            Vector3 look = anchor.position - hit.position;
            look.y = 0f;
            Quaternion rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

            GameObject instance = Instantiate(miniBossPrefab, hit.position, rotation);
            if (!instance.TryGetComponent(out NetworkObject networkObject))
            {
                Destroy(instance);
                Debug.LogError("MiniBossSpawner: el prefab no tiene NetworkObject.");
                return false;
            }

            networkObject.Spawn(true);
            alive.Add(networkObject);
            return true;
        }

        return false;
    }
}
