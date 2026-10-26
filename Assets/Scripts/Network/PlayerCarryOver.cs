using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Persistencia en memoria entre Suburbio y Oficinas (US 4.10).
// - El server guarda salud, inventario y tipos de arma (NetworkVariables).
// - Cada owner guarda munición e índice de arma localmente (AmmoManager no es de red).
// Los estáticos sobreviven al LoadScene Single porque no viven en la escena.
public static class PlayerCarryOver
{
    public const string SuburbSceneName = "SampleScene";
    public const string OfficesSceneName = "MapaOficina";

    private struct AmmoSlot
    {
        public int current;
        public int reserve;
        public bool hasValue;
    }

    private struct ServerSnapshot
    {
        public float health;
        public InventorySlot[] inventory;
        public int weaponSlot0Type;
        public int weaponSlot1Type;
    }

    private struct OwnerSnapshot
    {
        public int weaponIndex;
        public int weaponSlot0Type;
        public int weaponSlot1Type;
        public AmmoSlot ammo0;
        public AmmoSlot ammo1;
    }

    private static readonly Dictionary<ulong, ServerSnapshot> serverByClientId = new Dictionary<ulong, ServerSnapshot>();
    private static OwnerSnapshot? localOwnerSnapshot;
    private static bool serverPending;

    public static bool HasServerPending => serverPending;

    // Cada owner: munición + arma equipada. Llamar al completar el Suburbio.
    public static void CaptureLocalOwner(GameObject player)
    {
        if (player == null) return;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwner) return;

        WeaponSwitcher switcher = player.GetComponentInChildren<WeaponSwitcher>();
        WeaponLoadout loadout = player.GetComponentInChildren<WeaponLoadout>();

        OwnerSnapshot snap = new OwnerSnapshot
        {
            weaponIndex = switcher != null ? switcher.CurrentWeaponIndex : 0,
            weaponSlot0Type = loadout != null ? loadout.GetSlotType(0) : -1,
            weaponSlot1Type = loadout != null ? loadout.GetSlotType(1) : -1,
            ammo0 = ReadAmmo(loadout, 0),
            ammo1 = ReadAmmo(loadout, 1)
        };

        localOwnerSnapshot = snap;
    }

    public static bool HasLocalOwnerPending => localOwnerSnapshot.HasValue;

    // True cuando el loadout sincronizado ya coincide con lo capturado (o no habia loadout).
    public static bool IsLocalLoadoutReady(GameObject player)
    {
        if (!localOwnerSnapshot.HasValue || player == null) return true;

        WeaponLoadout loadout = player.GetComponentInChildren<WeaponLoadout>();
        if (loadout == null) return true;

        OwnerSnapshot snap = localOwnerSnapshot.Value;
        return loadout.GetSlotType(0) == snap.weaponSlot0Type
            && loadout.GetSlotType(1) == snap.weaponSlot1Type;
    }

    // Solo server: salud, inventario y loadout de todos los jugadores conectados.
    public static void CaptureServerState()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return;

        serverByClientId.Clear();

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            NetworkObject playerObj = networkManager.ConnectedClients[clientId].PlayerObject;
            if (playerObj == null) continue;

            GameObject player = playerObj.gameObject;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            WeaponLoadout loadout = player.GetComponentInChildren<WeaponLoadout>();

            InventorySlot[] slots = null;
            if (inventory != null && inventory.LoadedSlotCount > 0)
            {
                slots = new InventorySlot[inventory.LoadedSlotCount];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = inventory.GetSlot(i);
            }

            serverByClientId[clientId] = new ServerSnapshot
            {
                health = health != null ? health.CurrentHealth : 100f,
                inventory = slots,
                weaponSlot0Type = loadout != null ? loadout.GetSlotType(0) : -1,
                weaponSlot1Type = loadout != null ? loadout.GetSlotType(1) : -1
            };
        }

        serverPending = serverByClientId.Count > 0;
    }

    // Solo server, tras SpawnAsPlayerObject.
    public static void ApplyServerState(GameObject player, ulong clientId)
    {
        if (!serverPending || player == null) return;
        if (!serverByClientId.TryGetValue(clientId, out ServerSnapshot snap)) return;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
            health.ApplyCarryOver(snap.health);

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && snap.inventory != null)
            inventory.ApplyCarryOverSlots(snap.inventory);

        WeaponLoadout loadout = player.GetComponentInChildren<WeaponLoadout>();
        if (loadout != null)
        {
            loadout.SetSlotType(0, snap.weaponSlot0Type);
            loadout.SetSlotType(1, snap.weaponSlot1Type);
        }
    }

    // Owner: munición e índice de arma tras el spawn (esperar 1-2 frames a que el loadout se reconstruya).
    public static bool TryApplyLocalOwner(GameObject player)
    {
        if (player == null || !localOwnerSnapshot.HasValue) return false;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwner) return false;

        OwnerSnapshot snap = localOwnerSnapshot.Value;
        WeaponLoadout loadout = player.GetComponentInChildren<WeaponLoadout>();
        WeaponSwitcher switcher = player.GetComponentInChildren<WeaponSwitcher>();

        ApplyAmmo(loadout, 0, snap.ammo0);
        ApplyAmmo(loadout, 1, snap.ammo1);

        if (switcher != null)
            switcher.SelectWeapon(snap.weaponIndex);

        localOwnerSnapshot = null;
        return true;
    }

    public static void MarkServerApplied()
    {
        // Se limpia el pending de server cuando ya spawnearon todos (o al Clear).
        serverByClientId.Clear();
        serverPending = false;
    }

    public static void Clear()
    {
        serverByClientId.Clear();
        localOwnerSnapshot = null;
        serverPending = false;
    }

    private static AmmoSlot ReadAmmo(WeaponLoadout loadout, int slot)
    {
        AmmoSlot empty = new AmmoSlot { hasValue = false };
        if (loadout == null || loadout.Weapons == null || slot < 0 || slot >= loadout.Weapons.Length)
            return empty;

        GameObject weapon = loadout.Weapons[slot];
        if (weapon == null) return empty;

        AmmoManager ammo = weapon.GetComponent<AmmoManager>();
        if (ammo == null) return empty;

        return new AmmoSlot
        {
            current = ammo.CurrentAmmo,
            reserve = ammo.ReserveAmmo,
            hasValue = true
        };
    }

    private static void ApplyAmmo(WeaponLoadout loadout, int slot, AmmoSlot data)
    {
        if (!data.hasValue || loadout == null || loadout.Weapons == null) return;
        if (slot < 0 || slot >= loadout.Weapons.Length) return;

        GameObject weapon = loadout.Weapons[slot];
        if (weapon == null) return;

        AmmoManager ammo = weapon.GetComponent<AmmoManager>();
        if (ammo != null)
            ammo.SetAmmoState(data.current, data.reserve);
    }
}
