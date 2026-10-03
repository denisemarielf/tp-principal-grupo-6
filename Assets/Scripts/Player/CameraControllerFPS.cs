using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerFPS : NetworkBehaviour
{
    public float sensitivity = 2f;
    [SerializeField] private Transform weaponPivot; // el objeto que agrupa WeaponCamera + las armas
    [SerializeField] private WeaponSwitcher weaponSwitcher; // asignalo a mano: vive en un hermano de esta cámara (Player), no en un ancestro

    private float xRotation = 0f;

    private NetworkVariable<float> networkPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private void Awake()
    {
        // Fallback por si no lo asignaste a mano: WeaponSwitcher es hermano de
        // esta cámara (ambos hijos de Player), así que GetComponentInParent NO
        // lo encuentra; buscamos desde la raíz en su lugar.
        if (weaponSwitcher == null)
        {
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>();
        }
    }

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
        // El recoil de cámara es pura vista personal de quien dispara: solo se
        // calcula para el dueño, y solo con el CameraRecoil del arma
        // actualmente equipada (cada arma tiene su propia configuración).
        Vector3 recoilOffset = Vector3.zero;

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

                if (transform.parent != null)
                {
                    transform.parent.Rotate(Vector3.up * mouseX);
                }

                networkPitch.Value = xRotation;
            }

            CameraRecoil currentRecoil = weaponSwitcher != null && weaponSwitcher.CurrentWeaponLogic != null
                ? weaponSwitcher.CurrentWeaponLogic.cameraRecoil
                : null;

            if (currentRecoil != null)
            {
                recoilOffset = currentRecoil.UpdateRecoil();
            }

            // El recoil se suma como offset LOCAL de la cámara, encima del
            // pitch "limpio" del mouse (xRotation nunca se contamina con el
            // recoil, así que networkPitch sigue reflejando tu apuntado real).
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f) * Quaternion.Euler(recoilOffset);
        }

        if (weaponPivot != null)
        {
            float pitchToApply = IsOwner ? xRotation : networkPitch.Value;
            weaponPivot.localRotation = Quaternion.Euler(pitchToApply, 0f, 0f) * Quaternion.Euler(recoilOffset);
        }
    }
}