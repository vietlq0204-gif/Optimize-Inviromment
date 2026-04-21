Shader "Custom/Vit/Plant_URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1
        _ShadowFloor ("Shadow Floor", Range(0,1)) = 0
        [Toggle] _EnableMainLight ("Enable Main Light", Float) = 1
        _MainLightIntensity ("Main Light Intensity", Range(0,4)) = 1
        [Toggle] _EnableAdditionalLights ("Enable Additional Lights", Float) = 1
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0,4)) = 1
        [Toggle] _EnableAmbient ("Enable Ambient", Float) = 1
        _AmbientIntensity ("Ambient Intensity", Range(0,4)) = 1
        [Toggle] _TwoSidedLighting ("Two-Sided Lighting", Float) = 1

        [NoScaleOffset] _WindTexture ("Wind Texture", 2D) = "gray" {}
        _WindSpeed ("Grass Lean", Range(0,10)) = 10
        _WindDirection ("Wind Direction XZ", Vector) = (0.4472136,0.8944272,0,0)

        [Toggle] _EnableWaveShape ("Enable Wave Shape", Float) = 1
        [HideInInspector] _WaveFrequency ("Wave Frequency", Range(0.1,12)) = 3
        [HideInInspector] _WaveSpacingVariation ("Wave Spacing Variation", Range(0,2)) = 2
        [HideInInspector] _WaveSpeed ("Wave Speed", Range(0,8)) = 8
        [HideInInspector] _WaveStrength ("Wave Strength", Range(0,2)) = 0.4
        [HideInInspector] _WaveBodyInfluence ("Body Wave", Range(0,1)) = 0.5
        [HideInInspector] _WaveTipInfluence ("Tip Wave", Range(0,1)) = 1
        [HideInInspector] _WaveLateralInfluence ("Lateral Wave", Range(0,1)) = 1

        [HideInInspector] _WindTextureScale ("Texture Scale", Vector) = (60,50,0,0)
        [HideInInspector] _WindTextureScrollSpeed ("Texture Scroll Speed", Range(0,5)) = 0.25
        [HideInInspector] _WindTextureContrast ("Texture Contrast", Vector) = (0.2,0.8,0,0)
        [HideInInspector] _WindTextureInfluence ("Texture Influence", Range(0,1)) = 0.5
        [HideInInspector] _WindTextureWaveInfluence ("Texture To Wave", Range(0,1)) = 1

        [HideInInspector] _NearColor ("Near Color", Color) = (1,0.8753432,0,1)
        [HideInInspector] _FarColor ("Far Color", Color) = (1,1,1,1)
        [HideInInspector] _NearFarRange ("Near/Far Range", Vector) = (3,18,0,0)
        [HideInInspector] _BottomColor ("Bottom Color", Color) = (0.6981132,0.6981132,0.6981132,1)
        [HideInInspector] _HeightBlend ("Height Blend", Range(0,20)) = 4.5

        [HideInInspector] _UseTerrainColor ("Use Terrain Color", Float) = 0
        [HideInInspector] _TerrainColor ("Terrain Color", Color) = (0.9887863,1,0,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_WindTexture);
        SAMPLER(sampler_WindTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _ReceiveShadows;
            float _ShadowStrength;
            float _ShadowFloor;
            float _EnableMainLight;
            float _MainLightIntensity;
            float _EnableAdditionalLights;
            float _AdditionalLightIntensity;
            float _EnableAmbient;
            float _AmbientIntensity;
            float _TwoSidedLighting;
            float _WindSpeed;
            float4 _WindDirection;
            float _EnableWaveShape;
            float _WaveFrequency;
            float _WaveSpacingVariation;
            float _WaveSpeed;
            float _WaveStrength;
            float _WaveBodyInfluence;
            float _WaveTipInfluence;
            float _WaveLateralInfluence;
            float4 _WindTextureScale;
            float _WindTextureScrollSpeed;
            float4 _WindTextureContrast;
            float _WindTextureInfluence;
            float _WindTextureWaveInfluence;
            float4 _NearColor;
            float4 _FarColor;
            float4 _NearFarRange;
            float4 _BottomColor;
            float _HeightBlend;
            float _UseTerrainColor;
            float4 _TerrainColor;
        CBUFFER_END

        float GetBladeMaskFromUV(float uvY)
        {
            return saturate(uvY);
        }

        float GetToggle01(float value)
        {
            return step(0.5, value);
        }

        float GetWindLean01()
        {
            return saturate(_WindSpeed * 0.1);
        }

        float SampleWindTexture01(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_WindTexture, sampler_WindTexture, uv, 0).r;
        }

        float GetWindTextureMask(float along, float across)
        {
            float2 scale = max(_WindTextureScale.xy, float2(0.001, 0.001));
            float2 uv = float2(along / scale.x, across / scale.y);
            // Subtract time so the visible texture pattern travels with the wind direction
            // instead of appearing to move against it.
            uv.x -= _Time.y * _WindTextureScrollSpeed;

            float raw = SampleWindTexture01(uv);
            float minValue = _WindTextureContrast.x;
            float maxValue = max(_WindTextureContrast.y, minValue + 0.0001);
            float mask = saturate((raw - minValue) / (maxValue - minValue));

            return lerp(1.0, mask, saturate(_WindTextureInfluence));
        }

        float3 GetDistanceTint(float3 worldPos)
        {
            float dist = distance(_WorldSpaceCameraPos, worldPos);
            float nearRange = _NearFarRange.x;
            float farRange = max(_NearFarRange.y, nearRange + 0.0001);
            float t = saturate((dist - nearRange) / (farRange - nearRange));
            return lerp(_NearColor.rgb, _FarColor.rgb, t);
        }

        float3 GetHeightTint(float bladeMask)
        {
            float heightBlend = max(_HeightBlend, 0.0001);
            float t = saturate(bladeMask * heightBlend);
            return lerp(_BottomColor.rgb, float3(1.0, 1.0, 1.0), t);
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

            // Apply the deformation in world space so randomly rotated terrain detail
            // instances still lean in the same global wind direction.
            return worldPos + float3(offsetXZ.x, -sag, offsetXZ.y);
        }

        float GetDiffuseTerm(float3 normalWS, float3 lightDirectionWS)
        {
            float ndotl = dot(normalWS, lightDirectionWS);

            if (GetToggle01(_TwoSidedLighting) > 0.5)
            {
                ndotl = abs(ndotl);
            }

            return saturate(ndotl);
        }

        float GetRealtimeShadowTerm(float shadowAttenuation)
        {
            float atten = saturate(shadowAttenuation);
            float floorTerm = saturate(_ShadowFloor);
            float shadowTerm = max(atten, floorTerm);
            return lerp(1.0, shadowTerm, saturate(_ShadowStrength));
        }

        float3 EvaluateDiffuseLight(Light light, float3 normalWS, float intensity)
        {
            float shadowTerm = 1.0;
            if (GetToggle01(_ReceiveShadows) > 0.5)
            {
                shadowTerm = GetRealtimeShadowTerm(light.shadowAttenuation);
            }

            float diffuse = GetDiffuseTerm(normalWS, light.direction);
            float attenuation = light.distanceAttenuation * shadowTerm * intensity;
            return light.color * (diffuse * attenuation);
        }
        ENDHLSL

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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float bladeMask : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 vertexLighting : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float bladeMask = GetBladeMaskFromUV(input.uv.y);
                float3 baseWorldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldPos = ApplyWind(baseWorldPos, bladeMask);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalWSNormalized = normalize(normalWS);

                output.positionHCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldPos = worldPos;
                output.bladeMask = bladeMask;
                output.normalWS = normalWS;
                output.vertexLighting = float3(0.0, 0.0, 0.0);

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
                if (GetToggle01(_EnableAdditionalLights) > 0.5)
                {
                    uint lightsCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < lightsCount; ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, worldPos);
                        output.vertexLighting += EvaluateDiffuseLight(light, normalWSNormalized, 1.0);
                    }
                }
#endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = tex.a * _BaseColor.a;
                clip(alpha - _Cutoff);

                float3 color = tex.rgb * _BaseColor.rgb;
                color *= GetDistanceTint(input.worldPos);
                color *= GetHeightTint(input.bladeMask);

                if (_UseTerrainColor > 0.5)
                {
                    color = lerp(color, color * _TerrainColor.rgb, 0.35);
                }

                float3 normalWS = normalize(input.normalWS);
                float3 lighting = 0.0;

                if (GetToggle01(_EnableAmbient) > 0.5)
                {
                    lighting += saturate(SampleSH(normalWS)) * _AmbientIntensity;
                }

                if (GetToggle01(_EnableMainLight) > 0.5)
                {
                    Light mainLight;
                    if (GetToggle01(_ReceiveShadows) > 0.5)
                    {
                        float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                        mainLight = GetMainLight(shadowCoord);
                    }
                    else
                    {
                        mainLight = GetMainLight();
                    }

                    lighting += EvaluateDiffuseLight(mainLight, normalWS, _MainLightIntensity);
                }

#if defined(_ADDITIONAL_LIGHTS)
                if (GetToggle01(_EnableAdditionalLights) > 0.5)
                {
                    uint lightsCount = GetAdditionalLightsCount();
                    half4 shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                    LIGHT_LOOP_BEGIN(lightsCount)
                        Light light;
#if defined(_ADDITIONAL_LIGHT_SHADOWS)
                        light = GetAdditionalLight(lightIndex, input.worldPos, shadowMask);
#else
                        light = GetAdditionalLight(lightIndex, input.worldPos);
#endif
                        lighting += EvaluateDiffuseLight(light, normalWS, _AdditionalLightIntensity);
                    LIGHT_LOOP_END
                }
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
                if (GetToggle01(_EnableAdditionalLights) > 0.5)
                {
                    lighting += input.vertexLighting * _AdditionalLightIntensity;
                }
#endif

                color *= max(lighting, 0.0);

                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode"="ShadowCaster"
            }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float bladeMask = GetBladeMaskFromUV(input.uv.y);
                float3 baseWorldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldPos = ApplyWind(baseWorldPos, bladeMask);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - worldPos);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = tex.a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    CustomEditor "GrassWindShaderGUI"
}
