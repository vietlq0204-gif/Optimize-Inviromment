Shader "Hidden/Vit/GrassInteractionStamp"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 2)) = 1
        _Softness ("Softness", Range(0.01, 1)) = 0.6
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
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
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

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radialDistance = length(centeredUv);
                float innerRadius = 1.0 - saturate(_Softness);
                float mask = 1.0 - smoothstep(innerRadius, 1.0, radialDistance);
                mask *= saturate(_Intensity);
                return half4(mask, mask, mask, mask);
            }
            ENDHLSL
        }
    }
}
