using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraControllerFPS : NetworkBehaviour
{
    public float sensitivity = 2f;
    [SerializeField] private Transform weaponPivot; // el objeto que agrupa WeaponCamera + las armas
    [SerializeField] private WeaponSwitcher weaponSwitcher; // asignalo a mano: vive en un hermano de esta camara (Player), no en un ancestro

    private float xRotation = 0f;

    private NetworkVariable<float> networkPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private void Awake()
    {
        // Fallback por si no lo asignaste a mano: WeaponSwitcher es hermano de
        // esta camara (ambos hijos de Player), asi que GetComponentInParent NO
        // lo encuentra; buscamos desde la raiz en su lugar.
        if (weaponSwitcher == null)
        {
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    void Start()
    {
        if ((!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening || IsOwner) && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && (!IsSpawned || IsOwner) && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    // El cursor lo abre y lo cierra PauseMenuUI. Mientras esta libre, el mouse no mueve la camara.
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
        if ((!IsSpawned || IsOwner) && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    void Update()
    {
        bool isLocal = !IsSpawned || IsOwner;
        Vector3 recoilOffset = Vector3.zero;

        if (isLocal)
        {
            if (!PauseMenuUI.IsOpen
                && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    SetCursorLocked(true);
                }
            }

            if (!PauseMenuUI.IsOpen && Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
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

                if (IsSpawned)
                {
                    networkPitch.Value = xRotation;
                }
            }

            CameraRecoil currentRecoil = weaponSwitcher != null && weaponSwitcher.CurrentWeaponLogic != null
                ? weaponSwitcher.CurrentWeaponLogic.cameraRecoil
                : null;

            if (currentRecoil != null)
            {
                recoilOffset = currentRecoil.UpdateRecoil();
            }

            // El recoil se suma como offset LOCAL de la camara, encima del
            // pitch "limpio" del mouse (xRotation nunca se contamina con el
            // recoil, asi que networkPitch sigue reflejando tu apuntado real).
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f) * Quaternion.Euler(recoilOffset);
        }

        if (weaponPivot != null)
        {
            float pitchToApply = isLocal ? xRotation : (IsSpawned ? networkPitch.Value : xRotation);
            weaponPivot.localRotation = Quaternion.Euler(pitchToApply, 0f, 0f) * Quaternion.Euler(recoilOffset);
        }
    }
}
