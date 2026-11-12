using Unity.Netcode.Components;
using UnityEngine;

// Efectos de estado que el jefe aplica al jugador. Solo se usa en el cliente dueño (el movimiento es del dueño).
// Se agrega solo al jugador la primera vez que hace falta (PlayerStatusEffects.For).
public class PlayerStatusEffects : MonoBehaviour
{
    private PMovement movement;
    private CharacterController controller;
    private NetworkTransform networkTransform;
    private PlayerHealth health;

    private bool slowed;
    private float slowUntil;

    private Transform holdAnchor;
    private float holdUntil;

    public bool IsHeld => holdAnchor != null;

    public static PlayerStatusEffects For(PMovement movement)
    {
        PlayerStatusEffects fx = movement.GetComponent<PlayerStatusEffects>();
        if (fx == null)
            fx = movement.gameObject.AddComponent<PlayerStatusEffects>();
        return fx;
    }

    private void Awake()
    {
        movement = GetComponent<PMovement>();
        controller = GetComponent<CharacterController>();
        networkTransform = GetComponent<NetworkTransform>();
        health = GetComponent<PlayerHealth>();
    }

    // ---------- Lentitud ----------

    // Un efecto nuevo reemplaza al anterior (no se acumula).
    public void ApplySlow(float multiplier, float duration)
    {
        slowed = true;
        slowUntil = Time.time + duration;
        movement.SetExternalSlow(multiplier);
    }

    private void Update()
    {
        if (slowed && Time.time >= slowUntil)
        {
            slowed = false;
            movement.SetExternalSlow(1f);
        }
    }

    // ---------- Agarre ----------

    // Inmoviliza al jugador y lo mantiene pegado al ancla. 'duration' es un seguro por si nunca llega el Release.
    public void Hold(Transform anchor, float duration)
    {
        if (anchor == null)
            return;

        holdAnchor = anchor;
        holdUntil = Time.time + Mathf.Max(0.1f, duration);
        movement.SetHeld(true);
        MoveTo(anchor.position, false);
    }

    public void Release(bool teleport, Vector3 dropPoint)
    {
        if (holdAnchor == null)
            return;

        holdAnchor = null;
        movement.SetHeld(false);

        if (teleport)
            MoveTo(dropPoint, true);
    }

    // LateUpdate: despues de que el jefe (NetworkTransform) ya se movio en este frame.
    private void LateUpdate()
    {
        if (holdAnchor == null)
            return;

        if (Time.time >= holdUntil || (health != null && health.IsDead))
        {
            Release(false, Vector3.zero);
            return;
        }

        MoveTo(holdAnchor.position, false);
    }

    private void OnDisable()
    {
        if (movement == null)
            return;

        holdAnchor = null;
        slowed = false;
        movement.SetHeld(false);
        movement.SetExternalSlow(1f);
    }

    // El CharacterController pisa los cambios de transform.position, hay que apagarlo un instante.
    private void MoveTo(Vector3 position, bool syncNetwork)
    {
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled)
            controller.enabled = false;

        transform.position = position;

        if (wasEnabled)
            controller.enabled = true;

        // Evita que los demas clientes interpolen el salto de posicion.
        if (syncNetwork && networkTransform != null && networkTransform.CanCommitToTransform)
            networkTransform.Teleport(position, transform.rotation, transform.localScale);
    }
}