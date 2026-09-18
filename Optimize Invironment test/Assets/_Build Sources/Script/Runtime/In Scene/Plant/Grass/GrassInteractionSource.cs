using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Grass/Grass Interaction Source")]
public sealed class GrassInteractionSource : MonoBehaviour
{
    private static readonly HashSet<GrassInteractionSource> ActiveSources = new HashSet<GrassInteractionSource>();

    [Header("Contact")]
    [SerializeField, Min(0.01f)] private float radius = 1f;
    [SerializeField, Min(0f)] private float pushStrength = 1f;
    [SerializeField, Min(0f)] private float flattenStrength = 20f;

    [Header("Air Wake")]
    [Tooltip("Độ mạnh vùng nhiễu phía sau vật thể khi vật thể di chuyển nhanh. Tăng để cỏ phía sau bị quạt mạnh hơn; giảm nếu wake quá rõ.")]
    [SerializeField, Min(0f)] private float airWakeStrength = 0.2f;
    [Tooltip("Chiều dài wake tối đa theo tốc độ. Tăng để vật chạy nhanh để lại vệt dài hơn; giảm để wake ngắn hơn.")]
    [SerializeField, Min(0f)] private float wakeLengthPerSpeed = 0.35f;
    [Tooltip("Tốc độ wake dài ra theo quãng đường vật thể đã đi. 1 nghĩa là wake chỉ dài thêm đúng theo khoảng cách vật đã chạy; tăng để vệt dài ra nhanh hơn; giảm để vệt mọc chậm hơn.")]
    [SerializeField, Min(0f)] private float wakeBuildDistanceMultiplier = 1f;
    [Tooltip("Ngưỡng phát hiện rẽ gấp để reset wake. Cao hơn sẽ reset với góc rẽ nhỏ hơn; thấp hơn chỉ reset khi đổi hướng rất mạnh. 0.7 gần tương đương rẽ hơn khoảng 45 độ.")]
    [SerializeField, Range(-1f, 1f)] private float wakeDirectionResetDot = 0.7f;
    [Tooltip("Độ rộng phần đầu wake ngay sau vật thể, tính theo Radius. Tăng để wake bắt đầu rộng hơn; giảm để đầu wake hẹp như một đường thẳng.")]
    [SerializeField, Min(0f)] private float wakeHeadWidthRadiusScale = 0.35f;
    [Tooltip("Độ rộng phần đuôi wake khi đã mở hết, tính theo Radius. Tăng để đáy hình nón rộng hơn; giảm để wake hẹp hơn.")]
    [SerializeField, Min(0f)] private float wakeTailWidthRadiusScale = 3.25f;
    [Tooltip("Độ mềm/mờ hai bên mép wake, tính theo tỉ lệ độ rộng hiện tại. Tăng để mép wake mờ rộng hơn; giảm để mép sắc hơn.")]
    [SerializeField, Range(0.01f, 1f)] private float wakeEdgeFeather = 0.22f;
    [Tooltip("Vị trí dọc wake bắt đầu mở rộng thành hình nón. Tăng để wake giữ dạng đường thẳng lâu hơn rồi mới mở; giảm để mở rộng sớm hơn.")]
    [SerializeField, Range(0f, 1f)] private float wakeConeStart = 0.12f;
    [Tooltip("Độ mạnh nhiễu loạn ngẫu nhiên trong wake. Tăng để cỏ trong vệt rung hỗn loạn hơn; giảm để wake mượt hơn.")]
    [SerializeField, Min(0f)] private float turbulenceStrength = 0.2f;

    private Vector3 previousPosition;
    private Rigidbody cachedRigidbody;
    private Vector2 previousWakeDirection;
    private bool hasWakeDirection;

    public static IReadOnlyCollection<GrassInteractionSource> Sources => ActiveSources;
    public Vector3 Velocity { get; private set; }
    public float Radius => radius;
    public float PushStrength => pushStrength;
    public float FlattenStrength => flattenStrength;
    public float AirWakeStrength => airWakeStrength;
    public float WakeLengthPerSpeed => wakeLengthPerSpeed;
    public float WakeHeadWidthRadiusScale => wakeHeadWidthRadiusScale;
    public float WakeTailWidthRadiusScale => wakeTailWidthRadiusScale;
    public float WakeEdgeFeather => wakeEdgeFeather;
    public float WakeConeStart => wakeConeStart;
    public float CurrentWakeLength { get; private set; }
    public float TurbulenceStrength => turbulenceStrength;

    public float EffectiveRadius
    {
        get
        {
            return radius + CurrentWakeLength;
        }
    }

    private void OnEnable()
    {
        CacheRigidbody();
        previousPosition = transform.position;
        Velocity = Vector3.zero;
        CurrentWakeLength = 0f;
        hasWakeDirection = false;
        ActiveSources.Add(this);
    }

    private void OnDisable()
    {
        ActiveSources.Remove(this);
    }

    private void Update()
    {
        Vector3 currentPosition = transform.position;
        float deltaTime = Application.isPlaying ? Time.deltaTime : 0f;
        Velocity = MeasureVelocity(currentPosition, deltaTime);
        UpdateWakeLength(deltaTime);
        previousPosition = currentPosition;
    }

    private void OnTransformParentChanged()
    {
        CacheRigidbody();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        pushStrength = Mathf.Max(0f, pushStrength);
        flattenStrength = Mathf.Max(0f, flattenStrength);
        airWakeStrength = Mathf.Max(0f, airWakeStrength);
        wakeLengthPerSpeed = Mathf.Max(0f, wakeLengthPerSpeed);
        wakeBuildDistanceMultiplier = Mathf.Max(0f, wakeBuildDistanceMultiplier);
        wakeDirectionResetDot = Mathf.Clamp(wakeDirectionResetDot, -1f, 1f);
        wakeHeadWidthRadiusScale = Mathf.Max(0f, wakeHeadWidthRadiusScale);
        wakeTailWidthRadiusScale = Mathf.Max(0f, wakeTailWidthRadiusScale);
        wakeEdgeFeather = Mathf.Clamp(wakeEdgeFeather, 0.01f, 1f);
        wakeConeStart = Mathf.Clamp01(wakeConeStart);
        turbulenceStrength = Mathf.Max(0f, turbulenceStrength);
    }

    private void UpdateWakeLength(float deltaTime)
    {
        float planarSpeed = new Vector2(Velocity.x, Velocity.z).magnitude;
        if (!Application.isPlaying || deltaTime <= 0.00001f || planarSpeed <= 0.05f)
        {
            CurrentWakeLength = 0f;
            hasWakeDirection = false;
            return;
        }

        Vector2 wakeDirection = new Vector2(Velocity.x, Velocity.z) / planarSpeed;
        if (hasWakeDirection)
        {
            float directionAlignment = Vector2.Dot(previousWakeDirection, wakeDirection);
            if (directionAlignment < wakeDirectionResetDot)
            {
                CurrentWakeLength = 0f;
            }
        }

        float targetWakeLength = planarSpeed * wakeLengthPerSpeed;
        float travelledDistance = planarSpeed * deltaTime * wakeBuildDistanceMultiplier;
        CurrentWakeLength = Mathf.Min(targetWakeLength, CurrentWakeLength + travelledDistance);
        previousWakeDirection = wakeDirection;
        hasWakeDirection = true;
    }

    private Vector3 MeasureVelocity(Vector3 currentPosition, float deltaTime)
    {
        Vector3 transformVelocity = deltaTime > 0.00001f ? (currentPosition - previousPosition) / deltaTime : Vector3.zero;
        if (!Application.isPlaying || cachedRigidbody == null)
        {
            return transformVelocity;
        }

        Vector3 rigidbodyVelocity = cachedRigidbody.linearVelocity;
        Vector2 rigidbodyPlanarVelocity = new Vector2(rigidbodyVelocity.x, rigidbodyVelocity.z);
        return rigidbodyPlanarVelocity.sqrMagnitude > 0.000001f ? rigidbodyVelocity : transformVelocity;
    }

    private void CacheRigidbody()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.7f);
        DrawCircle(transform.position, radius);

        if (Application.isPlaying && Velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 planarVelocity = new Vector3(Velocity.x, 0f, Velocity.z);
            Vector3 wakeEnd = transform.position - planarVelocity.normalized * Mathf.Max(0f, EffectiveRadius - radius);
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.7f);
            Gizmos.DrawLine(transform.position, wakeEnd);
            DrawCircle(wakeEnd, radius * 0.5f);
        }
    }

    private static void DrawCircle(Vector3 center, float circleRadius)
    {
        const int Segments = 40;
        Vector3 previous = center + new Vector3(circleRadius, 0f, 0f);
        for (int i = 1; i <= Segments; i++)
        {
            float angle = (i / (float)Segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
