using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public static class SavedGameLoader
{
    private const string OwnerStateMessage = "LoadGame.OwnerState";

    private static GameSaveData pending;
    private static bool advancesLevel;

    public static void RegisterHandlers(NetworkManager manager)
    {
        manager.CustomMessagingManager.RegisterNamedMessageHandler(OwnerStateMessage, OnOwnerStateMessage);
    }

    public static bool NamesMatch(string a, string b)
    {
        return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static PlayerSaveData FindSavedPlayer(GameSaveData save, string name)
    {
        if (save == null) return null;

        foreach (PlayerSaveData player in save.players)
            if (NamesMatch(player.playerName, name)) return player;

        return null;
    }

    // Solo el host. Devuelve cuántos jugadores recuperan su estado.
    // advances: se continúa en el nivel siguiente al guardado, igual que al avanzar jugando.
    public static int Stage(GameSaveData save, List<KeyValuePair<ulong, string>> players, bool advances)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (save == null || manager == null || !manager.IsServer) return 0;

        PlayerCarryOver.Clear();
        pending = save;
        advancesLevel = advances;

        List<PlayerSaveData> available = new List<PlayerSaveData>(save.players);
        int restored = 0;

        foreach (KeyValuePair<ulong, string> player in players)
        {
            int index = available.FindIndex(saved => NamesMatch(saved.playerName, player.Value));
            if (index < 0) continue;

            StagePlayer(manager, player.Key, available[index]);
            available.RemoveAt(index);
            restored++;
        }

        return restored;
    }

    public static bool TryGetSafePoint(out Vector3 point)
    {
        point = default;
        if (pending == null || advancesLevel || !pending.hasSafePoint) return false;

        point = pending.safePoint;
        return true;
    }

    public static void Clear()
    {
        pending = null;
        advancesLevel = false;
    }

    private static void StagePlayer(NetworkManager manager, ulong clientId, PlayerSaveData saved)
    {
        WeaponSave weapon0 = FindWeapon(saved, 0);
        WeaponSave weapon1 = FindWeapon(saved, 1);
        int type0 = weapon0 != null ? weapon0.weaponType : -1;
        int type1 = weapon1 != null ? weapon1.weaponType : -1;

        // Al avanzar de nivel, o si estaba muerto al guardar, vuelve con la vida completa.
        float health = advancesLevel || saved.isDead || saved.health <= 0f ? saved.maxHealth : saved.health;
        PlayerCarryOver.StageServer(clientId, ToInventory(saved), type0, type1, health);

        int weaponIndex = type0 >= 0 ? 0 : (type1 >= 0 ? 1 : -1);
        int current0 = weapon0 != null ? weapon0.currentAmmo : -1;
        int reserve0 = weapon0 != null ? weapon0.reserveAmmo : 0;
        int current1 = weapon1 != null ? weapon1.currentAmmo : -1;
        int reserve1 = weapon1 != null ? weapon1.reserveAmmo : 0;

        if (clientId == NetworkManager.ServerClientId)
        {
            PlayerCarryOver.StageLocalOwner(weaponIndex, type0, type1, current0, reserve0, current1, reserve1);
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
        {
            writer.WriteValueSafe(weaponIndex);
            writer.WriteValueSafe(type0);
            writer.WriteValueSafe(type1);
            writer.WriteValueSafe(current0);
            writer.WriteValueSafe(reserve0);
            writer.WriteValueSafe(current1);
            writer.WriteValueSafe(reserve1);

            manager.CustomMessagingManager.SendNamedMessage(OwnerStateMessage, clientId, writer);
        }
    }

    private static void OnOwnerStateMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (senderClientId != NetworkManager.ServerClientId) return;

        reader.ReadValueSafe(out int weaponIndex);
        reader.ReadValueSafe(out int type0);
        reader.ReadValueSafe(out int type1);
        reader.ReadValueSafe(out int current0);
        reader.ReadValueSafe(out int reserve0);
        reader.ReadValueSafe(out int current1);
        reader.ReadValueSafe(out int reserve1);

        PlayerCarryOver.StageLocalOwner(weaponIndex, type0, type1, current0, reserve0, current1, reserve1);
    }

    private static WeaponSave FindWeapon(PlayerSaveData saved, int slot)
    {
        foreach (WeaponSave weapon in saved.weapons)
            if (weapon.slot == slot) return weapon;

        return null;
    }

    private static InventorySlot[] ToInventory(PlayerSaveData saved)
    {
        InventorySlot[] slots = new InventorySlot[saved.inventory.Count];
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlotSave slot = saved.inventory[i];
            slots[i] = slot.itemId < 0 || slot.quantity <= 0
                ? InventorySlot.Empty
                : new InventorySlot { itemId = slot.itemId, quantity = slot.quantity };
        }

        return slots;
    }
}
