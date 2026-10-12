using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Coordinador: mantiene el índice de arma equipada (red), recibe el input
/// y delega en Loadout / Visuals / Presentation / PickupHandler.
/// Conserva la API pública que usaban otros scripts.
/// </summary>
public class WeaponSwitcher : NetworkBehaviour
{
    [Header("Componentes")]
    [SerializeField] private WeaponLoadout loadout;
    [SerializeField] private WeaponVisuals visuals;
    [SerializeField] private WeaponPresentation presentation;
    [SerializeField] private WeaponPickupHandler pickupHandler;

    private ShootLogic currentWeaponShoot;
    public ShootLogic CurrentShoot => currentWeaponShoot;

    private readonly NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // ---------- API pública (compatibilidad) ----------

    public int CurrentWeaponIndex => networkWeaponIndex.Value;
    public int WeaponCount => loadout.Count;

    public WeaponLogic CurrentWeaponLogic =>
        currentWeaponShoot != null ? currentWeaponShoot.GetComponent<WeaponLogic>() : null;

    public AmmoManager CurrentAmmoManager =>
        currentWeaponShoot != null ? currentWeaponShoot.GetComponent<AmmoManager>() : null;

    public void GrantAmmoToCurrentWeapon(int amount)
    {
        if (!IsServer) return;
        GrantAmmoToCurrentWeaponClientRpc(amount);
    }

    [ClientRpc]
    private void GrantAmmoToCurrentWeaponClientRpc(int amount)
    {
        if (!IsOwner) return;

        AmmoManager ammo = CurrentAmmoManager;
        if (ammo != null)
            ammo.AddReserveAmmo(amount);
    }

    // ---------- Network spawn ----------

    public override void OnNetworkSpawn()
    {
        visuals.SetupCatalog(IsOwner);

        if (!IsOwner)
            presentation.HideArmsForRemotePlayer();

        loadout.SlotChanged += OnSlotChanged;
        networkWeaponIndex.OnValueChanged += OnWeaponIndexChanged;

        if (IsOwner)
        {
            presentation.StartOwnerUI();
            SelectWeapon(0);
        }
        else
        {
            visuals.UpdateWeaponVisuals(loadout.Weapons, networkWeaponIndex.Value);
            StartCoroutine(ReforzarVisualTrasSpawn());
        }

        StartCoroutine(DeferredCatalogWeaponsActivity());
    }

    public override void OnNetworkDespawn()
    {
        loadout.SlotChanged -= OnSlotChanged;
        networkWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;

        if (IsOwner)
            presentation.StopOwnerUI();
    }

    private IEnumerator ReforzarVisualTrasSpawn()
    {
        yield return null;
        yield return null;

        int idx = networkWeaponIndex.Value;
        visuals.UpdateWeaponVisuals(loadout.Weapons, idx);

        if (idx >= 0 && idx < loadout.Count && loadout.Weapons[idx] != null)
            presentation.ApplyGripsForWeapon(loadout.Weapons[idx]);
    }

    private IEnumerator DeferredCatalogWeaponsActivity()
    {
        // Esperamos un frame para que Netcode termine el OnNetworkSpawn de
        // todos los NetworkBehaviour del jugador antes de desactivar nada.
        yield return null;
        visuals.UpdateCatalogWeaponsActivity(loadout.Weapons);
    }

    // ---------- Eventos ----------

    private void OnWeaponIndexChanged(int oldIndex, int newIndex)
    {
        visuals.UpdateWeaponVisuals(loadout.Weapons, newIndex);

        if (newIndex >= 0 && newIndex < loadout.Count && loadout.Weapons[newIndex] != null)
            presentation.ApplyGripsForWeapon(loadout.Weapons[newIndex]);
    }

    private void OnSlotChanged(int slotIndex, GameObject oldWeapon, GameObject newWeapon)
    {
        if (oldWeapon != null && oldWeapon != newWeapon)
            visuals.HideAndUnequip(oldWeapon);

        visuals.PrepareWeapon(newWeapon, IsOwner);
        visuals.UpdateCatalogWeaponsActivity(loadout.Weapons);

        if (slotIndex == networkWeaponIndex.Value)
            SelectWeapon(slotIndex);
        else
            visuals.UpdateWeaponVisuals(loadout.Weapons, networkWeaponIndex.Value);
    }

    // ---------- Cambio de arma ----------

    public void SelectWeapon(int index)
    {
        GameObject[] weapons = loadout.Weapons;

        // -1 = "sin arma" (enfundar): índice centinela válido.
        if (index != -1 && (index < 0 || index >= weapons.Length))
            return;

        if (index >= 0 && weapons[index] == null)
            return;

        visuals.UpdateWeaponVisuals(weapons, index);

        currentWeaponShoot = GetShootAt(index);

        presentation.ShowWeapon(currentWeaponShoot, index >= 0 ? weapons[index] : null);
        presentation.PlaySwitchSound();

        if (IsOwner)
            networkWeaponIndex.Value = index;
    }

    public ShootLogic GetShootAt(int index)
    {
        GameObject[] weapons = loadout.Weapons;

        if (index >= 0 && index < weapons.Length && weapons[index] != null)
            return weapons[index].GetComponent<ShootLogic>();

        return null;
    }

    // ---------- Input ----------

 
    // ---------- Pickup (entrada de red) ----------

    /// <summary>Llamado por Character.OnInteraction() al presionar E cerca de un WeaponPickup.</summary>
    [ServerRpc]
    public void RequestPickupWeaponServerRpc(int weaponTypeIndex, ulong pickupNetworkObjectId)
    {
        pickupHandler.HandlePickup(weaponTypeIndex, pickupNetworkObjectId, networkWeaponIndex.Value);
    }
}