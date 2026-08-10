using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
[AddComponentMenu("Environment/Light Controller")]
public sealed class LightController : MonoBehaviour
{
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

        float intensityFactor = EvaluateIntensityFactor(clock.CurrentTimeHours);
        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, intensityFactor);
    }

    /// <summary>
    /// Đánh giá hệ số cường độ dựa trên giờ đã cho.
    /// </summary>
    /// <param name="hours">Giờ hiện tại.</param>
    /// <returns>Hệ số cường độ.</returns>
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
