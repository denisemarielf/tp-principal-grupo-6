using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponInputHandler : MonoBehaviour
{
    [SerializeField] private WeaponSwitcher switcher;
    private float nextScrollTime;

    // Tecla 1: el arma melee. Si no hay, enfunda.
    public void OnHolster(InputAction.CallbackContext context)
    {
        if (switcher == null || !switcher.IsOwner || !context.performed)
            return;

        if (switcher.GetWeaponAt(WeaponLoadout.MeleeSlot) != null)
            switcher.SelectWeapon(WeaponLoadout.MeleeSlot);
        else
            switcher.SelectWeapon(-1);
    }

    public void OnSelectSlot0(InputAction.CallbackContext context) => Select(context, 0);
    public void OnSelectSlot1(InputAction.CallbackContext context) => Select(context, 1);

    // Rueda: alterna las armas de las teclas 2 y 3 (slots 0 y 1). No enfunda.
    private void Update()
    {
        if (switcher == null || !switcher.IsOwner || PauseMenuUI.IsOpen)
            return;
        if (Cursor.lockState != CursorLockMode.Locked || Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f || Time.unscaledTime < nextScrollTime)
            return;

        nextScrollTime = Time.unscaledTime + 0.12f;

        int next = NextWeaponSlot(scroll > 0f ? 1 : -1);
        if (next >= 0)
            switcher.SelectWeapon(next);
    }

    private int NextWeaponSlot(int direction)
    {
        int current = switcher.CurrentWeaponIndex;
        if (current < 0 || current == WeaponLoadout.MeleeSlot)
        {
            int preferred = direction > 0 ? 0 : 1;
            if (switcher.GetWeaponAt(preferred) != null)
                return preferred;

            int other = preferred == 0 ? 1 : 0;
            return switcher.GetWeaponAt(other) != null ? other : -1;
        }

        int swapped = current == 0 ? 1 : 0;
        return switcher.GetWeaponAt(swapped) != null ? swapped : -1;
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!switcher.IsOwner || !context.performed || PauseMenuUI.IsOpen) return;

        switcher.CurrentWeapon?.OnShoot(context);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (!switcher.IsOwner || !context.performed) return;

        switcher.CurrentWeapon?.OnReload(context);
    }

    private void Select(InputAction.CallbackContext context, int index)
    {
        if (switcher.IsOwner && context.performed)
            switcher.SelectWeapon(index);
    }
}