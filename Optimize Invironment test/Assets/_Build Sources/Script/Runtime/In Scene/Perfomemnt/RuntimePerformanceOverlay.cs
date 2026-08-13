using System.Globalization;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class RuntimePerformanceOverlay : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool visible = true;
#if ENABLE_LEGACY_INPUT_MANAGER
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
#endif
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key toggleKeyInputSystem = Key.F3;
#endif
    [SerializeField] private Vector2 screenOffset = new(16f, 16f);
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.45f);

    [Header("Sampling")]
    [SerializeField] private float refreshInterval = 0.2f;
    [SerializeField] private int maxFrameTimings = 4;

    private readonly FrameTiming[] frameTimings = new FrameTiming[8];

    private float nextRefreshTime;
    private float smoothedFps;
    private float smoothedCpuFrameTimeMs;
    private float smoothedGpuFrameTimeMs;
    private float smoothedMainThreadPercent;
    private float smoothedRenderThreadPercent;

    private GUIStyle labelStyle;
    private Texture2D backgroundTexture;
    private Rect layoutRect;

    private void Awake()
    {
        // QualitySettings.vSyncCount = 0;
        // Application.targetFrameRate = 144;
        maxFrameTimings = Mathf.Clamp(maxFrameTimings, 1, frameTimings.Length);
        FrameTimingManager.CaptureFrameTimings(); 
        

    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            visible = !visible;
        }

        if (!visible)
        {
            FrameTimingManager.CaptureFrameTimings();
            return;
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            FrameTimingManager.CaptureFrameTimings();
            return;
        }

        RefreshMetrics();
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        FrameTimingManager.CaptureFrameTimings();
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[toggleKeyInputSystem].wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleKey);
#else
        return false;
#endif
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        EnsureGuiResources();

        string fpsText = FormatFps(smoothedFps);
        string cpuFrameTimeText = FormatMilliseconds(smoothedCpuFrameTimeMs);
        string gpuFrameTimeText = smoothedGpuFrameTimeMs > 0.01f
            ? FormatMilliseconds(smoothedGpuFrameTimeMs)
            : "n/a";
        string threadText = FormatThreadSplit(smoothedMainThreadPercent, smoothedRenderThreadPercent);

        string content =
            $"FPS : {fpsText}\n" +
            $"Global Frametime : {cpuFrameTimeText}\n" +
            $"GPU Frametime : {gpuFrameTimeText}\n" +
            $"CPU Main Thread vs Render Thread : {threadText}";

        Vector2 size = labelStyle.CalcSize(new GUIContent(content));
        layoutRect = new Rect(
            screenOffset.x,
            screenOffset.y,
            size.x + 24f,
            size.y + 18f);

        Color previousColor = GUI.color;
        GUI.color = backgroundColor;
        GUI.DrawTexture(layoutRect, backgroundTexture);
        GUI.color = previousColor;

        Rect labelRect = new Rect(
            layoutRect.x + 12f,
            layoutRect.y + 9f,
            layoutRect.width - 24f,
            layoutRect.height - 18f);
        GUI.Label(labelRect, content, labelStyle);
    }

    private void RefreshMetrics()
    {
        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float currentFps = 1f / deltaTime;
        float currentCpuFrameTimeMs = deltaTime * 1000f;

        uint capturedCount = FrameTimingManager.GetLatestTimings((uint)maxFrameTimings, frameTimings);
        if (capturedCount > 0)
        {
            double totalMainThreadMs = 0d;
            double totalRenderThreadMs = 0d;
            double totalGpuMs = 0d;
            int gpuSampleCount = 0;

            for (int i = 0; i < capturedCount; i++)
            {
                FrameTiming frameTiming = frameTimings[i];
                totalMainThreadMs += frameTiming.cpuMainThreadFrameTime;
                totalRenderThreadMs += frameTiming.cpuRenderThreadFrameTime;
                if (frameTiming.gpuFrameTime > 0d)
                {
                    totalGpuMs += frameTiming.gpuFrameTime;
                    gpuSampleCount++;
                }
            }

            currentCpuFrameTimeMs = (float)((totalMainThreadMs + totalRenderThreadMs) / capturedCount);

            if (gpuSampleCount > 0)
            {
                smoothedGpuFrameTimeMs = (float)(totalGpuMs / gpuSampleCount);
            }

            float totalCpuMs = (float)(totalMainThreadMs + totalRenderThreadMs);
            if (totalCpuMs > 0.001f)
            {
                smoothedMainThreadPercent = Mathf.Clamp01((float)totalMainThreadMs / totalCpuMs) * 100f;
                smoothedRenderThreadPercent = Mathf.Clamp01((float)totalRenderThreadMs / totalCpuMs) * 100f;
            }
        }
        else
        {
            smoothedMainThreadPercent = 100f;
            smoothedRenderThreadPercent = 0f;
        }

        smoothedFps = currentFps;
        smoothedCpuFrameTimeMs = currentCpuFrameTimeMs;
    }

    private void EnsureGuiResources()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
            };
        }

        labelStyle.fontSize = fontSize;
        labelStyle.normal.textColor = textColor;

        if (backgroundTexture != null)
        {
            return;
        }

        backgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        backgroundTexture.SetPixel(0, 0, Color.white);
        backgroundTexture.Apply(false, true);
    }

    private static string FormatFps(float fps)
    {
        return fps.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatMilliseconds(float valueMs)
    {
        return valueMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
    }

    private static string FormatThreadSplit(float mainThreadPercent, float renderThreadPercent)
    {
        return mainThreadPercent.ToString("0.0", CultureInfo.InvariantCulture) + "% vs " +
            renderThreadPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null)
        {
            Destroy(backgroundTexture);
            backgroundTexture = null;
        }
    }
}
