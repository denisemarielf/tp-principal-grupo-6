using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public enum SurvivalMatchOutcome : byte
{
    None,
    Victory,
    DefeatPlayers,
    DefeatCivilians
}

// Partida de Supervivencia (US 4.3). Solo el server decide el resultado y lo replica.
// Victoria: se acaba el tiempo y queda al menos un civil vivo.
// Derrota: mueren todos los jugadores o todos los civiles del refugio.
[RequireComponent(typeof(NetworkObject))]
public class SurvivalModeController : NetworkBehaviour
{
    public static SurvivalModeController Current { get; private set; }
    public static event Action<SurvivalMatchOutcome> MatchEnded;

    [SerializeField] private float matchDuration = 300f;
    [SerializeField] private int civilianCount = 4;
    [SerializeField] private int pointsPerCivilian = 100;
    [SerializeField] private float civilianRingRadius = 2.5f;
    [SerializeField] private GameObject civilianPrefab;

    private readonly NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        300f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<SurvivalMatchOutcome> outcome = new NetworkVariable<SurvivalMatchOutcome>(
        SurvivalMatchOutcome.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> aliveCivilians = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> endScore = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float durationLeft;
    private float timeSyncTimer;
    private int spawnedCivilians;
    private bool civiliansReady;

    public float TimeRemaining => timeRemaining.Value;
    public SurvivalMatchOutcome Outcome => outcome.Value;
    public int AliveCivilians => aliveCivilians.Value;
    public int EndScore => endScore.Value;
    public bool IsFinished => outcome.Value != SurvivalMatchOutcome.None;
    public bool CiviliansReady => civiliansReady;

    public override void OnNetworkSpawn()
    {
        Current = this;
        outcome.OnValueChanged += OnOutcomeChanged;

        if (IsServer)
        {
            durationLeft = matchDuration;
            timeRemaining.Value = matchDuration;
            SnapToNavMesh();
            SpawnCivilians();
        }

        if (outcome.Value != SurvivalMatchOutcome.None)
            MatchEnded?.Invoke(outcome.Value);
    }

    public override void OnNetworkDespawn()
    {
        outcome.OnValueChanged -= OnOutcomeChanged;
        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || outcome.Value != SurvivalMatchOutcome.None)
            return;

        if (!civiliansReady)
            return;

        durationLeft -= Time.deltaTime;
        timeSyncTimer -= Time.deltaTime;
        if (timeSyncTimer <= 0f)
        {
            timeSyncTimer = 0.25f;
            timeRemaining.Value = Mathf.Max(0f, durationLeft);
        }

        int alive = CountAliveShelterCivilians();
        if (aliveCivilians.Value != alive)
            aliveCivilians.Value = alive;

        if (spawnedCivilians > 0 && alive <= 0)
        {
            Finish(SurvivalMatchOutcome.DefeatCivilians, 0);
            return;
        }

        if (AllPlayersDead())
        {
            Finish(SurvivalMatchOutcome.DefeatPlayers, 0);
            return;
        }

        if (durationLeft <= 0f && alive > 0)
            Finish(SurvivalMatchOutcome.Victory, alive * pointsPerCivilian);
    }

    private void SnapToNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 12f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            return;
        }

        PlayerSpawnPoint spawn = FindAnyObjectByType<PlayerSpawnPoint>();
        if (spawn != null && NavMesh.SamplePosition(spawn.transform.position, out hit, 12f, NavMesh.AllAreas))
        {
            Debug.LogWarning("SurvivalModeController: no hay NavMesh en la estación. Uso el spawn de jugadores.");
            transform.position = hit.position;
            return;
        }

        Debug.LogWarning("SurvivalModeController: no hay NavMesh cerca del refugio.");
    }

    private void SpawnCivilians()
    {
        if (civilianPrefab == null)
        {
            Debug.LogError("SurvivalModeController: falta el prefab del civil.");
            return;
        }

        int count = Mathf.Max(1, civilianCount);
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count;
            Vector3 desired = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * civilianRingRadius;
            if (!TryNavPoint(desired, out Vector3 point) && !TryNavPoint(transform.position, out point))
            {
                Debug.LogWarning("SurvivalModeController: no pude apoyar un civil en el NavMesh.");
                continue;
            }

            GameObject civilian = Instantiate(civilianPrefab, point, Quaternion.identity);
            RescuableNpc npc = civilian.GetComponent<RescuableNpc>();
            if (npc != null)
                npc.ConfigureAsShelter();

            NetworkObject netObj = civilian.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("SurvivalModeController: el civil no tiene NetworkObject.");
                Destroy(civilian);
                continue;
            }

            netObj.Spawn(true);
            spawnedCivilians++;
        }

        civiliansReady = spawnedCivilians > 0;
        aliveCivilians.Value = CountAliveShelterCivilians();

        if (!civiliansReady)
            Debug.LogError("SurvivalModeController: no se pudo spawnear ningun civil del refugio.");
        else
            Debug.Log($"SurvivalModeController: refugio con {spawnedCivilians} civiles.");
    }

    private static bool TryNavPoint(Vector3 desired, out Vector3 point)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }

        point = desired;
        return false;
    }

    private static int CountAliveShelterCivilians()
    {
        int alive = 0;
        foreach (RescuableNpc npc in RescuableNpc.All)
        {
            if (npc != null && npc.IsShelter && !npc.IsDead)
                alive++;
        }

        return alive;
    }

    private static bool AllPlayersDead()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        int spawned = 0;
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned)
                continue;

            spawned++;
            if (!player.IsDead)
                return false;
        }

        return spawned > 0;
    }

    private void Finish(SurvivalMatchOutcome result, int score)
    {
        if (!IsServer || outcome.Value != SurvivalMatchOutcome.None)
            return;

        endScore.Value = score;
        timeRemaining.Value = Mathf.Max(0f, durationLeft);
        outcome.Value = result;
        Debug.Log($"SurvivalModeController: {result}. Puntos {score}.");
    }

    private void OnOutcomeChanged(SurvivalMatchOutcome previous, SurvivalMatchOutcome current)
    {
        if (current != SurvivalMatchOutcome.None)
            MatchEnded?.Invoke(current);
    }
}
