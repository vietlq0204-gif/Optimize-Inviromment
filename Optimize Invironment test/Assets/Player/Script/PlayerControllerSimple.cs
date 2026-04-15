using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple player controller for moving on Terrain.
/// </summary>
/// <remarks>
/// - Uses CharacterController for stable movement.
/// - Projects movement onto ground normal so the player follows slopes.
/// - Uses raycast to detect Terrain / ground normal.
/// - Reads movement from the active Unity input backend.
/// </remarks>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerControllerSimple : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float groundCheckOffset = 0.2f;

    private CharacterController characterController;
    private Vector3 groundNormal = Vector3.up;
    private float verticalVelocity;
    private bool isGrounded;

    /// <summary>
    /// Cache required components.
    /// </summary>
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (!TryGetComponent(out GrassInteractionSource _))
        {
            gameObject.AddComponent<GrassInteractionSource>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// Main update loop.
    /// </summary>
    private void Update()
    {
        UpdateGroundInfo();
        HandleMovement();
    }

    /// <summary>
    /// Updates grounded state and ground normal using raycast.
    /// </summary>
    /// <remarks>
    /// Casts from slightly above the player down to detect terrain/slope.
    /// </remarks>
    private void UpdateGroundInfo()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundCheckOffset;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    /// <summary>
    /// Handles input, ground projection, gravity, and rotation.
    /// </summary>
    private void HandleMovement()
    {
        Vector3 inputDirection = GetInputDirection();
        Vector3 moveDirection = GetMoveDirectionOnGround(inputDirection);

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);

        RotateTowards(moveDirection);
    }

    /// <summary>
    /// Reads player input and converts it to world-space direction.
    /// </summary>
    /// <returns>Normalized world-space input direction.</returns>
    private Vector3 GetInputDirection()
    {
        Vector2 moveInput = ReadMoveInput();
        float horizontal = moveInput.x;
        float vertical = moveInput.y;

        Vector3 input = new Vector3(horizontal, 0f, vertical).normalized;

        if (input.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null)
        {
            return input;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 worldDirection = cameraForward * input.z + cameraRight * input.x;
        return worldDirection.normalized;
    }

    /// <summary>
    /// Reads movement input from Input System or Legacy Input Manager.
    /// </summary>
    /// <returns>Normalized 2D move input.</returns>
    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 move = Vector2.zero;

        if (Gamepad.current != null)
        {
            move = Gamepad.current.leftStick.ReadValue();
        }

        if (move.sqrMagnitude <= 0.0001f && Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                move.x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                move.x += 1f;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                move.y -= 1f;
            }

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                move.y += 1f;
            }
        }

        return Vector2.ClampMagnitude(move, 1f);
#else
        return Vector2.zero;
#endif
    }

    /// <summary>
    /// Projects movement direction onto the ground plane.
    /// </summary>
    /// <param name="inputDirection">Raw world-space movement direction.</param>
    /// <returns>Direction adjusted to terrain slope.</returns>
    private Vector3 GetMoveDirectionOnGround(Vector3 inputDirection)
    {
        if (inputDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 projectedDirection = Vector3.ProjectOnPlane(inputDirection, groundNormal);
        return projectedDirection.normalized;
    }

    /// <summary>
    /// Rotates the player toward movement direction.
    /// </summary>
    /// <param name="moveDirection">Current move direction.</param>
    private void RotateTowards(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Draws the ground check ray in Scene view.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 rayOrigin = transform.position + Vector3.up * groundCheckOffset;
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * groundCheckDistance);
    }
}
