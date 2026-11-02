using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Effects/Ammo")]
public class AmmoEffect : ItemEffect
{
    [SerializeField] private int amount = 25;

    public override bool Apply(GameObject user)
    {
        var switcher = user.GetComponentInChildren<WeaponSwitcher>();
        if (switcher == null) switcher = user.GetComponentInParent<WeaponSwitcher>();
        if (switcher == null) return false;

        AmmoManager ammo = switcher.CurrentAmmoManager;
        if (ammo == null) return false;          // arma melee o sin arma equipada
        if (ammo.IsReserveFull) return false;    // no gastar el ítem si no sirve

        switcher.GrantAmmoToCurrentWeapon(amount);
        return true;
    }
}