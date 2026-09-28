using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PMovement))]
public class Character : NetworkBehaviour
{
    private PMovement movement;
   

    private void Awake()
    {
        movement = GetComponent<PMovement>();
        
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
}
