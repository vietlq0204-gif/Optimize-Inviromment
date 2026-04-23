using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
[VolumeComponentMenu("Post-processing/Sun Shafts")]
[VolumeRequiresRendererFeatures(typeof(SunShaftsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "Sun Shafts")]
public sealed class SunShaftsVolume : VolumeComponent, IPostProcessComponent
{
    public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
    public ColorParameter tint = new ColorParameter(new Color(1.0f, 0.86f, 0.62f, 1f), true, false, true);
    public ClampedIntParameter sampleCount = new ClampedIntParameter(24, 4, 64);
    public ClampedFloatParameter blurRadius = new ClampedFloatParameter(1f, 0.05f, 1.5f);
    public ClampedFloatParameter maxRadius = new ClampedFloatParameter(1.1f, 0.1f, 2f);
    public ClampedFloatParameter decay = new ClampedFloatParameter(0.94f, 0.5f, 1f);
    public ClampedFloatParameter weight = new ClampedFloatParameter(0.22f, 0f, 1f);
    public ClampedFloatParameter exposure = new ClampedFloatParameter(0.75f, 0f, 3f);
    public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.985f, 0.8f, 1f);
    public ClampedFloatParameter colorThreshold = new ClampedFloatParameter(0.55f, 0f, 1f);
    public ClampedFloatParameter colorInfluence = new ClampedFloatParameter(0.65f, 0f, 1f);
    public ClampedFloatParameter radialFalloff = new ClampedFloatParameter(1.35f, 0.25f, 4f);
    public ClampedFloatParameter edgeFade = new ClampedFloatParameter(0.75f, 0f, 1f);

    public bool IsActive()
    {
        return active && intensity.value > 0f && sampleCount.value > 0;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
