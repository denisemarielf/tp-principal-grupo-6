using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(AmmoManager))]
[RequireComponent(typeof(WeaponLogic))]
public class ShootLogic : NetworkBehaviour
{
    public float shootRate = 0.5f;
    public bool isAutomatic = true; // false = semiautomática (un disparo por apretón)

    private float shootRateTime = 0;
    private InputAction shootAction;
    private bool isEquipped = false;
    private AmmoManager ammoManager;
    private WeaponLogic weaponLogic;

    private void Awake()
    {
        ammoManager = GetComponent<AmmoManager>();
        weaponLogic = GetComponent<WeaponLogic>();
    }

    /// <summary>Llamado por WeaponSwitcher cada vez que cambia el arma equipada,
    /// para que esta arma solo reaccione al input cuando es la activa (el
    /// GameObject sigue vivo y corriendo aunque no esté equipada).</summary>
    public void SetEquipped(bool equipped)
    {

        isEquipped = equipped;
       
    }

    private void Update()
    {
        // Mientras el botón siga apretado (consultado directamente, no vía
        // eventos) y el arma sea automática, seguimos intentando disparar cada
        // frame; TryShoot respeta el cooldown solo. isEquipped evita que un
        // arma guardada (no equipada) siga disparando porque el botón físico
        // es el mismo para todas.
        if (!IsOwner || !isEquipped || PauseMenuUI.IsOpen) return;
       
        if (isAutomatic && shootAction != null && shootAction.IsPressed())
        {
            TryShoot();
        }
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        shootAction = context.action; // guardamos la referencia para consultarla en Update

        if (context.performed)
        {
            TryShoot(); // primer disparo inmediato al apretar, tanto auto como semi-auto
        }
    }

    private void TryShoot()
    {
        if (Time.time < shootRateTime) return;
        if (ammoManager.IsReloading) return; 

        shootRateTime = Time.time + shootRate;

        if (!ammoManager.HasAmmo)
        {
            ammoManager.PlayEmptySound();
            ammoManager.TryReload();
            return;
        }
        ammoManager.ConsumeShot();

        weaponLogic.PlayShootEffectsLocal();
        weaponLogic.ApplyCameraRecoil();
        weaponLogic.RequestFire();
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.performed) ammoManager.TryReload();
    }

    // Pasamanos hacia AmmoManager / WeaponLogic para no romper el API que ya usan
    // otros scripts (WeaponSwitcher, WeaponPowerUps, AmmoUI, etc.)
    public void AddReserveAmmo(int amount) => ammoManager.AddReserveAmmo(amount);
    public int CurrentAmmo => ammoManager.CurrentAmmo;
    public int ReserveAmmo => ammoManager.ReserveAmmo;
    public bool IsReloading => ammoManager.IsReloading;

    public int DamageAmount
    {
        get => weaponLogic.damageAmount;
        set => weaponLogic.damageAmount = value;
    }
}