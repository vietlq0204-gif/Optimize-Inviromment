#ifndef SG_GRASS_SHAPE_INCLUDED
#define SG_GRASS_SHAPE_INCLUDED

float4 _BaseMap_TexelSize;

float3 ApplyGrassConeShapeOS(float3 positionOS, float bladeMask)
{
    if (GetToggle01(_EnableGrassConeShape) < 0.5)
    {
        return positionOS;
    }

    float heightT = saturate(bladeMask);
    float flareProfile = heightT * heightT;
    float coneScale = lerp(1.0, max(_GrassConeTipScale, 0.001), flareProfile);
    positionOS.xz *= coneScale;
    return positionOS;
}

float GetGrassDistanceBlur01(float3 worldPos)
{
    if (GetToggle01(_EnableGrassDistanceBlur) < 0.5)
    {
        return 0.0;
    }

    float startDistance = max(_GrassDistanceBlurStart, 0.0);
    float endDistance = max(_GrassDistanceBlurEnd, startDistance + 0.001);
    float distanceToCamera = distance(_WorldSpaceCameraPos, worldPos);
    return saturate((distanceToCamera - startDistance) / (endDistance - startDistance));
}

float AccumulateGrassCoverage(float currentCoverage, half sampleAlpha, float weight)
{
    float weightedAlpha = saturate(sampleAlpha * weight);
    return 1.0 - ((1.0 - currentCoverage) * (1.0 - weightedAlpha));
}

half4 SampleGrassSourceBaseMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
}

float GetGrassTransparentBlend01(float blur01)
{
    if (GetToggle01(_EnableGrassTransparentBlurPath) < 0.5)
    {
        return 0.0;
    }

    return smoothstep(0.08, 0.82, blur01);
}

float GetGrassSolidFade01(float blur01)
{
    return 1.0 - GetGrassTransparentBlend01(blur01);
}

float GetGrassTransparentAlpha(float alpha, float blur01)
{
    return saturate(alpha * saturate(_GrassDistanceBlurOpacity) * GetGrassTransparentBlend01(blur01));
}

float GetGrassDitherNoise(float3 worldPos, float2 uv)
{
    float2 hashInput = worldPos.xz * 6.173 + uv * 19.731;
    return frac(sin(dot(hashInput, float2(12.9898, 78.233))) * 43758.5453);
}

half4 SampleGrassBaseMap(float2 uv, float blur01)
{
    half4 center = SampleGrassSourceBaseMap(uv);
    if (blur01 <= 0.0001)
    {
        return center;
    }

    float blurRadius = max(_GrassDistanceBlurRadius, 0.0) * blur01;
    float2 texel = _BaseMap_TexelSize.xy;
    float2 axisStep = float2(texel.x * 0.22, texel.y) * blurRadius;
    float2 axisStepWide = axisStep * 1.85;
    float2 axisStepFine = axisStep * 0.65;
    float2 sideStep = float2(texel.x * blurRadius * 0.75, 0.0);

    half4 upFine = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + axisStepFine);
    half4 downFine = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - axisStepFine);
    half4 up = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + axisStep);
    half4 down = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - axisStep);
    half4 upWide = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + axisStepWide);
    half4 downWide = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - axisStepWide);
    half4 left = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - sideStep);
    half4 right = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + sideStep);

    float3 blurredColor = 0.0;
    float colorWeight = 0.0;

    float centerWeight = center.a * 0.36;
    float fineWeight = 0.22;
    float mediumWeight = 0.17;
    float wideWeight = 0.11;
    float sideWeight = 0.12;

    blurredColor += center.rgb * centerWeight;
    colorWeight += centerWeight;

    blurredColor += upFine.rgb * (upFine.a * fineWeight);
    blurredColor += downFine.rgb * (downFine.a * fineWeight);
    colorWeight += (upFine.a + downFine.a) * fineWeight;

    blurredColor += up.rgb * (up.a * mediumWeight);
    blurredColor += down.rgb * (down.a * mediumWeight);
    colorWeight += (up.a + down.a) * mediumWeight;

    blurredColor += upWide.rgb * (upWide.a * wideWeight);
    blurredColor += downWide.rgb * (downWide.a * wideWeight);
    colorWeight += (upWide.a + downWide.a) * wideWeight;

    blurredColor += left.rgb * (left.a * sideWeight);
    blurredColor += right.rgb * (right.a * sideWeight);
    colorWeight += (left.a + right.a) * sideWeight;

    half4 blurred;
    blurred.rgb = colorWeight > 0.0001 ? blurredColor / colorWeight : center.rgb;

    float coverage = 0.0;
    coverage = AccumulateGrassCoverage(coverage, center.a, 0.42 + blur01 * 0.18);
    coverage = AccumulateGrassCoverage(coverage, upFine.a, 0.52);
    coverage = AccumulateGrassCoverage(coverage, downFine.a, 0.52);
    coverage = AccumulateGrassCoverage(coverage, up.a, 0.44);
    coverage = AccumulateGrassCoverage(coverage, down.a, 0.44);
    coverage = AccumulateGrassCoverage(coverage, upWide.a, 0.30);
    coverage = AccumulateGrassCoverage(coverage, downWide.a, 0.30);
    coverage = AccumulateGrassCoverage(coverage, left.a, 0.26);
    coverage = AccumulateGrassCoverage(coverage, right.a, 0.26);

    float alphaLift = saturate(blur01 * 0.28);
    float softenedAlpha = saturate(coverage + alphaLift * (1.0 - coverage));
    softenedAlpha = smoothstep(0.04, 0.82, softenedAlpha);
    float blurOpacity = lerp(1.0, saturate(_GrassDistanceBlurOpacity), blur01);
    blurred.a = softenedAlpha * blurOpacity;

    half contrastFlatten = saturate(blur01 * 0.72h);
    float blurBrightness = saturate(_GrassDistanceBlurBrightness) * blur01;
    half3 brightBase = max(blurred.rgb, center.rgb);
    half3 brightenedColor = brightBase + (1.0h - brightBase) * (blurBrightness * 0.75h);
    half luminance = dot(brightenedColor, half3(0.299h, 0.587h, 0.114h));
    brightenedColor = lerp(brightenedColor, luminance.xxx, contrastFlatten * 0.08h);
    blurred.rgb = lerp(blurred.rgb, saturate(brightenedColor), blurBrightness);

    return lerp(center, blurred, blur01);
}

float GetGrassBlurredCutoff(float blur01, float baseCutoff)
{
    float opacityCompensation = (1.0 - saturate(_GrassDistanceBlurOpacity)) * 0.18;
    return saturate(baseCutoff - saturate(_GrassDistanceBlurCutoffShift + opacityCompensation) * blur01);
}

#endif
