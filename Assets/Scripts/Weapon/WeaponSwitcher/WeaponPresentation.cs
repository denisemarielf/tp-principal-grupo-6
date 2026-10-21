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
        isOwner = false; // NUEVO

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
    private bool isOwner = true; // NUEVO


    public void ShowWeapon(ShootLogic shoot, GameObject weaponObject)
    {
        currentShoot = shoot;

        // CAMBIO: los brazos FPS solo existen para el dueño
        if (armsModel != null)
            armsModel.SetActive(isOwner && shoot != null);

        // El IK del personaje sí tiene que actualizarse en todos los clientes
        if (shoot != null)
            ApplyGripsForWeapon(weaponObject);

        // NUEVO: crosshair y HUD de munición solo del dueño
        if (!isOwner) return;

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
        {
            var link = weaponObject.GetComponent<WeaponWorldLink>();
            GameObject target = (link != null && link.worldModel != null)
                ? link.worldModel
                : weaponObject;

            characterModelGripController.SetGripsForWeapon(target);
        }
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