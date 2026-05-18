Shader "Hidden/Vit/EnvironmentInteractionBatch"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Overlay"
        }

        Pass
        {
            Name "BatchStamp"
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_SHAPES 16

            TEXTURE2D(_BaseInteractionMap);
            SAMPLER(sampler_BaseInteractionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _NeutralInteractionColor;
                float4 _InteractionRegion;
                float _ShapeCount;
            CBUFFER_END

            float4 _ShapeData0[MAX_SHAPES];
            float4 _ShapeData1[MAX_SHAPES];
            float4 _ShapeData2[MAX_SHAPES];

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float2 DecodeDirection(float2 encoded)
            {
                return encoded * 2.0 - 1.0;
            }

            float2 EncodeDirection(float2 direction)
            {
                return direction * 0.5 + 0.5;
            }

            float2 SafeNormalize(float2 value)
            {
                float lengthSq = dot(value, value);
                if (lengthSq > 0.00000001)
                {
                    return value * rsqrt(lengthSq);
                }

                return float2(0.0, 0.0);
            }

            void EvaluateShape(float2 worldXZ, int index, out float mask, out float recovery, out float2 direction)
            {
                float4 data0 = _ShapeData0[index];
                float4 data1 = _ShapeData1[index];
                float4 data2 = _ShapeData2[index];

                float2 pointA = data0.xy;
                float2 pointB = data0.zw;
                float radius = max(data1.x, 0.001);
                float strength = max(data1.y, 0.0);
                float softness = saturate(data1.z);
                float directionalInfluence = saturate(data1.w);
                float2 velocity = data2.xy;
                recovery = saturate(data2.z);
                float shapeType = data2.w;

                float2 closestPoint = pointA;
                if (shapeType > 0.5)
                {
                    float2 ab = pointB - pointA;
                    float denominator = max(dot(ab, ab), 0.000001);
                    float t = saturate(dot(worldXZ - pointA, ab) / denominator);
                    closestPoint = pointA + (ab * t);
                }

                float2 offset = worldXZ - closestPoint;
                float distanceToShape = length(offset);
                float normalizedDistance = distanceToShape / radius;
                float innerRadius = 1.0 - softness;
                mask = (1.0 - smoothstep(innerRadius, 1.0, normalizedDistance)) * strength;

                float2 radialDirection = distanceToShape > 0.0001 ? offset / distanceToShape : float2(0.0, 0.0);
                float velocityLength = length(velocity);
                float2 velocityDirection = velocityLength > 0.0001 ? velocity / velocityLength : float2(0.0, 0.0);
                float influence = directionalInfluence * step(0.0001, velocityLength);
                direction = SafeNormalize(lerp(radialDirection, velocityDirection, influence));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 current = SAMPLE_TEXTURE2D(_BaseInteractionMap, sampler_BaseInteractionMap, input.uv);
                float currentMask = saturate(current.a);
                float2 currentDirection = SafeNormalize(DecodeDirection(current.rg));
                float combinedMask = currentMask;
                float2 directionSum = currentDirection * currentMask;
                float totalWeight = currentMask;
                float recoverySum = current.b * currentMask;

                float2 worldXZ = _InteractionRegion.xy + ((input.uv - 0.5) * (_InteractionRegion.z * 2.0));
                [unroll]
                for (int index = 0; index < MAX_SHAPES; index++)
                {
                    if (index >= (int)_ShapeCount)
                    {
                        break;
                    }

                    float shapeMask;
                    float recovery;
                    float2 direction;
                    EvaluateShape(worldXZ, index, shapeMask, recovery, direction);
                    if (shapeMask <= 0.0001)
                    {
                        continue;
                    }

                    combinedMask = max(combinedMask, shapeMask);
                    directionSum += direction * shapeMask;
                    totalWeight += shapeMask;
                    recoverySum += recovery * shapeMask;
                }

                if (combinedMask <= 0.0001)
                {
                    return _NeutralInteractionColor;
                }

                float2 combinedDirection = SafeNormalize(directionSum);
                float combinedRecovery = totalWeight > 0.0001 ? recoverySum / totalWeight : 0.0;
                return half4(EncodeDirection(combinedDirection), saturate(combinedRecovery), combinedMask);
            }
            ENDHLSL
        }
    }
}
