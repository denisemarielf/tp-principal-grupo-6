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

    private float timer;
    private int waveIndex;
    private bool armed;

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

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

    private void SpawnWave()
    {
        int count = startCount + waveIndex * increment;
        waveIndex++;
        Vector3 center = transform.position;

        for (int i = 0; i < count; i++)
        {
            float angle = ((360f / count) * i + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 desired = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            EnemyVariant variant = (EnemyVariant)Random.Range(0, 3);
            EnemySceneSetup.SpawnNetworkEnemy(enemyPrefab, desired, variant);
        }

        Debug.Log($"SurvivalWaveDirector: oleada {waveIndex} con {count} enemigos.");
    }
}
