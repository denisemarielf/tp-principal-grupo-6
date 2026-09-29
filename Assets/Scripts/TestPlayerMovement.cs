using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlayerMovement : MonoBehaviour
{
    public float velocidad = 15f;

    void Update()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1;

        Vector3 movimiento = transform.right * input.x
                           + transform.forward * input.y;

        GetComponent<CharacterController>().Move(
            movimiento * velocidad * Time.deltaTime
        );
    }
}