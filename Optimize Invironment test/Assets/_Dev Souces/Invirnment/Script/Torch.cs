using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Environment/Torch")]
public sealed class Torch : MonoBehaviour
{
    [SerializeField]
    private MainClock clock;

    [SerializeField]
    private Light targetLight;

    [SerializeField]
    private ParticleSystem[] flameParticles;

    [SerializeField]
    private bool followDayNightCycle = true;

    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float turnOnHour = 18.5f;

    [SerializeField, Range(0f, MainClock.HoursPerDay)]
    private float turnOffHour = 5f;

    [SerializeField, Min(0f)]
    private float transitionHours = 0.75f;

    [SerializeField, Min(0f)]
    private float litIntensity = 6f;

    [SerializeField, Min(0f)]
    private float litRange = 8f;

    [SerializeField, Range(0f, 1f)]
    private float intensityFlickerAmplitude = 0.18f;

    [SerializeField, Range(0f, 1f)]
    private float rangeFlickerAmplitude = 0.08f;

    [SerializeField, Min(0f)]
    private float flickerSpeed = 7f;

    [SerializeField]
    private Color torchColor = new(1f, 0.55f, 0.22f, 1f);

    [SerializeField]
    private bool useColorTemperature = true;

    [SerializeField, Min(1000f)]
    private float colorTemperature = 1800f;

    [SerializeField]
    private int noiseSeed = 17;

    private bool particlesPlaying;

    private void Reset()
    {
        targetLight = GetComponentInChildren<Light>(true);
        flameParticles = GetComponentsInChildren<ParticleSystem>(true);
        AssignClockIfMissing();
        CaptureLightDefaults();
    }

    private void OnEnable()
    {
        ApplyCurrentState();
    }

    private void OnDisable()
    {
        if (targetLight != null)
        {
            targetLight.enabled = false;
        }

        SetParticlesPlaying(false);
    }

    private void OnValidate()
    {
        turnOnHour = MainClock.WrapHours(turnOnHour);
        turnOffHour = MainClock.WrapHours(turnOffHour);
        transitionHours = Mathf.Max(0f, transitionHours);
        litIntensity = Mathf.Max(0f, litIntensity);
        litRange = Mathf.Max(0f, litRange);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);
        colorTemperature = Mathf.Max(1000f, colorTemperature);

        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>(true);
        }

        if (flameParticles == null || flameParticles.Length == 0)
        {
            flameParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        AssignClockIfMissing();
        ApplyCurrentState();
    }

    private void LateUpdate()
    {
        ApplyCurrentState();
    }

    public void ApplyCurrentState()
    {
        if (!TryAssignReferences())
        {
            return;
        }

        float activeFactor = EvaluateActiveFactor();
        bool isActive = activeFactor > 0.0001f;

        if (!isActive)
        {
            targetLight.enabled = false;
            SetParticlesPlaying(false);
            return;
        }

        float timeValue = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float intensityNoise = EvaluateFlickerNoise(timeValue, 0.37f);
        float rangeNoise = EvaluateFlickerNoise(timeValue, 1.91f);

        targetLight.enabled = true;
        targetLight.color = torchColor;
        targetLight.useColorTemperature = useColorTemperature;

        if (useColorTemperature)
        {
            targetLight.colorTemperature = colorTemperature;
        }

        float intensityMultiplier = Mathf.Lerp(1f - intensityFlickerAmplitude, 1f + intensityFlickerAmplitude, intensityNoise);
        float rangeMultiplier = Mathf.Lerp(1f - rangeFlickerAmplitude, 1f + rangeFlickerAmplitude, rangeNoise);

        targetLight.intensity = litIntensity * activeFactor * intensityMultiplier;
        targetLight.range = litRange * activeFactor * rangeMultiplier;

        SetParticlesPlaying(true);
    }

    private float EvaluateActiveFactor()
    {
        if (!followDayNightCycle)
        {
            return 1f;
        }

        if (clock == null)
        {
            return 1f;
        }

        const float epsilon = 0.0001f;

        float nightStartHour = MainClock.WrapHours(turnOnHour);
        float nightEndHour = MainClock.WrapHours(turnOffHour);
        float nightDuration = GetForwardHours(nightStartHour, nightEndHour);

        if (nightDuration <= epsilon)
        {
            return 1f;
        }

        float progressIntoNight = GetForwardHours(nightStartHour, clock.CurrentTimeHours);
        if (progressIntoNight >= nightDuration)
        {
            return 0f;
        }

        float effectiveTransition = Mathf.Min(Mathf.Max(0f, transitionHours), nightDuration * 0.5f);
        if (effectiveTransition <= epsilon)
        {
            return 1f;
        }

        if (progressIntoNight < effectiveTransition)
        {
            return Mathf.SmoothStep(0f, 1f, progressIntoNight / effectiveTransition);
        }

        float remainingNight = nightDuration - progressIntoNight;
        if (remainingNight < effectiveTransition)
        {
            return Mathf.SmoothStep(0f, 1f, remainingNight / effectiveTransition);
        }

        return 1f;
    }

    private float EvaluateFlickerNoise(float timeValue, float offset)
    {
        float sampleTime = timeValue * flickerSpeed;
        float seedOffset = noiseSeed * 0.173f;
        float primary = Mathf.PerlinNoise(sampleTime + seedOffset, offset + seedOffset);
        float secondary = Mathf.PerlinNoise((sampleTime * 1.91f) + 9.13f + seedOffset, offset + 3.71f + seedOffset);
        return Mathf.Lerp(primary, secondary, 0.35f);
    }

    private static float GetForwardHours(float fromHour, float toHour)
    {
        return Mathf.Repeat(toHour - fromHour, MainClock.HoursPerDay);
    }

    private bool TryAssignReferences()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>(true);
        }

        if (flameParticles == null || flameParticles.Length == 0)
        {
            flameParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        AssignClockIfMissing();
        return targetLight != null;
    }

    private void AssignClockIfMissing()
    {
        if (clock == null)
        {
            clock = FindAnyObjectByType<MainClock>();
        }
    }

    private void CaptureLightDefaults()
    {
        if (targetLight == null)
        {
            return;
        }

        litIntensity = Mathf.Max(0f, targetLight.intensity);
        litRange = Mathf.Max(0f, targetLight.range);
        torchColor = targetLight.color;

        if (targetLight.useColorTemperature)
        {
            useColorTemperature = true;
            colorTemperature = Mathf.Max(1000f, targetLight.colorTemperature);
        }
    }

    private void SetParticlesPlaying(bool shouldPlay)
    {
        if (flameParticles == null || flameParticles.Length == 0)
        {
            return;
        }

        if (particlesPlaying == shouldPlay)
        {
            return;
        }

        particlesPlaying = shouldPlay;

        for (int i = 0; i < flameParticles.Length; i++)
        {
            ParticleSystem particle = flameParticles[i];
            if (particle == null)
            {
                continue;
            }

            if (!Application.isPlaying)
            {
                var emission = particle.emission;
                emission.enabled = shouldPlay;
                continue;
            }

            if (shouldPlay)
            {
                if (!particle.isPlaying)
                {
                    particle.Play(true);
                }
            }
            else if (particle.isPlaying)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
