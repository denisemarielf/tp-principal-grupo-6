using UnityEngine;
using System.Collections;

public class AmmoManager : MonoBehaviour
{
    [Header("Ammo")]
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private int reserveAmmo = 60;
    [SerializeField] private float reloadTime = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptySound;

    /*
    [Header("Visual")]
    [SerializeField] private WeaponSway weaponSway; */

    private int currentAmmo;
    private bool isReloading = false;

    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;
    public bool HasAmmo => currentAmmo > 0;

    private void Awake()
    {
        currentAmmo = magazineSize;

      //  FindWeaponsway();
    }

    /*
    private void FindWeaponsway()
    {
        if (weaponSway == null)
            weaponSway = GetComponentInParent<WeaponSway>();

        if (weaponSway == null)
            weaponSway = GetComponentInChildren<WeaponSway>();
    }*/
    public void ConsumeShot()
    {
        currentAmmo--;
    }

    public void PlayEmptySound()
    {
        if (audioSource != null && emptySound != null)
            audioSource.PlayOneShot(emptySound);
    }

    public void TryReload()
    {
        if (isReloading) return;
        if (currentAmmo >= magazineSize) return;
        if (reserveAmmo <= 0) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        PlayReloadSound();
      //  weaponSway?.PlayReloadDip(reloadTime); // NUEVO

        yield return new WaitForSeconds(reloadTime);
        int ammoNeeded = magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, reserveAmmo);
        currentAmmo += ammoToLoad;
        reserveAmmo -= ammoToLoad;
        isReloading = false;
    }

    private void PlayReloadSound()
    {
        if (audioSource != null && reloadSound != null)
            audioSource.PlayOneShot(reloadSound);
    }

    public void AddReserveAmmo(int amount)
    {
        reserveAmmo += amount;
    }
}