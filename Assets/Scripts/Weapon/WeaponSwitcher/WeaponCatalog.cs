using UnityEngine;

[System.Serializable]
public struct WeaponEntry
{
    public WeaponType type;
    public GameObject weaponObject;
}

[System.Serializable]
public struct WeaponPickupEntry
{
    public WeaponType type;
    public GameObject pickupPrefab;
}

/// <summary>
/// Datos estáticos: qué armas existen bajo WeaponPivot y qué prefab de pickup
/// corresponde a cada tipo. No tiene lógica de red ni de estado.
/// </summary>
public class WeaponCatalog : MonoBehaviour
{
    [Header("Catálogo de armas (TODAS las que existen bajo WeaponPivot)")]
    [SerializeField] private WeaponEntry[] allWeapons;

    [Header("Drop de armas")]
    [Tooltip("Un prefab de pickup por cada tipo de arma (cada uno ya con su propio mesh y weaponType configurado).")]
    [SerializeField] private WeaponPickupEntry[] pickupPrefabs;

    public WeaponEntry[] AllWeapons => allWeapons;

    public GameObject GetWeaponObjectByType(WeaponType type)
    {
        foreach (var entry in allWeapons)
            if (entry.type == type)
                return entry.weaponObject;

        return null;
    }

    public WeaponType GetTypeForWeaponObject(GameObject weaponObject)
    {
        foreach (var entry in allWeapons)
            if (entry.weaponObject == weaponObject)
                return entry.type;

        return (WeaponType)(-1);
    }

    public GameObject GetPickupPrefabByType(WeaponType type)
    {
        foreach (var entry in pickupPrefabs)
            if (entry.type == type)
                return entry.pickupPrefab;

        return null;
    }
}