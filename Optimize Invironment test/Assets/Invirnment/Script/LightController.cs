using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
[AddComponentMenu("Environment/Light Controller")]
public sealed class LightController : MonoBehaviour
{
    [SerializeField]
    private MainClock clock;

    [SerializeField]
    private Light targetLight;

    [SerializeField, Min(0f)]
    private float minIntensity = 0f;

    [SerializeField, Min(0f)]
    private float maxIntensity = 1f;

    [Header("Intensity Schedule")]
    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float maxIntensityHour = 12f;

    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float reachMinIntensityHour = 19f;

    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float leaveMinIntensityHour = 4.5f;

    private void Reset()
    {
        targetLight = GetComponent<Light>();
        AssignClockIfMissing();
    }

    private void OnEnable()
    {
        ApplyCurrentTime();
    }

    private void OnValidate()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }

        if (maxIntensity < minIntensity)
        {
            maxIntensity = minIntensity;
        }

        maxIntensityHour = MainClock.WrapHours(maxIntensityHour);
        reachMinIntensityHour = MainClock.WrapHours(reachMinIntensityHour);
        leaveMinIntensityHour = MainClock.WrapHours(leaveMinIntensityHour);

        AssignClockIfMissing();
        ApplyCurrentTime();
    }

    private void LateUpdate()
    {
        ApplyCurrentTime();
    }

    public void ApplyCurrentTime()
    {
        if (!TryAssignReferences())
        {
            return;
        }

        float intensityFactor = EvaluateIntensityFactor(clock.CurrentTimeHours);
        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, intensityFactor);
    }

    private float EvaluateIntensityFactor(float hours)
    {
        const float minimumSegmentHours = 0.0001f;

        hours = MainClock.WrapHours(hours);

        float dayStartHour = MainClock.WrapHours(leaveMinIntensityHour);
        float dayEndHour = MainClock.WrapHours(reachMinIntensityHour);
        float peakHour = MainClock.WrapHours(maxIntensityHour);

        float daylightDuration = GetForwardHours(dayStartHour, dayEndHour);
        if (daylightDuration <= minimumSegmentHours * 2f)
        {
            return 0f;
        }

        float progressIntoDay = GetForwardHours(dayStartHour, hours);
        if (progressIntoDay >= daylightDuration)
        {
            return 0f;
        }

        float peakProgress = Mathf.Clamp(
            GetForwardHours(dayStartHour, peakHour),
            minimumSegmentHours,
            daylightDuration - minimumSegmentHours);

        if (progressIntoDay <= peakProgress)
        {
            return progressIntoDay / peakProgress;
        }

        return 1f - ((progressIntoDay - peakProgress) / (daylightDuration - peakProgress));
    }

    private static float GetForwardHours(float fromHour, float toHour)
    {
        return Mathf.Repeat(toHour - fromHour, MainClock.HoursPerDay);
    }

    private bool TryAssignReferences()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }

        AssignClockIfMissing();
        return targetLight != null && clock != null;
    }

    private void AssignClockIfMissing()
    {
        if (clock == null)
        {
            clock = FindAnyObjectByType<MainClock>();
        }
    }
}
