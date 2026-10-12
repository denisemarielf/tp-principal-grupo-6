using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponInputHandler : MonoBehaviour
{
    [SerializeField] private WeaponSwitcher switcher;

    // Teclas de selección: -1 = enfundar, 0 = slot 0, 1 = slot 1
    public void OnHolster(InputAction.CallbackContext context) => Select(context, -1);
    public void OnSelectSlot0(InputAction.CallbackContext context) => Select(context, 0);
    public void OnSelectSlot1(InputAction.CallbackContext context) => Select(context, 1);

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!switcher.IsOwner || !context.performed || PauseMenuUI.IsOpen) return;

        ShootLogic shoot = switcher.CurrentShoot;
        if (shoot != null)
            shoot.OnShoot(context);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (!switcher.IsOwner || !context.performed) return;

        ShootLogic shoot = switcher.CurrentShoot;
        if (shoot != null)
            shoot.OnReload(context);
    }

    private void Select(InputAction.CallbackContext context, int index)
    {
        if (switcher.IsOwner && context.performed)
            switcher.SelectWeapon(index);
    }
}