using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawnerWaves : NetworkBehaviour
{
    [Header("--------Spawn--------")]
    public GameObject enemyPrefab;
    public Transform spawnPoint;
    public int enemiesToSpawn = 20;
    public float timeSpawns = 2f;

    [Header("--------Oleadas--------")]
    public float waveInterval = 30f;
    [Tooltip("Si esta apagado, spawnea una sola oleada.")]
    public bool repeatWaves = true;

    private Coroutine spawnRoutine;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        spawnRoutine = StartCoroutine(SpawnWavesLoop());
    }

    public override void OnNetworkDespawn()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnWavesLoop()
    {
        do
        {
            yield return StartCoroutine(SpawnEnemies());

            if (repeatWaves)
                yield return new WaitForSeconds(waveInterval);
        }
        while (repeatWaves);
    }

    private IEnumerator SpawnEnemies()
    {
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnOne();
            yield return new WaitForSeconds(timeSpawns);
        }
    }

    private void SpawnOne()
    {
        if (enemyPrefab == null || spawnPoint == null)
            return;

        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        if (enemy.TryGetComponent(out NetworkObject netObj))
            netObj.Spawn();
        else
        {
            Debug.LogError("enemyPrefab no tiene NetworkObject asignado.", this);
            Destroy(enemy);
        }
    }

    public void setEnemiesToSpawn(int number)
    {
        enemiesToSpawn = number;
    }

    public void setWaveInterval(float number)
    {
        waveInterval = number;
    }
}