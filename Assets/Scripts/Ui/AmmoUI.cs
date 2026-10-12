
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    private ShootLogic currentShoot;

    [SerializeField] private TMP_Text clipSize;
    [SerializeField] private TMP_Text ammoRemaining;
    [SerializeField] private Image weaponIcon;

    void Update()
    {
        UpdateAmmoUI();
    }

    public void SetWeapon(ShootLogic weapon)
    {
        currentShoot = weapon;
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (currentShoot == null)
        {
            clipSize.text = " ";
            ammoRemaining.text = " ";

            if (weaponIcon != null)
            {
                weaponIcon.sprite = null;
                weaponIcon.enabled = false;
            }

            return;
        }

        clipSize.text =
            $"{currentShoot.CurrentAmmo} / {currentShoot.MagazineSize}";

        ammoRemaining.text =
            currentShoot.ReserveAmmo.ToString();

        if (weaponIcon != null)
        {
            weaponIcon.sprite = currentShoot.HudIcon;
            weaponIcon.enabled = currentShoot.HudIcon != null;
        }
    }
}
