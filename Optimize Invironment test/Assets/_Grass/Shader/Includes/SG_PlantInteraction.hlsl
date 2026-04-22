#ifndef SG_PLANT_INTERACTION_INCLUDED
#define SG_PLANT_INTERACTION_INCLUDED

TEXTURE2D(_GrassInteractionMap);
SAMPLER(sampler_GrassInteractionMap);

float4 _GrassInteractionCameraPosition;
float4 _GrassInteractionMap_TexelSize;
float4 _GrassInteractionParams;

float2 GetInteractionUV(float3 worldPos)
{
    float orthoSize = max(_GrassInteractionCameraPosition.w, 0.001);
    return ((worldPos.xz - _GrassInteractionCameraPosition.xz) / (orthoSize * 2.0)) + 0.5;
}

float GetInteractionMask01(float3 worldPos)
{
    if (GetToggle01(_EnableInteraction) < 0.5 || _GrassInteractionParams.x < 0.5)
    {
        return 0.0;
    }

    float2 uv = GetInteractionUV(worldPos);
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return 0.0;
    }

    float mask = SAMPLE_TEXTURE2D_LOD(_GrassInteractionMap, sampler_GrassInteractionMap, uv, 0).r;
    float trailResponse = lerp(2.0, 0.65, saturate(_InteractionTrail));
    mask = pow(saturate(mask), trailResponse);

    float projectionHeight = _GrassInteractionParams.y;
    float verticalRange = max(_InteractionVerticalRange, 0.001);
    float verticalMask = 1.0 - saturate(abs(worldPos.y - projectionHeight) / verticalRange);

    return saturate(mask * verticalMask * _GrassInteractionParams.z * _InteractionStrength);
}

float2 GetInteractionPushDirection(float3 worldPos)
{
    float2 uv = GetInteractionUV(worldPos);
    float2 texel = _GrassInteractionMap_TexelSize.xy * max(_InteractionRadiusMultiplier, 0.1);

    float sampleL = SAMPLE_TEXTURE2D_LOD(_GrassInteractionMap, sampler_GrassInteractionMap, uv - float2(texel.x, 0.0), 0).r;
    float sampleR = SAMPLE_TEXTURE2D_LOD(_GrassInteractionMap, sampler_GrassInteractionMap, uv + float2(texel.x, 0.0), 0).r;
    float sampleD = SAMPLE_TEXTURE2D_LOD(_GrassInteractionMap, sampler_GrassInteractionMap, uv - float2(0.0, texel.y), 0).r;
    float sampleU = SAMPLE_TEXTURE2D_LOD(_GrassInteractionMap, sampler_GrassInteractionMap, uv + float2(0.0, texel.y), 0).r;

    float2 gradient = float2(sampleR - sampleL, sampleU - sampleD);
    float gradientLength = length(gradient);
    if (gradientLength <= 0.0001)
    {
        return float2(0.0, 0.0);
    }

    return -gradient / gradientLength;
}

float3 ApplyInteraction(float3 worldPos, float bladeMask)
{
    float interactionMask = GetInteractionMask01(worldPos);
    if (interactionMask <= 0.0001)
    {
        return worldPos;
    }

    float rootLock = smoothstep(0.08, 0.35, bladeMask);
    float tipInfluence = rootLock * pow(bladeMask, 1.65);
    float flattenAmount = interactionMask * saturate(_InteractionFlatten) * tipInfluence;
    float pushAmount = interactionMask * _InteractionPushAway * tipInfluence;
    float2 pushDirection = GetInteractionPushDirection(worldPos);

    worldPos.xz += pushDirection * pushAmount;
    worldPos.y -= flattenAmount;
    return worldPos;
}

#endif
