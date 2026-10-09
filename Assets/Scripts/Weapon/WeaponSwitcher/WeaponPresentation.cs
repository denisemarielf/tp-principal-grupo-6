using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Feedback al jugador al cambiar de arma: brazos FPS, grips, crosshair,
/// AmmoUI y sonido de cambio.
/// </summary>
public class WeaponPresentation : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip switchSound;
    public AmmoUI ammoUI;
    public CrosshairController crosshairController;

    [Header("Brazos")]
    public GameObject armsModel;
    public ArmsGripController armsGripController;
    public ArmsGripController characterModelGripController;

    private ShootLogic currentShoot;
    private bool ownerUiActive;

    // ---------- Ciclo de vida (lo llama WeaponSwitcher) ----------

    public void HideArmsForRemotePlayer()
    {
        if (armsModel != null)
            armsModel.SetActive(false);
    }

    public void StartOwnerUI()
    {
        ownerUiActive = true;
        StartCoroutine(BuscarAmmoUI());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void StopOwnerUI()
    {
        if (!ownerUiActive) return;

        ownerUiActive = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy() => StopOwnerUI();

    // ---------- API ----------

    /// <summary>Refleja el arma actual (shoot == null significa "enfundado").</summary>
    public void ShowWeapon(ShootLogic shoot, GameObject weaponObject)
    {
        currentShoot = shoot;

        if (armsModel != null)
            armsModel.SetActive(shoot != null);

        if (shoot != null)
            ApplyGripsForWeapon(weaponObject);

        if (crosshairController != null)
            crosshairController.SetVisible(shoot != null);

        if (ammoUI != null)
            ammoUI.SetWeapon(shoot);
    }

    public void ApplyGripsForWeapon(GameObject weaponObject)
    {
        if (weaponObject == null) return;

        if (armsGripController != null)
            armsGripController.SetGripsForWeapon(weaponObject);

        if (characterModelGripController != null)
            characterModelGripController.SetGripsForWeapon(weaponObject);
    }

    public void PlaySwitchSound()
    {
        if (audioSource != null && switchSound != null)
            audioSource.PlayOneShot(switchSound);
    }

    // ---------- Ammo UI ----------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ammoUI = null;
        StartCoroutine(BuscarAmmoUI());
    }

    private IEnumerator BuscarAmmoUI()
    {
        while (ammoUI == null)
        {
            ammoUI = FindAnyObjectByType<AmmoUI>();

            if (ammoUI == null)
                yield return null;
        }

        ammoUI.SetWeapon(currentShoot);
    }
}