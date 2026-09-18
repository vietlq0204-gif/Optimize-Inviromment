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
    [SerializeField, Min(0f)] private float airWakeStrength = 0.2f;
    [SerializeField, Min(0f)] private float wakeLengthPerSpeed = 0.35f;
    [SerializeField, Min(0f)] private float turbulenceStrength = 0.2f;

    private Vector3 previousPosition;
    private Rigidbody cachedRigidbody;

    public static IReadOnlyCollection<GrassInteractionSource> Sources => ActiveSources;
    public Vector3 Velocity { get; private set; }
    public float Radius => radius;
    public float PushStrength => pushStrength;
    public float FlattenStrength => flattenStrength;
    public float AirWakeStrength => airWakeStrength;
    public float WakeLengthPerSpeed => wakeLengthPerSpeed;
    public float TurbulenceStrength => turbulenceStrength;

    public float EffectiveRadius
    {
        get
        {
            float planarSpeed = new Vector2(Velocity.x, Velocity.z).magnitude;
            return radius + planarSpeed * wakeLengthPerSpeed;
        }
    }

    private void OnEnable()
    {
        CacheRigidbody();
        previousPosition = transform.position;
        Velocity = Vector3.zero;
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
        turbulenceStrength = Mathf.Max(0f, turbulenceStrength);
    }

    private Vector3 MeasureVelocity(Vector3 currentPosition, float deltaTime)
    {
        Vector3 transformVelocity = deltaTime > 0.00001f ? (currentPosition - previousPosition) / deltaTime : Vector3.zero;
        if (!Application.isPlaying || cachedRigidbody == null)
        {
            return transformVelocity;
        }

        Vector3 rigidbodyVelocity = cachedRigidbody.linearVelocity;
        return rigidbodyVelocity.sqrMagnitude > 0.000001f ? rigidbodyVelocity : transformVelocity;
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
