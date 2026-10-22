using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Guardado de partida (US 3.6). Solo el host guarda (CA3), solo dentro de la zona segura de una escena de
// Extraccion (CA2, CA5), y se guarda el estado de todos los jugadores (CA1, CA4).
//
// Salud e inventario los conoce el host (NetworkVariables). La municion vive solo en el cliente dueño
// (AmmoManager no es de red), asi que el host se la pide a cada cliente con mensajes con nombre y espera
// las respuestas antes de escribir el archivo. Un cliente nunca puede disparar un guardado.
public class SaveGameService : MonoBehaviour
{
    private const string AmmoRequestMessage = "SaveGame.AmmoRequest";
    private const string AmmoReportMessage = "SaveGame.AmmoReport";
    private const float AmmoTimeoutSeconds = 2f;

    private struct AmmoPair
    {
        public int current;   // -1 = sin arma en ese slot
        public int reserve;
    }

    private static SaveGameService instance;

    private NetworkManager handlersOn;
    private bool saving;
    private int requestId;
    private readonly Dictionary<ulong, AmmoPair[]> reports = new Dictionary<ulong, AmmoPair[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("SaveGameService");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SaveGameService>();
    }

    // ---------- API ----------

    // Por que no se puede guardar ahora mismo (None = se puede). Lo usa la UI para habilitar el boton.
    // automatic: el autoguardado de fin de nivel no exige estar dentro de la zona (completar el nivel ya lo garantiza).
    public static SaveBlockReason GetBlockReason(bool automatic = false)
    {
        NetworkManager manager = NetworkManager.Singleton;
        bool hasSession = manager != null && manager.IsListening;
        bool isHost = hasSession && manager.IsServer;

        SafeZone zone = SafeZone.Current;
        bool hostInside = false;
        if (isHost && zone != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            hostInside = zone.IsPlayerInside(manager.LocalClient.PlayerObject.GetComponent<PlayerHealth>());

        if (automatic)
            return SaveSystem.Evaluate(hasSession, isHost, true, true);

        return SaveSystem.Evaluate(hasSession, isHost, zone != null, hostInside);
    }

    // Guarda la partida. onDone(exito, mensaje) se llama al terminar.
    public static void RequestSave(Action<bool, string> onDone)
    {
        if (instance == null)
        {
            onDone?.Invoke(false, "El servicio de guardado no esta disponible.");
            return;
        }

        if (instance.saving)
        {
            onDone?.Invoke(false, "Ya se esta guardando.");
            return;
        }

        instance.StartCoroutine(instance.SaveRoutine(onDone, false));
    }

    // ---------- Autoguardado ----------

    private void OnEnable()
    {
        SafeZone.LevelCompleted += OnLevelCompleted;
    }

    private void OnDisable()
    {
        SafeZone.LevelCompleted -= OnLevelCompleted;
    }

    // Al completar el nivel el host guarda solo; los clientes reciben el evento pero no guardan (CA3).
    private void OnLevelCompleted()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || !manager.IsServer) return;

        if (saving)
        {
            Debug.LogWarning("SaveGameService: autoguardado omitido, ya habia un guardado en curso.");
            return;
        }

        StartCoroutine(SaveRoutine((success, message) =>
        {
            if (success)
                Debug.Log("SaveGameService: autoguardado al completar el nivel.");
            else
                Debug.LogWarning("SaveGameService: fallo el autoguardado. " + message);
        }, true));
    }

    // ---------- Mensajes ----------

    private void Update()
    {
        // Registra los handlers apenas el NetworkManager actual empieza a escuchar (el de MainMenu se destruye al salir).
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || manager == handlersOn) return;

        manager.CustomMessagingManager.RegisterNamedMessageHandler(AmmoRequestMessage, OnAmmoRequest);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(AmmoReportMessage, OnAmmoReport);
        handlersOn = manager;
    }

    // Cliente: el host pide la municion del jugador local.
    private void OnAmmoRequest(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);

        NetworkManager manager = NetworkManager.Singleton;
        AmmoPair[] ammo = ReadLocalAmmo(manager.LocalClient != null ? manager.LocalClient.PlayerObject : null);

        using (FastBufferWriter writer = new FastBufferWriter(8 + ammo.Length * 8, Allocator.Temp))
        {
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(ammo.Length);
            foreach (AmmoPair pair in ammo)
            {
                writer.WriteValueSafe(pair.current);
                writer.WriteValueSafe(pair.reserve);
            }

            manager.CustomMessagingManager.SendNamedMessage(AmmoReportMessage, NetworkManager.ServerClientId, writer);
        }
    }

    // Host: llega la municion de un cliente.
    private void OnAmmoReport(ulong senderClientId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        reader.ReadValueSafe(out int id);
        reader.ReadValueSafe(out int count);
        if (id != requestId || count < 0 || count > 8) return;

        AmmoPair[] ammo = new AmmoPair[count];
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ammo[i].current);
            reader.ReadValueSafe(out ammo[i].reserve);
        }

        reports[senderClientId] = ammo;
    }

    // Municion por slot de arma del jugador indicado (solo tiene sentido en el equipo que lo controla).
    private static AmmoPair[] ReadLocalAmmo(NetworkObject player)
    {
        WeaponSwitcher switcher = player != null ? player.GetComponentInChildren<WeaponSwitcher>() : null;
        if (switcher == null) return new AmmoPair[0];

        AmmoPair[] ammo = new AmmoPair[switcher.WeaponCount];
        for (int i = 0; i < ammo.Length; i++)
        {
            ShootLogic shoot = switcher.GetShootAt(i);
            AmmoManager manager = shoot != null ? shoot.GetComponent<AmmoManager>() : null;
            ammo[i] = manager != null
                ? new AmmoPair { current = manager.CurrentAmmo, reserve = manager.ReserveAmmo }
                : new AmmoPair { current = -1, reserve = 0 };
        }
        return ammo;
    }

    // ---------- Guardado ----------

    private IEnumerator SaveRoutine(Action<bool, string> onDone, bool automatic)
    {
        SaveBlockReason reason = GetBlockReason(automatic);
        if (reason != SaveBlockReason.None)
        {
            onDone?.Invoke(false, SaveSystem.Describe(reason));
            yield break;
        }

        saving = true;
        NetworkManager manager = NetworkManager.Singleton;

        // Pide la municion a los clientes y espera sus respuestas.
        requestId++;
        reports.Clear();
        List<ulong> remoteIds = new List<ulong>();
        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.ServerClientId || client.PlayerObject == null) continue;
            remoteIds.Add(client.ClientId);

            using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
            {
                writer.WriteValueSafe(requestId);
                manager.CustomMessagingManager.SendNamedMessage(AmmoRequestMessage, client.ClientId, writer);
            }
        }

        float deadline = Time.realtimeSinceStartup + AmmoTimeoutSeconds;
        while (reports.Count < remoteIds.Count && Time.realtimeSinceStartup < deadline)
            yield return null;

        // Pudo cambiar el estado mientras esperabamos (el host salio de la zona, se cayo la sesion...).
        reason = GetBlockReason(automatic);
        if (reason != SaveBlockReason.None)
        {
            saving = false;
            onDone?.Invoke(false, SaveSystem.Describe(reason));
            yield break;
        }

        GameSaveData data = BuildSaveData(manager, out string missing);
        saving = false;

        if (data == null)
        {
            onDone?.Invoke(false, "No se pudo guardar: sin respuesta de " + missing + ".");
            yield break;
        }

        if (!SaveSystem.TryWrite(data, out string error))
        {
            onDone?.Invoke(false, "No se pudo escribir el archivo: " + error);
            yield break;
        }

        Debug.Log($"SaveGameService: partida guardada ({data.players.Count} jugadores) en {SaveSystem.DefaultPath}");
        onDone?.Invoke(true, "Partida guardada.");
    }

    // Arma el guardado con el estado actual de todos los jugadores. Devuelve null si falta la municion de alguno.
    private GameSaveData BuildSaveData(NetworkManager manager, out string missingPlayer)
    {
        missingPlayer = null;

        GameSaveData data = new GameSaveData
        {
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };

        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            bool isHost = client.ClientId == NetworkManager.ServerClientId;
            Character character = playerObject.GetComponent<Character>();
            string playerName = character != null ? character.PlayerName : "Jugador " + (client.ClientId + 1);

            AmmoPair[] ammo;
            if (isHost)
                ammo = ReadLocalAmmo(playerObject);
            else if (!reports.TryGetValue(client.ClientId, out ammo))
            {
                missingPlayer = playerName;
                return null;
            }

            data.players.Add(BuildPlayer(client.ClientId, isHost, playerName, playerObject, ammo));
        }

        return data;
    }

    private static PlayerSaveData BuildPlayer(ulong clientId, bool isHost, string playerName, NetworkObject playerObject, AmmoPair[] ammo)
    {
        PlayerSaveData player = new PlayerSaveData
        {
            playerName = playerName,
            clientId = clientId,
            isHost = isHost
        };

        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
        if (health != null)
        {
            player.health = health.CurrentHealth;
            player.maxHealth = health.MaxHealth;
            player.isDead = health.IsDead;
        }

        PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            for (int i = 0; i < inventory.LoadedSlotCount; i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                player.inventory.Add(new InventorySlotSave
                {
                    itemId = slot.IsEmpty ? -1 : slot.itemId,
                    quantity = slot.IsEmpty ? 0 : slot.quantity
                });
            }
        }

        WeaponLoadout loadout = playerObject.GetComponentInChildren<WeaponLoadout>();
        if (loadout != null)
        {
            for (int slot = 0; slot < loadout.Count; slot++)
            {
                AmmoPair pair = slot < ammo.Length ? ammo[slot] : new AmmoPair { current = -1 };
                player.weapons.Add(new WeaponSave
                {
                    slot = slot,
                    weaponType = loadout.GetSlotType(slot),
                    currentAmmo = Mathf.Max(0, pair.current),
                    reserveAmmo = pair.reserve
                });
            }
        }

        return player;
    }
}
