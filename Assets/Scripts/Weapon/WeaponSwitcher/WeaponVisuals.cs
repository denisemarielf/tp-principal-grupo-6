using UnityEngine;
using UnityEngine.Rendering;

public class WeaponVisuals : MonoBehaviour
{
    [SerializeField] private WeaponCatalog catalog;
    private bool isOwner;

    public void SetupCatalog(bool isOwner)
    {
        this.isOwner = isOwner;
        int layer = GetLayer(isOwner);

        foreach (var entry in catalog.AllWeapons)
        {
            if (entry.weaponObject == null) continue;

            entry.weaponObject.SetActive(true);
            SetLayerRecursively(entry.weaponObject, layer);
            SetWorldModelVisible(entry.weaponObject, false);
        }
    }

    public void PrepareWeapon(GameObject weapon, bool isOwner)
    {
        if (weapon == null) return;

        weapon.SetActive(true);
        SetLayerRecursively(weapon, GetLayer(isOwner));
    }

    public void UpdateWeaponVisuals(GameObject[] weapons, int index)
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null) continue;

            bool isSelected = i == index;
            SetWeaponVisible(weapons[i], isSelected);
            SetWorldModelVisible(weapons[i], isSelected);

            ShootLogic shootLogic = weapons[i].GetComponent<ShootLogic>();
            if (shootLogic != null)
                shootLogic.SetEquipped(isSelected);
        }
    }

    public void HideAndUnequip(GameObject weapon)
    {
        if (weapon == null) return;

        SetWeaponVisible(weapon, false);
        SetWorldModelVisible(weapon, false);

        ShootLogic shootLogic = weapon.GetComponent<ShootLogic>();
        if (shootLogic != null)
            shootLogic.SetEquipped(false);
    }

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
        {
            bool isGeometry = r is MeshRenderer || r is SkinnedMeshRenderer;

            if (isOwner)
                r.enabled = visible;
            else if (isGeometry)
                r.enabled = false; // remotos: el mesh del viewmodel nunca se dibuja
        }
    }

    private void SetWorldModelVisible(GameObject weapon, bool visible)
    {
        var link = weapon.GetComponent<WeaponWorldLink>();
        if (link == null || link.worldModel == null) return;

        link.worldModel.SetActive(visible);
        if (!visible) return;

        var mode = isOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        foreach (Renderer r in link.worldModel.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = mode;
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