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

float4 SampleInteractionData(float3 worldPos)
{
    float2 uv = GetInteractionUV(worldPos);
    if (InteractionEnabled01() < 0.5 || _GrassInteractionParams.x < 0.5)
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
    float radiusResponse = rcp(max(GetInteractionRadiusMultiplierValue(), 0.1));
    return pow(saturate(mask), radiusResponse);
}

float2 GetFallbackRecoveryAxis(float3 worldPos)
{
    float hash = frac(sin(dot(worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
    float angle = hash * 6.2831853;
    return float2(cos(angle), sin(angle));
}

float2 DecodeInteractionAxis(float2 field, float3 worldPos)
{
    float fieldLengthSq = dot(field, field);
    if (fieldLengthSq > 0.00000001)
    {
        return field * rsqrt(fieldLengthSq);
    }

    return GetFallbackRecoveryAxis(worldPos);
}

float2 ApplyInteractionResponseCurve(float2 interactionAxis, float shapedMask)
{
    if (shapedMask > 0.0001)
    {
        return interactionAxis * shapedMask;
    }

    return float2(0.0, 0.0);
}

float GetInteractionReleaseProgress(float recoveryAlpha)
{
    float trailResponse = saturate(GetInteractionTrailValue());
    float releaseStart = lerp(0.08, 0.24, trailResponse);
    float releaseEnd = lerp(0.68, 0.94, trailResponse);
    float releaseProgress = smoothstep(releaseStart, releaseEnd, 1.0 - recoveryAlpha);
    return saturate(releaseProgress);
}

float GetInteractionReleaseReadiness(float interactionMask)
{
    float contactSuppression = 1.0 - saturate(interactionMask);
    return smoothstep(0.0, 0.12, contactSuppression);
}

float2 GetInteractionField(
    float3 worldPos,
    out float interactionMask,
    out float recoveryWeight,
    out float recoveryAlpha,
    out float2 interactionAxis)
{
    float projectionHeight = _GrassInteractionParams.y;
    float verticalRange = max(GetInteractionVerticalRangeValue(), 0.001);
    float verticalMask = 1.0 - saturate(abs(worldPos.y - projectionHeight) / verticalRange);
    float strength = verticalMask * _GrassInteractionParams.z * GetInteractionStrengthValue();
    if (strength <= 0.0001)
    {
        interactionMask = 0.0;
        recoveryWeight = 0.0;
        recoveryAlpha = 0.0;
        interactionAxis = float2(0.0, 0.0);
        return float2(0.0, 0.0);
    }

    float4 data = SampleInteractionData(worldPos);
    float rawMask = saturate(data.a);
    float recovery = rawMask > 0.0001 ? saturate(data.b / rawMask) : 0.0;
    recoveryAlpha = rawMask;

    if (rawMask <= 0.0001 && recovery <= 0.0001)
    {
        interactionMask = 0.0;
        recoveryWeight = 0.0;
        interactionAxis = float2(0.0, 0.0);
        return float2(0.0, 0.0);
    }

    float shapedMask = GetInteractionMaskResponse(rawMask);

    interactionMask = saturate(shapedMask * strength);
    recoveryWeight = saturate(recovery * strength);
    if (interactionMask <= 0.0001 && recoveryWeight <= 0.0001)
    {
        interactionAxis = float2(0.0, 0.0);
        return float2(0.0, 0.0);
    }

    float2 encodedField = data.rg * 2.0 - 1.0;
    interactionAxis = DecodeInteractionAxis(encodedField, worldPos);
    float2 field = ApplyInteractionResponseCurve(interactionAxis, shapedMask);
    return field * strength;
}

float3 ApplyInteraction(float3 worldPos, float bladeMask)
{
    float rootLock = smoothstep(0.08, 0.35, bladeMask);
    if (rootLock <= 0.0001)
    {
        return worldPos;
    }

    float tipInfluence = rootLock * pow(bladeMask, 1.65);
    if (tipInfluence <= 0.0001)
    {
        return worldPos;
    }

    float interactionMask;
    float recoveryWeight;
    float recoveryAlpha;
    float2 interactionAxis;
    float2 interactionField = GetInteractionField(worldPos, interactionMask, recoveryWeight, recoveryAlpha, interactionAxis);
    if (interactionMask <= 0.0001 && recoveryWeight <= 0.0001)
    {
        return worldPos;
    }

    float flattenAmount = 0.0;
    float bendAmount = 0.0;
    float releaseProgress = GetInteractionReleaseProgress(recoveryAlpha);
    float releaseReadiness = GetInteractionReleaseReadiness(interactionMask);

    if (interactionMask > 0.0001)
    {
        flattenAmount = interactionMask * saturate(GetInteractionFlattenValue()) * tipInfluence;
        bendAmount = length(interactionField) * GetInteractionPushAwayValue() * tipInfluence;
    }

    float returnProgress = releaseReadiness * releaseProgress * releaseProgress;
    float retainedBend = bendAmount * (1.0 - returnProgress);
    float recoveryEnvelope = recoveryWeight * releaseReadiness * pow(saturate(recoveryAlpha), 2.2) * tipInfluence;
    float totalDisplacement = retainedBend;

    if (recoveryEnvelope > 0.0001)
    {
        float phase = dot(worldPos.xz, float2(0.73, -1.11)) * GetInteractionRecoveryNoiseScaleValue();
        phase += _Time.y * GetInteractionRecoveryFrequencyValue();
        totalDisplacement += sin(phase) * GetInteractionRecoveryStrengthValue() * recoveryEnvelope;
    }

    if (abs(totalDisplacement) > 0.0001)
    {
        worldPos.xz += interactionAxis * totalDisplacement;
    }

    if (flattenAmount > 0.0)
    {
        worldPos.y -= flattenAmount;
    }

    return worldPos;
}

#endif
