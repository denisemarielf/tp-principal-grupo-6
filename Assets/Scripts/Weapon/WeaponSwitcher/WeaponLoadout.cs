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

    [Tooltip("Armas iniciales. Los slots 0 y 1 son las teclas 2 y 3. El slot 2 es el melee de la tecla 1.")]
    [SerializeField] private GameObject[] weapons;

    // Tecla 1. Siempre un arma cuerpo a cuerpo, o vacío.
    public const int MeleeSlot = 2;

    // -1 = slot vacío
    private readonly NetworkVariable<int> slot0Type = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> slot1Type = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> slotMeleeType = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>(slotIndex, armaAnterior, armaNueva). Se dispara en todos los clientes.</summary>
    public event Action<int, GameObject, GameObject> SlotChanged;

    public GameObject[] Weapons => weapons;
    public int Count => weapons != null ? weapons.Length : 0;

    private void Awake()
    {
        EnsureSlots();
        MoveMeleeToItsSlot();
    }

    public override void OnNetworkSpawn()
    {
        EnsureSlots();

        if (IsServer)
        {
            slot0Type.Value = TypeAt(0);
            slot1Type.Value = TypeAt(1);
            slotMeleeType.Value = TypeAt(MeleeSlot);
        }

        slot0Type.OnValueChanged += OnSlot0Changed;
        slot1Type.OnValueChanged += OnSlot1Changed;
        slotMeleeType.OnValueChanged += OnSlotMeleeChanged;
    }

    public override void OnNetworkDespawn()
    {
        slot0Type.OnValueChanged -= OnSlot0Changed;
        slot1Type.OnValueChanged -= OnSlot1Changed;
        slotMeleeType.OnValueChanged -= OnSlotMeleeChanged;
    }

    private void OnSlot0Changed(int _, int __) => RebuildSlot(0);
    private void OnSlot1Changed(int _, int __) => RebuildSlot(1);
    private void OnSlotMeleeChanged(int _, int __) => RebuildSlot(MeleeSlot);

    public int GetSlotType(int slot)
    {
        if (slot == 0) return slot0Type.Value;
        if (slot == 1) return slot1Type.Value;
        return slotMeleeType.Value;
    }

    public bool HasWeaponType(WeaponType type)
    {
        int value = (int)type;
        return slot0Type.Value == value || slot1Type.Value == value || slotMeleeType.Value == value;
    }

    public bool IsMeleeType(int typeValue)
    {
        if (typeValue < 0 || catalog == null)
            return false;

        return IsMelee(catalog.GetWeaponObjectByType((WeaponType)typeValue));
    }

    /// <summary>Solo servidor.</summary>
    public void SetSlotType(int slot, int typeValue)
    {
        if (!IsServer) return;

        if (slot == 0) slot0Type.Value = typeValue;
        else if (slot == 1) slot1Type.Value = typeValue;
        else slotMeleeType.Value = typeValue;
    }

    /// <summary>Primer slot de arma de fuego vacío, o -1. El slot melee no entra.</summary>
    public int FindFreeSlot()
    {
        if (slot0Type.Value < 0) return 0;
        if (slot1Type.Value < 0) return 1;
        return -1;
    }

    private void EnsureSlots()
    {
        if (weapons != null && weapons.Length >= 3)
            return;

        GameObject[] next = new GameObject[3];
        if (weapons != null)
        {
            for (int i = 0; i < weapons.Length && i < next.Length; i++)
                next[i] = weapons[i];
        }

        weapons = next;
    }

    // Una sola melee, y siempre en la tecla 1.
    private void MoveMeleeToItsSlot()
    {
        int found = -1;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (!IsMelee(weapons[i]))
                continue;

            if (found < 0)
                found = i;
            else
                weapons[i] = null;
        }

        if (found < 0 || found == MeleeSlot)
            return;

        GameObject displaced = weapons[MeleeSlot];
        weapons[MeleeSlot] = weapons[found];
        weapons[found] = IsMelee(displaced) ? null : displaced;
    }

    private int TypeAt(int slot)
    {
        if (weapons == null || slot < 0 || slot >= weapons.Length || weapons[slot] == null)
            return -1;

        return (int)catalog.GetTypeForWeaponObject(weapons[slot]);
    }

    private static bool IsMelee(GameObject weapon)
    {
        return weapon != null && weapon.GetComponent<MeleeLogic>() != null;
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