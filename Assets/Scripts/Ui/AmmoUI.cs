
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    private ShootLogic currentShoot;

    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TMP_Text clipSize;
    [SerializeField] private TMP_Text ammoRemaining;
    [SerializeField] private Image weaponIcon;

    private void Awake()
    {
        if (panelGroup == null)
            panelGroup = GetComponent<CanvasGroup>();

        SetPanelVisible(false);
    }

    void Update()
    {
        UpdateAmmoUI();
    }

    public void SetWeapon(ShootLogic weapon)
    {
        currentShoot = weapon;
        SetPanelVisible(weapon != null);
        UpdateAmmoUI();
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelGroup != null)
            panelGroup.alpha = visible ? 1f : 0f;
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

        clipSize.text = $"{currentShoot.CurrentAmmo} / {currentShoot.MagazineSize}";
        ammoRemaining.text = currentShoot.ReserveAmmo.ToString();

        if (weaponIcon != null)
        {
            weaponIcon.sprite = currentShoot.HudIcon;
            weaponIcon.enabled = currentShoot.HudIcon != null;
        }
    }
}