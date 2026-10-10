using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Qué arma ocupa cada slot (sincronizado por red). Solo gestiona datos:
/// no toca visuales, UI ni input. Avisa de los cambios mediante SlotChanged.
/// </summary>
public class WeaponLoadout : NetworkBehaviour
{
    [SerializeField] private WeaponCatalog catalog;

    [Tooltip("Armas iniciales del loadout (slot 0 y slot 1).")]
    [SerializeField] private GameObject[] weapons;

    // -1 = slot vacío
    private readonly NetworkVariable<int> slot0Type = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> slot1Type = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>(slotIndex, armaAnterior, armaNueva). Se dispara en todos los clientes.</summary>
    public event Action<int, GameObject, GameObject> SlotChanged;

    public GameObject[] Weapons => weapons;
    public int Count => weapons.Length;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            slot0Type.Value = weapons.Length > 0 ? (int)catalog.GetTypeForWeaponObject(weapons[0]) : -1;
            slot1Type.Value = weapons.Length > 1 ? (int)catalog.GetTypeForWeaponObject(weapons[1]) : -1;
        }

        slot0Type.OnValueChanged += OnSlot0Changed;
        slot1Type.OnValueChanged += OnSlot1Changed;
    }

    public override void OnNetworkDespawn()
    {
        slot0Type.OnValueChanged -= OnSlot0Changed;
        slot1Type.OnValueChanged -= OnSlot1Changed;
    }

    private void OnSlot0Changed(int _, int __) => RebuildSlot(0);
    private void OnSlot1Changed(int _, int __) => RebuildSlot(1);

    public int GetSlotType(int slot) => slot == 0 ? slot0Type.Value : slot1Type.Value;

    public bool HasWeaponType(WeaponType type) =>
        slot0Type.Value == (int)type || slot1Type.Value == (int)type;

    /// <summary>Solo servidor.</summary>
    public void SetSlotType(int slot, int typeValue)
    {
        if (!IsServer) return;

        if (slot == 0) slot0Type.Value = typeValue;
        else slot1Type.Value = typeValue;
    }

    /// <summary>Primer slot vacío, o -1 si están todos ocupados.</summary>
    public int FindFreeSlot()
    {
        if (slot0Type.Value < 0) return 0;
        if (slot1Type.Value < 0) return 1;
        return -1;
    }

    private void RebuildSlot(int slotIndex)
    {
        int typeValue = GetSlotType(slotIndex);

        GameObject newWeapon = typeValue >= 0
            ? catalog.GetWeaponObjectByType((WeaponType)typeValue)
            : null;

        GameObject oldWeapon = weapons[slotIndex];
        weapons[slotIndex] = newWeapon;

        SlotChanged?.Invoke(slotIndex, oldWeapon, newWeapon);
    }
}