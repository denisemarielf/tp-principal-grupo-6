
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    private static readonly Color Bone = new Color(0.93f, 0.94f, 0.92f, 1f);

    private ShootLogic currentShoot;

    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TMP_Text clipSize;
    [SerializeField] private TMP_Text ammoRemaining;
    [SerializeField] private Image weaponIcon;

    private void Awake()
    {
        if (panelGroup == null)
            panelGroup = GetComponent<CanvasGroup>();

        ApplyQuietStyle();
        SetPanelVisible(false);
    }

    // Colores nada más. La posición la define el prefab, así se puede mover a mano.
    private void ApplyQuietStyle()
    {
        if (weaponIcon != null)
            weaponIcon.enabled = false;

        if (clipSize != null)
        {
            clipSize.color = Bone;
            clipSize.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (ammoRemaining != null)
        {
            ammoRemaining.color = Bone;
            ammoRemaining.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (panelGroup == null)
            return;

        Image plate = panelGroup.GetComponent<Image>();
        if (plate != null)
            plate.enabled = false;

        Transform bullets = panelGroup.transform.Find("shotIcon");
        if (bullets == null)
            return;

        Image bulletImage = bullets.GetComponent<Image>();
        if (bulletImage != null)
            bulletImage.color = Color.white;
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

            return;
        }

        clipSize.text = $"{currentShoot.CurrentAmmo} / {currentShoot.MagazineSize}";
        ammoRemaining.text = currentShoot.ReserveAmmo.ToString();
    }
}