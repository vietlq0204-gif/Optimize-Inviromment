using UnityEngine;

[AddComponentMenu("Environment/Main Clock")]
public sealed class MainClock : MonoBehaviour
{
    public const float HoursPerDay = 24f;

    [SerializeField, Range(0f, HoursPerDay)]
    private float initialTimeHours = 12f;

    [SerializeField, Range(0f, HoursPerDay)]
    private float currentTimeHours = 12f;

    [SerializeField, Min(0.01f)]
    private float dayDurationSeconds = 300f;

    [SerializeField]
    private bool isRunning = true;

    [SerializeField]
    private bool resetToInitialTimeOnPlay = true;

    [SerializeField]
    private bool useUnscaledTime;

    public float CurrentTimeHours => WrapHours(currentTimeHours);
    public float NormalizedTime => CurrentTimeHours / HoursPerDay;
    public int CurrentHour => Mathf.FloorToInt(CurrentTimeHours);
    public int CurrentMinute => Mathf.FloorToInt((CurrentTimeHours - CurrentHour) * 60f);
    public float NoonFactor => EvaluateNoonFactor(CurrentTimeHours);
    public string Time24Text => $"{CurrentHour:00}:{CurrentMinute:00}";
    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (resetToInitialTimeOnPlay)
        {
            SetTimeHours(initialTimeHours);
        }
        else
        {
            currentTimeHours = WrapHours(currentTimeHours);
        }
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float hoursPerSecond = HoursPerDay / Mathf.Max(0.01f, dayDurationSeconds);
        AddHours(deltaTime * hoursPerSecond);
    }

    public void SetTimeHours(float hours)
    {
        currentTimeHours = WrapHours(hours);
    }

    public void SetTimeNormalized(float normalizedTime)
    {
        SetTimeHours(Mathf.Clamp01(normalizedTime) * HoursPerDay);
    }

    public void AddHours(float hoursDelta)
    {
        SetTimeHours(currentTimeHours + hoursDelta);
    }

    public void Pause()
    {
        isRunning = false;
    }

    public void Resume()
    {
        isRunning = true;
    }

    public void ToggleRunning()
    {
        isRunning = !isRunning;
    }

    public static float WrapHours(float hours)
    {
        return Mathf.Repeat(hours, HoursPerDay);
    }

    public static float EvaluateNoonFactor(float hours)
    {
        float normalizedTime = WrapHours(hours) / HoursPerDay;
        return 0.5f - (0.5f * Mathf.Cos(normalizedTime * Mathf.PI * 2f));
    }

    private void OnValidate()
    {
        initialTimeHours = WrapHours(initialTimeHours);
        currentTimeHours = WrapHours(currentTimeHours);
        dayDurationSeconds = Mathf.Max(0.01f, dayDurationSeconds);
    }
}
