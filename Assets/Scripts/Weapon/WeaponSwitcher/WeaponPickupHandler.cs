using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Lógica SOLO DE SERVIDOR para recoger y dropear armas.
/// No es un NetworkBehaviour: la entrada de red sigue siendo el ServerRpc
/// de WeaponSwitcher, que delega aquí.
/// </summary>
public class WeaponPickupHandler : MonoBehaviour
{
    [SerializeField] private WeaponCatalog catalog;
    [SerializeField] private WeaponLoadout loadout;

    public void HandlePickup(int weaponTypeIndex, ulong pickupNetworkObjectId, int currentWeaponIndex)
    {
        var type = (WeaponType)weaponTypeIndex;

        // Ya tenemos esta arma: el pickup permanece.
        if (loadout.HasWeaponType(type)) return;

        if (catalog.GetWeaponObjectByType(type) == null) return;

        int freeSlot = loadout.FindFreeSlot();

        if (freeSlot >= 0)
        {
            loadout.SetSlotType(freeSlot, weaponTypeIndex);
        }
        else
        {
            // Ambos ocupados: reemplazamos el arma en mano y la tiramos al suelo.
            int slotToReplace = currentWeaponIndex >= 0 ? currentWeaponIndex : 0;
            int droppedType = loadout.GetSlotType(slotToReplace);

            loadout.SetSlotType(slotToReplace, weaponTypeIndex);

            SpawnDroppedWeapon(
                (WeaponType)droppedType,
                transform.position + transform.forward * 1f + Vector3.up * 0.3f);
        }

        ConsumePickup(pickupNetworkObjectId);
    }

    private void ConsumePickup(ulong pickupNetworkObjectId)
    {
        var spawned = NetworkManager.Singleton.SpawnManager.SpawnedObjects;

        if (spawned.TryGetValue(pickupNetworkObjectId, out NetworkObject pickupNetObj))
        {
            WeaponPickup pickup = pickupNetObj.GetComponent<WeaponPickup>();
            if (pickup != null)
                pickup.Consume();
        }
    }

    private void SpawnDroppedWeapon(WeaponType type, Vector3 position)
    {
        GameObject prefab = catalog.GetPickupPrefabByType(type);
        if (prefab == null) return;

        GameObject dropped = Instantiate(prefab, position, Quaternion.identity);

        NetworkObject netObj = dropped.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
    }
}