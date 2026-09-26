using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSway : NetworkBehaviour
{
    private Quaternion startRotation;
    private Vector3 startPosition;
    [Header("Bob (balanceo al caminar)")]
    private PMovement playerMovement;
    public float bobFrequency = 8f;   // qué tan rápido oscila
    public float bobAmount = 0.02f;   // qué tan grande es el movimiento (metros)
    private float bobTimer = 0f;
    private float bobAmountCurrent = 0f;

    [Header("Respiración en reposo")]
    public float breathingFrequency = 1.5f;
    public float breathingAmount = 0.004f;
    private float breathingTimer = 0f;

    public float swayAmount = 8f;
    public float mouseSensitivity = 1.25f;
    public float recoilKick = 5f;
    public float recoilRecoverySpeed = 6f;
    private float currentRecoil;

    /*
    [Header("Recarga (dip visual)")]
    public float reloadDipAmount = 0.08f;
    private float reloadDipCurrent = 0f;
    private Coroutine reloadDipRoutine;*/

    void Start()
    {
        startRotation = transform.localRotation;
        startPosition = transform.localPosition;
        FindPmovement();
    }

    void Update()
    {
        HandleRecoilRecovery();

        if (IsOwner)
        {
            Sway(); // sway con el mouse local + recoil, solo tiene sentido para el dueño
            ApplyBob();
        }
        else
        {
            ApplyRecoilOnly(); // los demás solo ven el "kick" del retroceso, sin sway de mouse
        }
    }

    private void FindPmovement()
    {

        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PMovement>();
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

    /*
    public void PlayReloadDip(float duration)
    {
        if (reloadDipRoutine != null) StopCoroutine(reloadDipRoutine);
        reloadDipRoutine = StartCoroutine(ReloadDipRoutine(duration));
    }

    private System.Collections.IEnumerator ReloadDipRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            // Va de 0 a 1 y vuelve a 0 a lo largo de la recarga: baja y sube.
            reloadDipCurrent = Mathf.Sin(normalized * Mathf.PI);
            yield return null;
        }
        reloadDipCurrent = 0f;
    }*/

    private void ApplyBob()
    {
        if (playerMovement == null) return;

        bool isMoving = playerMovement.MoveInput.magnitude > 0.1f && playerMovement.IsGrounded;

        if (isMoving)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            bobTimer %= Mathf.PI * 2f;
        }

        float targetAmplitude = isMoving ? 1f : 0f;
        bobAmountCurrent = Mathf.Lerp(bobAmountCurrent, targetAmplitude, Time.deltaTime * 6f);

        float verticalBob = Mathf.Sin(bobTimer) * bobAmount * bobAmountCurrent;
        float horizontalBob = Mathf.Cos(bobTimer * 0.5f) * bobAmount * 0.5f * bobAmountCurrent;
        Vector3 bobOffset = new Vector3(horizontalBob, verticalBob, 0f);

        // NUEVO: respiración, siempre activa, se suma por encima del bob
        breathingTimer += Time.deltaTime * breathingFrequency;
        float breathingX = Mathf.Sin(breathingTimer) * breathingAmount;
        float breathingY = Mathf.Sin(breathingTimer * 1.7f) * breathingAmount * 0.6f;
        Vector3 breathingOffset = new Vector3(breathingX, breathingY, 0f);

        // Vector3 reloadOffset = Vector3.down * reloadDipAmount * reloadDipCurrent; // NUEVO
       // Vector3 targetLocalPosition = startPosition + bobOffset + breathingOffset + reloadOffset;
        Vector3 targetLocalPosition = startPosition + bobOffset + breathingOffset;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetLocalPosition,
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
