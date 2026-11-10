using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Oleadas alrededor del refugio. Cada oleada es una ronda; la cantidad de rondas para ganar la define la dificultad (SurvivalModeController).
[RequireComponent(typeof(NetworkObject))]
public class SurvivalWaveDirector : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject miniBossPrefab;
    [Tooltip("En estas rondas (2, 4, 6...) aparece un mini jefe ademas de los zombies.")]
    [SerializeField] private int bossEveryRounds = 2;
    [SerializeField] private float firstWaveDelay = 5f;
    [SerializeField] private float delayAfterClear = 2f;
    [Tooltip("Si no se pudo ubicar a ningun zombie de la oleada, reintenta en este tiempo.")]
    [SerializeField] private float retryDelay = 1f;
    [SerializeField] private int startCount = 8;
    [SerializeField] private int increment = 2;
    [SerializeField] private float minRadius = 40f;
    [SerializeField] private float maxRadius = 52f;

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

    private readonly NetworkVariable<int> roundsCleared = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly List<EnemyHealth> currentWave = new List<EnemyHealth>();
    private float timer;
    private int waveIndex;
    private bool armed;
    private bool waveActive;
    private bool waitingToSpawn;

    public int WaveNumber => waveNumber.Value;
    public int ZombiesRemaining => zombiesRemaining.Value;
    public int RoundsCleared => roundsCleared.Value;

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        RefreshRemaining();

        SurvivalModeController mode = SurvivalModeController.Current;
        if (mode == null || mode.IsFinished || !mode.CiviliansReady)
            return;

        // La primera ronda espera a que se cierre la pantalla de carga: si no, los zombies atacan antes de que el jugador vea el mapa.
        if (!armed)
        {
            if (LoadingScreen.EstaVisible)
                return;

            armed = true;
            waitingToSpawn = true;
            timer = firstWaveDelay;
        }

        if (waveActive && zombiesRemaining.Value <= 0)
        {
            waveActive = false;
            roundsCleared.Value++;

            // Con la ultima ronda superada no se arma nada mas: la victoria la decide el controlador.
            if (roundsCleared.Value >= mode.RoundsTarget)
                return;

            waitingToSpawn = true;
            timer = delayAfterClear;
        }

        if (!waitingToSpawn)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        waitingToSpawn = false;
        if (SpawnWave())
        {
            waveActive = true;
        }
        else
        {
            waitingToSpawn = true;
            timer = retryDelay;
        }
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

    // Devuelve false si no se pudo ubicar a ningun zombie: en ese caso la ronda no cuenta y se reintenta.
    private bool SpawnWave()
    {
        currentWave.Clear();
        int count = DifficultyTuning.ScaleCount(startCount + waveIndex * increment);
        Vector3 center = transform.position;

        if (!NavMesh.SamplePosition(center, out NavMeshHit refugeHit, 8f, EnemySceneSetup.WalkableMask))
            return false;

        var used = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            if (!EnemySceneSetup.TryFindWaveSpawn(center, refugeHit.position, minRadius, maxRadius, used, out Vector3 point))
                continue;

            used.Add(point);
            EnemyVariant variant = (EnemyVariant)Random.Range(0, 3);
            EnemyHealth spawned = EnemySceneSetup.SpawnWaveEnemy(enemyPrefab, point, variant);
            if (spawned != null)
                currentWave.Add(spawned);
        }

        if (currentWave.Count == 0)
        {
            Debug.LogWarning("SurvivalWaveDirector: no se pudo ubicar ningun zombie. Reintento.");
            return false;
        }

        int round = waveIndex + 1;
        bool withBoss = SpawnBoss(round, center, refugeHit.position, used);

        waveIndex++;
        waveNumber.Value = waveIndex;
        zombiesRemaining.Value = currentWave.Count;
        Debug.Log($"SurvivalWaveDirector: ronda {waveIndex} con {currentWave.Count} enemigos{(withBoss ? " y un mini jefe" : "")}.");
        return true;
    }

    // Rondas 2, 4, 6... El jefe entra en la oleada: la ronda no termina hasta que muera.
    private bool SpawnBoss(int round, Vector3 center, Vector3 refuge, List<Vector3> used)
    {
        if (miniBossPrefab == null || bossEveryRounds <= 0 || round % bossEveryRounds != 0)
            return false;

        if (!EnemySceneSetup.TryFindWaveSpawn(center, refuge, minRadius, maxRadius, used, out Vector3 point))
        {
            Debug.LogWarning($"SurvivalWaveDirector: ronda {round} sin lugar para el mini jefe.");
            return false;
        }

        EnemyHealth boss = EnemySceneSetup.SpawnWaveEnemy(miniBossPrefab, point, EnemyVariant.Normal);
        if (boss == null)
            return false;

        currentWave.Add(boss);
        return true;
    }
}
