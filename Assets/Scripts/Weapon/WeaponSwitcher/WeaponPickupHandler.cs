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
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
        .TryGetValue(pickupNetworkObjectId, out NetworkObject pickupNetObj))
            return;
        var type = (WeaponType)weaponTypeIndex;
       
        if (loadout.HasWeaponType(type)) return;


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

        // Más arriba y más adelante, para no nacer dentro del suelo ni del jugador
        Vector3 spawnPos = transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;

        GameObject dropped = Instantiate(prefab, spawnPos, Quaternion.identity);

        var rb = dropped.GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Evita el empujón por superposición con el CharacterController
        var col = dropped.GetComponentInChildren<Collider>();
        var playerCol = GetComponent<Collider>();   // o CharacterController
        if (col != null && playerCol != null)
            Physics.IgnoreCollision(col, playerCol);

        NetworkObject netObj = dropped.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
    }
}