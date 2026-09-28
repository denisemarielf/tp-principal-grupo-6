using UnityEngine;

public class CameraBob : MonoBehaviour
{
    [SerializeField] private float walkBobSpeed = 4f;
    [SerializeField] private float walkBobAmount = 0.03f;
    [SerializeField] private float sprintBobSpeed = 6f;
    [SerializeField] private float sprintBobAmount = 0.1f;

    private float timer = 0f;
    private Vector3 startPos;

    private PMovement movement;

    private void Awake()
    {
        movement = GetComponentInParent<PMovement>();
    }
    private void Start()
    {
        startPos = transform.localPosition;
    }

    private void Update()
    {
        if (movement == null) return;

        Vector2 input = movement.MoveInput;
        bool isMoving = input.magnitude > 0.1f;

        if (isMoving && movement.IsGrounded)
        {
            float bobSpeed = movement.isSprinting ? sprintBobSpeed : walkBobSpeed;
            float bobAmount = movement.isSprinting ? sprintBobAmount : walkBobAmount;

            timer += Time.deltaTime * bobSpeed;
            float offsetY = Mathf.Sin(timer) * bobAmount;
            float offsetX = Mathf.Cos(timer / 2f) * bobAmount;

            transform.localPosition = startPos + new Vector3(offsetX, offsetY, 0f);
        }
        else
        {
            // Volver suavemente a la posición inicial
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPos, Time.deltaTime * 5f);
            timer = 0f;
        }
    }
}