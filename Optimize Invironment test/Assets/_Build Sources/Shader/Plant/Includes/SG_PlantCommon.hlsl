#ifndef SG_PLANT_COMMON_INCLUDED
#define SG_PLANT_COMMON_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_WindTexture);
SAMPLER(sampler_WindTexture);
TEXTURE2D(_GrassShadowNoiseTex);
SAMPLER(sampler_GrassShadowNoiseTex);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _Cutoff;
    float _ReceiveShadows;
    float _ShadowStrength;
    float _ShadowFloor;
    float _EnableMainLight;
    float _MainLightIntensity;
    float _EnableAdditionalLights;
    float _AdditionalLightIntensity;
    float _EnableAmbient;
    float _AmbientIntensity;
    float _TwoSidedLighting;
    float _WindSpeed;
    float4 _WindDirection;
    float _CameraBendStrength;
    float _EnableGrassConeShape;
    float _GrassConeTipScale;
    float _EnableGrassDistanceBlur;
    float _EnableGrassTransparentBlurPath;
    float _GrassDistanceBlurStart;
    float _GrassDistanceBlurEnd;
    float _GrassDistanceBlurRadius;
    float _GrassDistanceBlurOpacity;
    float _GrassDistanceBlurBrightness;
    float _GrassDistanceBlurCutoffShift;
    float _EnableGrassShadowNoise;
    float _GrassShadowNoiseStrength;
    float _GrassShadowNoiseContrast;
    float4 _GrassShadowNoiseScale;
    float _GrassShadowNoiseScrollSpeed;
    float _EnableWaveShape;
    float _WaveFrequency;
    float _WaveSpacingVariation;
    float _WaveSpeed;
    float _WaveStrength;
    float _WaveBodyInfluence;
    float _WaveTipInfluence;
    float _WaveLateralInfluence;
    float4 _WindTextureScale;
    float _WindTextureScrollSpeed;
    float4 _WindTextureContrast;
    float _WindTextureInfluence;
    float _WindTextureWaveInfluence;
    float4 _NearColor;
    float4 _FarColor;
    float4 _NearFarRange;
    float4 _BottomColor;
    float _HeightBlend;
    float _UseTerrainColor;
    float4 _TerrainColor;
    float _TerrainBlendStrength;
    float _DetailFallbackMode;
    float _DetailFallbackLightingMin;
    float _EnableInteraction;
    float _InteractionStrength;
    float _InteractionPushAway;
    float _InteractionFlatten;
    float _InteractionRadiusMultiplier;
    float _InteractionVerticalRange;
    float _InteractionTrail;
    float _InteractionRecoveryStrength;
    float _InteractionRecoveryFrequency;
    float _InteractionRecoveryNoiseScale;
CBUFFER_END

float4 _GrassCameraForwardWS;

float GetBladeMaskFromUV(float uvY)
{
    return saturate(uvY);
}

float GetToggle01(float value)
{
    return step(0.5, value);
}

float UseDetailFallback()
{
    return GetToggle01(_DetailFallbackMode);
}

float GetWindLean01()
{
    return saturate(_WindSpeed * 0.1);
}

float SampleWindTexture01(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_WindTexture, sampler_WindTexture, uv, 0).r;
}

float3 GetDistanceTint(float3 worldPos)
{
    float dist = distance(_WorldSpaceCameraPos, worldPos);
    float nearRange = _NearFarRange.x;
    float farRange = max(_NearFarRange.y, nearRange + 0.0001);
    float t = saturate((dist - nearRange) / (farRange - nearRange));
    return lerp(_NearColor.rgb, _FarColor.rgb, t);
}

float3 GetHeightTint(float bladeMask)
{
    float heightBlend = max(_HeightBlend, 0.0001);
    float t = saturate(bladeMask * heightBlend);
    return lerp(_BottomColor.rgb, float3(1.0, 1.0, 1.0), t);
}

#endif
