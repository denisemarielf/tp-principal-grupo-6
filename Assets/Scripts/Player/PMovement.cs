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

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private float verticalVelocity;

    private NetworkVariable<float> speedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public Vector2 MoveInput => moveInput;
    public bool IsGrounded => controller != null && controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        UpdateAnimator();
        ApplyGravity();
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
        if (IsGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    public void ResetMovement()
    {
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        moveInput = Vector2.zero;
        if (animator != null)
        {
            animator.SetFloat("speed", 0f);
        }
    }

    private void HandleMovement()
    {
        if (controller == null) return;

        Vector3 wishDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        float wishSpeed = moveSpeed * speedMultiplier.Value;

        
        if (isSprinting)
        {
            wishSpeed *= sprintMultiplier;
        }
        float currentSpeed = Vector3.Dot(velocity, wishDir);
        float addSpeed = wishSpeed - currentSpeed;
        if (addSpeed > 0)
        {
            float accel = IsGrounded ? acceleration : airAcceleration;
            float accelSpeed = accel * Time.deltaTime * wishSpeed;
            if (accelSpeed > addSpeed) accelSpeed = addSpeed;
            velocity += wishDir * accelSpeed;
        }

     
        if (IsGrounded)
        {
            float speed = velocity.magnitude;
            if (speed != 0)
            {
                float drop = speed * friction * Time.deltaTime;
                velocity *= Mathf.Max(speed - drop, 0) / speed;
            }
        }

      
        Vector3 move = velocity * Time.deltaTime;
        move.y = verticalVelocity * Time.deltaTime;
        controller.Move(move);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat("speed", moveInput.magnitude * (isSprinting ? sprintMultiplier : 1f));
    }

    private void ApplyGravity()
    {
        if (controller == null) return;
        if (IsGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        if (!IsServer) return;
        StartCoroutine(SpeedBoostRoutine(multiplier, duration));
    }

    private IEnumerator SpeedBoostRoutine(float multiplier, float duration)
    {
        speedMultiplier.Value = multiplier;
        yield return new WaitForSeconds(duration);
        speedMultiplier.Value = 1f;
    }
}
