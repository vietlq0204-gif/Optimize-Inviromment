Shader "Hidden/Vit/SunShaftsFullscreen"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "SunShafts" 

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _SunShaftsSunPos;
            float4 _SunShaftsTint;
            float4 _SunShaftsLightColor;
            float _SunShaftsIntensity;
            float _SunShaftsBlurRadius;
            float _SunShaftsMaxRadius;
            float _SunShaftsDecay;
            float _SunShaftsWeight;
            float _SunShaftsExposure;
            float _SunShaftsDepthThreshold;
            float _SunShaftsColorThreshold;
            float _SunShaftsColorInfluence;
            float _SunShaftsRadialFalloff;
            float _SunShaftsEdgeFade;
            int _SunShaftsSampleCount;

            float GetSceneDepth01(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return Linear01Depth(rawDepth, _ZBufferParams);
            }

            float GetSceneDepth01NoDerivatives(float2 uv)
            {
                uv = ClampAndScaleUVForBilinear(UnityStereoTransformScreenSpaceTex(uv), _CameraDepthTexture_TexelSize.xy);
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
                return Linear01Depth(rawDepth, _ZBufferParams);
            }

            float GetSkyVisibility(float2 uv)
            {
                return smoothstep(_SunShaftsDepthThreshold, 1.0, GetSceneDepth01(uv));
            }

            float GetSkyVisibilityNoDerivatives(float2 uv)
            {
                return smoothstep(_SunShaftsDepthThreshold, 1.0, GetSceneDepth01NoDerivatives(uv));
            }

            float GetLuminance(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float GetBrightnessMask(float3 color)
            {
                float luminance = GetLuminance(color);
                return saturate((luminance - _SunShaftsColorThreshold) / max(1e-4, 1.0 - _SunShaftsColorThreshold));
            }

            float GetEdgeFade(float2 sunUv)
            {
                float2 centered = abs(sunUv * 2.0 - 1.0);
                float edge = saturate(1.0 - max(centered.x, centered.y));
                return lerp(1.0, edge, saturate(_SunShaftsEdgeFade));
            }

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                if (_SunShaftsSunPos.z <= 0.0 || _SunShaftsIntensity <= 0.0 || _SunShaftsSampleCount <= 0)
                {
                    return sceneColor;
                }

                float2 sunUv = _SunShaftsSunPos.xy;
                float2 rayToSun = sunUv - uv;
                float distanceToSun = length(rayToSun);
                float maxRadius = max(_SunShaftsMaxRadius, 1e-4);
                float radialMask = saturate(1.0 - distanceToSun / maxRadius);

                if (radialMask <= 0.0)
                {
                    return sceneColor;
                }

                radialMask = pow(radialMask, max(_SunShaftsRadialFalloff, 1e-4));

                float sampleCount = max((float)_SunShaftsSampleCount, 1.0);
                float2 stepUv = rayToSun * (_SunShaftsBlurRadius / sampleCount);
                float2 sampleUv = uv;
                float illuminationDecay = 1.0;
                float shafts = 0.0;
                float sunVisibility = GetSkyVisibilityNoDerivatives(saturate(sunUv));

                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= _SunShaftsSampleCount)
                    {
                        break;
                    }

                    sampleUv += stepUv;
                    float2 clampedUv = saturate(sampleUv);

                    float skyVisibility = GetSkyVisibilityNoDerivatives(clampedUv);
                    float3 sampleColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, clampedUv, 0).rgb;
                    float brightnessMask = GetBrightnessMask(sampleColor);
                    float occlusionMask = lerp(skyVisibility, skyVisibility * brightnessMask, saturate(_SunShaftsColorInfluence));

                    shafts += occlusionMask * illuminationDecay * _SunShaftsWeight;
                    illuminationDecay *= _SunShaftsDecay;
                }

                shafts *= _SunShaftsExposure;
                shafts *= _SunShaftsIntensity;
                shafts *= sunVisibility;
                shafts *= radialMask;
                shafts *= GetEdgeFade(sunUv);

                float3 shaftsColor = shafts * _SunShaftsTint.rgb * _SunShaftsLightColor.rgb;
                return half4(sceneColor.rgb + shaftsColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
