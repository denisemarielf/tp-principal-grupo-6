using UnityEngine;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Shoot currentShoot;
    [SerializeField] private TMP_Text clipSize;
    [SerializeField] private TMP_Text ammoRemaining;


    void Update()
    {
        UpdateAmmoUI();

    }


    public void SetWeapon(Shoot weapon)
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
            return;
        }

        clipSize.text = currentShoot.CurrentAmmo.ToString();
        ammoRemaining.text = currentShoot.ReserveAmmo.ToString();
    }
}
