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

        // Melee, sin arma equipada o reserva llena: no gastar el ítem.
        // (El servidor no tiene el AmmoManager real del cliente, por eso se consulta esto.)
        if (!switcher.CanReceiveAmmo) return false;

        switcher.GrantAmmoToCurrentWeapon(amount);
        return true;
    }
}
