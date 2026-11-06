using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [Header("--------Spawn--------")]
    public GameObject enemyPrefab;
    public Transform spawnPoint;
    public int enemiesToSpawn = 20;
    public float timeSpawns = 2f;
    [Header("--------Oleadas--------")]
    public float waveInterval = 30f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        StartCoroutine(SpawnWavesLoop());
    }

    private IEnumerator SpawnWavesLoop()
    {
        while (true)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsMatchOver)
                yield break;

            yield return StartCoroutine(SpawnEnemies());

            if (GameManager.Instance != null && GameManager.Instance.IsMatchOver)
                yield break;

            yield return new WaitForSeconds(waveInterval);
        }
    }

    private IEnumerator SpawnEnemies()
    {
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsMatchOver)
                yield break;

            if (enemyPrefab != null && spawnPoint != null)
            {
                GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

                NetworkObject netObj = enemy.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
                else
                {
                    Debug.LogError("enemyPrefab no tiene NetworkObject asignado.");
                }
            }

            yield return new WaitForSeconds(timeSpawns);
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
