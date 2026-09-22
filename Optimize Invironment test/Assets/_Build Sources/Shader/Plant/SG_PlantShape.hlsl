#ifndef SG_PLANT_SHAPE_INCLUDED
#define SG_PLANT_SHAPE_INCLUDED

float3 ApplyPlantConeShapeOS(float3 positionOS, float bladeMask)
{
    if (GetToggle01(_EnablePlantConeShape) < 0.5)
    {
        return positionOS;
    }

    float heightT = saturate(bladeMask);
    float flareProfile = heightT * heightT;
    float coneScale = lerp(1.0, max(_PlantConeTipScale, 0.001), flareProfile);
    positionOS.xz *= coneScale;
    return positionOS;
}

half4 SamplePlantSourceBaseMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
}

float2 GetPlantShadowNoiseUV(float3 worldPos)
{
    float2 dir = normalize(_WindDirection.xy + float2(0.0001, 0.0001));
    float2 perp = float2(-dir.y, dir.x);
    float along = dot(worldPos.xz, dir);
    float across = dot(worldPos.xz, perp);
    float2 scale = max(_WindTextureScale.xy, float2(0.001, 0.001));
    float2 uv = float2(along / scale.x, across / scale.y);
    uv.x -= _Time.y * _WindTextureScrollSpeed;
    return uv;
}

float GetPlantShadowNoiseAttenuation(float3 worldPos)
{
    if (WindEnabled01() < 0.5 || GetToggle01(_EnablePlantShadowNoise) < 0.5)
    {
        return 1.0;
    }

    float strength = saturate(_PlantShadowNoiseStrength);
    if (strength <= 0.0001)
    {
        return 1.0;
    }

    float raw = SampleWindTexture01(GetPlantShadowNoiseUV(worldPos));
    float contrast = max(_PlantShadowNoiseContrast, 0.001);
    float mask = saturate((raw - 0.5) * contrast + 0.5);
    return 1.0 - ((1.0 - mask) * strength);
}

#endif
