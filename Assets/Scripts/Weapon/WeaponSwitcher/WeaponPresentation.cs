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
    public WorldGripProxy worldGripProxy;
    private EquippableWeapon currentWeapon; // antes: ShootLogic currentShoot
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


    public void ShowWeapon(EquippableWeapon weapon, GameObject weaponObject)
    {
        currentWeapon = weapon;
        bool hasWeapon = weapon != null;
        ShootLogic firearm = weapon as ShootLogic; // null si es melee o está enfundado

        // Los brazos FPS solo existen para el dueño; sirven para fuego y melee
        if (armsModel != null)
            armsModel.SetActive(isOwner && hasWeapon);

        // El IK del personaje se actualiza en todos los clientes
        if (hasWeapon)
            ApplyGripsForWeapon(weaponObject);

        if (!isOwner) return;

        // Crosshair y munición solo para armas de fuego
        if (crosshairController != null)
            crosshairController.SetVisible(firearm != null);

        if (ammoUI != null)
            ammoUI.SetWeapon(firearm); // null oculta el HUD de munición
    }

    public void ApplyGripsForWeapon(GameObject weaponObject)
    {
        if (weaponObject == null) return;

        // Brazos FPS -> grips del viewmodel (el arma original)
        if (armsGripController != null)
            armsGripController.SetGripsForWeapon(weaponObject);
        else
            Debug.LogWarning("[IK] WeaponPresentation: armsGripController NO asignado.", this);

        // Personaje -> grips del worldmodel, vía el proxy fuera del Animator
        if (characterModelGripController != null)
        {
            var link = weaponObject.GetComponent<WeaponWorldLink>();
            GameObject world = (link != null && link.worldModel != null) ? link.worldModel : weaponObject;

            if (worldGripProxy != null)
            {
                worldGripProxy.SetSources(world);
                characterModelGripController.SetGripsForWeapon(worldGripProxy.gameObject);
            }
            else
            {
                characterModelGripController.SetGripsForWeapon(world);
            }
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
            if (ammoUI == null) yield return null;
        }

        ammoUI.SetWeapon(currentWeapon as ShootLogic);
    }
}