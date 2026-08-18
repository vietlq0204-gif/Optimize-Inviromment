using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerMovementConfig movementConfig;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
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
    private PlayerInputReader playerInputReader;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private bool isJumping;
    private float lastGroundedTime = float.NegativeInfinity;
    private float queuedJumpTime = float.NegativeInfinity;
    private bool jumpQueued;

    public PlayerInputReader InputReader => playerInputReader;
    public Transform CameraTransform => cameraTransform;
    public bool IsGrounded => isGrounded;
    public bool IsJumping => isJumping;
    public Vector3 GroundNormal => groundNormal;
    public float MoveSpeed => GetMoveSpeedSetting();
    public float RunSpeed => GetRunSpeedSetting();
    public float CrouchSpeed => GetCrouchSpeedSetting();
    public float CurrentMoveSpeed => GetCurrentMoveSpeed();
    public bool IsSprinting => playerInputReader != null && playerInputReader.IsSprinting;
    public bool IsCrouching => playerInputReader != null && playerInputReader.IsCrouching;
    public Vector2 MoveInput => playerInputReader != null ? playerInputReader.MoveInput : Vector2.zero;

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

    protected virtual void Awake()
    {
        CacheComponents();
        ApplyConfigToPhysics();
        ResolveCameraTransform();
    }

    protected virtual void OnEnable()
    {
        ResolveCameraTransform();
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
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

        SyncLegacyCharacterController();
        CacheComponents();
        ApplyConfigToPhysics();
    }
#endif

    protected virtual void Update()
    {
        if (playerInputReader != null && playerInputReader.JumpTriggeredThisFrame)
        {
            QueueJump();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!HasRequiredComponents())
        {
            return;
        }

        UpdateGroundInfo();
        HandleJump();
        HandleMovement();
        ApplyGravity();
        UpdateRotation();
    }

    private bool HasRequiredComponents()
    {
        return rigidbodyComponent != null && capsuleCollider != null && playerInputReader != null;
    }

    private void CacheComponents()
    {
        rigidbodyComponent = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        playerInputReader = GetComponent<PlayerInputReader>();

        if (rigidbodyComponent == null || capsuleCollider == null || playerInputReader == null)
        {
            enabled = false;
        }
    }

    private void ResolveCameraTransform()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

#if UNITY_EDITOR
    private void SyncLegacyCharacterController()
    {
        CharacterController legacyController = GetComponent<CharacterController>();
        CapsuleCollider localCapsule = GetComponent<CapsuleCollider>();
        if (legacyController == null || localCapsule == null)
        {
            return;
        }

        localCapsule.center = legacyController.center;
        localCapsule.radius = legacyController.radius;
        localCapsule.height = legacyController.height;
        localCapsule.direction = 1;

        if (!Application.isPlaying)
        {
            DestroyImmediate(legacyController);
        }
    }
#endif

    private void ApplyConfigToPhysics()
    {
        if (rigidbodyComponent == null)
        {
            return;
        }

        rigidbodyComponent.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbodyComponent.collisionDetectionMode = GetCollisionDetectionModeSetting();
        rigidbodyComponent.useGravity = !GetOverrideRigidbodyGravitySetting();

        RigidbodyConstraints constraints = rigidbodyComponent.constraints;
        constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);

        if (GetKeepWorldUpAlignedSetting())
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

        if (GetKeepWorldUpAlignedSetting() || !isGrounded)
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
        return worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.zero;
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
        if (GetJumpSpeedSetting() <= 0f || !jumpQueued)
        {
            return;
        }

        if (Time.time - queuedJumpTime > GetJumpBufferTimeSetting())
        {
            jumpQueued = false;
            return;
        }

        if (!CanJump())
        {
            return;
        }

        Vector3 velocity = rigidbodyComponent.linearVelocity;
        velocity.y = GetJumpSpeedSetting();
        rigidbodyComponent.linearVelocity = velocity;

        queuedJumpTime = float.NegativeInfinity;
        jumpQueued = false;
        isGrounded = false;
        isJumping = true;
        lastGroundedTime = float.NegativeInfinity;
    }

    private bool CanJump()
    {
        return isGrounded || Time.time - lastGroundedTime <= GetCoyoteTimeSetting();
    }

    private void QueueJump()
    {
        queuedJumpTime = Time.time;
        jumpQueued = true;
    }

    private float GetTargetVerticalVelocity()
    {
        float currentVerticalVelocity = rigidbodyComponent.linearVelocity.y;
        if (!isGrounded || currentVerticalVelocity > 0f)
        {
            return currentVerticalVelocity;
        }

        return GetGroundedVerticalVelocitySetting();
    }

    private void ApplyGravity()
    {
        if (!GetOverrideRigidbodyGravitySetting())
        {
            return;
        }

        rigidbodyComponent.AddForce(GetGravityOverrideSetting(), ForceMode.Acceleration);
    }

    private void UpdateRotation()
    {
        if (!GetRotateTowardsMovementSetting())
        {
            return;
        }

        Vector3 desiredMoveDirection = GetDesiredMoveDirection();
        if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 targetUp = GetKeepWorldUpAlignedSetting() ? Vector3.up : groundNormal;
        Vector3 targetForward = Vector3.ProjectOnPlane(desiredMoveDirection, targetUp);
        if (targetForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetForward.normalized, targetUp);
        Quaternion nextRotation = Quaternion.Slerp(
            rigidbodyComponent.rotation,
            targetRotation,
            GetRotationSpeedSetting() * Time.fixedDeltaTime);

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

        if (isJumping && rigidbodyComponent != null && rigidbodyComponent.linearVelocity.y > 0.05f)
        {
            groundNormal = Vector3.up;
            isGrounded = false;
            return;
        }

        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float radius = GetGroundProbeRadius();
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f * Mathf.Max(scaleY, 0.0001f), radius);
        float castStartOffset = Mathf.Max(0f, halfHeight - radius) + GetGroundCheckOffsetSetting();
        Vector3 castOrigin = worldCenter + up * castStartOffset;
        float castDistance = GetGroundCheckDistanceSetting() + castStartOffset;

        if (Physics.SphereCast(
                castOrigin,
                radius,
                -up,
                out RaycastHit hit,
                castDistance,
                GetGroundMaskSetting(),
                QueryTriggerInteraction.Ignore))
        {
            float groundAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (groundAngle <= GetMaxGroundAngleSetting())
            {
                groundNormal = hit.normal;
                isGrounded = true;
                isJumping = false;
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
        return Mathf.Max(0.05f, capsuleCollider.radius * maxHorizontalScale * GetGroundProbeRadiusScaleSetting());
    }

    private float GetCurrentMoveSpeed()
    {
        if (IsCrouching)
        {
            return GetCrouchSpeedSetting();
        }

        if (IsSprinting)
        {
            return GetRunSpeedSetting();
        }

        return GetMoveSpeedSetting();
    }

    private float GetMoveSpeedSetting() => movementConfig != null ? movementConfig.MoveSpeed : moveSpeed;
    private float GetRunSpeedSetting() => movementConfig != null ? movementConfig.RunSpeed : runSpeed;
    private float GetCrouchSpeedSetting() => movementConfig != null ? movementConfig.CrouchSpeed : crouchSpeed;
    private float GetRotationSpeedSetting() => movementConfig != null ? movementConfig.RotationSpeed : rotationSpeed;
    private bool GetRotateTowardsMovementSetting() => movementConfig != null ? movementConfig.RotateTowardsMovement : rotateTowardsMovement;
    private bool GetKeepWorldUpAlignedSetting() => movementConfig != null ? movementConfig.KeepWorldUpAligned : keepWorldUpAligned;
    private float GetGroundedVerticalVelocitySetting() => movementConfig != null ? movementConfig.GroundedVerticalVelocity : groundedVerticalVelocity;
    private float GetJumpSpeedSetting() => movementConfig != null ? movementConfig.JumpSpeed : jumpSpeed;
    private float GetJumpBufferTimeSetting() => movementConfig != null ? movementConfig.JumpBufferTime : jumpBufferTime;
    private float GetCoyoteTimeSetting() => movementConfig != null ? movementConfig.CoyoteTime : coyoteTime;
    private bool GetOverrideRigidbodyGravitySetting() => movementConfig != null ? movementConfig.OverrideRigidbodyGravity : overrideRigidbodyGravity;
    private Vector3 GetGravityOverrideSetting() => movementConfig != null ? movementConfig.GravityOverride : gravityOverride;
    private LayerMask GetGroundMaskSetting() => movementConfig != null ? movementConfig.GroundMask : groundMask;
    private float GetGroundCheckDistanceSetting() => movementConfig != null ? movementConfig.GroundCheckDistance : groundCheckDistance;
    private float GetGroundCheckOffsetSetting() => movementConfig != null ? movementConfig.GroundCheckOffset : groundCheckOffset;
    private float GetGroundProbeRadiusScaleSetting() => movementConfig != null ? movementConfig.GroundProbeRadiusScale : groundProbeRadiusScale;
    private float GetMaxGroundAngleSetting() => movementConfig != null ? movementConfig.MaxGroundAngle : maxGroundAngle;
    private CollisionDetectionMode GetCollisionDetectionModeSetting() => movementConfig != null ? movementConfig.CollisionDetectionMode : collisionDetectionMode;

    protected virtual void OnDrawGizmosSelected()
    {
        CapsuleCollider gizmoCapsule = capsuleCollider != null ? capsuleCollider : GetComponent<CapsuleCollider>();
        if (gizmoCapsule == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(gizmoCapsule.center);
        float radius = GetGroundProbeRadius();
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float halfHeight = Mathf.Max(gizmoCapsule.height * 0.5f * Mathf.Max(scaleY, 0.0001f), radius);
        float castStartOffset = Mathf.Max(0f, halfHeight - radius) + GetGroundCheckOffsetSetting();
        Vector3 castOrigin = worldCenter + up * castStartOffset;
        Vector3 castEnd = castOrigin - up * (GetGroundCheckDistanceSetting() + castStartOffset);

        Gizmos.DrawWireSphere(castOrigin, radius);
        Gizmos.DrawLine(castOrigin, castEnd);
        Gizmos.DrawWireSphere(castEnd, radius);
    }
}
