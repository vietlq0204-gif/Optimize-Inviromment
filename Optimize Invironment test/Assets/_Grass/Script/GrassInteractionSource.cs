using UnityEngine;

/// <summary>
/// Marks a moving object as a grass interactor and exposes its contact data to the shader system.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class GrassInteractionSource : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Min(0.05f)] private float radius = 1.05f;
    [SerializeField, Range(0f, 2f)] private float strength = 1.15f;

    [Header("Anchor")]
    [SerializeField] private bool anchorToColliderBottom = true;
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float velocitySmoothing = 14f;

    [Header("Persistent Trail")]
    [SerializeField] private bool emitPersistentTrail = true;
    [SerializeField, Range(0f, 2f)] private float trailStrength = 1.2f;
    [SerializeField, Min(0.05f)] private float trailRadiusMultiplier = 1.5f;
    [SerializeField, Range(0f, 4f)] private float trailLength = 2.4f;

    private CharacterController cachedCharacterController;
    private Collider cachedCollider;
    private Vector3 currentAnchorPosition;
    private Vector3 lastAnchorPosition;
    private Vector3 smoothedVelocity;

    internal Vector4 InteractorData => new(currentAnchorPosition.x, currentAnchorPosition.y, currentAnchorPosition.z, radius);
    internal Vector4 VelocityData => new(smoothedVelocity.x, smoothedVelocity.y, smoothedVelocity.z, strength);
    internal bool EmitsPersistentTrail => emitPersistentTrail;
    internal Vector4 TrailStampData => new(
        currentAnchorPosition.x,
        currentAnchorPosition.z,
        radius * trailRadiusMultiplier,
        strength * trailStrength);
    internal Vector4 TrailMotionData => new(smoothedVelocity.x, smoothedVelocity.z, trailLength, 0f);

    private void Awake()
    {
        TryGetComponent(out cachedCharacterController);
        TryGetComponent(out cachedCollider);
    }

    private void OnEnable()
    {
        currentAnchorPosition = ResolveAnchorPosition();
        lastAnchorPosition = currentAnchorPosition;
        smoothedVelocity = Vector3.zero;
        GrassInteractionSystem.Register(this);
    }

    private void OnDisable()
    {
        GrassInteractionSystem.Unregister(this);
    }

    private void LateUpdate()
    {
        currentAnchorPosition = ResolveAnchorPosition();

        float deltaTime = Time.deltaTime;
        if (deltaTime > Mathf.Epsilon)
        {
            Vector3 targetVelocity = (currentAnchorPosition - lastAnchorPosition) / deltaTime;
            float blend = 1f - Mathf.Exp(-velocitySmoothing * deltaTime);
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, targetVelocity, blend);
        }
        else
        {
            smoothedVelocity = Vector3.zero;
        }

        lastAnchorPosition = currentAnchorPosition;
    }

    private Vector3 ResolveAnchorPosition()
    {
        Vector3 anchorPosition = transform.position + centerOffset;

        if (cachedCharacterController == null)
        {
            TryGetComponent(out cachedCharacterController);
        }

        if (cachedCollider == null)
        {
            TryGetComponent(out cachedCollider);
        }

        if (!anchorToColliderBottom)
        {
            return anchorPosition + Vector3.up * surfaceOffset;
        }

        if (cachedCharacterController != null)
        {
            Bounds bounds = cachedCharacterController.bounds;
            anchorPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return anchorPosition + Vector3.up * surfaceOffset;
        }

        if (cachedCollider != null)
        {
            Bounds bounds = cachedCollider.bounds;
            anchorPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return anchorPosition + Vector3.up * surfaceOffset;
        }

        return anchorPosition + Vector3.up * surfaceOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.25f, 0.9f);
        Vector3 anchorPosition = Application.isPlaying ? currentAnchorPosition : ResolveAnchorPosition();
        Gizmos.DrawWireSphere(anchorPosition, radius);
    }
}
