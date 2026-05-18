#ifndef SG_PLANT_COMMON_INCLUDED
#define SG_PLANT_COMMON_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_WindTexture);
SAMPLER(sampler_WindTexture);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _EnableColor;
    float _Cutoff;
    float _EnableLighting;
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
    float _EnableWind;
    float _WindSpeed;
    float4 _WindDirection;
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
    float _EnableTerrain;
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

float4 _GlobalPlantInteractionConfigState;
float4 _GlobalPlantInteractionConfigA;
float4 _GlobalPlantInteractionConfigB;

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

float WindEnabled01()
{
    return GetToggle01(_EnableWind);
}

float ColorEnabled01()
{
    return GetToggle01(_EnableColor);
}

float UseGlobalPlantInteractionConfig01()
{
    return step(0.5, _GlobalPlantInteractionConfigState.x);
}

float InteractionEnabled01()
{
    if (UseGlobalPlantInteractionConfig01() > 0.5)
    {
        return step(0.5, _GlobalPlantInteractionConfigState.y);
    }

    return GetToggle01(_EnableInteraction);
}

float GetInteractionStrengthValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigState.z : _InteractionStrength;
}

float GetInteractionPushAwayValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigState.w : _InteractionPushAway;
}

float GetInteractionFlattenValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigA.x : _InteractionFlatten;
}

float GetInteractionRadiusMultiplierValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigA.y : _InteractionRadiusMultiplier;
}

float GetInteractionVerticalRangeValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigA.z : _InteractionVerticalRange;
}

float GetInteractionTrailValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigA.w : _InteractionTrail;
}

float GetInteractionRecoveryStrengthValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigB.x : _InteractionRecoveryStrength;
}

float GetInteractionRecoveryFrequencyValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigB.y : _InteractionRecoveryFrequency;
}

float GetInteractionRecoveryNoiseScaleValue()
{
    return UseGlobalPlantInteractionConfig01() > 0.5 ? _GlobalPlantInteractionConfigB.z : _InteractionRecoveryNoiseScale;
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
