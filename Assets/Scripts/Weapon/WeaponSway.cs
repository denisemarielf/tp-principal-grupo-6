
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSway : NetworkBehaviour
{
    private Quaternion startRotation;
    private Vector3 startPosition;

    [Header("Bob (balanceo al caminar)")]
    private PMovement playerMovement;

    public float bobFrequency = 8f;
    public float bobAmount = 0.02f;

    private float bobTimer = 0f;
    private float bobAmountCurrent = 0f;

    [Header("Respiración en reposo")]
    public float breathingFrequency = 1.5f;
    public float breathingAmount = 0.004f;

    private float breathingTimer = 0f;

    [Header("Sway")]
    public float swayAmount = 8f;
    public float mouseSensitivity = 1.25f;

    [Header("Recoil")]
    public float recoilKick = 5f;
    public float recoilRecoverySpeed = 6f;

    private float currentRecoil;

    [Header("Recarga (dip visual)")]
    public float reloadDipAmount = 0.08f;
    public float reloadTiltAmount = 35f;

    private float reloadDipCurrent = 0f;
    private Coroutine reloadDipRoutine;


    // =========================================================
    // MOVIMIENTO SINCRONIZADO
    // =========================================================

    private NetworkVariable<bool> networkIsMoving =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        startRotation = transform.localRotation;
        startPosition = transform.localPosition;

        FindPMovement();
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        HandleRecoilRecovery();

        if (IsOwner)
        {
            // Sway del mouse solamente para el jugador local.
            Sway();

            // Actualizamos el estado de movimiento.
            UpdateMovementState();

            // Bob + breathing + reload.
            ApplyVisualMovement(
                networkIsMoving.Value
            );
        }
        else
        {
            // Los jugadores remotos no reciben
            // el mouse del jugador dueño.

            ApplyRemoteVisuals();
        }
    }


    // =========================================================
    // BUSCAR PMOVEMENT
    // =========================================================

    private void FindPMovement()
    {
        if (playerMovement == null)
        {
            playerMovement =
                GetComponentInParent<PMovement>();
        }
    }


    // =========================================================
    // MOVIMIENTO NETWORK
    // =========================================================

    private void UpdateMovementState()
    {
        if (playerMovement == null)
            return;

        bool isMoving =
            playerMovement.MoveInput.magnitude > 0.1f &&
            playerMovement.IsGrounded;

        networkIsMoving.Value = isMoving;
    }


    // =========================================================
    // SWAY
    // =========================================================

    private void Sway()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        float mouseX =
            mouseDelta.x *
            mouseSensitivity *
            Time.deltaTime;

        float mouseY =
            mouseDelta.y *
            mouseSensitivity *
            Time.deltaTime;

        Quaternion xAngle =
            Quaternion.AngleAxis(
                mouseX * -1f,
                Vector3.up
            );

        Quaternion yAngle =
            Quaternion.AngleAxis(
                mouseY * -1f,
                Vector3.right
            );

        Quaternion recoilRotation =
            Quaternion.AngleAxis(
                -currentRecoil,
                Vector3.right
            );

        Quaternion reloadTiltRotation =
            Quaternion.AngleAxis(
                reloadTiltAmount *
                reloadDipCurrent,
                Vector3.right
            );

        Quaternion targetRotation =
            startRotation *
            xAngle *
            yAngle *
            recoilRotation *
            reloadTiltRotation;

        transform.localRotation =
            Quaternion.Lerp(
                transform.localRotation,
                targetRotation,
                Time.deltaTime * swayAmount
            );
    }


    // =========================================================
    // BOB + BREATHING + RELOAD
    // =========================================================

    private void ApplyVisualMovement(bool isMoving)
    {
        // =====================================================
        // BOB
        // =====================================================

        if (isMoving)
        {
            bobTimer +=
                Time.deltaTime *
                bobFrequency;

            bobTimer %= Mathf.PI * 2f;
        }

        float targetAmplitude =
            isMoving ? 1f : 0f;

        bobAmountCurrent =
            Mathf.Lerp(
                bobAmountCurrent,
                targetAmplitude,
                Time.deltaTime * 6f
            );

        float verticalBob =
            Mathf.Sin(bobTimer) *
            bobAmount *
            bobAmountCurrent;

        float horizontalBob =
            Mathf.Cos(bobTimer * 0.5f) *
            bobAmount *
            0.5f *
            bobAmountCurrent;

        Vector3 bobOffset =
            new Vector3(
                horizontalBob,
                verticalBob,
                0f
            );


        // =====================================================
        // BREATHING
        // =====================================================

        breathingTimer +=
            Time.deltaTime *
            breathingFrequency;

        float breathingX =
            Mathf.Sin(breathingTimer) *
            breathingAmount;

        float breathingY =
            Mathf.Sin(
                breathingTimer * 1.7f
            ) *
            breathingAmount *
            0.6f;

        Vector3 breathingOffset =
            new Vector3(
                breathingX,
                breathingY,
                0f
            );


        // =====================================================
        // RELOAD DIP
        // =====================================================

        Vector3 reloadOffset =
            Vector3.down *
            reloadDipAmount *
            reloadDipCurrent;


        // =====================================================
        // POSICIÓN FINAL
        // =====================================================

        Vector3 targetLocalPosition =
            startPosition +
            bobOffset +
            breathingOffset +
            reloadOffset;

        transform.localPosition =
            Vector3.Lerp(
                transform.localPosition,
                targetLocalPosition,
                Time.deltaTime * swayAmount
            );
    }


    // =========================================================
    // VISUALES REMOTOS
    // =========================================================

    private void ApplyRemoteVisuals()
    {
        // Bob + breathing + reload.
        ApplyVisualMovement(
            networkIsMoving.Value
        );


        // Recoil + reload tilt.
        ApplyRemoteRotation();
    }


    private void ApplyRemoteRotation()
    {
        Quaternion recoilRotation =
            Quaternion.AngleAxis(
                -currentRecoil,
                Vector3.right
            );

        Quaternion reloadTiltRotation =
            Quaternion.AngleAxis(
                reloadTiltAmount *
                reloadDipCurrent,
                Vector3.right
            );

        Quaternion targetRotation =
            startRotation *
            recoilRotation *
            reloadTiltRotation;

        transform.localRotation =
            Quaternion.Lerp(
                transform.localRotation,
                targetRotation,
                Time.deltaTime * swayAmount
            );
    }


    // =========================================================
    // RELOAD
    // =========================================================

    public void PlayReloadDip(float duration)
    {
        // El dueño reproduce inmediatamente.
        StartReloadDip(duration);

        // Sincronizar con los demás clientes.
        if (IsOwner)
        {
            PlayReloadDipServerRpc(duration);
        }
    }


    private void StartReloadDip(float duration)
    {
        if (reloadDipRoutine != null)
        {
            StopCoroutine(reloadDipRoutine);
        }

        reloadDipRoutine =
            StartCoroutine(
                ReloadDipRoutine(duration)
            );
    }


    private IEnumerator ReloadDipRoutine(float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float normalized =
                Mathf.Clamp01(
                    t / duration
                );

            // 0 → 1 → 0
            reloadDipCurrent =
                Mathf.Sin(
                    normalized * Mathf.PI
                );

            yield return null;
        }

        reloadDipCurrent = 0f;
    }


    [ServerRpc]
    private void PlayReloadDipServerRpc(
        float duration)
    {
        PlayReloadDipClientRpc(duration);
    }


    [ClientRpc]
    private void PlayReloadDipClientRpc(
        float duration)
    {
        // El dueño ya lo ejecutó localmente.
        if (IsOwner)
            return;

        // Los demás reproducen el reload.
        StartReloadDip(duration);
    }


    // =========================================================
    // RECOIL
    // =========================================================

    public void AddRecoil()
    {
        // Solo el dueño puede iniciar el recoil.
        if (!IsOwner)
            return;

        // Recoil inmediato.
        currentRecoil += recoilKick;

        // Sincronizar.
        AddRecoilServerRpc();
    }


    [ServerRpc]
    private void AddRecoilServerRpc()
    {
        AddRecoilClientRpc();
    }


    [ClientRpc]
    private void AddRecoilClientRpc()
    {
        // El dueño ya lo aplicó.
        if (IsOwner)
            return;

        // Los demás lo reproducen.
        currentRecoil += recoilKick;
    }


    // =========================================================
    // RECUPERACIÓN DEL RECOIL
    // =========================================================

    private void HandleRecoilRecovery()
    {
        currentRecoil =
            Mathf.Lerp(
                currentRecoil,
                0f,
                Time.deltaTime *
                recoilRecoverySpeed
            );
    }
}


