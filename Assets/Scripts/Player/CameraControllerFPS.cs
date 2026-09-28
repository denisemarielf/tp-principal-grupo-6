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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            if (Mouse.current != null)
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