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
    

    private readonly NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // ---------- API pública (compatibilidad) ----------
    private EquippableWeapon currentWeapon;
    public EquippableWeapon CurrentWeapon => currentWeapon;
    // Compatibilidad: devuelve null si el arma equipada es melee
    public ShootLogic CurrentShoot => currentWeapon as ShootLogic;
    public WeaponLogic CurrentWeaponLogic =>
    currentWeapon != null ? currentWeapon.GetComponent<WeaponLogic>() : null;

    public AmmoManager CurrentAmmoManager =>
        currentWeapon != null ? currentWeapon.GetComponent<AmmoManager>() : null;

    public int CurrentWeaponIndex => networkWeaponIndex.Value;

    // El dueño publica si la reserva del arma actual está llena, para que el servidor
    // pueda decidir sin tener el AmmoManager real del cliente.
    private readonly NetworkVariable<bool> currentReserveFull = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public bool CurrentWeaponUsesAmmo => GetShootAt(networkWeaponIndex.Value) != null;
    public bool CanReceiveAmmo => CurrentWeaponUsesAmmo && !currentReserveFull.Value;

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        AmmoManager ammo = CurrentAmmoManager;
        bool full = ammo != null && ammo.IsReserveFull;
        if (currentReserveFull.Value != full)
            currentReserveFull.Value = full;
    }

    // El dueño ve el arma del cuerpo cuando está en tercera persona. El modelo nuevo vuelve a pedir los visuals.
    public void SetThirdPersonView(bool thirdPerson)
    {
        if (visuals == null) return;
        visuals.SetOwnerThirdPerson(thirdPerson);
        if (loadout != null && loadout.Weapons != null)
            visuals.UpdateWeaponVisuals(loadout.Weapons, networkWeaponIndex.Value);
    }
    public int WeaponCount => loadout.Count;

  

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

        currentWeapon = GetWeaponAt(index);

        presentation.ShowWeapon(currentWeapon, index >= 0 ? weapons[index] : null);
        presentation.PlaySwitchSound();

        if (IsOwner)
            networkWeaponIndex.Value = index;
    }
    public EquippableWeapon GetWeaponAt(int index)
    {
        GameObject[] weapons = loadout.Weapons;
        if (index >= 0 && index < weapons.Length && weapons[index] != null)
            return weapons[index].GetComponent<EquippableWeapon>();
        return null;
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