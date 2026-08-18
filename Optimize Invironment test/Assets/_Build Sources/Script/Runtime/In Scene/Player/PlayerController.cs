using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputState))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private Transform cameraTransform;

    private Rigidbody rigidbodyComponent;
    private CapsuleCollider capsuleCollider;
    private PlayerInputState playerInputState;

    [Header("Movement")] [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private bool rotateTowardsMovement = true;
    [SerializeField] private bool keepWorldUpAligned = true;
    [SerializeField] private float groundedVerticalVelocity = -2f;

    [Header("Jump")] [SerializeField] private float jumpSpeed = 6f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.1f;

    [Header("Gravity")] [SerializeField] private bool overrideRigidbodyGravity;
    [SerializeField] private Vector3 gravityOverride = new Vector3(0f, -9.81f, 0f);

    [Header("Ground Check")] [SerializeField]
    private LayerMask groundMask = ~0;

    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float groundCheckOffset = 0.2f;
    [SerializeField] [Range(0.1f, 1f)] private float groundProbeRadiusScale = 0.9f;
    [SerializeField] [Range(0f, 89f)] private float maxGroundAngle = 65f;

    [Header("Physics")] [SerializeField]
    private CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private float lastGroundedTime = float.NegativeInfinity;
    private float queuedJumpTime = float.NegativeInfinity;
    private bool jumpQueued;

    #region PublicField

    public Vector2 MoveInput => playerInputState != null ? playerInputState.MoveInput : Vector2.zero;
    public Vector2 LookInput => playerInputState != null ? playerInputState.LookInput : Vector2.zero;
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public float MoveSpeed => moveSpeed;
    public float RunSpeed => runSpeed;
    public float CrouchSpeed => crouchSpeed;
    public float CurrentMoveSpeed => GetCurrentMoveSpeed();
    public bool IsSprinting => playerInputState != null && playerInputState.IsSprinting;
    public bool IsCrouching => playerInputState != null && playerInputState.IsCrouching;
    public bool IsAttacking => playerInputState != null && playerInputState.IsAttacking;
    public bool IsInteracting => playerInputState != null && playerInputState.IsInteracting;
    public bool IsJumpPressed => playerInputState != null && playerInputState.IsJumpPressed;
    public bool IsPreviousPressed => playerInputState != null && playerInputState.IsPreviousPressed;
    public bool IsNextPressed => playerInputState != null && playerInputState.IsNextPressed;
    public bool AttackTriggeredThisFrame => playerInputState != null && playerInputState.AttackTriggeredThisFrame;
    public bool InteractTriggeredThisFrame => playerInputState != null && playerInputState.InteractTriggeredThisFrame;
    public bool CrouchTriggeredThisFrame => playerInputState != null && playerInputState.CrouchTriggeredThisFrame;
    public bool JumpTriggeredThisFrame => playerInputState != null && playerInputState.JumpTriggeredThisFrame;
    public bool PreviousTriggeredThisFrame => playerInputState != null && playerInputState.PreviousTriggeredThisFrame;
    public bool NextTriggeredThisFrame => playerInputState != null && playerInputState.NextTriggeredThisFrame;

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

    #endregion

    private void Awake()
    {
        EnsurePhysicsSetup();
        EnsureInputSetup();
        ApplyGravitySettings();
        ApplyRotationConstraints();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        EnsureInputSetup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        runSpeed = Mathf.Max(moveSpeed, runSpeed);
        crouchSpeed = Mathf.Clamp(crouchSpeed, 0f, runSpeed);
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
        EnsureInputSetup();
        ApplyGravitySettings();
        ApplyRotationConstraints();
    }
#endif

    private void Update()
    {
        if (JumpTriggeredThisFrame)
        {
            QueueJump();
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundInfo();
        HandleJump();
        HandleMovement();
        ApplyGravity();
        UpdateRotation();
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
    
    private void ApplyGravitySettings()
    {
        if (rigidbodyComponent == null)
        {
            return;
        }

        rigidbodyComponent.useGravity = !overrideRigidbodyGravity;
    }

    private void EnsureInputSetup()
    {
        playerInputState = GetComponent<PlayerInputState>();

        if (playerInputState == null)
        {
            playerInputState = gameObject.AddComponent<PlayerInputState>();
        }
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
        Vector2 clampedInput = Vector2.ClampMagnitude(MoveInput, 1f);
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
    
    
    private void HandleMovement()
    {
        Vector3 desiredMoveDirection = GetDesiredMoveDirection();
        Vector3 velocity = desiredMoveDirection * GetCurrentMoveSpeed();
        velocity.y = GetTargetVerticalVelocity();
        rigidbodyComponent.linearVelocity = velocity;
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
    
    private bool CanJump()
    {
        return isGrounded || Time.time - lastGroundedTime <= coyoteTime;
    }
    
    private void QueueJump()
    {
        queuedJumpTime = Time.time;
        jumpQueued = true;
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

        if (Physics.SphereCast(castOrigin, radius, -up, out RaycastHit hit, castDistance, groundMask,
                QueryTriggerInteraction.Ignore))
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
        if (IsCrouching)
        {
            return crouchSpeed;
        }

        if (IsSprinting)
        {
            return runSpeed;
        }

        return moveSpeed;
    }

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