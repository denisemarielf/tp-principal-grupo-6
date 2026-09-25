using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSway : NetworkBehaviour
{
    private Quaternion startRotation;
    public float swayAmount = 8f;
    public float mouseSensitivity = 1.25f;
    public float recoilKick = 5f;
    public float recoilRecoverySpeed = 6f;
    private float currentRecoil;

    void Start()
    {
        startRotation = transform.localRotation;
    }

    void Update()
    {
        HandleRecoilRecovery();

        if (IsOwner)
        {
            Sway(); // sway con el mouse local + recoil, solo tiene sentido para el dueño
        }
        else
        {
            ApplyRecoilOnly(); // los demás solo ven el "kick" del retroceso, sin sway de mouse
        }
    }

    private void Sway()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;
        Quaternion xAngle = Quaternion.AngleAxis(mouseX * -1f, Vector3.up);
        Quaternion yAngle = Quaternion.AngleAxis(mouseY * -1f, Vector3.right);
        Quaternion recoilRotation = Quaternion.AngleAxis(-currentRecoil, Vector3.right);
        Quaternion targetRotation = startRotation * xAngle * yAngle * recoilRotation;
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * swayAmount
        );
    }

    private void ApplyRecoilOnly()
    {
        Quaternion recoilRotation = Quaternion.AngleAxis(-currentRecoil, Vector3.right);
        Quaternion targetRotation = startRotation * recoilRotation;
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * swayAmount
        );
    }

    private void HandleRecoilRecovery()
    {
        currentRecoil = Mathf.Lerp(currentRecoil, 0f, Time.deltaTime * recoilRecoverySpeed);
    }

    public void AddRecoil()
    {
        currentRecoil += recoilKick;
    }

    [ServerRpc]
    private void AddRecoilServerRpc()
    {
        AddRecoilClientRpc();
    }

    [ClientRpc]
    private void AddRecoilClientRpc()
    {
        if (IsOwner) return; // el dueño ya lo aplicó localmente, evitamos duplicarlo
        currentRecoil += recoilKick;
    }
}
