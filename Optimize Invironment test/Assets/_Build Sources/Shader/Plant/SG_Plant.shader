Shader "Custom/Vit/Plant_URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("MainTex", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,0.9510489,0,1)
        [Toggle] _EnableColor ("Enable Color", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle] _EnableLighting ("Enable Lighting", Float) = 1
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1
        _ShadowFloor ("Shadow Floor", Range(0,1)) = 0.15
        [Toggle] _EnableMainLight ("Enable Main Light", Float) = 1
        _MainLightIntensity ("Main Light Intensity", Range(0,4)) = 1.2
        [Toggle] _EnableAdditionalLights ("Enable Additional Lights", Float) = 0
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0,4)) = 1
        [Toggle] _EnableAmbient ("Enable Ambient", Float) = 1
        _AmbientIntensity ("Ambient Intensity", Range(0,4)) = 0.5
        [Toggle] _TwoSidedLighting ("Two-Sided Lighting", Float) = 1

        [Toggle] _EnableWind ("Enable Wind", Float) = 1
        [NoScaleOffset] _WindTexture ("Wind Noise", 2D) = "gray" {}
        _WindSpeed ("Lean", Range(0,10)) = 10
        _WindDirection ("Wind Direction XZ", Vector) = (0.4472136,0.8944272,0,0)
        [Toggle] _EnableGrassConeShape ("Enable Grass Cone Shape", Float) = 0
        _GrassConeTipScale ("Grass Cone Tip Scale", Range(0.25,4)) = 1.25
        [Toggle] _EnableGrassDistanceBlur ("Enable Grass Distance Blur", Float) = 0
        [Toggle] _EnableGrassTransparentBlurPath ("Enable Grass Transparent Blur Path", Float) = 0
        _GrassDistanceBlurStart ("Grass Distance Blur Start", Range(0,200)) = 18
        _GrassDistanceBlurEnd ("Grass Distance Blur End", Range(0.1,300)) = 45
        _GrassDistanceBlurRadius ("Grass Distance Blur Radius", Range(0,8)) = 2
        _GrassDistanceBlurOpacity ("Grass Distance Blur Opacity", Range(0.1,1)) = 0.68
        _GrassDistanceBlurBrightness ("Grass Distance Blur Brightness", Range(0,1)) = 0.35
        _GrassDistanceBlurCutoffShift ("Grass Distance Blur Cutoff Shift", Range(0,0.5)) = 0.12
        [Toggle] _EnableGrassShadowNoise ("Enable Wind Noise", Float) = 0
        _GrassShadowNoiseStrength ("Wind Noise Shadow Strength", Range(0,1)) = 0
        _GrassShadowNoiseContrast ("Wind Noise Shadow Contrast", Range(0.1,4)) = 1.5
        [HideInInspector] _PlantSrcBlend ("Plant Src Blend", Float) = 1
        [HideInInspector] _PlantDstBlend ("Plant Dst Blend", Float) = 0
        [HideInInspector] _PlantZWrite ("Plant ZWrite", Float) = 1

        [Toggle] _EnableWaveShape ("Enable Wind Vibrate", Float) = 1
        [HideInInspector] _WaveFrequency ("Wave Frequency", Range(0.1,12)) = 1.2
        [HideInInspector] _WaveSpacingVariation ("Wave Spacing Variation", Range(0,2)) = 1
        [HideInInspector] _WaveSpeed ("Wave Speed", Range(0,8)) = 4
        [HideInInspector] _WaveStrength ("Wave Strength", Range(0,2)) = 0.5
        [HideInInspector] _WaveBodyInfluence ("Body Wave", Range(0,1)) = 1
        [HideInInspector] _WaveTipInfluence ("Tip Wave", Range(0,1)) = 1
        [HideInInspector] _WaveLateralInfluence ("Lateral Wave", Range(0,1)) = 1

        [HideInInspector] _WindTextureScale ("Wind Noise Scale", Vector) = (60,50,0,0)
        [HideInInspector] _WindTextureScrollSpeed ("Wind Noise Scroll Speed", Range(0,5)) = 0.15
        [HideInInspector] _WindTextureContrast ("Wind Noise Contrast", Vector) = (0.2,0.8,0,0)
        [HideInInspector] _WindTextureInfluence ("Noise To Lean", Range(0,1)) = 0.8
        [HideInInspector] _WindTextureWaveInfluence ("Noise To Vibrate", Range(0,1)) = 0

        [HideInInspector] _NearColor ("Near Color", Color) = (0.6595166,0.7132074,0,1)
        [HideInInspector] _FarColor ("Far Color", Color) = (0.8032724,1,0,1)
        [HideInInspector] _NearFarRange ("Near/Far Range", Vector) = (5,18,0,0)
        [HideInInspector] _BottomColor ("Bottom Color", Color) = (1,1,1,1)
        [HideInInspector] _HeightBlend ("Height Blend", Range(0,20)) = 4.5

        [Toggle] _EnableTerrain ("Enable Terrain", Float) = 1
        [HideInInspector] _UseTerrainColor ("Use Terrain Color", Float) = 1
        [HideInInspector] _TerrainColor ("Terrain Color", Color) = (0.9887863,1,0,1)
        [HideInInspector] _TerrainBlendStrength ("Terrain Blend Strength", Range(0,1)) = 1
        [HideInInspector] _DetailFallbackMode ("Detail Fallback Mode", Float) = 0
        [HideInInspector] _DetailFallbackLightingMin ("Detail Fallback Lighting Min", Range(0,1)) = 0.8

        [Toggle] _EnableInteraction ("Enable Interaction", Float) = 1
        [HideInInspector] _InteractionStrength ("Interaction Strength", Range(0,2)) = 1
        [HideInInspector] _InteractionPushAway ("Interaction Push Away", Range(0,2)) = 0.1
        [HideInInspector] _InteractionFlatten ("Interaction Flatten", Range(0,1)) = 0
        [HideInInspector] _InteractionRadiusMultiplier ("Interaction Radius Multiplier", Range(0.25,4)) = 1.2
        [HideInInspector] _InteractionVerticalRange ("Interaction Vertical Range", Range(0.1,5)) = 1
        [HideInInspector] _InteractionTrail ("Interaction Trail Response", Range(0,1)) = 0.3
        [HideInInspector] _InteractionRecoveryStrength ("Interaction Recovery Strength", Range(0,1)) = 0.5
        [HideInInspector] _InteractionRecoveryFrequency ("Interaction Recovery Frequency", Range(0,24)) = 10
        [HideInInspector] _InteractionRecoveryNoiseScale ("Interaction Recovery Noise Scale", Range(0,8)) = 5
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
        #include "Includes/SG_PlantCommon.hlsl"
        #include "../Grass/SG_GrassShape.hlsl"
        #include "Includes/SG_PlantWind.hlsl"
        #include "Includes/SG_PlantInteraction.hlsl"
        #include "Includes/SG_PlantTerrainBlend.hlsl"
        #include "Includes/SG_PlantLighting.hlsl"

        float3 ApplyPlantMotion(float3 worldPos, float bladeMask)
        {
            float3 animatedWorldPos = ApplyWind(worldPos, bladeMask);
            if (UseDetailFallback() <= 0.5)
            {
                animatedWorldPos = ApplyInteraction(animatedWorldPos, bladeMask);
            }

            return animatedWorldPos;
        }

        struct PlantDepthAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct PlantDepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 worldPos : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct PlantDepthNormalsVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 worldPos : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct PlantSurfaceSample
        {
            half4 baseTex;
            half4 blurTex;
            half baseAlpha;
            half blurAlpha;
            half solidAlpha;
            half transparentAlpha;
            float blur01;
        };

        float3 GetShapedPlantObjectPos(float3 positionOS, float2 uv)
        {
            float bladeMask = GetBladeMaskFromUV(uv.y);
            return ApplyGrassConeShapeOS(positionOS, bladeMask);
        }

        float3 GetAnimatedPlantWorldPos(float4 positionOS, float2 uv)
        {
            float bladeMask = GetBladeMaskFromUV(uv.y);
            float3 shapedPositionOS = GetShapedPlantObjectPos(positionOS.xyz, uv);
            float3 baseWorldPos = TransformObjectToWorld(shapedPositionOS);
            return ApplyPlantMotion(baseWorldPos, bladeMask);
        }

        PlantSurfaceSample SamplePlantSurface(float2 uv, float3 worldPos)
        {
            PlantSurfaceSample sample;
            sample.blur01 = GetGrassDistanceBlur01(worldPos);
            sample.baseTex = SampleGrassSourceBaseMap(uv);
            sample.blurTex = SampleGrassBaseMap(uv, sample.blur01);
            sample.baseAlpha = sample.baseTex.a * _BaseColor.a;
            sample.blurAlpha = sample.blurTex.a * _BaseColor.a;
            sample.solidAlpha = step(_Cutoff, sample.baseAlpha) * GetGrassSolidFade01(sample.blur01);
            sample.transparentAlpha = GetGrassTransparentAlpha(sample.blurAlpha, sample.blur01);
            return sample;
        }

        void AlphaClipPlant(float2 uv, float3 worldPos)
        {
            PlantSurfaceSample sample = SamplePlantSurface(uv, worldPos);
            float useTransparentBlurPath = GetToggle01(_EnableGrassDistanceBlur) * GetToggle01(_EnableGrassTransparentBlurPath);
            if (useTransparentBlurPath > 0.5)
            {
                clip(sample.baseAlpha - _Cutoff);

                float solidFade = GetGrassSolidFade01(sample.blur01);
                if (solidFade < 0.9999)
                {
                    clip(solidFade - GetGrassDitherNoise(worldPos, uv));
                }

                return;
            }

            clip(sample.blurAlpha - GetGrassBlurredCutoff(sample.blur01, _Cutoff));
        }

        PlantDepthVaryings DepthOnlyVertex(PlantDepthAttributes input)
        {
            PlantDepthVaryings output = (PlantDepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 worldPos = GetAnimatedPlantWorldPos(input.positionOS, input.uv);
            output.positionCS = TransformWorldToHClip(worldPos);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.worldPos = worldPos;
            return output;
        }

        half DepthOnlyFragment(PlantDepthVaryings input) : SV_TARGET
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            AlphaClipPlant(input.uv, input.worldPos);
            return input.positionCS.z;
        }

        PlantDepthNormalsVaryings DepthNormalsOnlyVertex(PlantDepthAttributes input)
        {
            PlantDepthNormalsVaryings output = (PlantDepthNormalsVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 worldPos = GetAnimatedPlantWorldPos(input.positionOS, input.uv);
            output.positionCS = TransformWorldToHClip(worldPos);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.worldPos = worldPos;
            return output;
        }

        void DepthNormalsOnlyFragment(
            PlantDepthNormalsVaryings input,
            out half4 outNormalWS : SV_Target0
        #ifdef _WRITE_RENDERING_LAYERS
            , out uint outRenderingLayers : SV_Target1
        #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            AlphaClipPlant(input.uv, input.worldPos);

        #if defined(_GBUFFER_NORMALS_OCT)
            float3 normalWS = normalize(input.normalWS);
            float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
            float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
            half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
            outNormalWS = half4(packedNormalWS, 0.0);
        #else
            float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            outNormalWS = half4(normalWS, 0.0);
        #endif

        #ifdef _WRITE_RENDERING_LAYERS
            outRenderingLayers = EncodeMeshRenderingLayer();
        #endif
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
            ZWrite [_PlantZWrite]
            Blend [_PlantSrcBlend] [_PlantDstBlend]
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

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
                half fogFactor : TEXCOORD5;
                float3 shadowWorldPos : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float bladeMask = GetBladeMaskFromUV(input.uv.y);
                float3 shapedPositionOS = GetShapedPlantObjectPos(input.positionOS.xyz, input.uv);
                float3 baseWorldPos = TransformObjectToWorld(shapedPositionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 worldPos = ApplyPlantMotion(baseWorldPos, bladeMask);
                float3 normalWSNormalized = normalize(normalWS);

                output.positionHCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldPos = worldPos;
                output.bladeMask = bladeMask;
                output.normalWS = normalWS;
                output.vertexLighting = float3(0.0, 0.0, 0.0);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.shadowWorldPos = baseWorldPos;

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

                PlantSurfaceSample surface = SamplePlantSurface(input.uv, input.worldPos);
                float useTransparentBlurPath = GetToggle01(_EnableGrassDistanceBlur) * GetToggle01(_EnableGrassTransparentBlurPath);

                half alpha;
                float3 color;
                float colorEnabled = ColorEnabled01();
                float3 baseColorTint = lerp(float3(1.0, 1.0, 1.0), _BaseColor.rgb, colorEnabled);
                if (useTransparentBlurPath > 0.5)
                {
                    alpha = saturate(surface.solidAlpha + surface.transparentAlpha);
                    clip(max(surface.baseAlpha - _Cutoff, surface.transparentAlpha - 0.001h));

                    float baseWeight = surface.solidAlpha;
                    float blurWeight = surface.transparentAlpha;
                    float weightSum = max(baseWeight + blurWeight, 0.0001);
                    float3 baseColor = surface.baseTex.rgb * baseColorTint;
                    float3 blurColor = surface.blurTex.rgb * baseColorTint;
                    color = ((baseColor * baseWeight) + (blurColor * blurWeight)) / weightSum;
                }
                else
                {
                    alpha = surface.blurAlpha;
                    clip(alpha - GetGrassBlurredCutoff(surface.blur01, _Cutoff));
                    color = surface.blurTex.rgb * baseColorTint;
                }

                color *= lerp(float3(1.0, 1.0, 1.0), GetDistanceTint(input.worldPos), colorEnabled);
                color *= lerp(float3(1.0, 1.0, 1.0), GetHeightTint(input.bladeMask), colorEnabled);
                color = ApplyTerrainBlend(color, input.worldPos);
                color *= GetGrassShadowNoiseAttenuation(input.shadowWorldPos);

                if (GetToggle01(_EnableLighting) < 0.5)
                {
                    color = MixFog(color, input.fogFactor);
                    return half4(color, alpha);
                }

                float3 normalWS = normalize(input.normalWS);
                float3 lighting = 0.0;

                if (UseDetailFallback() > 0.5)
                {
                    if (GetToggle01(_EnableAmbient) > 0.5)
                    {
                        lighting += _AmbientIntensity;
                    }

                    if (GetToggle01(_EnableMainLight) > 0.5)
                    {
                        Light mainLight = GetMainLight();
                        lighting += mainLight.color * (GetDiffuseTerm(normalWS, mainLight.direction) * _MainLightIntensity);
                    }

                    color *= max(lighting, _DetailFallbackLightingMin.xxx);
                    return half4(saturate(color), alpha);
                }

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
                color = MixFog(color, input.fogFactor);

                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode"="DepthOnly"
            }

            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode"="DepthNormalsOnly"
            }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsOnlyVertex
            #pragma fragment DepthNormalsOnlyFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
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
                float3 worldPos : TEXCOORD1;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float bladeMask = GetBladeMaskFromUV(input.uv.y);
                float3 shapedPositionOS = GetShapedPlantObjectPos(input.positionOS.xyz, input.uv);
                float3 baseWorldPos = TransformObjectToWorld(shapedPositionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 worldPos = ApplyPlantMotion(baseWorldPos, bladeMask);

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
                float bladeMask = GetBladeMaskFromUV(input.uv.y);
                float3 shapedPositionOS = GetShapedPlantObjectPos(input.positionOS.xyz, input.uv);
                float3 baseWorldPos = TransformObjectToWorld(shapedPositionOS);
                float3 worldPos = ApplyPlantMotion(baseWorldPos, bladeMask);

                output.worldPos = worldPos;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                AlphaClipPlant(input.uv, input.worldPos);
                return 0;
            }
            ENDHLSL
        }
    }

    CustomEditor "PlantShaderGUI"
}
