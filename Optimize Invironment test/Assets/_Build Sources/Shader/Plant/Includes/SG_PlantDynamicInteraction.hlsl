#ifndef SG_PLANT_DYNAMIC_INTERACTION_INCLUDED
#define SG_PLANT_DYNAMIC_INTERACTION_INCLUDED

TEXTURE2D(_GrassDynamicInteractionStateMap);
SAMPLER(sampler_GrassDynamicInteractionStateMap);
TEXTURE2D(_GrassDynamicInteractionFlattenMap);
SAMPLER(sampler_GrassDynamicInteractionFlattenMap);

float4 _GrassDynamicInteractionFieldParams;
float4 _GrassDynamicInteractionRenderParams;
float4 _GrassDynamicInteractionDebugColor;
float4 _GrassDynamicInteractionDebugParams; // x enabled, y tint strength, z bend sensitivity, w flatten sensitivity

float2 GetDynamicInteractionUV(float3 worldPos)
{
    float coverage = max(_GrassDynamicInteractionFieldParams.z, 0.001);
    return ((worldPos.xz - _GrassDynamicInteractionFieldParams.xy) / coverage) + 0.5;
}

float3 ApplyDynamicGrassInteraction(float3 worldPos, float bladeMask)
{
    if (_GrassDynamicInteractionFieldParams.w < 0.5)
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

    float4 state = SAMPLE_TEXTURE2D_LOD(_GrassDynamicInteractionStateMap, sampler_GrassDynamicInteractionStateMap, uv, 0);
    float2 bend = state.rg * _GrassDynamicInteractionRenderParams.x;
    float flatten = SAMPLE_TEXTURE2D_LOD(_GrassDynamicInteractionFlattenMap, sampler_GrassDynamicInteractionFlattenMap, uv, 0).r;
    flatten *= _GrassDynamicInteractionRenderParams.y;

    worldPos.xz += bend * tipInfluence;
    worldPos.y -= flatten * tipInfluence;
    return worldPos;
}

float GetDynamicGrassInteractionAmount(float3 worldPos)
{
    if (_GrassDynamicInteractionFieldParams.w < 0.5 || _GrassDynamicInteractionDebugParams.x < 0.5)
    {
        return 0.0;
    }

    float2 uv = GetDynamicInteractionUV(worldPos);
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return 0.0;
    }

    float4 state = SAMPLE_TEXTURE2D_LOD(_GrassDynamicInteractionStateMap, sampler_GrassDynamicInteractionStateMap, uv, 0);
    float flatten = SAMPLE_TEXTURE2D_LOD(_GrassDynamicInteractionFlattenMap, sampler_GrassDynamicInteractionFlattenMap, uv, 0).r;
    float amount = length(state.rg) * _GrassDynamicInteractionDebugParams.z + flatten * _GrassDynamicInteractionDebugParams.w;
    return saturate(amount);
}

float3 ApplyDynamicGrassInteractionDebugTint(float3 color, float3 worldPos, float bladeMask)
{
    float amount = GetDynamicGrassInteractionAmount(worldPos);
    float bladeVisibility = lerp(0.45, 1.0, saturate(bladeMask));
    float tint = saturate(amount * _GrassDynamicInteractionDebugParams.y * bladeVisibility);
    return lerp(color, _GrassDynamicInteractionDebugColor.rgb, tint);
}

#endif
