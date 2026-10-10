using UnityEngine;

/// <summary>
/// Todo lo visual de las armas: layers, mostrar/ocultar renderers y
/// activar/desactivar el WeaponModel de las armas fuera del loadout.
/// No conoce la red.
/// </summary>
public class WeaponVisuals : MonoBehaviour
{
    [SerializeField] private WeaponCatalog catalog;

    /// <summary>Activa todas las armas del catálogo y les asigna el layer según sea owner o no.</summary>
    public void SetupCatalog(bool isOwner)
    {
        int layer = GetLayer(isOwner);

        foreach (var entry in catalog.AllWeapons)
        {
            if (entry.weaponObject == null) continue;

            entry.weaponObject.SetActive(true);
            SetLayerRecursively(entry.weaponObject, layer);
        }
    }

    /// <summary>Prepara un arma recién asignada a un slot (activa + layer).</summary>
    public void PrepareWeapon(GameObject weapon, bool isOwner)
    {
        if (weapon == null) return;

        weapon.SetActive(true);
        SetLayerRecursively(weapon, GetLayer(isOwner));
    }

    /// <summary>Muestra solo el arma 'index' y actualiza el equipped de cada ShootLogic.</summary>
    public void UpdateWeaponVisuals(GameObject[] weapons, int index)
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null) continue;

            bool isSelected = i == index;
            SetWeaponVisible(weapons[i], isSelected);

            ShootLogic shootLogic = weapons[i].GetComponent<ShootLogic>();
            if (shootLogic != null)
                shootLogic.SetEquipped(isSelected);
        }
    }

    /// <summary>Oculta visualmente un arma y la desequipa (sin desactivar el GameObject).</summary>
    public void HideAndUnequip(GameObject weapon)
    {
        if (weapon == null) return;

        SetWeaponVisible(weapon, false);

        ShootLogic shootLogic = weapon.GetComponent<ShootLogic>();
        if (shootLogic != null)
            shootLogic.SetEquipped(false);
    }

    /// <summary>Desactiva el WeaponModel de toda arma del catálogo que no esté en el loadout.</summary>
    public void UpdateCatalogWeaponsActivity(GameObject[] loadoutWeapons)
    {
        foreach (var entry in catalog.AllWeapons)
        {
            if (entry.weaponObject == null) continue;

            bool isInLoadout = System.Array.IndexOf(loadoutWeapons, entry.weaponObject) != -1;

            Transform weaponModel = entry.weaponObject.transform.Find("WeaponModel");
            if (weaponModel != null)
                weaponModel.gameObject.SetActive(isInLoadout);
        }
    }

    private void SetWeaponVisible(GameObject weapon, bool visible)
    {
        foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    private static int GetLayer(bool isOwner) =>
        LayerMask.NameToLayer(isOwner ? "Weapon" : "Default");

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}