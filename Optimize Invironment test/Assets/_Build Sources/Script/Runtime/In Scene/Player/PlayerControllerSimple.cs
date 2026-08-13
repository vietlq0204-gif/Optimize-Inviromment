using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple player controller for moving on Terrain.
/// </summary>
/// <remarks>
/// - Uses Rigidbody for movement and custom gravity.
/// - Migrates legacy CharacterController setups to CapsuleCollider.
/// - Projects movement onto ground normal so the player follows slopes.
/// - Uses raycast to detect Terrain / ground normal.
/// - Reads movement from the PlayerInput component on this player.
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class PlayerControllerSimple : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

#if ENABLE_INPUT_SYSTEM
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
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private bool rotateTowardsMovement = true;
    [SerializeField] private bool keepWorldUpAligned = true;

    [Header("Gravity")]
    [SerializeField] private bool overrideRigidbodyGravity;
    [SerializeField] private Vector3 gravityOverride = new Vector3(0f, -9.81f, 0f);

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float groundCheckOffset = 0.2f;

    private Rigidbody rigidbodyComponent;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 pendingMoveDirection;
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
    private bool useSendMessageCallbacks;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool attackPressed;
    private bool interactPressed;
    private bool crouchPressed;
    private bool jumpPressed;
    private bool previousPressed;
    private bool nextPressed;
    private bool sprintPressed;
    private bool attackTriggeredThisFrame;
    private bool interactTriggeredThisFrame;
    private bool crouchTriggeredThisFrame;
    private bool jumpTriggeredThisFrame;
    private bool previousTriggeredThisFrame;
    private bool nextTriggeredThisFrame;
#endif

    public float MoveSpeed => moveSpeed;
    public float CurrentMoveSpeed => GetCurrentMoveSpeed();
    public bool IsSprinting
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return sprintPressed;
#else
            return false;
#endif
        }
    }

    public bool IsCrouching
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return crouchPressed;
#else
            return false;
#endif
        }
    }

    public bool JumpTriggeredThisFrame
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return jumpTriggeredThisFrame;
#else
            return false;
#endif
        }
    }

    public bool AttackTriggeredThisFrame
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return attackTriggeredThisFrame;
#else
            return false;
#endif
        }
    }

    public bool InteractTriggeredThisFrame
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return interactTriggeredThisFrame;
#else
            return false;
#endif
        }
    }

    public bool PreviousTriggeredThisFrame
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return previousTriggeredThisFrame;
#else
            return false;
#endif
        }
    }

    public bool NextTriggeredThisFrame
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return nextTriggeredThisFrame;
#else
            return false;
#endif
        }
    }

    public Vector2 LookInput
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return lookInput;
#else
            return Vector2.zero;
#endif
        }
    }

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

    /// <summary>
    /// Cache required components.
    /// </summary>
    private void Awake()
    {
        EnsurePhysicsSetup();
        ApplyGravitySettings();
        ApplyRotationConstraints();

#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
        CacheInputActions();
#endif

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

#if ENABLE_INPUT_SYSTEM
    private void OnEnable()
    {
        CacheInputActions();
        SubscribeInputActions();
    }

    private void OnDisable()
    {
        UnsubscribeInputActions();
    }
#endif

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsurePhysicsSetup();
        ApplyGravitySettings();
        ApplyRotationConstraints();

        if (!Application.isPlaying)
        {
            return;
        }
    }
#endif

    /// <summary>
    /// Main update loop.
    /// </summary>
    private void Update()
    {
        pendingMoveDirection = GetMoveDirectionOnGround(GetInputDirection());
    }

    private void LateUpdate()
    {
#if ENABLE_INPUT_SYSTEM
        attackTriggeredThisFrame = false;
        interactTriggeredThisFrame = false;
        crouchTriggeredThisFrame = false;
        jumpTriggeredThisFrame = false;
        previousTriggeredThisFrame = false;
        nextTriggeredThisFrame = false;
#endif
    }

    /// <summary>
    /// Physics update loop.
    /// </summary>
    private void FixedUpdate()
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
            groundNormal = hit.normal;
        }
        else
        {
            groundNormal = Vector3.up;
        }
    }

    /// <summary>
    /// Handles input, ground projection, gravity, and rotation.
    /// </summary>
    private void HandleMovement()
    {
        Vector3 velocity = pendingMoveDirection * GetCurrentMoveSpeed();
        velocity.y += rigidbodyComponent.linearVelocity.y;

        rigidbodyComponent.linearVelocity = velocity;
        ApplyExtraGravity();

        UpdateRotation(pendingMoveDirection);
    }

    /// <summary>
    /// Applies gravity for this player without changing global Physics gravity.
    /// </summary>
    private void ApplyExtraGravity()
    {
        if (!overrideRigidbodyGravity)
        {
            return;
        }

        rigidbodyComponent.AddForce(gravityOverride, ForceMode.Acceleration);
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
    /// Reads movement input from the PlayerInput component.
    /// </summary>
    /// <returns>Normalized 2D move input.</returns>
    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        return Vector2.ClampMagnitude(moveInput, 1f);
#else
        return Vector2.zero;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void CacheInputActions()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput == null)
        {
            moveAction = null;
            return;
        }

        useSendMessageCallbacks = playerInput.notificationBehavior == PlayerNotifications.SendMessages ||
            playerInput.notificationBehavior == PlayerNotifications.BroadcastMessages;
        moveAction = FindAction(moveActionName);
        lookAction = FindAction(lookActionName);
        attackAction = FindAction(attackActionName);
        interactAction = FindAction(interactActionName);
        crouchAction = FindAction(crouchActionName);
        jumpAction = FindAction(jumpActionName);
        previousAction = FindAction(previousActionName);
        nextAction = FindAction(nextActionName);
        sprintAction = FindAction(sprintActionName);
        moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    }

    private InputAction FindAction(string actionName)
    {
        InputAction action = playerInput.currentActionMap?.FindAction(actionName, false);

        if (action == null && playerInput.actions != null)
        {
            action = playerInput.actions.FindAction(actionName, false);
        }

        return action;
    }

    private void SubscribeInputActions()
    {
        if (useSendMessageCallbacks)
        {
            return;
        }

        SubscribeValueAction(moveAction, OnMovePerformed, OnMoveCanceled);
        SubscribeValueAction(lookAction, OnLookPerformed, OnLookCanceled);
        SubscribeButtonAction(attackAction, OnAttackPerformed, OnAttackCanceled);
        SubscribeButtonAction(interactAction, OnInteractPerformed, OnInteractCanceled);
        SubscribeButtonAction(crouchAction, OnCrouchPerformed, OnCrouchCanceled);
        SubscribeButtonAction(jumpAction, OnJumpPerformed, OnJumpCanceled);
        SubscribeButtonAction(previousAction, OnPreviousPerformed, OnPreviousCanceled);
        SubscribeButtonAction(nextAction, OnNextPerformed, OnNextCanceled);
        SubscribeButtonAction(sprintAction, OnSprintPerformed, OnSprintCanceled);
    }

    private void UnsubscribeInputActions()
    {
        if (useSendMessageCallbacks)
        {
            return;
        }

        UnsubscribeValueAction(moveAction, OnMovePerformed, OnMoveCanceled);
        UnsubscribeValueAction(lookAction, OnLookPerformed, OnLookCanceled);
        UnsubscribeButtonAction(attackAction, OnAttackPerformed, OnAttackCanceled);
        UnsubscribeButtonAction(interactAction, OnInteractPerformed, OnInteractCanceled);
        UnsubscribeButtonAction(crouchAction, OnCrouchPerformed, OnCrouchCanceled);
        UnsubscribeButtonAction(jumpAction, OnJumpPerformed, OnJumpCanceled);
        UnsubscribeButtonAction(previousAction, OnPreviousPerformed, OnPreviousCanceled);
        UnsubscribeButtonAction(nextAction, OnNextPerformed, OnNextCanceled);
        UnsubscribeButtonAction(sprintAction, OnSprintPerformed, OnSprintCanceled);
    }

    private static void SubscribeValueAction(InputAction action, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
    {
        if (action == null)
        {
            return;
        }

        action.performed -= performed;
        action.performed += performed;
        action.canceled -= canceled;
        action.canceled += canceled;
    }

    private static void UnsubscribeValueAction(InputAction action, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
    {
        if (action == null)
        {
            return;
        }

        action.performed -= performed;
        action.canceled -= canceled;
    }

    private static void SubscribeButtonAction(InputAction action, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
    {
        if (action == null)
        {
            return;
        }

        action.performed -= performed;
        action.performed += performed;
        action.canceled -= canceled;
        action.canceled += canceled;
    }

    private static void UnsubscribeButtonAction(InputAction action, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
    {
        if (action == null)
        {
            return;
        }

        action.performed -= performed;
        action.canceled -= canceled;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext context)
    {
        lookInput = Vector2.zero;
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        SetButtonState(ref attackPressed, ref attackTriggeredThisFrame, true);
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        attackPressed = false;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        SetButtonState(ref interactPressed, ref interactTriggeredThisFrame, true);
    }

    private void OnInteractCanceled(InputAction.CallbackContext context)
    {
        interactPressed = false;
    }

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        SetButtonState(ref crouchPressed, ref crouchTriggeredThisFrame, true);
    }

    private void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        crouchPressed = false;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        SetButtonState(ref jumpPressed, ref jumpTriggeredThisFrame, true);
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpPressed = false;
    }

    private void OnPreviousPerformed(InputAction.CallbackContext context)
    {
        SetButtonState(ref previousPressed, ref previousTriggeredThisFrame, true);
    }

    private void OnPreviousCanceled(InputAction.CallbackContext context)
    {
        previousPressed = false;
    }

    private void OnNextPerformed(InputAction.CallbackContext context)
    {
        SetButtonState(ref nextPressed, ref nextTriggeredThisFrame, true);
    }

    private void OnNextCanceled(InputAction.CallbackContext context)
    {
        nextPressed = false;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        sprintPressed = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        sprintPressed = false;
    }

    private static void SetButtonState(ref bool pressedState, ref bool triggeredThisFrame, bool isPressed)
    {
        if (isPressed)
        {
            triggeredThisFrame = true;
        }

        pressedState = isPressed;
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
        bool isPressed = value != null && value.isPressed;
        SetButtonState(ref attackPressed, ref attackTriggeredThisFrame, isPressed);
    }

    public void OnInteract(InputValue value)
    {
        bool isPressed = value != null && value.isPressed;
        SetButtonState(ref interactPressed, ref interactTriggeredThisFrame, isPressed);
    }

    public void OnCrouch(InputValue value)
    {
        bool isPressed = value != null && value.isPressed;
        SetButtonState(ref crouchPressed, ref crouchTriggeredThisFrame, isPressed);
    }

    public void OnJump(InputValue value)
    {
        bool isPressed = value != null && value.isPressed;
        SetButtonState(ref jumpPressed, ref jumpTriggeredThisFrame, isPressed);
    }

    public void OnPrevious(InputValue value)
    {
        bool isPressed = value != null && value.isPressed;
        SetButtonState(ref previousPressed, ref previousTriggeredThisFrame, isPressed);
    }

    public void OnNext(InputValue value)
    {
        bool isPressed = value != null && value.isPressed;
        SetButtonState(ref nextPressed, ref nextTriggeredThisFrame, isPressed);
    }

    public void OnSprint(InputValue value)
    {
        sprintPressed = value != null && value.isPressed;
    }
#endif

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
    /// Updates the player rotation using movement direction and ground alignment settings.
    /// </summary>
    /// <param name="moveDirection">Current move direction.</param>
    private void UpdateRotation(Vector3 moveDirection)
    {
        Vector3 targetUp = keepWorldUpAligned ? Vector3.up : groundNormal;
        Vector3 targetForward = GetTargetForward(moveDirection, targetUp);

        if (targetForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);
        Quaternion nextRotation = Quaternion.Slerp(
            rigidbodyComponent.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        rigidbodyComponent.MoveRotation(nextRotation);
    }

    /// <summary>
    /// Calculates the target forward direction based on the rotation options.
    /// </summary>
    /// <param name="moveDirection">Current move direction.</param>
    /// <param name="targetUp">Desired up vector for the character.</param>
    /// <returns>Forward direction to use for target rotation.</returns>
    private Vector3 GetTargetForward(Vector3 moveDirection, Vector3 targetUp)
    {
        if (rotateTowardsMovement && moveDirection.sqrMagnitude > 0.0001f)
        {
            return Vector3.ProjectOnPlane(moveDirection, targetUp).normalized;
        }

        if (!rotateTowardsMovement)
        {
            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                return keepWorldUpAligned
                    ? Vector3.zero
                    : Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;
            }

            if (cameraTransform != null)
            {
                Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, targetUp);
                if (cameraForward.sqrMagnitude > 0.0001f)
                {
                    return cameraForward.normalized;
                }
            }
        }

        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, targetUp);
        if (projectedForward.sqrMagnitude > 0.0001f)
        {
            return projectedForward.normalized;
        }

        return Vector3.ProjectOnPlane(transform.up, targetUp).normalized;
    }

    /// <summary>
    /// Ensures the player uses Rigidbody and CapsuleCollider instead of CharacterController.
    /// </summary>
    private void EnsurePhysicsSetup()
    {
        CharacterController legacyController = GetComponent<CharacterController>();
        CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();

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
    }

    /// <summary>
    /// Applies Rigidbody rotation constraints according to the current alignment settings.
    /// </summary>
    private void ApplyRotationConstraints()
    {
        if (rigidbodyComponent == null)
        {
            return;
        }

        RigidbodyConstraints alignmentConstraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (keepWorldUpAligned)
        {
            rigidbodyComponent.constraints |= alignmentConstraints;
        }
        else
        {
            rigidbodyComponent.constraints &= ~alignmentConstraints;
        }
    }

    private void ApplyGravitySettings()
    {
        if (rigidbodyComponent == null)
        {
            return;
        }

        rigidbodyComponent.useGravity = !overrideRigidbodyGravity;
    }

    private float GetCurrentMoveSpeed()
    {
#if ENABLE_INPUT_SYSTEM
        if (sprintPressed)
        {
            return Mathf.Max(sprintSpeed, moveSpeed);
        }
#endif

        return moveSpeed;
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

#if UNITY_EDITOR
    private void Reset()
    {
        sprintSpeed = Mathf.Max(sprintSpeed, moveSpeed);
    }
#endif
}
