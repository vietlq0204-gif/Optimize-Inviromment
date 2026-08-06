using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Component chịu trách nhiệm phát ra các tương tác (interaction) vào môi trường.
/// Nó tạo ra các hình dạng (shape) như đĩa (disc) và viên thuốc (capsule) để mô phỏng
/// hiệu ứng vật thể đè nén hoặc di chuyển qua thảm thực vật (ví dụ: cỏ).
/// Các thông số có thể được tùy chỉnh trực tiếp hoặc thông qua một 'GrassInteractionConfig'.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Environment/Interaction Interactor")]
public class EnvironmentInteractor : MonoBehaviour
{
    private const float DebugDrawHeightOffset = 0.03f;

    [Header("Config")]
    [Tooltip("Config cỏ dùng riêng cho source này. Nếu bỏ trống, source sẽ dùng config đang gắn trên EnvironmentInteractionSystem.")]
    [SerializeField] private GrassInteractionConfig interactionConfig;

    [Header("Target")]
    [Tooltip("Nhóm hệ thống sẽ nhận interaction này. Với cỏ, giữ Vegetation.")]
    [SerializeField] protected InteractionTargetMask targets = InteractionTargetMask.Vegetation;
    [Tooltip("Độ lệch chiều cao của điểm ghi tương tác so với vị trí object khi không có GrassInteractionConfig.")]
    [SerializeField] protected float heightOffset = 0.05f;

    [Header("Contact Shape")]
    [Tooltip("Bật/tắt vùng đè trực tiếp quanh object.")]
    [SerializeField] protected bool emitContactShape = true;
    [Tooltip("Bán kính vùng cỏ bị đè trực tiếp khi không có GrassInteractionConfig.")]
    [SerializeField] protected float contactRadius = 0.6f;
    [Tooltip("Cường độ vùng đè trực tiếp khi không có GrassInteractionConfig.")]
    [SerializeField] protected float contactStrength = 1f;

    [Header("Trail Shape")]
    [Tooltip("Bật/tắt vệt cỏ khi object di chuyển.")]
    [SerializeField] protected bool emitTrailShape = true;
    [Tooltip("Bán kính vệt cỏ phía sau khi không có GrassInteractionConfig.")]
    [SerializeField] protected float trailRadius = 0.45f;
    [Tooltip("Cường độ vệt cỏ khi không có GrassInteractionConfig.")]
    [SerializeField] protected float trailStrength = 1f;
    [Tooltip("Quãng đường tối thiểu để tạo trail khi không có GrassInteractionConfig.")]
    [SerializeField] protected float minimumTrailDistance = 0.02f;

    [Header("Behavior")]
    [Tooltip("Vẫn ghi vùng contact khi object đứng yên.")]
    [SerializeField] protected bool emitWhileStationary = true;
    [Tooltip("Chặn recovery khi object đứng yên trên cỏ.")]
    [SerializeField] protected bool suppressRecoveryWhileStationary = true;

    [Header("Debug")]
    [Tooltip("Vẽ gizmo debug cho vùng contact/trail trong Scene view.")]
    [SerializeField] private bool drawDebugShapes = true;
    [Tooltip("Chỉ vẽ gizmo debug khi chọn object.")]
    [SerializeField] private bool drawDebugOnlyWhenSelected = true;
    [Tooltip("Vẽ mũi tên hướng/tốc độ di chuyển.")]
    [SerializeField] private bool drawDebugVelocity = true;
    [Tooltip("Hiện label thông số debug khi chọn object.")]
    [SerializeField] private bool drawDebugLabels = true;
    [Tooltip("Màu gizmo vùng contact.")]
    [SerializeField] private Color debugContactColor = new Color(0.15f, 0.85f, 1f, 0.9f);
    [Tooltip("Màu gizmo vùng trail.")]
    [SerializeField] private Color debugTrailColor = new Color(1f, 0.45f, 0.2f, 0.9f);
    [Tooltip("Màu gizmo hướng vận tốc.")]
    [SerializeField] private Color debugVelocityColor = new Color(1f, 0.95f, 0.2f, 0.95f);

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastLossyScale;
    private Vector3 stableContactPosition;
    private Vector3 previousContactPosition;
    private Vector2 stablePlanarDirection = Vector2.up;
    private float planarSpeed;
    private bool isStationary = true;
    private bool hasLastState;
    private bool rootTransformChanged;
    private bool hasRenderableTrail;

    /// <summary>
    /// Được gọi khi component được kích hoạt. Khởi tạo trạng thái và đăng ký interactor với hệ thống trung tâm.
    /// </summary>
    protected virtual void OnEnable()
    {
        InitializeRuntimeState();
        EnvironmentInteractionRegistry.Register(this);
    }

    /// <summary>
    /// Được gọi khi component bị vô hiệu hóa. Hủy đăng ký interactor và reset trạng thái.
    /// </summary>
    protected virtual void OnDisable()
    {
        EnvironmentInteractionRegistry.Unregister(this);
        hasLastState = false;
        rootTransformChanged = false;
        hasRenderableTrail = false;
    }

    /// <summary>
    /// Được gọi trong Editor khi một giá trị được thay đổi. Đảm bảo các giá trị cấu hình luôn hợp lệ (không âm).
    /// </summary>
    protected virtual void OnValidate()
    {
        heightOffset = Mathf.Max(-5f, heightOffset);
        contactRadius = Mathf.Max(0.01f, contactRadius);
        trailRadius = Mathf.Max(0.01f, trailRadius);
        contactStrength = Mathf.Max(0f, contactStrength);
        trailStrength = Mathf.Max(0f, trailStrength);
        minimumTrailDistance = Mathf.Max(0f, minimumTrailDistance);
    }

    /// <summary>
    /// Được gọi mỗi khung hình. Cập nhật trạng thái runtime của interactor.
    /// </summary>
    protected virtual void Update()
    {
        RefreshRuntimeState();
    }

    /// <summary>
    /// Thu thập các hình dạng tương tác (disc, capsule) và thêm vào buffer.
    /// Hàm này được gọi bởi một hệ thống bên ngoài (ví dụ: EnvironmentInteractionSystem) để xử lý hiệu ứng.
    /// </summary>
    /// <param name="buffer">Danh sách để thêm các InteractionShape được tạo ra.</param>
    /// <param name="context">Ngữ cảnh của việc thu thập, chứa vị trí tập trung và khoảng cách tối đa.</param>
    public virtual void CollectShapes(List<InteractionShape> buffer, InteractionCollectContext context)
    {
        if (buffer == null)
        {
            return;
        }

        if (targets == InteractionTargetMask.None)
        {
            return;
        }

        if (!GetEmitWhileStationary() && !rootTransformChanged && !hasRenderableTrail)
        {
            return;
        }

        Vector3 contactPosition = stableContactPosition;
        float configuredContactRadius = GetContactRadius();
        float configuredTrailRadius = GetTrailRadius();
        float maxRadius = Mathf.Max(configuredContactRadius, configuredTrailRadius);
        if (!IsWithinCollectionDistance(contactPosition, context.FocusPosition, context.MaxDistance + maxRadius))
        {
            return;
        }

        Vector3 velocity = new Vector3(stablePlanarDirection.x, 0f, stablePlanarDirection.y) * planarSpeed;
        bool isMovingInteraction = IsMovingInteraction();
        if (emitContactShape && GetContactStrength() > 0f)
        {
            buffer.Add(new InteractionShape
            {
                Type = InteractionShapeType.Disc,
                Targets = targets,
                PointA = contactPosition,
                PointB = contactPosition,
                Velocity = velocity,
                Radius = configuredContactRadius,
                Strength = GetContactStrength(),
                Softness = GetContactSoftness(),
                DirectionalInfluence = GetContactDirectionalInfluence(),
                RecoveryWeight = GetSuppressRecoveryWhileStationary() && !isMovingInteraction
                    ? 0f
                    : GetContactRecoveryWeight(),
            });
        }

        if (!emitTrailShape || !hasRenderableTrail || GetTrailStrength() <= 0f)
        {
            return;
        }

        buffer.Add(new InteractionShape
        {
            Type = InteractionShapeType.Capsule,
            Targets = targets,
            PointA = previousContactPosition,
            PointB = contactPosition,
            Velocity = velocity,
            Radius = configuredTrailRadius,
            Strength = GetTrailStrength(),
            Softness = GetTrailSoftness(),
            DirectionalInfluence = GetTrailDirectionalInfluence(),
            RecoveryWeight = GetTrailRecoveryWeight(),
        });
    }

    /// <summary>
    /// Khởi tạo hoặc reset trạng thái runtime của interactor về giá trị ban đầu.
    /// </summary>
    protected virtual void InitializeRuntimeState()
    {
        Vector3 currentPosition = transform.position;
        Vector3 contactPosition = currentPosition + Vector3.up * GetHeightOffset();
        lastPosition = currentPosition;
        lastRotation = transform.rotation;
        lastLossyScale = transform.lossyScale;
        stableContactPosition = contactPosition;
        previousContactPosition = contactPosition;
        stablePlanarDirection = Vector2.up;
        planarSpeed = 0f;
        isStationary = true;
        hasLastState = false;
        rootTransformChanged = false;
        hasRenderableTrail = false;
    }

    /// <summary>
    /// Cập nhật trạng thái của interactor mỗi khung hình, tính toán tốc độ, hướng di chuyển, và vị trí tiếp xúc ổn định.
    /// Đây là "bộ não" của component, xử lý logic di chuyển.
    /// </summary>
    protected virtual void RefreshRuntimeState()
    {
        float deltaTime = GetDeltaTime();
        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        Vector3 currentLossyScale = transform.lossyScale;

        if (!hasLastState)
        {
            Vector3 initialContactPosition = currentPosition + Vector3.up * GetHeightOffset();
            lastPosition = currentPosition;
            lastRotation = currentRotation;
            lastLossyScale = currentLossyScale;
            stableContactPosition = initialContactPosition;
            previousContactPosition = initialContactPosition;
            hasLastState = true;
            rootTransformChanged = false;
            hasRenderableTrail = false;
            planarSpeed = 0f;
            return;
        }

        rootTransformChanged = HasRootTransformChanged(currentPosition, currentRotation, currentLossyScale);
        Vector2 planarDirection = GetFilteredPlanarDirection(currentPosition - lastPosition, deltaTime, out float nextPlanarSpeed);
        Vector3 nextContactPosition = GetStableContactPosition(currentPosition, nextPlanarSpeed, deltaTime) + Vector3.up * GetHeightOffset();

        previousContactPosition = stableContactPosition;
        stableContactPosition = nextContactPosition;
        planarSpeed = nextPlanarSpeed;
        hasRenderableTrail = Vector3.Distance(previousContactPosition, stableContactPosition) >= GetMinimumTrailDistance();

        lastPosition = currentPosition;
        lastRotation = currentRotation;
        lastLossyScale = currentLossyScale;

        if (planarDirection.sqrMagnitude > 0.0001f)
        {
            stablePlanarDirection = planarDirection.normalized;
        }
    }

    /// <summary>
    /// Tính toán và lọc hướng di chuyển trên mặt phẳng (XZ) dựa trên sự thay đổi vị trí.
    /// Hàm này có logic để xử lý "vùng chết" (dead zone) và làm mượt hướng để tránh rung giật.
    /// </summary>
    /// <param name="delta">Vector thay đổi vị trí so với khung hình trước.</param>
    /// <param name="deltaTime">Thời gian giữa các khung hình.</param>
    /// <param name="currentPlanarSpeed">Tốc độ di chuyển trên mặt phẳng được tính toán (tham số out).</param>
    /// <returns>Vector2 đại diện cho hướng di chuyển trên mặt phẳng đã được lọc và làm mượt.</returns>
    protected virtual Vector2 GetFilteredPlanarDirection(Vector3 delta, float deltaTime, out float currentPlanarSpeed)
    {
        Vector2 planarDelta = new Vector2(delta.x, delta.z);
        float rawDistance = planarDelta.magnitude;
        float rawSpeed = rawDistance / Mathf.Max(deltaTime, 0.0001f);
        float deadZone = 0.0025f;
        float minimumDirectionalSpeed = GetMinimumDirectionalSpeed();
        float speedEnterStationary = Mathf.Max(minimumDirectionalSpeed * 2.75f, 0.12f);
        float speedExitStationary = speedEnterStationary * 1.35f;

        if (rawDistance <= deadZone)
        {
            isStationary = true;
            currentPlanarSpeed = 0f;
            return Vector2.zero;
        }

        isStationary = isStationary ? rawSpeed < speedExitStationary : rawSpeed < speedEnterStationary;
        if (isStationary)
        {
            currentPlanarSpeed = 0f;
            return Vector2.zero;
        }

        Vector2 rawDirection = planarDelta / rawDistance;
        float smoothing = 1f - Mathf.Exp(-18f * deltaTime);
        stablePlanarDirection = Vector2.Lerp(stablePlanarDirection, rawDirection, smoothing);
        if (stablePlanarDirection.sqrMagnitude <= 0.0001f)
        {
            stablePlanarDirection = rawDirection;
        }
        else
        {
            stablePlanarDirection.Normalize();
        }

        currentPlanarSpeed = rawSpeed;
        return stablePlanarDirection;
    }

    /// <summary>
    /// Tính toán vị trí tiếp xúc ổn định (stable contact position) bằng cách làm mượt vị trí hiện tại của đối tượng.
    /// Giúp hiệu ứng tương tác mượt mà hơn, không bị giật khi đối tượng di chuyển không đều.
    /// </summary>
    /// <param name="currentPosition">Vị trí hiện tại của transform.</param>
    /// <param name="currentPlanarSpeed">Tốc độ di chuyển trên mặt phẳng hiện tại.</param>
    /// <param name="deltaTime">Thời gian giữa các khung hình.</param>
    /// <returns>Vị trí tiếp xúc đã được làm mượt.</returns>
    protected virtual Vector3 GetStableContactPosition(Vector3 currentPosition, float currentPlanarSpeed, float deltaTime)
    {
        Vector3 targetPosition = currentPosition;
        float minimumSpeed = GetMinimumDirectionalSpeed();
        if (currentPlanarSpeed < minimumSpeed)
        {
            const float stationaryThreshold = 0.02f;
            Vector2 planarOffset = new Vector2(targetPosition.x - stableContactPosition.x, targetPosition.z - stableContactPosition.z);
            if (planarOffset.magnitude < stationaryThreshold)
            {
                targetPosition.x = stableContactPosition.x;
                targetPosition.z = stableContactPosition.z;
            }
        }

        float followSharpness = currentPlanarSpeed >= minimumSpeed ? 30f : 12f;
        float followT = 1f - Mathf.Exp(-followSharpness * deltaTime);
        return Vector3.Lerp(stableContactPosition - Vector3.up * GetHeightOffset(), targetPosition, followT);
    }

    /// <summary>
    /// Kiểm tra xem transform của đối tượng (vị trí, xoay, tỷ lệ) có thay đổi đáng kể so với khung hình trước không.
    /// </summary>
    /// <param name="currentPosition">Vị trí hiện tại.</param>
    /// <param name="currentRotation">Rotation hiện tại.</param>
    /// <param name="currentLossyScale">Tỷ lệ lossy hiện tại.</param>
    /// <returns>True nếu có sự thay đổi vượt ngưỡng, ngược lại trả về false.</returns>
    protected virtual bool HasRootTransformChanged(Vector3 currentPosition, Quaternion currentRotation, Vector3 currentLossyScale)
    {
        const float positionThreshold = 0.0005f;
        const float scaleThreshold = 0.0005f;
        const float rotationThreshold = 0.05f;

        bool positionChanged = Vector3.SqrMagnitude(currentPosition - lastPosition) > positionThreshold * positionThreshold;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastRotation) > rotationThreshold;
        bool scaleChanged = Vector3.SqrMagnitude(currentLossyScale - lastLossyScale) > scaleThreshold * scaleThreshold;
        return positionChanged || rotationChanged || scaleChanged;
    }

    /// <summary>
    /// Kiểm tra xem một vị trí có nằm trong khoảng cách thu thập (collection distance) so với điểm tập trung hay không.
    /// </summary>
    /// <param name="position">Vị trí cần kiểm tra.</param>
    /// <param name="focusPosition">Vị trí trung tâm (thường là camera hoặc người chơi).</param>
    /// <param name="maxDistance">Khoảng cách tối đa.</param>
    /// <returns>True nếu nằm trong khoảng cách, ngược lại là false.</returns>
    protected static bool IsWithinCollectionDistance(Vector3 position, Vector3 focusPosition, float maxDistance)
    {
        Vector2 delta = new Vector2(position.x - focusPosition.x, position.z - focusPosition.z);
        return delta.sqrMagnitude <= maxDistance * maxDistance;
    }

    /// <summary>
    /// Lấy giá trị delta time một cách an toàn, hoạt động được cả trong Play Mode và Edit Mode.
    /// </summary>
    /// <returns>Time.deltaTime nếu đang chạy, hoặc một giá trị cố định (1/60s) nếu trong Editor.</returns>
    protected static float GetDeltaTime()
    {
        return Application.isPlaying ? Mathf.Max(Time.deltaTime, 0.0001f) : (1f / 60f);
    }

    /// <summary>
    /// Xác định xem tương tác hiện tại có được coi là "đang di chuyển" hay không.
    /// </summary>
    /// <returns>True nếu tốc độ lớn hơn ngưỡng hoặc có vệt (trail) có thể render.</returns>
    protected virtual bool IsMovingInteraction()
    {
        float minimumSpeed = GetMinimumDirectionalSpeed();
        return planarSpeed >= minimumSpeed || hasRenderableTrail;
    }

    /// <summary>
    /// Xác định và trả về cấu hình tương tác (GrassInteractionConfig) sẽ được sử dụng.
    /// Ưu tiên config được gán trực tiếp trên component, nếu không có sẽ lấy config chung từ hệ thống.
    /// </summary>
    /// <returns>Đối tượng GrassInteractionConfig đang hoạt động.</returns>
    protected GrassInteractionConfig ResolveInteractionConfig()
    {
        return interactionConfig != null ? interactionConfig : EnvironmentInteractionSystem.ActiveInteractionConfig;
    }

    /// <summary>
    /// Lấy giá trị độ lệch chiều cao (height offset) từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected float GetHeightOffset()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.heightOffset : heightOffset;
    }

    /// <summary>
    /// Lấy giá trị bán kính vùng tiếp xúc (contact radius) từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected float GetContactRadius()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.contactRadius : contactRadius;
    }

    /// <summary>
    /// Lấy giá trị cường độ vùng tiếp xúc (contact strength) từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected float GetContactStrength()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.contactStrength : contactStrength;
    }

    /// <summary>
    /// Lấy giá trị bán kính vệt di chuyển (trail radius) từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected float GetTrailRadius()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.trailRadius : trailRadius;
    }

    /// <summary>
    /// Lấy giá trị cường độ vệt di chuyển (trail strength) từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected float GetTrailStrength()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.trailStrength : trailStrength;
    }

    /// <summary>
    /// Lấy giá trị khoảng cách tối thiểu để tạo vệt (minimum trail distance) từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected float GetMinimumTrailDistance()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.minimumTrailDistance : minimumTrailDistance;
    }

    /// <summary>
    /// Lấy giá trị xác định có phát tương tác khi đứng yên không, từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected bool GetEmitWhileStationary()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.emitWhileStationary : emitWhileStationary;
    }

    /// <summary>
    /// Lấy giá trị xác định có chặn sự phục hồi của cỏ khi đứng yên không, từ config hoặc từ giá trị mặc định.
    /// </summary>
    protected bool GetSuppressRecoveryWhileStationary()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        return config != null ? config.suppressRecoveryWhileStationary : suppressRecoveryWhileStationary;
    }

    /// <summary>
    /// Lấy giá trị tốc độ tối thiểu để xác định hướng di chuyển, từ config hoặc giá trị mặc định an toàn.
    /// </summary>
    protected float GetMinimumDirectionalSpeed()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return Mathf.Max(config.minimumDirectionalSpeed, 0.0001f);
        }

        return 0.0001f;
    }

    /// <summary>
    /// Lấy giá trị độ mềm (softness) của vùng tiếp xúc từ config hoặc giá trị mặc định.
    /// </summary>
    protected float GetContactSoftness()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return config.contactSoftness;
        }

        return 0.45f;
    }

    /// <summary>
    /// Lấy giá trị ảnh hưởng của hướng (directional influence) cho vùng tiếp xúc từ config hoặc giá trị mặc định.
    /// </summary>
    protected float GetContactDirectionalInfluence()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return config.contactDirectionalInfluence;
        }

        return 0.1f;
    }

    /// <summary>
    /// Lấy giá trị trọng số phục hồi (recovery weight) của vùng tiếp xúc từ config hoặc giá trị mặc định.
    /// </summary>
    protected float GetContactRecoveryWeight()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return config.contactRecoveryWeight;
        }

        return 0.16f;
    }

    /// <summary>
    /// Lấy giá trị độ mềm (softness) của vệt di chuyển từ config hoặc giá trị mặc định.
    /// </summary>
    protected float GetTrailSoftness()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return config.trailSoftness;
        }

        return 0.68f;
    }

    /// <summary>
    /// Lấy giá trị ảnh hưởng của hướng (directional influence) cho vệt di chuyển từ config hoặc giá trị mặc định.
    /// </summary>
    protected float GetTrailDirectionalInfluence()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return config.trailDirectionalInfluence;
        }

        return 0.35f;
    }

    /// <summary>
    /// Lấy giá trị trọng số phục hồi (recovery weight) của vệt di chuyển từ config hoặc giá trị mặc định.
    /// </summary>
    protected float GetTrailRecoveryWeight()
    {
        GrassInteractionConfig config = ResolveInteractionConfig();
        if (config != null)
        {
            return config.trailRecoveryWeight;
        }

        return 0.9f;
    }

    /// <summary>
    /// Callback của Unity để vẽ Gizmos trong Scene view. Chỉ vẽ khi không được chọn và tùy chọn `drawDebugOnlyWhenSelected` là false.
    /// </summary>
    protected virtual void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!drawDebugShapes || drawDebugOnlyWhenSelected)
        {
            return;
        }

        DrawDebugShapesInternal(false);
#endif
    }

    /// <summary>
    /// Callback của Unity để vẽ Gizmos trong Scene view. Chỉ vẽ khi đối tượng được chọn.
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!drawDebugShapes)
        {
            return;
        }

        DrawDebugShapesInternal(true);
#endif
    }

#if UNITY_EDITOR
    private void DrawDebugShapesInternal(bool isSelected)
    {
        Vector3 baseContactPosition = hasLastState ? stableContactPosition : transform.position + Vector3.up * heightOffset;
        Vector3 baseTrailStartPosition = hasLastState ? previousContactPosition : baseContactPosition;
        Vector3 contactPosition = baseContactPosition + Vector3.up * DebugDrawHeightOffset;
        Vector3 trailStartPosition = baseTrailStartPosition + Vector3.up * DebugDrawHeightOffset;
        bool isActive = emitWhileStationary || rootTransformChanged || hasRenderableTrail;

        Color contactColor = GetDebugColor(debugContactColor, isActive, isSelected);
        Color trailColor = GetDebugColor(debugTrailColor, isActive && hasRenderableTrail, isSelected);
        Color velocityColor = GetDebugColor(debugVelocityColor, planarSpeed > 0.0001f, isSelected);

        if (emitContactShape && contactRadius > 0f)
        {
            DrawDisc(contactPosition, contactRadius, contactColor);
        }

        if (emitTrailShape && trailRadius > 0f)
        {
            if (hasRenderableTrail)
            {
                DrawCapsule(trailStartPosition, contactPosition, trailRadius, trailColor);
            }
            else
            {
                DrawDisc(contactPosition, trailRadius, new Color(trailColor.r, trailColor.g, trailColor.b, trailColor.a * 0.35f));
            }
        }

        if (drawDebugVelocity && planarSpeed > 0.0001f)
        {
            Vector3 direction = new Vector3(stablePlanarDirection.x, 0f, stablePlanarDirection.y);
            float arrowLength = Mathf.Clamp(planarSpeed * 0.1f, Mathf.Max(contactRadius, 0.2f), Mathf.Max(contactRadius, trailRadius) * 2.5f);
            DrawArrow(contactPosition, direction, arrowLength, velocityColor);
        }

        if (!drawDebugLabels || !isSelected)
        {
            return;
        }

        Vector3 labelPosition = contactPosition + Vector3.up * Mathf.Max(0.35f, Mathf.Max(contactRadius, trailRadius) * 0.25f);
        string label =
            "Interactor\n" +
            "contact r=" + contactRadius.ToString("0.00") + " s=" + contactStrength.ToString("0.00") + "\n" +
            "trail r=" + trailRadius.ToString("0.00") + " s=" + trailStrength.ToString("0.00") + "\n" +
            "speed=" + planarSpeed.ToString("0.00");
        Handles.Label(labelPosition, label);
    }

    private static Color GetDebugColor(Color baseColor, bool isActive, bool isSelected)
    {
        float alpha = isActive ? baseColor.a : baseColor.a * 0.3f;
        if (!isSelected)
        {
            alpha *= 0.8f;
        }

        return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }

    private static void DrawDisc(Vector3 center, float radius, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Color fillColor = new Color(color.r, color.g, color.b, color.a * 0.08f);
            Handles.color = fillColor;
            Handles.DrawSolidDisc(center, Vector3.up, radius);
        }
    }

    private static void DrawCapsule(Vector3 pointA, Vector3 pointB, float radius, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            Handles.DrawWireDisc(pointA, Vector3.up, radius);
            Handles.DrawWireDisc(pointB, Vector3.up, radius);

            Vector3 axis = pointB - pointA;
            axis.y = 0f;
            if (axis.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            axis.Normalize();
            Vector3 perpendicular = new Vector3(-axis.z, 0f, axis.x) * radius;
            Handles.DrawLine(pointA + perpendicular, pointB + perpendicular);
            Handles.DrawLine(pointA - perpendicular, pointB - perpendicular);

            Color fillColor = new Color(color.r, color.g, color.b, color.a * 0.04f);
            Handles.color = fillColor;
            Vector3[] quad =
            {
                pointA + perpendicular,
                pointB + perpendicular,
                pointB - perpendicular,
                pointA - perpendicular,
            };
            Handles.DrawAAConvexPolygon(quad);
        }
    }

    private static void DrawArrow(Vector3 origin, Vector3 direction, float length, Color color)
    {
        if (direction.sqrMagnitude <= 0.0001f || length <= 0.0001f)
        {
            return;
        }

        using (new Handles.DrawingScope(color))
        {
            Vector3 normalizedDirection = direction.normalized;
            Vector3 end = origin + (normalizedDirection * length);
            Handles.DrawLine(origin, end);
            Handles.ArrowHandleCap(0, end, Quaternion.LookRotation(normalizedDirection), Mathf.Max(0.2f, length * 0.35f), EventType.Repaint);
        }
    }
#endif
}
