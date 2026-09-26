using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(AmmoManager))]
[RequireComponent(typeof(WeaponLogic))]
public class ShootLogic : NetworkBehaviour
{
    public float shootRate = 0.5f;

    private float shootRateTime = 0;
    private AmmoManager ammoManager;
    private WeaponLogic weaponLogic;

    private void Awake()
    {
        ammoManager = GetComponent<AmmoManager>();
        weaponLogic = GetComponent<WeaponLogic>();
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.performed && Time.time >= shootRateTime)
        {
            shootRateTime = Time.time + shootRate;

            if (!ammoManager.HasAmmo)
            {
                ammoManager.PlayEmptySound();
                ammoManager.TryReload();
                return;
            }
            ammoManager.ConsumeShot();

            // Efectos para mí mismo, instantáneos, sin esperar ida y vuelta al servidor
            weaponLogic.PlayShootEffectsLocal();

            // La bala real la crea el servidor, y desde ahí avisa al resto
            weaponLogic.RequestFire();
        }
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.performed) ammoManager.TryReload();
    }


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