Shader "Hidden/GrassTrailMap"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Fade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFade

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _FadeMultiplier;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 FragFade(Varyings input) : SV_Target
            {
                half4 previous = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half trail = previous.r * _FadeMultiplier;
                half2 direction = lerp(half2(0.5h, 0.5h), previous.gb, previous.r);
                return half4(trail, direction.x, direction.y, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Stamp"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragStamp

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _TrailWorldBounds;
            float4 _StampParams;
            float4 _StampMotion;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float ComputeTrailDistance(float2 delta, float radius, float3 stampMotion)
            {
                float2 velocity = stampMotion.xy;
                float speed = length(velocity);
                if (speed <= 0.0001)
                {
                    return length(delta);
                }

                float2 dir = velocity / speed;
                float along = dot(delta, dir);
                float2 lateralVec = delta - dir * along;
                float lateral = length(lateralVec);
                float stretch = 1.0 + stampMotion.z * saturate(speed * 0.14) * step(along, 0.0);
                float effectiveAlong = along / max(stretch, 1.0);
                return length(float2(effectiveAlong, lateral));
            }

            half4 FragStamp(Varyings input) : SV_Target
            {
                half4 previous = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 boundsSize = max(_TrailWorldBounds.zw, float2(0.001, 0.001));
                float2 worldXZ = _TrailWorldBounds.xy + input.uv * boundsSize;
                float2 delta = worldXZ - _StampParams.xy;
                float radius = max(_StampParams.z, 0.001);
                float distanceToStamp = ComputeTrailDistance(delta, radius, _StampMotion.xyz);

                float innerRadius = radius * 0.18;
                float stamp = 1.0 - smoothstep(innerRadius, radius, distanceToStamp);
                stamp *= _StampParams.w;

                float trailMask = max(previous.r, stamp);

                float2 velocity = _StampMotion.xy;
                float speed = length(velocity);
                float2 direction = previous.r > 0.0001 ? previous.gb * 2.0 - 1.0 : float2(0.0, 0.0);

                if (speed > 0.0001 && stamp > 0.0001)
                {
                    float2 stampDirection = velocity / speed;
                    float2 blendedDirection = normalize(direction * previous.r + stampDirection * stamp + float2(0.0001, 0.0001));
                    direction = blendedDirection;
                }

                float2 encodedDirection = length(direction) > 0.0001 ? direction * 0.5 + 0.5 : float2(0.5, 0.5);
                return half4(trailMask, encodedDirection.x, encodedDirection.y, 1.0h);
            }
            ENDHLSL
        }
    }
}
