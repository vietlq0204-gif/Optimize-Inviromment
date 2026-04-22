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

float4 SampleInteractionData(float3 worldPos, out float2 uv)
{
    uv = GetInteractionUV(worldPos);
    if (GetToggle01(_EnableInteraction) < 0.5 || _GrassInteractionParams.x < 0.5)
    {
        return float4(0.5, 0.5, 0.0, 0.0);
    }

    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return float4(0.5, 0.5, 0.0, 0.0);
    }

    return SAMPLE_TEXTURE2D_LOD(_GrassInteractionMap, sampler_GrassInteractionMap, uv, 0);
}

float GetInteractionMaskResponse(float mask)
{
    float radiusResponse = rcp(max(_InteractionRadiusMultiplier, 0.1));
    return pow(saturate(mask), radiusResponse);
}

float2 GetFallbackRecoveryAxis(float3 worldPos)
{
    float hash = frac(sin(dot(worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
    float angle = hash * 6.2831853;
    return float2(cos(angle), sin(angle));
}

float2 ApplyInteractionResponseCurve(float2 field, float mask)
{
    float shapedMask = GetInteractionMaskResponse(mask);
    float fieldLength = length(field);
    if (fieldLength > 0.0001)
    {
        field *= shapedMask / fieldLength;
    }
    else
    {
        field = float2(0.0, 0.0);
    }

    return field;
}

float2 GetInteractionField(float3 worldPos, out float interactionMask, out float recoveryWeight, out float recoveryAlpha)
{
    float2 uv;
    float4 data = SampleInteractionData(worldPos, uv);
    float2 field = data.rg * 2.0 - 1.0;
    float rawMask = saturate(data.a);
    float recovery = rawMask > 0.0001 ? saturate(data.b / rawMask) : 0.0;
    float shapedMask = GetInteractionMaskResponse(rawMask);
    field = ApplyInteractionResponseCurve(field, rawMask);

    float projectionHeight = _GrassInteractionParams.y;
    float verticalRange = max(_InteractionVerticalRange, 0.001);
    float verticalMask = 1.0 - saturate(abs(worldPos.y - projectionHeight) / verticalRange);
    float strength = verticalMask * _GrassInteractionParams.z * _InteractionStrength;
    recoveryAlpha = rawMask;

    interactionMask = saturate(shapedMask * strength);
    recoveryWeight = saturate(recovery * recoveryAlpha * strength);
    return field * strength;
}

float3 ApplyInteraction(float3 worldPos, float bladeMask)
{
    float interactionMask;
    float recoveryWeight;
    float recoveryAlpha;
    float2 interactionField = GetInteractionField(worldPos, interactionMask, recoveryWeight, recoveryAlpha);
    if (interactionMask <= 0.0001 && recoveryWeight <= 0.0001)
    {
        return worldPos;
    }

    float rootLock = smoothstep(0.08, 0.35, bladeMask);
    float tipInfluence = rootLock * pow(bladeMask, 1.65);
    float flattenAmount = 0.0;

    if (interactionMask > 0.0001)
    {
        flattenAmount = interactionMask * saturate(_InteractionFlatten) * tipInfluence;
        worldPos.xz += interactionField * (_InteractionPushAway * tipInfluence);
    }

    if (recoveryWeight > 0.0001)
    {
        float2 recoveryAxis = float2(-interactionField.y, interactionField.x);
        float axisLength = length(recoveryAxis);
        if (axisLength > 0.0001)
        {
            recoveryAxis /= axisLength;
        }
        else
        {
            recoveryAxis = GetFallbackRecoveryAxis(worldPos);
        }

        if (length(recoveryAxis) > 0.0001)
        {
            float releaseEnvelope = lerp(0.22, 1.0, saturate(1.0 - interactionMask));
            float recoveryWindow = recoveryWeight * recoveryAlpha * releaseEnvelope;
            float phase = dot(worldPos.xz, float2(0.73, -1.11)) * _InteractionRecoveryNoiseScale;
            phase += _Time.y * _InteractionRecoveryFrequency;
            float wobble = sin(phase) * _InteractionRecoveryStrength * recoveryWindow * tipInfluence;
            worldPos.xz += recoveryAxis * wobble;
        }
    }

    if (flattenAmount > 0.0)
    {
        worldPos.y -= flattenAmount;
    }

    return worldPos;
}

#endif
