using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 3.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private Animator animator;
    public Vector2 MoveInput => moveInput;
    public bool IsGrounded => controller != null && controller.isGrounded;

    private CharacterController controller;
    private Vector2 moveInput;
    private float verticalVelocity;

    private NetworkVariable<float> speedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        
    }

    private void Update()
    {
        if (!IsOwner)
            return;


        Move();
        UpdateAnimator();
        ApplyGravity();
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    public void TryJump()
    {
        if (controller != null && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    public void ResetMovement()
    {
        verticalVelocity = 0f;
        moveInput = Vector2.zero;
        if (animator != null)
        {
            animator.SetFloat("speed", 0f);
        }
    }

    private void Move()
    {
        
        if (controller == null) return;
        Vector3 movement =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;
        controller.Move(movement * moveSpeed * speedMultiplier.Value * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat("speed", moveInput.magnitude);
    }

    private void ApplyGravity()
    {
        if (controller == null) return;
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(
            Vector3.up * verticalVelocity * Time.deltaTime
        );
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
