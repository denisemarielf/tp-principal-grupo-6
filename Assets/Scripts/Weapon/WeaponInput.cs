using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(WeaponSwitcher))]
public class WeaponInput : NetworkBehaviour
{
    private WeaponSwitcher weaponSwitcher;

    private void Awake()
    {
        weaponSwitcher = GetComponent<WeaponSwitcher>();
    }

    public void OnSelectWeapon1(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.SelectWeapon(-1);
       
    }

    public void OnSelectWeapon2(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.SelectWeapon(0);
    }

    public void OnSelectWeapon3(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.SelectWeapon(1);
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (IsOwner)
            weaponSwitcher.Shoot(context);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (IsOwner)
            weaponSwitcher.Reload(context);
    }
}