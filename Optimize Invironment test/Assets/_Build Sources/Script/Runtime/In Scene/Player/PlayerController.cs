using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

#if ENABLE_INPUT_SYSTEM
    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string crouchActionName = "Crouch";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string previousActionName = "Previous";
    [SerializeField] private string nextActionName = "Next";
    [SerializeField] private string sprintActionName = "Sprint";
#endif

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private bool rotateTowardsMovement = true;
    [SerializeField] private bool keepWorldUpAligned = true;
    [SerializeField] private float groundedVerticalVelocity = -2f;

    [Header("Jump")]
    [SerializeField] private float jumpSpeed = 6f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.1f;

    [Header("Gravity")]
    [SerializeField] private bool overrideRigidbodyGravity;
    [SerializeField] private Vector3 gravityOverride = new Vector3(0f, -9.81f, 0f);

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float groundCheckOffset = 0.2f;
    [SerializeField] [Range(0.1f, 1f)] private float groundProbeRadiusScale = 0.9f;
    [SerializeField] [Range(0f, 89f)] private float maxGroundAngle = 65f;

    [Header("Physics")]
    [SerializeField] private CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

    private Rigidbody rigidbodyComponent;
    private CapsuleCollider capsuleCollider;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private float lastGroundedTime = float.NegativeInfinity;
    private float queuedJumpTime = float.NegativeInfinity;
    private bool jumpQueued;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool attackPressed;
    private bool interactPressed;
    private bool crouchPressed;
    private bool jumpPressed;
    private bool previousPressed;
    private bool nextPressed;
    private bool sprintPressed;
    private int attackTriggeredFrame = -1;
    private int interactTriggeredFrame = -1;
    private int crouchTriggeredFrame = -1;
    private int jumpTriggeredFrame = -1;
    private int previousTriggeredFrame = -1;
    private int nextTriggeredFrame = -1;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction attackAction;
    private InputAction interactAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private InputAction previousAction;
    private InputAction nextAction;
    private InputAction sprintAction;
#endif

    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public float MoveSpeed => moveSpeed;
    public float SprintSpeed => sprintSpeed;
    public float CrouchSpeed => crouchSpeed;
    public float CurrentMoveSpeed => GetCurrentMoveSpeed();
    public bool IsSprinting => sprintPressed;
    public bool IsCrouching => crouchPressed;
    public bool IsAttacking => attackPressed;
    public bool IsInteracting => interactPressed;
    public bool IsJumpPressed => jumpPressed;
    public bool IsPreviousPressed => previousPressed;
    public bool IsNextPressed => nextPressed;
    public bool AttackTriggeredThisFrame => attackTriggeredFrame == Time.frameCount;
    public bool InteractTriggeredThisFrame => interactTriggeredFrame == Time.frameCount;
    public bool CrouchTriggeredThisFrame => crouchTriggeredFrame == Time.frameCount;
    public bool JumpTriggeredThisFrame => jumpTriggeredFrame == Time.frameCount;
    public bool PreviousTriggeredThisFrame => previousTriggeredFrame == Time.frameCount;
    public bool NextTriggeredThisFrame => nextTriggeredFrame == Time.frameCount;

    public float CurrentPlanarSpeed
    {
        get
        {
            if (rigidbodyComponent == null)
            {
                return 0f;
            }

            Vector3 planarVelocity = rigidbodyComponent.linearVelocity;
            planarVelocity.y = 0f;
            return planarVelocity.magnitude;
        }
    }

    private void Awake()
    {
        EnsurePhysicsSetup();
        ApplyGravitySettings();
        ApplyRotationConstraints();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
        CacheInputActions();
#endif
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        CacheInputActions();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        sprintSpeed = Mathf.Max(moveSpeed, sprintSpeed);
        crouchSpeed = Mathf.Clamp(crouchSpeed, 0f, sprintSpeed);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        groundedVerticalVelocity = Mathf.Min(0f, groundedVerticalVelocity);
        jumpSpeed = Mathf.Max(0f, jumpSpeed);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        groundCheckDistance = Mathf.Max(0.05f, groundCheckDistance);
        groundCheckOffset = Mathf.Max(0f, groundCheckOffset);
        groundProbeRadiusScale = Mathf.Clamp(groundProbeRadiusScale, 0.1f, 1f);
        maxGroundAngle = Mathf.Clamp(maxGroundAngle, 0f, 89f);

        EnsurePhysicsSetup();
        ApplyGravitySettings();
        ApplyRotationConstraints();
    }
#endif

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        RefreshInputState();
#endif
    }

    private void FixedUpdate()
    {
        UpdateGroundInfo();
        HandleJump();
        HandleMovement();
        ApplyGravity();
        UpdateRotation();
    }

    private void HandleMovement()
    {
        Vector3 desiredMoveDirection = GetDesiredMoveDirection();
        Vector3 velocity = desiredMoveDirection * GetCurrentMoveSpeed();
        velocity.y = GetTargetVerticalVelocity();
        rigidbodyComponent.linearVelocity = velocity;
    }

    private Vector3 GetDesiredMoveDirection()
    {
        Vector3 inputDirection = GetInputDirection();
        if (inputDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        if (keepWorldUpAligned || !isGrounded)
        {
            return inputDirection.normalized;
        }

        Vector3 projectedDirection = Vector3.ProjectOnPlane(inputDirection, groundNormal);
        if (projectedDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return projectedDirection.normalized;
    }

    private Vector3 GetInputDirection()
    {
        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
        if (clampedInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 input = new Vector3(clampedInput.x, 0f, clampedInput.y);

        if (cameraTransform == null)
        {
            return input.normalized;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude <= 0.0001f || cameraRight.sqrMagnitude <= 0.0001f)
        {
            return input.normalized;
        }

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 worldDirection = cameraForward * input.z + cameraRight * input.x;
        return worldDirection.sqrMagnitude > 0.0001f
            ? worldDirection.normalized
            : Vector3.zero;
    }

    private void HandleJump()
    {
        if (jumpSpeed <= 0f)
        {
            return;
        }

        if (!jumpQueued)
        {
            return;
        }

        if (Time.time - queuedJumpTime > jumpBufferTime)
        {
            jumpQueued = false;
            return;
        }

        if (!CanJump())
        {
            return;
        }

        Vector3 velocity = rigidbodyComponent.linearVelocity;
        velocity.y = jumpSpeed;
        rigidbodyComponent.linearVelocity = velocity;

        queuedJumpTime = float.NegativeInfinity;
        jumpQueued = false;
        isGrounded = false;
        lastGroundedTime = float.NegativeInfinity;
    }

    private float GetTargetVerticalVelocity()
    {
        float currentVerticalVelocity = rigidbodyComponent.linearVelocity.y;

        if (!isGrounded)
        {
            return currentVerticalVelocity;
        }

        if (currentVerticalVelocity > 0f)
        {
            return currentVerticalVelocity;
        }

        return groundedVerticalVelocity;
    }

    private bool CanJump()
    {
        return isGrounded || Time.time - lastGroundedTime <= coyoteTime;
    }

    private void ApplyGravity()
    {
        if (!overrideRigidbodyGravity)
        {
            return;
        }

        rigidbodyComponent.AddForce(gravityOverride, ForceMode.Acceleration);
    }

    private void UpdateRotation()
    {
        if (!rotateTowardsMovement)
        {
            return;
        }

        Vector3 desiredMoveDirection = GetDesiredMoveDirection();
        if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 targetUp = keepWorldUpAligned ? Vector3.up : groundNormal;
        Vector3 targetForward = Vector3.ProjectOnPlane(desiredMoveDirection, targetUp);
        if (targetForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetForward.normalized, targetUp);
        Quaternion nextRotation = Quaternion.Slerp(
            rigidbodyComponent.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        rigidbodyComponent.MoveRotation(nextRotation);
    }

    private void UpdateGroundInfo()
    {
        if (capsuleCollider == null)
        {
            groundNormal = Vector3.up;
            isGrounded = false;
            return;
        }

        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float radius = GetGroundProbeRadius();
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, radius);
        float castStartOffset = Mathf.Max(0f, halfHeight - radius) + groundCheckOffset;
        Vector3 castOrigin = worldCenter + up * castStartOffset;
        float castDistance = groundCheckDistance + castStartOffset;

        if (Physics.SphereCast(castOrigin, radius, -up, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            float groundAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (groundAngle <= maxGroundAngle)
            {
                groundNormal = hit.normal;
                isGrounded = true;
                lastGroundedTime = Time.time;
                return;
            }
        }

        groundNormal = Vector3.up;
        isGrounded = false;
    }

    private float GetGroundProbeRadius()
    {
        if (capsuleCollider == null)
        {
            return 0.05f;
        }

        float lossyScaleX = Mathf.Abs(transform.lossyScale.x);
        float lossyScaleZ = Mathf.Abs(transform.lossyScale.z);
        float maxHorizontalScale = Mathf.Max(lossyScaleX, lossyScaleZ, 0.0001f);
        return Mathf.Max(0.05f, capsuleCollider.radius * maxHorizontalScale * groundProbeRadiusScale);
    }

    private float GetCurrentMoveSpeed()
    {
        if (crouchPressed)
        {
            return crouchSpeed;
        }

        if (sprintPressed)
        {
            return sprintSpeed;
        }

        return moveSpeed;
    }

    private void EnsurePhysicsSetup()
    {
        CharacterController legacyController = GetComponent<CharacterController>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (capsuleCollider == null)
        {
            capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        }

        if (legacyController != null)
        {
            capsuleCollider.center = legacyController.center;
            capsuleCollider.radius = legacyController.radius;
            capsuleCollider.height = legacyController.height;
            capsuleCollider.direction = 1;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(legacyController);
            }
            else
#endif
            {
                Destroy(legacyController);
            }
        }

        rigidbodyComponent = GetComponent<Rigidbody>();
        if (rigidbodyComponent == null)
        {
            rigidbodyComponent = gameObject.AddComponent<Rigidbody>();
        }

        rigidbodyComponent.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbodyComponent.collisionDetectionMode = collisionDetectionMode;
    }

    private void ApplyRotationConstraints()
    {
        if (rigidbodyComponent == null)
        {
            return;
        }

        RigidbodyConstraints constraints = rigidbodyComponent.constraints;
        constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);

        if (keepWorldUpAligned)
        {
            constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        rigidbodyComponent.constraints = constraints;
    }

    private void ApplyGravitySettings()
    {
        if (rigidbodyComponent == null)
        {
            return;
        }

        rigidbodyComponent.useGravity = !overrideRigidbodyGravity;
    }

#if ENABLE_INPUT_SYSTEM
    private void CacheInputActions()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput == null || playerInput.actions == null)
        {
            moveAction = null;
            lookAction = null;
            attackAction = null;
            interactAction = null;
            crouchAction = null;
            jumpAction = null;
            previousAction = null;
            nextAction = null;
            sprintAction = null;
            return;
        }

        InputActionMap actionMap = playerInput.currentActionMap;
        if (actionMap == null && !string.IsNullOrWhiteSpace(actionMapName))
        {
            actionMap = playerInput.actions.FindActionMap(actionMapName, false);
        }

        moveAction = FindAction(actionMap, moveActionName);
        lookAction = FindAction(actionMap, lookActionName);
        attackAction = FindAction(actionMap, attackActionName);
        interactAction = FindAction(actionMap, interactActionName);
        crouchAction = FindAction(actionMap, crouchActionName);
        jumpAction = FindAction(actionMap, jumpActionName);
        previousAction = FindAction(actionMap, previousActionName);
        nextAction = FindAction(actionMap, nextActionName);
        sprintAction = FindAction(actionMap, sprintActionName);
    }

    private void RefreshInputState()
    {
        if (moveAction == null && playerInput != null)
        {
            CacheInputActions();
        }

        moveInput = ReadVector2(moveAction);
        lookInput = ReadVector2(lookAction);

        attackPressed = IsActionPressed(attackAction);
        interactPressed = IsActionPressed(interactAction);
        crouchPressed = IsActionPressed(crouchAction);
        jumpPressed = IsActionPressed(jumpAction);
        previousPressed = IsActionPressed(previousAction);
        nextPressed = IsActionPressed(nextAction);
        sprintPressed = IsActionPressed(sprintAction);

        if (WasActionPressedThisFrame(attackAction))
        {
            attackTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(interactAction))
        {
            interactTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(crouchAction))
        {
            crouchTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(jumpAction))
        {
            jumpTriggeredFrame = Time.frameCount;
            QueueJump();
        }

        if (WasActionPressedThisFrame(previousAction))
        {
            previousTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(nextAction))
        {
            nextTriggeredFrame = Time.frameCount;
        }
    }

    private static InputAction FindAction(InputActionMap actionMap, string actionName)
    {
        if (actionMap == null || string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        return actionMap.FindAction(actionName, false);
    }

    private static Vector2 ReadVector2(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    private static bool IsActionPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private static bool WasActionPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value != null ? value.Get<Vector2>() : Vector2.zero;
    }

    public void OnLook(InputValue value)
    {
        lookInput = value != null ? value.Get<Vector2>() : Vector2.zero;
    }

    public void OnAttack(InputValue value)
    {
        UpdateButtonState(value, ref attackPressed, ref attackTriggeredFrame);
    }

    public void OnInteract(InputValue value)
    {
        UpdateButtonState(value, ref interactPressed, ref interactTriggeredFrame);
    }

    public void OnCrouch(InputValue value)
    {
        UpdateButtonState(value, ref crouchPressed, ref crouchTriggeredFrame);
    }

    public void OnJump(InputValue value)
    {
        bool wasPressed = UpdateButtonState(value, ref jumpPressed, ref jumpTriggeredFrame);
        if (wasPressed)
        {
            QueueJump();
        }
    }

    private void QueueJump()
    {
        queuedJumpTime = Time.time;
        jumpQueued = true;
    }

    public void OnPrevious(InputValue value)
    {
        UpdateButtonState(value, ref previousPressed, ref previousTriggeredFrame);
    }

    public void OnNext(InputValue value)
    {
        UpdateButtonState(value, ref nextPressed, ref nextTriggeredFrame);
    }

    public void OnSprint(InputValue value)
    {
        sprintPressed = value != null && value.isPressed;
    }

    private static bool UpdateButtonState(InputValue value, ref bool pressedState, ref int triggeredFrame)
    {
        bool isPressed = value != null && value.isPressed;
        if (isPressed && !pressedState)
        {
            triggeredFrame = Time.frameCount;
        }

        pressedState = isPressed;
        return isPressed;
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (capsuleCollider == null)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
        }

        if (capsuleCollider == null)
        {
            return;
        }

        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float radius = GetGroundProbeRadius();
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, radius);
        float castStartOffset = Mathf.Max(0f, halfHeight - radius) + groundCheckOffset;
        Vector3 castOrigin = worldCenter + up * castStartOffset;
        Vector3 castEnd = castOrigin - up * (groundCheckDistance + castStartOffset);

        Gizmos.DrawWireSphere(castOrigin, radius);
        Gizmos.DrawLine(castOrigin, castEnd);
        Gizmos.DrawWireSphere(castEnd, radius);
    }
}
