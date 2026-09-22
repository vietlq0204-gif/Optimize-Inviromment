#ifndef SG_PLANT_TERRAIN_BLEND_INCLUDED
#define SG_PLANT_TERRAIN_BLEND_INCLUDED

TEXTURE2D(_PlantTerrainColorMap);
SAMPLER(sampler_PlantTerrainColorMap);

float4 _PlantTerrainColorMapWorldBounds;
float4 _PlantTerrainColorMapParams;

float3 GetTerrainTint(float3 worldPos)
{
    float3 tint = _TerrainColor.rgb;

    if (_PlantTerrainColorMapParams.x > 0.5)
    {
        float2 uv = (worldPos.xz - _PlantTerrainColorMapWorldBounds.xy) /
            max(_PlantTerrainColorMapWorldBounds.zw, float2(0.001, 0.001));

        if (all(uv >= 0.0) && all(uv <= 1.0))
        {
            float3 sampledTint = SAMPLE_TEXTURE2D(_PlantTerrainColorMap, sampler_PlantTerrainColorMap, uv).rgb;
            tint = lerp(tint, sampledTint, saturate(_PlantTerrainColorMapParams.y));
        }
    }

    return tint;
}

float3 ApplyTerrainBlend(float3 color, float3 worldPos)
{
    if (GetToggle01(_EnableTerrain) < 0.5)
    {
        return color;
    }

    if (_UseTerrainColor <= 0.5 && _PlantTerrainColorMapParams.x <= 0.5)
    {
        return color;
    }

    float3 tint = GetTerrainTint(worldPos);
    return lerp(color, color * tint, saturate(_TerrainBlendStrength));
}

#endif
