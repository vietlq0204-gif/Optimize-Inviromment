#ifndef SG_PLANT_DYNAMIC_INTERACTION_INCLUDED
#define SG_PLANT_DYNAMIC_INTERACTION_INCLUDED

TEXTURE2D(_PlantDynamicInteractionStateMap);
SAMPLER(sampler_PlantDynamicInteractionStateMap);
TEXTURE2D(_PlantDynamicInteractionFlattenMap);
SAMPLER(sampler_PlantDynamicInteractionFlattenMap);

float4 _PlantDynamicInteractionFieldParams;
float4 _PlantDynamicInteractionRenderParams;
float4 _PlantDynamicInteractionDebugColor;
float4 _PlantDynamicInteractionDebugParams; // x enabled, y tint strength, z bend sensitivity, w flatten sensitivity

float2 GetDynamicInteractionUV(float3 worldPos)
{
    float coverage = max(_PlantDynamicInteractionFieldParams.z, 0.001);
    return ((worldPos.xz - _PlantDynamicInteractionFieldParams.xy) / coverage) + 0.5;
}

float3 ApplyDynamicPlantInteraction(float3 worldPos, float bladeMask)
{
    if (_PlantDynamicInteractionFieldParams.w < 0.5)
    {
        return worldPos;
    }

    float2 uv = GetDynamicInteractionUV(worldPos);
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return worldPos;
    }

    float rootLock = smoothstep(0.06, 0.22, bladeMask);
    float tipInfluence = rootLock * pow(saturate(bladeMask), 1.45);
    if (tipInfluence <= 0.0001)
    {
        return worldPos;
    }

    float4 state = SAMPLE_TEXTURE2D_LOD(_PlantDynamicInteractionStateMap, sampler_PlantDynamicInteractionStateMap, uv, 0);
    float2 bend = state.rg * _PlantDynamicInteractionRenderParams.x;
    float flatten = SAMPLE_TEXTURE2D_LOD(_PlantDynamicInteractionFlattenMap, sampler_PlantDynamicInteractionFlattenMap, uv, 0).r;
    flatten *= _PlantDynamicInteractionRenderParams.y;

    worldPos.xz += bend * tipInfluence;
    worldPos.y -= flatten * tipInfluence;
    return worldPos;
}

float GetDynamicPlantInteractionAmount(float3 worldPos)
{
    if (_PlantDynamicInteractionFieldParams.w < 0.5 || _PlantDynamicInteractionDebugParams.x < 0.5)
    {
        return 0.0;
    }

    float2 uv = GetDynamicInteractionUV(worldPos);
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return 0.0;
    }

    float4 state = SAMPLE_TEXTURE2D_LOD(_PlantDynamicInteractionStateMap, sampler_PlantDynamicInteractionStateMap, uv, 0);
    float flatten = SAMPLE_TEXTURE2D_LOD(_PlantDynamicInteractionFlattenMap, sampler_PlantDynamicInteractionFlattenMap, uv, 0).r;
    float amount = length(state.rg) * _PlantDynamicInteractionDebugParams.z + flatten * _PlantDynamicInteractionDebugParams.w;
    return saturate(amount);
}

float3 ApplyDynamicPlantInteractionDebugTint(float3 color, float3 worldPos, float bladeMask)
{
    float amount = GetDynamicPlantInteractionAmount(worldPos);
    float bladeVisibility = lerp(0.45, 1.0, saturate(bladeMask));
    float tint = saturate(amount * _PlantDynamicInteractionDebugParams.y * bladeVisibility);
    return lerp(color, _PlantDynamicInteractionDebugColor.rgb, tint);
}

#endif
