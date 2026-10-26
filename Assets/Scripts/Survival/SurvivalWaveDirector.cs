using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Oleadas alrededor del refugio mientras la partida de Supervivencia sigue abierta.
[RequireComponent(typeof(NetworkObject))]
public class SurvivalWaveDirector : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float interval = 25f;
    [SerializeField] private int startCount = 4;
    [SerializeField] private int increment = 2;
    [SerializeField] private float minRadius = 18f;
    [SerializeField] private float maxRadius = 28f;

    private readonly NetworkVariable<int> waveNumber = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> zombiesRemaining = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly List<EnemyHealth> currentWave = new List<EnemyHealth>();
    private float timer;
    private int waveIndex;
    private bool armed;

    public int WaveNumber => waveNumber.Value;
    public int ZombiesRemaining => zombiesRemaining.Value;

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        RefreshRemaining();

        SurvivalModeController mode = SurvivalModeController.Current;
        if (mode == null || mode.IsFinished || !mode.CiviliansReady)
            return;

        if (!armed)
        {
            armed = true;
            timer = interval;
        }

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = interval;
        SpawnWave();
    }

    private void RefreshRemaining()
    {
        int alive = 0;
        for (int i = currentWave.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = currentWave[i];
            if (enemy == null)
            {
                currentWave.RemoveAt(i);
                continue;
            }

            if (!enemy.IsDead())
                alive++;
        }

        if (zombiesRemaining.Value != alive)
            zombiesRemaining.Value = alive;
    }

    private void SpawnWave()
    {
        currentWave.Clear();
        int count = startCount + waveIndex * increment;
        waveIndex++;
        waveNumber.Value = waveIndex;
        Vector3 center = transform.position;

        for (int i = 0; i < count; i++)
        {
            float angle = ((360f / count) * i + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 desired = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            EnemyVariant variant = (EnemyVariant)Random.Range(0, 3);
            EnemyHealth spawned = EnemySceneSetup.SpawnNetworkEnemy(enemyPrefab, desired, variant);
            if (spawned != null)
                currentWave.Add(spawned);
        }

        zombiesRemaining.Value = currentWave.Count;
        Debug.Log($"SurvivalWaveDirector: oleada {waveIndex} con {currentWave.Count} enemigos.");
    }
}
