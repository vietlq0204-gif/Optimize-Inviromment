#ifndef SG_PLANT_TERRAIN_BLEND_INCLUDED
#define SG_PLANT_TERRAIN_BLEND_INCLUDED

TEXTURE2D(_GrassTerrainColorMap);
SAMPLER(sampler_GrassTerrainColorMap);

float4 _GrassTerrainColorMapWorldBounds;
float4 _GrassTerrainColorMapParams;

float3 GetTerrainTint(float3 worldPos)
{
    float3 tint = _TerrainColor.rgb;

    if (_GrassTerrainColorMapParams.x > 0.5)
    {
        float2 uv = (worldPos.xz - _GrassTerrainColorMapWorldBounds.xy) /
            max(_GrassTerrainColorMapWorldBounds.zw, float2(0.001, 0.001));

        if (all(uv >= 0.0) && all(uv <= 1.0))
        {
            float3 sampledTint = SAMPLE_TEXTURE2D(_GrassTerrainColorMap, sampler_GrassTerrainColorMap, uv).rgb;
            tint = lerp(tint, sampledTint, saturate(_GrassTerrainColorMapParams.y));
        }
    }

    return tint;
}

float3 ApplyTerrainBlend(float3 color, float3 worldPos)
{
    if (_UseTerrainColor <= 0.5 && _GrassTerrainColorMapParams.x <= 0.5)
    {
        return color;
    }

    float3 tint = GetTerrainTint(worldPos);
    return lerp(color, color * tint, saturate(_TerrainBlendStrength));
}

#endif
