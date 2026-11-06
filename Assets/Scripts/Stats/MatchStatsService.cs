using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Cuenta bajas, muertes, daño y rescates en el host y, al cerrar la partida,
// manda el tablero a todos. Los clientes lo leen desde Current.
public class MatchStatsService : MonoBehaviour
{
    private const string StatsMessage = "MatchStats.Board";
    private const float WipeCheckInterval = 0.25f;

    public static MatchBoard Current { get; private set; }
    public static event Action BoardReady;

    private static MatchStatsService instance;

    private readonly MatchStatsLedger ledger = new MatchStatsLedger();

    private NetworkManager handlersOn;
    private bool published;
    private float wipeTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("MatchStatsService");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MatchStatsService>();
    }

    public static void NotifyDamage(Transform attacker, float amount)
    {
        if (amount <= 0f || !TryPlayer(attacker, out ulong clientId))
            return;

        instance.ledger.AddDamage(clientId, Mathf.CeilToInt(amount));
    }

    public static void NotifyKill(Transform attacker)
    {
        if (!TryPlayer(attacker, out ulong clientId))
            return;

        instance.ledger.AddKill(clientId);
    }

    public static void NotifyDeath(ulong clientId)
    {
        if (instance == null || !IsServer)
            return;

        instance.ledger.AddDeath(clientId);
    }

    public static void NotifyRescue(ulong clientId)
    {
        if (instance == null || !IsServer)
            return;

        instance.ledger.AddRescue(clientId);
    }

    private static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private static bool TryPlayer(Transform attacker, out ulong clientId)
    {
        clientId = 0;
        if (instance == null || !IsServer || attacker == null)
            return false;

        NetworkObject owner = attacker.GetComponentInParent<NetworkObject>();
        if (owner == null || !owner.IsPlayerObject)
            return false;

        clientId = owner.OwnerClientId;
        return true;
    }

    private void OnEnable()
    {
        SurvivalModeController.MatchEnded += OnSurvivalEnded;
        SafeZone.LevelCompleted += OnLevelCompleted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SurvivalModeController.MatchEnded -= OnSurvivalEnded;
        SafeZone.LevelCompleted -= OnLevelCompleted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetMatch();
    }

    private void ResetMatch()
    {
        ledger.Clear();
        published = false;
        Current = null;
        wipeTimer = 0f;
    }

    private void Update()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && manager != handlersOn)
        {
            manager.CustomMessagingManager.RegisterNamedMessageHandler(StatsMessage, OnStatsMessage);
            handlersOn = manager;
            ResetMatch();
        }

        wipeTimer -= Time.unscaledDeltaTime;
        if (wipeTimer > 0f)
            return;

        wipeTimer = WipeCheckInterval;
        TryPublishWipe();
    }

    private void OnSurvivalEnded(SurvivalMatchOutcome outcome)
    {
        if (!IsServer || published)
            return;

        SurvivalModeController mode = SurvivalModeController.Current;
        if (mode == null)
            return;

        int total = Mathf.CeilToInt(Mathf.Max(0f, mode.TimeRemaining));
        string extra = $"Tiempo restante {total / 60:00}:{total % 60:00}";
        SurvivalWaveDirector waves = mode.GetComponent<SurvivalWaveDirector>();
        if (waves != null)
            extra += $"    Ronda {waves.WaveNumber}";

        Publish(new MatchTeamSummary
        {
            Result = outcome == SurvivalMatchOutcome.Victory ? "Victoria" : "Derrota",
            Score = mode.EndScore,
            Civilians = "Civiles vivos: " + mode.AliveCivilians,
            Extra = extra
        });
    }

    private void OnLevelCompleted()
    {
        if (!IsServer || published || SurvivalModeController.Current != null)
            return;

        SafeZone zone = SafeZone.Current;
        Publish(new MatchTeamSummary
        {
            Result = "Nivel completado",
            Score = zone != null ? zone.TeamScore : 0,
            Civilians = zone != null && zone.TotalNpcs > 0
                ? $"Civiles: {zone.RescuedNpcs}/{zone.TotalNpcs}"
                : "",
            Extra = ""
        });
    }

    // Derrota de campaña: murieron todos y el nivel no se completó.
    // Supervivencia cierra por su propio resultado, así que acá no se publica.
    private void TryPublishWipe()
    {
        if (!IsServer || published || SurvivalModeController.Current != null)
            return;

        if (SceneManager.GetActiveScene().name == GameSessionManager.MainMenuSceneName)
            return;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || manager.ShutdownInProgress)
            return;

        if (SafeZone.Current != null && SafeZone.Current.IsCompleted)
            return;

        if (!AllPlayersDead())
            return;

        SafeZone zone = SafeZone.Current;
        Publish(new MatchTeamSummary
        {
            Result = "Derrota",
            Score = zone != null ? zone.TeamScore : 0,
            Civilians = zone != null && zone.TotalNpcs > 0
                ? $"Civiles: {zone.RescuedNpcs}/{zone.TotalNpcs}"
                : "",
            Extra = ""
        });
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

    private void Publish(MatchTeamSummary team)
    {
        if (!IsServer || published)
            return;

        published = true;
        StampNames();
        ApplyLocal(MatchBoard.From(team, ledger.BuildRows()));
        Send(Current);
    }

    private void StampNames()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
            return;

        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            string name = MatchStatsLedger.FallbackName(client.ClientId);
            if (client.PlayerObject != null)
            {
                Character character = client.PlayerObject.GetComponent<Character>();
                if (character != null && !string.IsNullOrWhiteSpace(character.PlayerName))
                    name = character.PlayerName;
            }

            ledger.SetName(client.ClientId, name);
        }
    }

    private static void ApplyLocal(MatchBoard board)
    {
        Current = board;
        BoardReady?.Invoke();
    }

    private static void Send(MatchBoard board)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || board == null)
            return;

        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client.ClientId == manager.LocalClientId)
                continue;

            var writer = new FastBufferWriter(4096, Allocator.Temp);
            try
            {
                board.Write(ref writer);
                manager.CustomMessagingManager.SendNamedMessage(StatsMessage, client.ClientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }
    }

    private void OnStatsMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton == null || senderClientId != NetworkManager.ServerClientId)
            return;

        ApplyLocal(MatchBoardIO.Read(ref reader));
    }
}

public static class MatchBoardIO
{
    public static void Write(this MatchBoard board, ref FastBufferWriter writer)
    {
        writer.WriteValueSafe(board.Result ?? "");
        writer.WriteValueSafe(board.Score);
        writer.WriteValueSafe(board.Civilians ?? "");
        writer.WriteValueSafe(board.Extra ?? "");

        List<MatchPlayerRow> players = board.Players ?? new List<MatchPlayerRow>();
        writer.WriteValueSafe(players.Count);
        foreach (MatchPlayerRow row in players)
        {
            writer.WriteValueSafe(row.ClientId);
            writer.WriteValueSafe(row.Name ?? "");
            writer.WriteValueSafe(row.Kills);
            writer.WriteValueSafe(row.Deaths);
            writer.WriteValueSafe(row.Damage);
            writer.WriteValueSafe(row.Rescues);
        }
    }

    public static MatchBoard Read(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out string result);
        reader.ReadValueSafe(out int score);
        reader.ReadValueSafe(out string civilians);
        reader.ReadValueSafe(out string extra);
        reader.ReadValueSafe(out int count);
        if (count < 0 || count > 16)
            count = 0;

        var players = new List<MatchPlayerRow>(count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out string name);
            reader.ReadValueSafe(out int kills);
            reader.ReadValueSafe(out int deaths);
            reader.ReadValueSafe(out int damage);
            reader.ReadValueSafe(out int rescues);
            players.Add(new MatchPlayerRow
            {
                ClientId = clientId,
                Name = name,
                Kills = kills,
                Deaths = deaths,
                Damage = damage,
                Rescues = rescues
            });
        }

        return new MatchBoard
        {
            Result = result,
            Score = score,
            Civilians = civilians,
            Extra = extra,
            Players = players
        };
    }
}
