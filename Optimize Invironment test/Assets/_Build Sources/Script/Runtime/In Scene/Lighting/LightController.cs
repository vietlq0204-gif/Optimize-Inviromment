using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
[AddComponentMenu("Environment/Light Controller")]
public sealed class LightController : MonoBehaviour
{
    private enum EnvironmentIntensityPhase
    {
        Night,
        Sunrise,
        Day,
        Sunset
    }

    /// <summary>
    /// Đồng hồ chính được sử dụng để lấy thời gian hiện tại.
    /// </summary>
    [SerializeField]
    private MainClock clock;

    /// <summary>
    /// Thành phần ánh sáng để điều khiển.
    /// </summary>
    [SerializeField]
    private Light targetLight;

    /// <summary>
    /// Cường độ tối thiểu của ánh sáng.
    /// </summary>
    [SerializeField, Min(0f)]
    private float minIntensity = 0f;

    /// <summary>
    /// Cường độ tối đa của ánh sáng.
    /// </summary>
    [SerializeField, Min(0f)]
    private float maxIntensity = 1f;

    [SerializeField, Range(0f, 1f)]
    private float maxIntensityMultiplier = 0.5f;

    [SerializeField, Range(0f, 1f)]
    private float intensityMultiplier = 0.5f;

    [SerializeField]
    private bool overrideEnvironmentIntensityMultiplier = true;

    [SerializeField]
    private bool snapEnvironmentIntensityMultiplier = true;

    [SerializeField, Range(0f, 1f)]
    private float sunriseEnvironmentIntensityMultiplier = 0.2f;

    [SerializeField, Range(0f, 1f)]
    private float dayEnvironmentIntensityMultiplier = 0.5f;

    [SerializeField, Range(0f, 1f)]
    private float sunsetEnvironmentIntensityMultiplier = 0.2f;

    [SerializeField, Range(0f, 1f)]
    private float nightEnvironmentIntensityMultiplier = 0f;

    [SerializeField]
    private bool useColorTemperature = true;

    [SerializeField, Min(1000f)]
    private float dayColorTemperature = 6570f;

    [SerializeField, Min(1000f)]
    private float sunriseColorTemperature = 3600f;

    [SerializeField, Min(1000f)]
    private float sunsetColorTemperature = 3200f;

    [SerializeField, Min(0f)]
    private float twilightBlendRangeHours = 1.5f;
    
    /// <summary>
    /// Mốc thời gian 'Giờ' mà ánh sáng đạt cường độ tối đa (giữa trưa)
    /// </summary>
    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float maxIntensityHour = 12f;

    /// <summary>
    /// Mốc thời gian 'Giờ' mà ánh sáng bắt đầu giảm dần về cường độ tối thiểu. (hoàng hôn)
    /// </summary>
    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float reachMinIntensityHour = 19f;

    /// <summary>
    /// Mốc thời gian 'Giờ' mà ánh sáng bắt đầu tăng dần từ cường độ tối thiểu. (bình minh)
    /// </summary>
    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float leaveMinIntensityHour = 4.5f;

    public float IntensityMultiplier => intensityMultiplier;

    /// <summary>
    /// Đặt lại thành phần về trạng thái mặc định.
    /// </summary>
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
        maxIntensityMultiplier = Mathf.Clamp01(maxIntensityMultiplier);
        intensityMultiplier = Mathf.Clamp(intensityMultiplier, 0f, maxIntensityMultiplier);
        sunriseEnvironmentIntensityMultiplier = Mathf.Clamp(sunriseEnvironmentIntensityMultiplier, 0f, maxIntensityMultiplier);
        dayEnvironmentIntensityMultiplier = Mathf.Clamp(dayEnvironmentIntensityMultiplier, 0f, maxIntensityMultiplier);
        sunsetEnvironmentIntensityMultiplier = Mathf.Clamp(sunsetEnvironmentIntensityMultiplier, 0f, maxIntensityMultiplier);
        nightEnvironmentIntensityMultiplier = Mathf.Clamp(nightEnvironmentIntensityMultiplier, 0f, maxIntensityMultiplier);
        dayColorTemperature = Mathf.Max(1000f, dayColorTemperature);
        sunriseColorTemperature = Mathf.Max(1000f, sunriseColorTemperature);
        sunsetColorTemperature = Mathf.Max(1000f, sunsetColorTemperature);
        twilightBlendRangeHours = Mathf.Max(0f, twilightBlendRangeHours);

        AssignClockIfMissing();
        ApplyCurrentTime();
    }
    
    private void LateUpdate()
    {
        ApplyCurrentTime();
    }

    /// <summary>
    /// Áp dụng cường độ ánh sáng dựa trên thời gian hiện tại.
    /// </summary>
    public void ApplyCurrentTime()
    {
        if (!TryAssignReferences())
        {
            return;
        }

        float currentHours = clock.CurrentTimeHours;
        intensityMultiplier = EvaluateIntensityMultiplier(currentHours) * maxIntensityMultiplier;
        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, intensityMultiplier);

        if (overrideEnvironmentIntensityMultiplier)
        {
            ApplyEnvironmentLightingIntensity(currentHours);
        }

        targetLight.useColorTemperature = useColorTemperature;

        if (useColorTemperature)
        {
            targetLight.colorTemperature = EvaluateColorTemperature(currentHours);
        }
    }

    /// <summary>
    /// Đánh giá hệ số cường độ dựa trên giờ đã cho.
    /// </summary>
    /// <param name="hours">Giờ hiện tại.</param>
    /// <returns>Hệ số cường độ.</returns>
    private float EvaluateIntensityMultiplier(float hours)
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
            return Mathf.SmoothStep(0f, 1f, progressIntoDay / peakProgress);
        }

        float progressAfterPeak = (progressIntoDay - peakProgress) / (daylightDuration - peakProgress);
        return Mathf.SmoothStep(1f, 0f, progressAfterPeak);
    }

    private void ApplyEnvironmentLightingIntensity(float hours)
    {
        float environmentIntensityMultiplier = snapEnvironmentIntensityMultiplier
            ? EvaluateSnappedEnvironmentIntensityMultiplier(hours)
            : intensityMultiplier;

        if (Mathf.Approximately(RenderSettings.ambientIntensity, environmentIntensityMultiplier))
        {
            return;
        }

        RenderSettings.ambientIntensity = environmentIntensityMultiplier;
        DynamicGI.UpdateEnvironment();
    }

    private float EvaluateSnappedEnvironmentIntensityMultiplier(float hours)
    {
        switch (EvaluateEnvironmentIntensityPhase(hours))
        {
            case EnvironmentIntensityPhase.Sunrise:
                return sunriseEnvironmentIntensityMultiplier;
            case EnvironmentIntensityPhase.Day:
                return dayEnvironmentIntensityMultiplier;
            case EnvironmentIntensityPhase.Sunset:
                return sunsetEnvironmentIntensityMultiplier;
            default:
                return nightEnvironmentIntensityMultiplier;
        }
    }

    private EnvironmentIntensityPhase EvaluateEnvironmentIntensityPhase(float hours)
    {
        const float minimumSegmentHours = 0.0001f;

        hours = MainClock.WrapHours(hours);

        float dayStartHour = MainClock.WrapHours(leaveMinIntensityHour);
        float dayEndHour = MainClock.WrapHours(reachMinIntensityHour);
        float peakHour = MainClock.WrapHours(maxIntensityHour);
        float daylightDuration = GetForwardHours(dayStartHour, dayEndHour);

        if (daylightDuration <= minimumSegmentHours * 2f)
        {
            return EnvironmentIntensityPhase.Night;
        }

        float progressIntoDay = GetForwardHours(dayStartHour, hours);
        if (progressIntoDay >= daylightDuration)
        {
            return EnvironmentIntensityPhase.Night;
        }

        float peakProgress = Mathf.Clamp(
            GetForwardHours(dayStartHour, peakHour),
            minimumSegmentHours,
            daylightDuration - minimumSegmentHours);
        float sunriseEndProgress = peakProgress * 0.5f;
        float sunsetStartProgress = peakProgress + ((daylightDuration - peakProgress) * 0.5f);

        if (progressIntoDay <= sunriseEndProgress)
        {
            return EnvironmentIntensityPhase.Sunrise;
        }

        if (progressIntoDay >= sunsetStartProgress)
        {
            return EnvironmentIntensityPhase.Sunset;
        }

        return EnvironmentIntensityPhase.Day;
    }

    private float EvaluateColorTemperature(float hours)
    {
        float sunriseBlend = EvaluateTwilightBlend(hours, leaveMinIntensityHour);
        float sunsetBlend = EvaluateTwilightBlend(hours, reachMinIntensityHour);
        float dayWeight = Mathf.Max(0f, 1f - Mathf.Clamp01(sunriseBlend + sunsetBlend));
        float totalWeight = dayWeight + sunriseBlend + sunsetBlend;

        if (totalWeight <= 0.0001f)
        {
            return dayColorTemperature;
        }

        return ((dayColorTemperature * dayWeight)
            + (sunriseColorTemperature * sunriseBlend)
            + (sunsetColorTemperature * sunsetBlend)) / totalWeight;
    }

    private float EvaluateTwilightBlend(float hours, float targetHour)
    {
        if (twilightBlendRangeHours <= 0.0001f)
        {
            return 0f;
        }

        float wrappedHours = MainClock.WrapHours(hours);
        float distance = GetShortestHourDistance(wrappedHours, MainClock.WrapHours(targetHour));
        float proximity = Mathf.Clamp01(1f - (distance / twilightBlendRangeHours));
        return Mathf.SmoothStep(0f, 1f, proximity);
    }

    /// <summary>
    /// Tính toán số giờ tiến từ một giờ đã cho đến một giờ khác.
    /// </summary>
    /// <param name="fromHour">Giờ bắt đầu.</param>
    /// <param name="toHour">Giờ kết thúc.</param>
    /// <returns>Số giờ.</returns>
    private static float GetForwardHours(float fromHour, float toHour)
    {
        return Mathf.Repeat(toHour - fromHour, MainClock.HoursPerDay);
    }

    private static float GetShortestHourDistance(float fromHour, float toHour)
    {
        float forward = GetForwardHours(fromHour, toHour);
        return Mathf.Min(forward, MainClock.HoursPerDay - forward);
    }

    /// <summary>
    /// Cố gắng gán các tham chiếu cần thiết nếu chúng bị thiếu.
    /// </summary>
    /// <returns>True nếu tất cả các tham chiếu được gán, ngược lại là false.</returns>
    private bool TryAssignReferences()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }

        AssignClockIfMissing();
        return targetLight != null && clock != null;
    }

    /// <summary>
    /// Gán tham chiếu đồng hồ nếu nó bị thiếu.
    /// </summary>
    private void AssignClockIfMissing()
    {
        if (clock == null)
        {
            clock = FindAnyObjectByType<MainClock>();
        }
    }
}
