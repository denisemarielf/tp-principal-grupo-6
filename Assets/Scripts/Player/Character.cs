using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PMovement))]
public class Character : NetworkBehaviour
{
    private PMovement movement;
    private WeaponSwitcher weaponSwitcher;
    [SerializeField] private float interactRange = 2f;
    [SerializeField] private LayerMask pickupLayer;



    private void Awake()
    {
        movement = GetComponent<PMovement>();
        weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();

    }


    public void OnMove(InputAction.CallbackContext context)
    {
       
        movement.SetMoveInput(context.ReadValue<Vector2>());
    }
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
        
            movement.SetSprint(true);
        }
        else if (context.canceled)
        {
            
            movement.SetSprint(false);
        }
        
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
    public void OnInteraction(InputAction.CallbackContext context)
    {
        Debug.Log("interactuando");
        if (!IsOwner) return;
        if (!context.performed) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, pickupLayer);
        WeaponPickup nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            WeaponPickup pickup = hit.GetComponent<WeaponPickup>();
            if (pickup == null) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = pickup;
            }
        }

        if (nearest != null)
        {
            weaponSwitcher?.RequestPickupWeaponServerRpc(
                (int)nearest.weaponType,
                nearest.GetComponent<NetworkObject>().NetworkObjectId
            );
        }
    }
}
