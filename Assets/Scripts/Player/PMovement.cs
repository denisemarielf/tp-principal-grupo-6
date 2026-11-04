
using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float airAcceleration = 2f;
    [SerializeField] private float friction = 6f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 3.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Sprint")]
    [SerializeField] private float sprintMultiplier = 1.5f;
    public bool isSprinting;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    private float launchLockUntil;
    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private float verticalVelocity;

    public Vector2 MoveInput => moveInput;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public bool IsSprinting => isSprinting;

    private float externalSlow = 1f;
    private bool held;
    public bool IsHeld => held;

    private NetworkVariable<float> speedMultiplier = new(
    1f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> slowMultiplier = new(
    1f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    // Por debajo de esto el golpe dejaria al jugador casi quieto.
    public const float MinHitSlow = 0.4f;

    private Coroutine hitSlowRoutine;
    private int hitSlowToken;

    private readonly NetworkVariable<bool> frozen = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsFrozen => frozen.Value;

    public void SetFrozen(bool value)
    {
        if (IsServer) frozen.Value = value;
    }
    private void Awake()
    {
        controller = GetComponent<CharacterController>();

    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (held)
        {
            UpdateAnimator();
            return;
        }
        HandleMovement();
        ApplyGravity();
        UpdateAnimator();
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    public void SetSprint(bool sprinting)
    {
      
        isSprinting = sprinting;
    }

 

  


    public void TryJump()
    {
        if (frozen.Value || held)
            return;
        if (IsGrounded)
        {
            verticalVelocity =
            Mathf.Sqrt(jumpHeight * -2f * gravity);
            TriggerJump();
        }
    }

    private void TriggerJump()
    {
        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "Jump" && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(parameter.name);
                return;
            }
        }
    }

    public void ResetMovement()
    {
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        moveInput = Vector2.zero;

        if (animator != null)
        {
            animator.SetFloat("velX", 0f);
            animator.SetFloat("velZ", 0f);
        }
    }

    private void HandleMovement()
    {
        if (controller == null)
            return;

        if (frozen.Value)
        {
            velocity = Vector3.zero;
            controller.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
            return;
        }
        Vector2 input = Time.time < launchLockUntil ? Vector2.zero : moveInput;
        Vector3 wishDir =
            (transform.right * input.x +
             transform.forward * input.y).normalized;


        float wishSpeed =
        moveSpeed * CombineSpeed(speedMultiplier.Value, slowMultiplier.Value) * externalSlow;


        if (isSprinting)
        {
            wishSpeed *= sprintMultiplier;
        }

        float currentSpeed =
        Vector3.Dot(velocity, wishDir);

        float addSpeed =
        wishSpeed - currentSpeed;

        if (addSpeed > 0)
        {
            float accel =
            IsGrounded
            ? acceleration
            : airAcceleration;

            float accelSpeed =
            accel * Time.deltaTime * wishSpeed;

            if (accelSpeed > addSpeed)
            {
                accelSpeed = addSpeed;
            }

            velocity += wishDir * accelSpeed;
        }

        if (IsGrounded)
        {
            float speed = velocity.magnitude;

            if (speed > 0f)
            {
                float drop =
                speed * friction * Time.deltaTime;

                velocity *=
                Mathf.Max(speed - drop, 0f) / speed;
            }
        }

        Vector3 move =
        velocity * Time.deltaTime;

        move.y =
        verticalVelocity * Time.deltaTime;

        controller.Move(move);
    }

    private void ApplyGravity()
    {
        if (controller == null)
            return;

        if (controller.isGrounded &&
        verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity +=
        gravity * Time.deltaTime;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;
        Vector2 input = (frozen.Value || held) ? Vector2.zero : moveInput;
        animator.SetFloat("velX", input.x, 0.1f, Time.deltaTime);
        animator.SetFloat("velZ", input.y, 0.1f, Time.deltaTime);
        bool isActuallySprinting = isSprinting && input.magnitude > 0.1f;
        animator.SetBool("isSprinting", isActuallySprinting);
    }

    public void ApplySpeedBoost(
    float multiplier,
    float duration)
    {
        if (!IsServer)
            return;

        StartCoroutine(
        SpeedBoostRoutine(
        multiplier,
        duration
        )
        );
    }

    private IEnumerator SpeedBoostRoutine(
    float multiplier,
    float duration)
    {
        speedMultiplier.Value = multiplier;

        yield return new WaitForSeconds(duration);

        speedMultiplier.Value = 1f;
    }

    // El host aplica el frenado del golpe. Un golpe nuevo renueva la duracion
    // y la corrutina anterior no puede devolver la velocidad a 1 antes de tiempo.
    public void ApplyHitSlow(float multiplier, float duration)
    {
        if (!IsServer)
            return;

        if (hitSlowRoutine != null)
            StopCoroutine(hitSlowRoutine);

        int token = ++hitSlowToken;
        hitSlowRoutine = StartCoroutine(HitSlowRoutine(ClampHitSlow(multiplier), duration, token));
    }

    public static float ClampHitSlow(float multiplier)
    {
        return Mathf.Clamp(multiplier, MinHitSlow, 1f);
    }

    public static float CombineSpeed(float boost, float slow)
    {
        return boost * slow;
    }
    public void SetExternalSlow(float multiplier)
    {
        externalSlow = Mathf.Clamp(multiplier, 0.05f, 1f);
    }
    public void SetHeld(bool value)
    {
        held = value;
        if (value)
        {
            velocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
    private IEnumerator HitSlowRoutine(float multiplier, float duration, int token)
    {
        slowMultiplier.Value = multiplier;

        yield return new WaitForSeconds(duration);

        if (token != hitSlowToken)
            yield break;

        slowMultiplier.Value = 1f;
        hitSlowRoutine = null;
    }
    public void Launch(Vector3 launchVelocity, float controlLockSeconds = 0.6f)
    {
        velocity = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
        verticalVelocity = launchVelocity.y;
        launchLockUntil = Time.time + controlLockSeconds;
    }
}