using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AchievementService : MonoBehaviour
{
    private const string UnlockMessage = "Achievement.Unlock";

    private static AchievementService instance;

    private NetworkManager handlersOn;

    private readonly Dictionary<ulong, int> kills = new Dictionary<ulong, int>();
    private readonly HashSet<string> awarded = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("AchievementService");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AchievementService>();
    }

    // ---------- API para el resto del juego (solo tiene efecto en el host) ----------

    public static void NotifyEnemyKilled(Transform attacker, EnemyVariant variant)
    {
        if (instance == null || !IsServer || attacker == null) return;

        NetworkObject owner = attacker.GetComponentInParent<NetworkObject>();
        if (owner == null || !owner.IsPlayerObject) return;

        instance.HandleKill(owner.OwnerClientId, variant);
    }

    public static void NotifyCivilianRescued(ulong leaderClientId)
    {
        if (instance == null || !IsServer) return;

        instance.Award(leaderClientId, AchievementIds.FirstRescue);
    }

    private static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    // ---------- Ciclo de vida ----------

    private void OnEnable()
    {
        SafeZone.LevelCompleted += OnLevelCompleted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SafeZone.LevelCompleted -= OnLevelCompleted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetMatch();
    }

    private void ResetMatch()
    {
        kills.Clear();
        awarded.Clear();
    }

    // Registra el handler cuando el NetworkManager actual empieza a escuchar.
    private void Update()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || manager == handlersOn) return;

        manager.CustomMessagingManager.RegisterNamedMessageHandler(UnlockMessage, OnUnlockMessage);
        handlersOn = manager;
        ResetMatch();
    }

    // ---------- Reglas (host) ----------

    private void HandleKill(ulong clientId, EnemyVariant variant)
    {
        kills.TryGetValue(clientId, out int count);
        count++;
        kills[clientId] = count;

        List<string> unlocks = new List<string>();
        AchievementRules.CollectKillUnlocks(count, variant == EnemyVariant.Resistente, unlocks);

        foreach (string id in unlocks)
            Award(clientId, id);
    }

    private void OnLevelCompleted()
    {
        if (!IsServer) return;

        SafeZone zone = SafeZone.Current;
        bool allCiviliansSafe = zone != null && AchievementRules.AllCiviliansSafe(zone.TotalNpcs, zone.RescuedNpcs);

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            Award(client.ClientId, AchievementIds.LevelComplete);
            if (allCiviliansSafe)
                Award(client.ClientId, AchievementIds.NoOneLeftBehind);
        }
    }

    // ---------- Entrega ----------

    private void Award(ulong clientId, string achievementId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) return;
        if (!awarded.Add(clientId + ":" + achievementId)) return;

        if (clientId == NetworkManager.ServerClientId)
        {
            Deliver(achievementId);
            return;
        }

        if (!manager.ConnectedClients.ContainsKey(clientId)) return;

        using (FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteValueSafe(achievementId);
            manager.CustomMessagingManager.SendNamedMessage(UnlockMessage, clientId, writer);
        }
    }

    private void OnUnlockMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (senderClientId != NetworkManager.ServerClientId) return;

        reader.ReadValueSafe(out string achievementId);
        Deliver(achievementId);
    }

    private static void Deliver(string achievementId)
    {
        AchievementDef def = AchievementCatalog.Find(achievementId);
        if (def == null) return;

        if (AchievementStore.TryUnlock(achievementId))
            AchievementToastUI.Show(def);
    }
}
