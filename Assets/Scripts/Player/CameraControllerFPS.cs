using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerFPS : NetworkBehaviour
{
    public float sensitivity = 2f;
    [SerializeField] private Transform weaponPivot; // el objeto que agrupa WeaponCamera + las armas

    private float xRotation = 0f;

    private NetworkVariable<float> networkPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    void Start()
    {
        if (IsOwner)
        {
            SetCursorLocked(true);
        }
    }

    // Esc libera el cursor para poder usar la UI (por ejemplo "Salir de la partida") y lo vuelve a bloquear.
    // Mientras esta libre, el mouse no mueve la camara.
    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void ResetCamera()
    {
        xRotation = 0f;
        transform.localRotation = Quaternion.identity;
        if (weaponPivot != null)
        {
            weaponPivot.localRotation = Quaternion.identity;
        }
        if (IsOwner)
        {
            SetCursorLocked(true);
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            }

            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                float mouseX = mouseDelta.x * sensitivity * Time.deltaTime;
                float mouseY = mouseDelta.y * sensitivity * Time.deltaTime;

                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -90f, 90f);

                transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
                if (transform.parent != null)
                {
                    transform.parent.Rotate(Vector3.up * mouseX);
                }

                networkPitch.Value = xRotation;
            }
        }

        if (weaponPivot != null)
        {
            float pitchToApply = IsOwner ? xRotation : networkPitch.Value;
            weaponPivot.localRotation = Quaternion.Euler(pitchToApply, 0f, 0f);
        }
    }
}