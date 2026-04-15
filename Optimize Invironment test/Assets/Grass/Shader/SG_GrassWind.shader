Shader "Custom/Vit/GrassWind_URP"
{
    Properties
    {
        // ================================================================
        // Common
        // ================================================================
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.3

        [NoScaleOffset] _WindTexture ("Wind Texture", 2D) = "gray" {}
        _WindSpeed ("Wind Speed", Range(0,10)) = 0.72
        _WindStrength ("Wind Strength", Range(0,1)) = 0.22
        _WindDirection ("Wind Direction XZ", Vector) = (0.92,0.30,0,0)
        [Toggle] _EnableWave ("Enable Wave", Float) = 1

        // ================================================================
        // Wind Shape - Advanced
        // ================================================================
        [HideInInspector] _WaveFrequency ("Wave Frequency", Range(0,10)) = 0.34
        [HideInInspector] _WaveSharpness ("Wave Sharpness", Range(1,8)) = 2.2
        [HideInInspector] _MacroWaveStrength ("Macro Wave Strength", Range(0,1)) = 0.68
        [HideInInspector] _SideVariation ("Side Variation", Range(0,2)) = 0.22

        // ================================================================
        // Gust Front - Advanced
        // ================================================================
        [HideInInspector] _GustFrontStrength ("Gust Front Strength", Range(0,2)) = 0.95
        [HideInInspector] _GustFrontSpeed ("Gust Front Speed", Range(0,25)) = 6.5
        [HideInInspector] _GustFrontSpacing ("Gust Front Spacing", Range(1,40)) = 12.0
        [HideInInspector] _GustFrontWidth ("Gust Front Width", Range(0.1,8)) = 1.4
        [HideInInspector] _GustFrontTrail ("Gust Front Trail", Range(0.1,14)) = 4.0
        [HideInInspector] _GustFrontCurvature ("Gust Front Curvature", Range(0,0.08)) = 0.014
        [HideInInspector] _GustFrontOverlap ("Gust Front Overlap", Range(0,1.5)) = 0.85
        [HideInInspector] _GustFrontBreakup ("Gust Front Breakup", Range(0,1)) = 0.55
        [HideInInspector] _GustFrontWarp ("Gust Front Warp", Range(0,6)) = 1.35
        [HideInInspector] _GustFrontLateralScale ("Gust Front Lateral Scale", Range(0.5,40)) = 7.5

        // ================================================================
        // Wind Noise - Advanced
        // ================================================================
        [HideInInspector] _WindScale ("Wind Scale", Range(0,10)) = 1.15
        [HideInInspector] _WindNoiseScale ("Wind Noise Scale", Vector) = (34,52,0,0)
        [HideInInspector] _WindNoiseSpeed ("Wind Noise Speed", Range(0,5)) = 0.22
        [HideInInspector] _WindNoiseContrast ("Wind Noise Contrast", Vector) = (0.26,0.82,0,0)

        // ================================================================
        // Blade Bend - Advanced
        // ================================================================
        [HideInInspector] _TopBend ("Top Bend", Range(0.1,5)) = 1.65
        [HideInInspector] _StemBend ("Stem Bend", Range(0,1)) = 0.45
        [HideInInspector] _WindHeight ("Wind Height", Range(0,1)) = 0.84
        [HideInInspector] _DownBend ("Down Bend", Range(0,1)) = 0.14
        [HideInInspector] _DetailStrength ("Detail Strength", Range(0,1)) = 0.06
        [HideInInspector] _FlutterSpeed ("Flutter Speed", Range(0,10)) = 1.65

        // ================================================================
        // Color - Advanced
        // ================================================================
        [HideInInspector] _NearColor ("Near Color", Color) = (0.48,1.00,0.48,1)
        [HideInInspector] _FarColor ("Far Color", Color) = (0.24,0.58,0.24,1)
        [HideInInspector] _NearFarRange ("Near/Far Range", Vector) = (3,18,0,0)
        [HideInInspector] _BottomColor ("Bottom Color", Color) = (0.18,0.42,0.16,1)
        [HideInInspector] _HeightBlend ("Height Blend", Range(0,20)) = 4.5

        // ================================================================
        // Terrain - Advanced
        // ================================================================
        [HideInInspector] _UseTerrainColor ("Use Terrain Color", Float) = 0
        [HideInInspector] _TerrainColor ("Terrain Color", Color) = (0.30,0.50,0.25,1)

        // ================================================================
        // Unused / Reserved
        // ================================================================
        [HideInInspector] _ShadowColor ("Shadow Color", Color) = (0.30,0.22,0.10,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            Cull Off
            ZWrite On
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            /// <summary>
            /// Vertex input data from mesh.
            /// </summary>
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            /// <summary>
            /// Data passed from vertex stage to fragment stage.
            /// </summary>
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float bladeMask : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WindTexture);
            SAMPLER(sampler_WindTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _EnableWave;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _TopBend;

                float4 _WindDirection;
                float _WaveFrequency;
                float _WaveSharpness;
                float _SideVariation;
                float _DetailStrength;
                float _GustFrontStrength;
                float _GustFrontSpeed;
                float _GustFrontSpacing;
                float _GustFrontWidth;
                float _GustFrontCurvature;
                float _GustFrontOverlap;
                float _GustFrontBreakup;
                float _StemBend;

                float4 _NearColor;
                float4 _FarColor;
                float4 _NearFarRange;

                float4 _BottomColor;
                float _HeightBlend;

                float4 _ShadowColor;

                float _UseTerrainColor;
                float4 _TerrainColor;

                float4 _WindTexture_ST;
                float4 _WindNoiseScale;
                float _WindNoiseSpeed;
                float4 _WindNoiseContrast;
                float _WindHeight;
                float _GustFrontTrail;
                float _GustFrontWarp;
                float _GustFrontLateralScale;

                float _DownBend;
                float _MacroWaveStrength;
                float _FlutterSpeed;
            
            
            CBUFFER_END

            /// <summary>
            /// Calculates blade height mask in object space.
            /// </summary>
            /// <remarks>
            /// Lower part stays more stable, upper part receives stronger wind/color blend.
            /// </remarks>
            /// <param name="positionOS">Object-space vertex position.</param>
            /// <returns>Height mask in range 0..1.</returns>
            float GetBladeHeightMask(float3 positionOS)
            {
                return saturate(positionOS.y * _HeightBlend);
            }

            /// <summary>
            /// Samples animated wind noise in world space.
            /// </summary>
            /// <remarks>
            /// World-space sampling helps multiple grass patches move coherently.
            /// </remarks>
            /// <param name="worldPos">World-space position.</param>
            /// <returns>Wind noise value in range 0..1.</returns>
            float SampleWindNoise(float3 worldPos)
            {
                float2 windDir = normalize(_WindDirection.xy + float2(0.0001, 0.0001));

                float2 uv;
                uv.x = worldPos.x / max(_WindNoiseScale.x, 0.0001);
                uv.y = worldPos.z / max(_WindNoiseScale.y, 0.0001);

                uv += windDir * (_Time.y * _WindNoiseSpeed);

                // Vertex-stage texture sampling on D3D11 needs an explicit LOD.
                float noise = SAMPLE_TEXTURE2D_LOD(_WindTexture, sampler_WindTexture, uv, 0).r;

                float minC = _WindNoiseContrast.x;
                float maxC = max(_WindNoiseContrast.y, minC + 0.0001);

                noise = saturate((noise - minC) / (maxC - minC));

                return noise;
            }

            /// <summary>
            /// Calculates distance-based grass tint from camera.
            /// </summary>
            /// <param name="worldPos">Fragment world position.</param>
            /// <returns>Near/Far blended color.</returns>
            float3 GetDistanceTint(float3 worldPos)
            {
                float dist = distance(_WorldSpaceCameraPos, worldPos);

                float nearRange = _NearFarRange.x;
                float farRange = max(_NearFarRange.y, nearRange + 0.0001);

                float t = saturate((dist - nearRange) / (farRange - nearRange));

                return lerp(_NearColor.rgb, _FarColor.rgb, t);
            }

            /// <summary>
            /// Calculates height-based blade tint.
            /// </summary>
            /// <param name="positionOS">Original object-space position.</param>
            /// <returns>Bottom-to-top blended color.</returns>
            float3 GetHeightTint(float3 positionOS)
            {
                float h = GetBladeHeightMask(positionOS);
                return lerp(_BottomColor.rgb, _BaseColor.rgb, h);
            }

            float GetBladeMaskFromUV(float uvY)
            {
                return saturate(uvY);
            }

            /// <summary>
            /// Remaps a value into 0..1 using a min/max range.
            /// </summary>
            /// <param name="value">Input value.</param>
            /// <param name="minMax">Range in x=min, y=max.</param>
            /// <returns>Remapped value in range 0..1.</returns>
            float Remap01(float value, float2 minMax)
            {
                float maxValue = max(minMax.y, minMax.x + 0.0001);
                return saturate((value - minMax.x) / (maxValue - minMax.x));
            }

            /// <summary>
            /// Samples grayscale noise in world space.
            /// </summary>
            /// <param name="uv">Noise UV.</param>
            /// <returns>Noise value in range 0..1.</returns>
            float SampleNoise01(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_WindTexture, sampler_WindTexture, uv, 0).r;
            }

            float2 SampleFrontSignal(float coord, float travel, float spacing, float width, float trail)
            {
                float phase = frac((coord - travel) / spacing);
                float distBehindFront = phase * spacing;

                float safeWidth = max(width, 0.001);
                float safeTrail = max(trail, 0.001);
                float totalLength = safeWidth + safeTrail;

                float head = exp2(-(distBehindFront * distBehindFront) / (safeWidth * safeWidth * 1.7));
                float tail = exp2(-distBehindFront / (safeTrail * 0.9));
                float fadeOut = 1.0 - smoothstep(safeWidth + safeTrail * 1.2, safeWidth + safeTrail * 2.6, distBehindFront);
                float front = saturate(max(head, tail * 0.62) * fadeOut);

                float waveT = saturate(distBehindFront / max(totalLength, 0.001));
                float waveRise = smoothstep(0.03, 0.26, waveT);
                float waveFall = 1.0 - smoothstep(0.72, 1.02, waveT);
                float undulation = sin(waveT * 3.14159265) * waveRise * waveFall * front;

                return float2(front, undulation);
            }

            float SampleFrontBreakup(float2 breakupUV, float breakupAmount)
            {
                float breakupA = SampleNoise01(breakupUV);
                float breakupB = SampleNoise01(breakupUV * 1.91 + float2(0.37, 0.63));
                float breakupMask = smoothstep(0.20, 0.86, breakupA * 0.62 + breakupB * 0.38);
                return lerp(1.0, breakupMask, breakupAmount);
            }

            float2 SampleGustFront(float along, float across, float2 fieldUV, float warp)
            {
                float width = max(_GustFrontWidth, 0.001);
                float trail = max(_GustFrontTrail, 0.001);
                float spacing = max(_GustFrontSpacing, width + trail + 0.001);
                float spacingB = max(spacing * 0.74, width * 1.12 + trail * 0.82 + 0.001);
                float spacingC = max(spacing * 1.43, width * 0.86 + trail * 1.36 + 0.001);
                float curvature = max(_GustFrontCurvature, 0.0);
                float lateralScale = max(_GustFrontLateralScale, 0.001);
                float overlap = max(_GustFrontOverlap, 0.0);
                float breakupAmount = saturate(_GustFrontBreakup);
                float travel = _Time.y * _GustFrontSpeed;

                // Keep front shaping anchored in world space so the wave sweeps through the grass
                // instead of re-shaping every frame.
                float lateralShiftA = SampleNoise01(fieldUV * 0.33 + float2(0.17, 0.29));
                float lateralShiftB = SampleNoise01(fieldUV * 0.51 + float2(0.61, -0.13));
                float lateralShiftC = SampleNoise01(fieldUV * 0.79 + float2(-0.24, 0.09));

                lateralShiftA = (lateralShiftA * 2.0 - 1.0) * _GustFrontWarp * 5.0;
                lateralShiftB = (lateralShiftB * 2.0 - 1.0) * _GustFrontWarp * 3.6 + lateralScale * 0.35;
                lateralShiftC = (lateralShiftC * 2.0 - 1.0) * _GustFrontWarp * 6.4 - lateralScale * 0.42;

                float shiftedAcrossA = across + lateralShiftA;
                float shiftedAcrossB = across + lateralShiftB;
                float shiftedAcrossC = across + lateralShiftC;

                float coordA = along - shiftedAcrossA * shiftedAcrossA * curvature + warp * 1.35;
                float coordB = along - shiftedAcrossB * shiftedAcrossB * (curvature * 0.72) + warp * 0.92;
                float coordC = along - shiftedAcrossC * shiftedAcrossC * (curvature * 1.28) + warp * 1.82;

                float2 layerA = SampleFrontSignal(coordA, travel, spacing, width, trail);
                float2 layerB = SampleFrontSignal(
                    coordB,
                    travel * 1.12 + spacing * 0.37,
                    spacingB,
                    width * 1.12,
                    trail * 0.82);
                float2 layerC = SampleFrontSignal(
                    coordC,
                    travel * 0.81 + spacing * 0.71,
                    spacingC,
                    width * 0.86,
                    trail * 1.36);

                float frontCoordA = coordA - travel;
                float frontCoordB = coordB - (travel * 1.12 + spacing * 0.37);
                float frontCoordC = coordC - (travel * 0.81 + spacing * 0.71);

                float breakupA = SampleFrontBreakup(
                    float2(shiftedAcrossA / lateralScale, frontCoordA * 0.028) + float2(0.23, 0.41),
                    breakupAmount);
                float breakupB = SampleFrontBreakup(
                    float2(shiftedAcrossB / (lateralScale * 0.82), frontCoordB * 0.031) + float2(0.61, -0.27),
                    breakupAmount);
                float breakupC = SampleFrontBreakup(
                    float2(shiftedAcrossC / (lateralScale * 1.24), frontCoordC * 0.024) + float2(-0.37, 0.18),
                    breakupAmount);

                float front = layerA.x * breakupA;
                front += layerB.x * breakupB * overlap * 0.76;
                front += layerC.x * breakupC * overlap * 0.52;

                float wave = layerA.y * breakupA;
                wave += layerB.y * breakupB * overlap * 0.76;
                wave += layerC.y * breakupC * overlap * 0.52;

                return float2(min(front, 1.85), min(wave, 1.25)) * _GustFrontStrength;
            }

            float GetStemBendProfile(float bladeMask)
            {
                float stemT = saturate(_StemBend);

                float tipOnly = pow(bladeMask, 2.8);
                float softStem = pow(bladeMask, 1.4);
                float fullStem = pow(bladeMask, 0.58);

                float profile = lerp(tipOnly, softStem, saturate(stemT * 2.0));
                profile = lerp(profile, fullStem, saturate((stemT - 0.5) * 2.0));

                float tipBias = saturate((_TopBend - 0.1) / 4.9);
                float tipFocus = pow(bladeMask, lerp(1.0, 1.85, tipBias));

                return lerp(profile, profile * tipFocus, tipBias * 0.55);
            }

            /// <summary>
            /// Calculates height-based blade tint.
            /// </summary>
            /// <param name="bladeMask">Blade mask from uv.y.</param>
            /// <returns>Bottom-to-top blended tint.</returns>
            float3 GetHeightTint(float bladeMask)
            {
                return lerp(_BottomColor.rgb, _BaseColor.rgb, bladeMask);
            }

            float3 ApplyWind(float3 positionOS, float bladeMask)
            {
                float3 worldPos = TransformObjectToWorld(positionOS);

                float2 dir = normalize(_WindDirection.xy + float2(0.0001, 0.0001));
                float2 perp = float2(-dir.y, dir.x);
                float2 noiseScale = max(_WindNoiseScale.xy, float2(0.001, 0.001));

                float2 fieldUV = worldPos.xz / noiseScale;
                float2 flow = dir * (_Time.y * (_WindNoiseSpeed * 0.15));
                float2 warpFlow = perp * (_Time.y * (_WindNoiseSpeed * 0.03));

                float along = dot(worldPos.xz, dir);
                float across = dot(worldPos.xz, perp);

                // ------------------------------------------------------------
                // 1) Domain warp
                // ------------------------------------------------------------
                float warpA = SampleNoise01(fieldUV * 0.55 + warpFlow + float2(0.11, 0.37));
                float warpB = SampleNoise01(fieldUV * 0.95 + flow * 0.35 + float2(0.53, 0.07));
                float warp = ((warpA + warpB) * 0.5 * 2.0 - 1.0) * 2.2;

                // ------------------------------------------------------------
                // 2) Broad gust zones
                // ------------------------------------------------------------
                float gustNoise = SampleNoise01(fieldUV + flow + dir * warp * 0.12);
                gustNoise = smoothstep(_WindNoiseContrast.x, max(_WindNoiseContrast.y, _WindNoiseContrast.x + 0.0001),
                    gustNoise);

                // Stronger contrast than before so wave regions are readable.
                gustNoise = lerp(0.20, 1.25, gustNoise);

                // ------------------------------------------------------------
                // 3) Travelling wave front
                // ------------------------------------------------------------
                float curvedAlong = along - across * across * (_GustFrontCurvature * 0.35);
                float bandPhaseA = curvedAlong * _WaveFrequency + warp * 0.36 - _Time.y * _WindSpeed;
                float bandPhaseB =
                    curvedAlong * (_WaveFrequency * 0.58 + 0.02) - warp * 0.24 - _Time.y * (_WindSpeed * 0.63) + 1.7;
                float band = (sin(bandPhaseA) * 0.65 + sin(bandPhaseB) * 0.35) * 0.5 + 0.5;

                float sharpT = saturate((_WaveSharpness - 1.0) / 7.0);
                float bandMin = lerp(0.16, 0.32, sharpT);
                float bandMax = lerp(0.70, 0.90, sharpT);
                band = smoothstep(bandMin, bandMax, band);

                float macroInfluence = saturate(_MacroWaveStrength);
                float oceanMask = lerp(1.0, lerp(0.70, 1.35, band), macroInfluence);

                // ------------------------------------------------------------
                // 4) Directional gust fronts
                // ------------------------------------------------------------
                float waveEnabled = step(0.5, _EnableWave);
                float2 gustSignal = SampleGustFront(along, across, fieldUV, warp) * waveEnabled;
                float gustFront = gustSignal.x;
                float gustWave = gustSignal.y;

                // ------------------------------------------------------------
                // 5) Side variation
                // ------------------------------------------------------------
                float sideMask = 1.0 + sin(across * 0.035 - _Time.y * (_WindSpeed * 0.15)) * _SideVariation;
                sideMask = max(sideMask, 0.35);

                // ------------------------------------------------------------
                // 6) Tip flutter
                // ------------------------------------------------------------
                float detailScale = max(_WindScale, 0.001);
                float flutterNoise = SampleNoise01(
                    fieldUV * detailScale * 2.6 + flow * (_FlutterSpeed * 0.9) + float2(0.27, 0.61));
                flutterNoise = flutterNoise * 2.0 - 1.0;

                float flutterSine = sin(along * (detailScale * 1.6) - _Time.y * _FlutterSpeed + across * 0.08);
                float flutter = (flutterNoise * 0.55 + flutterSine * 0.45) * _DetailStrength;

                // ------------------------------------------------------------
                // 7) Body mask vs tip mask
                // ------------------------------------------------------------
                float rootLock = smoothstep(0.03, lerp(0.18, 0.09, saturate(_StemBend)), bladeMask);
                float stemProfile = GetStemBendProfile(bladeMask);
                float mainBendMask = rootLock * stemProfile;

                float flutterHeightMask = smoothstep(1.0 - _WindHeight, 1.0, bladeMask);
                float flutterBodyMask = pow(bladeMask, lerp(1.9, 1.05, saturate(_StemBend)));
                float flutterMask = rootLock * lerp(flutterBodyMask, flutterHeightMask, 0.68);

                // ------------------------------------------------------------
                // 8) Final bend
                // ------------------------------------------------------------
                float backgroundBend = oceanMask * gustNoise * sideMask;
                float frontResponse = smoothstep(0.02, 0.88, saturate(gustFront * 0.9));
                float impactBend = frontResponse * (0.45 + gustNoise * 0.65);
                float waveRoll = gustWave * (0.16 + gustNoise * 0.10);
                float bend = (backgroundBend + impactBend) * _WindStrength * mainBendMask;

                float2 offsetXZ = dir * bend;

                // Gust fronts flatten the grass more aggressively along their path.
                float flatten = frontResponse * _WindStrength * mainBendMask;
                offsetXZ += dir * (flatten * 0.62);

                // A soft travelling swell rides with the front so the grass rolls with the wave.
                float rollAmount = waveRoll * _WindStrength * mainBendMask;
                offsetXZ += dir * (rollAmount * 0.22);
                offsetXZ += perp * (rollAmount * 0.10);

                // Small sideways softness with extra spread near the gust front.
                offsetXZ += perp * (bend * 0.14 + flatten * 0.07);

                // Tip flutter rides on top of the main bend.
                offsetXZ += dir * (flutter * flutterMask * 0.22);
                offsetXZ += perp * (flutter * flutterMask * 0.10);

                float waveLift = rollAmount * 0.22;
                float offsetY = -((bend * bend) + flatten * 0.42) * _DownBend + waveLift;

                return positionOS + float3(offsetXZ.x, offsetY, offsetXZ.y);
            }

            /// <summary>
            /// Vertex shader.
            /// </summary>
            /// <param name="input">Mesh vertex input.</param>
            /// <returns>Interpolated data for fragment stage.</returns>
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float bladeMask = GetBladeMaskFromUV(input.uv.y);
                float3 animatedPosOS = ApplyWind(input.positionOS.xyz, bladeMask);
                float3 worldPos = TransformObjectToWorld(animatedPosOS);

                output.positionHCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldPos = worldPos;
                output.bladeMask = bladeMask;

                return output;
            }

            /// <summary>
            /// Fragment shader with alpha clipping and layered grass tint.
            /// </summary>
            /// <param name="input">Interpolated fragment input.</param>
            /// <returns>Final pixel color.</returns>
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 color = tex * _BaseColor;

                clip(color.a - _Cutoff);

                float3 distanceTint = GetDistanceTint(input.worldPos);
                float3 heightTint = GetHeightTint(input.bladeMask);

                color.rgb *= distanceTint;
                color.rgb *= heightTint;

                if (_UseTerrainColor > 0.5)
                {
                    color.rgb = lerp(color.rgb, color.rgb * _TerrainColor.rgb, 0.35);
                }

                return color;
            }
            ENDHLSL
        }
    }

    CustomEditor "GrassWindShaderGUI"
}
