using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PMovement))]
public class Character : NetworkBehaviour
{
    private PMovement movement;
    private WeaponSwitcher weaponSwitcher;


    private void Awake()
    {
        movement = GetComponent<PMovement>();
        weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();

    }


    public void OnMove(InputAction.CallbackContext context)
    {
       
        movement.SetMoveInput(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;


        if (context.performed)
        {
            movement.TryJump();
        }
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
        
        if (IsOwner && context.performed)
            weaponSwitcher.Shoot(context);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.Reload(context);
    }
}
