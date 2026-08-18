using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Game/Player/Movement Config")]
public sealed class PlayerMovementConfig : ScriptableObject
{
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

    public float MoveSpeed => moveSpeed;
    public float RunSpeed => runSpeed;
    public float CrouchSpeed => crouchSpeed;
    public float RotationSpeed => rotationSpeed;
    public bool RotateTowardsMovement => rotateTowardsMovement;
    public bool KeepWorldUpAligned => keepWorldUpAligned;
    public float GroundedVerticalVelocity => groundedVerticalVelocity;
    public float JumpSpeed => jumpSpeed;
    public float JumpBufferTime => jumpBufferTime;
    public float CoyoteTime => coyoteTime;
    public bool OverrideRigidbodyGravity => overrideRigidbodyGravity;
    public Vector3 GravityOverride => gravityOverride;
    public LayerMask GroundMask => groundMask;
    public float GroundCheckDistance => groundCheckDistance;
    public float GroundCheckOffset => groundCheckOffset;
    public float GroundProbeRadiusScale => groundProbeRadiusScale;
    public float MaxGroundAngle => maxGroundAngle;
    public CollisionDetectionMode CollisionDetectionMode => collisionDetectionMode;

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
    }
#endif
}
