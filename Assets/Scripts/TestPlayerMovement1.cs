using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TestPlayerMovement1 : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float velocidad = 5f;
    [SerializeField] private float velocidadSprint = 8f;

    [Header("Salto y gravedad")]
    [SerializeField] private float alturaSalto = 1.5f;
    [SerializeField] private float gravedad = -20f;

    [Header("Cámara")]
    [SerializeField] private Transform camara;
    [SerializeField] private float sensibilidadMouse = 2f;
    [SerializeField] private float limiteVertical = 80f;

    private CharacterController controller;

    private Vector3 velocidadVertical;
    private float rotacionVertical = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Si no asignamos la cámara desde el Inspector,
        // intenta encontrar una cámara hija del Player.
        if (camara == null)
        {
            Camera cam = GetComponentInChildren<Camera>();

            if (cam != null)
                camara = cam.transform;
        }
    }

    private void Start()
    {
        // Oculta y bloquea el cursor en el centro de la pantalla.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Movimiento();
        GravedadYSalto();
        MirarConMouse();

        // ESC libera el mouse para poder salir de la prueba.
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Movimiento()
    {
        if (Keyboard.current == null)
            return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1;

        // Evita que moverse en diagonal sea más rápido.
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 direccion =
            transform.right * input.x +
            transform.forward * input.y;

        float velocidadActual = velocidad;

        // SHIFT = correr
        if (Keyboard.current.leftShiftKey.isPressed)
            velocidadActual = velocidadSprint;

        Vector3 movimiento =
            direccion * velocidadActual;

        controller.Move(
            movimiento * Time.deltaTime
        );
    }

    private void GravedadYSalto()
    {
        if (controller.isGrounded)
        {
            // Mantiene al personaje pegado al suelo.
            if (velocidadVertical.y < 0)
                velocidadVertical.y = -2f;

            // ESPACIO = salto
            if (Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                velocidadVertical.y =
                    Mathf.Sqrt(
                        alturaSalto * -2f * gravedad
                    );
            }
        }

        // Gravedad
        velocidadVertical.y +=
            gravedad * Time.deltaTime;

        controller.Move(
            velocidadVertical * Time.deltaTime
        );
    }

    private void MirarConMouse()
    {
        if (Mouse.current == null || camara == null)
            return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        // Rotación horizontal del Player
        float rotacionHorizontal =
            mouseDelta.x * sensibilidadMouse;

        transform.Rotate(
            Vector3.up * rotacionHorizontal
        );

        // Rotación vertical de la cámara
        rotacionVertical -=
            mouseDelta.y * sensibilidadMouse;

        rotacionVertical =
            Mathf.Clamp(
                rotacionVertical,
                -limiteVertical,
                limiteVertical
            );

        camara.localRotation =
            Quaternion.Euler(
                rotacionVertical,
                0f,
                0f
            );
    }

    private void OnDisable()
    {
        // Devuelve el cursor cuando se desactiva el Player.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}