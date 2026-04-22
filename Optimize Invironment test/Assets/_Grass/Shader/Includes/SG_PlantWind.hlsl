#ifndef SG_PLANT_WIND_INCLUDED
#define SG_PLANT_WIND_INCLUDED

float GetWindTextureMask(float along, float across)
{
    float2 scale = max(_WindTextureScale.xy, float2(0.001, 0.001));
    float2 uv = float2(along / scale.x, across / scale.y);
    uv.x -= _Time.y * _WindTextureScrollSpeed;

    float raw = SampleWindTexture01(uv);
    float minValue = _WindTextureContrast.x;
    float maxValue = max(_WindTextureContrast.y, minValue + 0.0001);
    float mask = saturate((raw - minValue) / (maxValue - minValue));

    return lerp(1.0, mask, saturate(_WindTextureInfluence));
}

float ComputeWaveSignal(float along, float across, float textureMask)
{
    if (GetToggle01(_EnableWaveShape) < 0.5)
    {
        return 0.0;
    }

    float travel = _Time.y * _WaveSpeed;
    float spacingVariation = saturate(_WaveSpacingVariation);
    float spacingWarp = 0.0;
    if (spacingVariation > 0.0001)
    {
        float warpCoordA = along * (_WaveFrequency * 0.19) + across * 0.045 + 0.73;
        float warpCoordB = along * (_WaveFrequency * 0.09) - across * 0.082 - 1.41;
        float warpNoise = sin(warpCoordA) * 0.62 + sin(warpCoordB) * 0.38;
        spacingWarp = warpNoise * (spacingVariation * (1.2 / max(_WaveFrequency, 0.1)));
    }

    float irregularAlong = along + spacingWarp;
    float phaseA = irregularAlong * _WaveFrequency - travel;
    float phaseB = irregularAlong * (_WaveFrequency * 0.56) - travel * 1.28 + across * 0.12 + 1.17;
    float signal = sin(phaseA) * 0.72 + sin(phaseB) * 0.28;
    float textureWave = lerp(1.0, textureMask, saturate(_WindTextureWaveInfluence));

    return signal * _WaveStrength * textureWave;
}

float3 ApplyWind(float3 worldPos, float bladeMask)
{
    float2 dir = normalize(_WindDirection.xy + float2(0.0001, 0.0001));
    float2 perp = float2(-dir.y, dir.x);
    float along = dot(worldPos.xz, dir);
    float across = dot(worldPos.xz, perp);

    float rootLock = smoothstep(0.04, 0.16, bladeMask);
    float leanProfile = rootLock * pow(bladeMask, 1.45);
    float leanAmount = GetWindLean01();
    float textureMask = GetWindTextureMask(along, across);

    float baseLean = leanAmount * textureMask * leanProfile * 0.38;
    float waveSignal = ComputeWaveSignal(along, across, textureMask);

    float bodyWaveProfile = rootLock * pow(bladeMask, 1.35);
    float tipWaveProfile = rootLock * pow(bladeMask, 3.4);
    float bodyWave = waveSignal * _WaveBodyInfluence * bodyWaveProfile * 0.18;
    float tipWave = waveSignal * _WaveTipInfluence * tipWaveProfile * 0.32;
    float totalWave = bodyWave + tipWave;

    float2 offsetXZ = dir * (baseLean + totalWave);
    offsetXZ += perp * (totalWave * _WaveLateralInfluence * (0.35 + bladeMask * 0.2));

    float sag = baseLean * baseLean * lerp(0.18, 0.85, bladeMask);
    sag += abs(totalWave) * lerp(0.0, 0.08, bladeMask);

    return worldPos + float3(offsetXZ.x, -sag, offsetXZ.y);
}

#endif
