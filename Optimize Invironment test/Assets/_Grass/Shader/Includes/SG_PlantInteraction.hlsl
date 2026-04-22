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
    float shapedMask = pow(saturate(mask), radiusResponse);
    float trailResponse = lerp(2.0, 0.65, saturate(_InteractionTrail));
    return pow(shapedMask, trailResponse);
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

float2 GetInteractionField(float3 worldPos, out float interactionMask, out float recoveryWeight)
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

    interactionMask = saturate(shapedMask * strength);
    recoveryWeight = recovery * interactionMask;
    return field * strength;
}

float3 ApplyInteraction(float3 worldPos, float bladeMask)
{
    float interactionMask;
    float recoveryWeight;
    float2 interactionField = GetInteractionField(worldPos, interactionMask, recoveryWeight);
    if (interactionMask <= 0.0001)
    {
        return worldPos;
    }

    float rootLock = smoothstep(0.08, 0.35, bladeMask);
    float tipInfluence = rootLock * pow(bladeMask, 1.65);
    float flattenAmount = interactionMask * saturate(_InteractionFlatten) * tipInfluence;

    worldPos.xz += interactionField * (_InteractionPushAway * tipInfluence);
    if (recoveryWeight > 0.0001)
    {
        float2 perpendicular = float2(-interactionField.y, interactionField.x);
        float perpendicularLength = length(perpendicular);
        if (perpendicularLength > 0.0001)
        {
            perpendicular /= perpendicularLength;
            float recoveryWindow = recoveryWeight * saturate(1.0 - interactionMask * 1.35);
            float phase = dot(worldPos.xz, float2(0.73, -1.11)) * _InteractionRecoveryNoiseScale;
            phase += _Time.y * _InteractionRecoveryFrequency;
            float wobble = sin(phase) * _InteractionRecoveryStrength * recoveryWindow * tipInfluence;
            worldPos.xz += perpendicular * wobble;
        }
    }

    worldPos.y -= flattenAmount;
    return worldPos;
}

#endif
