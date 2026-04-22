Shader "Hidden/Vit/GrassInteractionAccumulate"
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
            Name "Accumulate"
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CurrentInteractionMap);
            SAMPLER(sampler_CurrentInteractionMap);
            TEXTURE2D(_PreviousInteractionMap);
            SAMPLER(sampler_PreviousInteractionMap);

            CBUFFER_START(UnityPerMaterial)
                float _HistoryPersistence;
                float4 _NeutralInteractionColor;
            CBUFFER_END

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

            half4 frag(Varyings input) : SV_Target
            {
                float4 current = SAMPLE_TEXTURE2D(_CurrentInteractionMap, sampler_CurrentInteractionMap, input.uv);
                float4 previous = SAMPLE_TEXTURE2D(_PreviousInteractionMap, sampler_PreviousInteractionMap, input.uv);

                float currentMask = saturate(current.a);
                float previousMask = saturate(previous.a) * saturate(_HistoryPersistence);

                float combinedMask = max(currentMask, previousMask);
                if (combinedMask <= 0.0001)
                {
                    return _NeutralInteractionColor;
                }

                float2 currentDirection = DecodeDirection(current.rg);
                float2 previousDirection = DecodeDirection(previous.rg);
                float2 directionSum = currentDirection * currentMask + previousDirection * previousMask;

                float directionLength = length(directionSum);
                float2 combinedDirection;
                if (directionLength > 0.0001)
                {
                    combinedDirection = directionSum / directionLength;
                }
                else if (currentMask >= previousMask && currentMask > 0.0001)
                {
                    combinedDirection = normalize(currentDirection);
                }
                else if (previousMask > 0.0001)
                {
                    combinedDirection = normalize(previousDirection);
                }
                else
                {
                    combinedDirection = float2(0.0, 0.0);
                }

                float totalWeight = currentMask + previousMask;
                float combinedRecovery = 0.0;
                if (totalWeight > 0.0001)
                {
                    combinedRecovery = ((current.b * currentMask) + (previous.b * previousMask)) / totalWeight;
                }

                return half4(EncodeDirection(combinedDirection), saturate(combinedRecovery), combinedMask);
            }
            ENDHLSL
        }
    }
}
