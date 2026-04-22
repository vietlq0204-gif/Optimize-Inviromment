Shader "Hidden/Vit/GrassInteractionStamp"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 2)) = 1
        _Softness ("Softness", Range(0.01, 1)) = 0.6
        _Direction ("Direction XY", Vector) = (0, 0, 0, 0)
        _RecoveryWeight ("Recovery Weight", Range(0, 1)) = 1
        _UseParticleColorEncoding ("Use Particle Color Encoding", Range(0, 1)) = 1
        _DirectionalInfluence ("Directional Influence", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "InteractionStamp"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
                float4 _Direction;
                float _RecoveryWeight;
                float _UseParticleColorEncoding;
                float _DirectionalInfluence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radialDistance = length(centeredUv);
                float innerRadius = 1.0 - saturate(_Softness);
                float mask = 1.0 - smoothstep(innerRadius, 1.0, radialDistance);
                float2 radialDirection = radialDistance > 0.0001 ? centeredUv / radialDistance : float2(0.0, 0.0);
                float useParticleColorEncoding = step(0.5, _UseParticleColorEncoding);
                float2 movementDirection = _Direction.xy;
                if (useParticleColorEncoding > 0.5)
                {
                    movementDirection = input.color.rg * 2.0 - 1.0;
                }

                float movementLength = length(movementDirection);
                if (movementLength > 0.0001)
                {
                    movementDirection /= movementLength;
                }
                else
                {
                    movementDirection = float2(0.0, 0.0);
                }

                float directionalInfluence = saturate(_DirectionalInfluence) * step(0.0001, movementLength);
                float2 fieldDirection = lerp(radialDirection, movementDirection, directionalInfluence);
                float fieldLength = length(fieldDirection);
                if (fieldLength > 0.0001)
                {
                    fieldDirection /= fieldLength;
                }
                else
                {
                    fieldDirection = float2(0.0, 0.0);
                }

                float2 encodedVector = fieldDirection * 0.5 + 0.5;
                float recoveryWeight = useParticleColorEncoding > 0.5 ? saturate(input.color.b) : saturate(_RecoveryWeight);
                if (recoveryWeight <= 0.0001)
                {
                    recoveryWeight = saturate(_RecoveryWeight);
                }
                mask *= saturate(input.color.a * _Intensity);
                return half4(encodedVector, recoveryWeight, mask);
            }
            ENDHLSL
        }
    }
}
