using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Sun Shafts")]
public sealed class SunShaftsRendererFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Vit/SunShaftsFullscreen";

    [SerializeField]
    private Shader shader;

    [SerializeField]
    private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

    [SerializeField]
    private bool renderInSceneView = true;

    private Material material;
    private SunShaftsPass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find(ShaderName);
        }

        if (material != null && material.shader != shader)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        if (material == null && shader != null)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        pass ??= new SunShaftsPass();
        pass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null)
        {
            return;
        }

        Camera camera = renderingData.cameraData.camera;
        if (camera == null ||
            renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection ||
            UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
        {
            return;
        }

        if (!renderInSceneView && renderingData.cameraData.isSceneViewCamera)
        {
            return;
        }

        SunShaftsVolume volume = VolumeManager.instance.stack.GetComponent<SunShaftsVolume>();
        if (volume == null || !volume.IsActive())
        {
            return;
        }

        if (!TryGetSunData(camera, out Vector3 sunViewport, out Color lightColor))
        {
            return;
        }

        pass.renderPassEvent = injectionPoint;
        pass.Setup(material, volume, sunViewport, lightColor);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    private static bool TryGetSunData(Camera camera, out Vector3 sunViewport, out Color lightColor)
    {
        sunViewport = default;
        lightColor = Color.white;

        Light sunLight = GetSunLight();
        if (sunLight == null || sunLight.type != LightType.Directional || sunLight.intensity <= 0f)
        {
            return false;
        }

        Vector3 sunDirection = -sunLight.transform.forward;
        Vector3 sampleWorldPosition = camera.transform.position + (sunDirection * 10000f);
        sunViewport = camera.WorldToViewportPoint(sampleWorldPosition);
        if (sunViewport.z <= 0f)
        {
            return false;
        }

        lightColor = sunLight.color.linear * Mathf.Max(sunLight.intensity, 0f);
        return true;
    }

    private static Light GetSunLight()
    {
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
        {
            return RenderSettings.sun;
        }

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light != null && light.type == LightType.Directional && light.enabled)
            {
                return light;
            }
        }

        return null;
    }

    private sealed class SunShaftsPass : ScriptableRenderPass
    {
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int SunPosId = Shader.PropertyToID("_SunShaftsSunPos");
        private static readonly int TintId = Shader.PropertyToID("_SunShaftsTint");
        private static readonly int LightColorId = Shader.PropertyToID("_SunShaftsLightColor");
        private static readonly int IntensityId = Shader.PropertyToID("_SunShaftsIntensity");
        private static readonly int SampleCountId = Shader.PropertyToID("_SunShaftsSampleCount");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_SunShaftsBlurRadius");
        private static readonly int MaxRadiusId = Shader.PropertyToID("_SunShaftsMaxRadius");
        private static readonly int DecayId = Shader.PropertyToID("_SunShaftsDecay");
        private static readonly int WeightId = Shader.PropertyToID("_SunShaftsWeight");
        private static readonly int ExposureId = Shader.PropertyToID("_SunShaftsExposure");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_SunShaftsDepthThreshold");
        private static readonly int ColorThresholdId = Shader.PropertyToID("_SunShaftsColorThreshold");
        private static readonly int ColorInfluenceId = Shader.PropertyToID("_SunShaftsColorInfluence");
        private static readonly int RadialFalloffId = Shader.PropertyToID("_SunShaftsRadialFalloff");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_SunShaftsEdgeFade");

        private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();

        private readonly ProfilingSampler sunShaftsProfilingSampler = new ProfilingSampler("Sun Shafts");
        private Material material;

        public SunShaftsPass()
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
            requiresIntermediateTexture = true;
        }

        public void Setup(Material targetMaterial, SunShaftsVolume settings, Vector3 sunViewport, Color lightColor)
        {
            material = targetMaterial;

            material.SetVector(SunPosId, new Vector4(sunViewport.x, sunViewport.y, sunViewport.z, 0f));
            material.SetColor(TintId, settings.tint.value);
            material.SetColor(LightColorId, lightColor);
            material.SetFloat(IntensityId, settings.intensity.value);
            material.SetInt(SampleCountId, settings.sampleCount.value);
            material.SetFloat(BlurRadiusId, settings.blurRadius.value);
            material.SetFloat(MaxRadiusId, settings.maxRadius.value);
            material.SetFloat(DecayId, settings.decay.value);
            material.SetFloat(WeightId, settings.weight.value);
            material.SetFloat(ExposureId, settings.exposure.value);
            material.SetFloat(DepthThresholdId, settings.depthThreshold.value);
            material.SetFloat(ColorThresholdId, settings.colorThreshold.value);
            material.SetFloat(ColorInfluenceId, settings.colorInfluence.value);
            material.SetFloat(RadialFalloffId, settings.radialFalloff.value);
            material.SetFloat(EdgeFadeId, settings.edgeFade.value);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer || !resourceData.cameraDepthTexture.IsValid())
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "CameraColor-SunShafts";
            destinationDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Sun Shafts", out PassData passData, sunShaftsProfilingSampler))
            {
                passData.material = material;
                passData.sourceTexture = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    SharedPropertyBlock.Clear();
                    SharedPropertyBlock.SetTexture(BlitTextureId, data.sourceTexture);
                    SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
                });
            }

            resourceData.cameraColor = destination;
        }

        private sealed class PassData
        {
            internal Material material;
            internal TextureHandle sourceTexture;
        }
    }
}
