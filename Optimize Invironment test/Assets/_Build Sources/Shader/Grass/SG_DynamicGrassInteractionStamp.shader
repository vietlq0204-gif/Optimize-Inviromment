Shader "Hidden/Vit/DynamicGrassInteractionStamp"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            struct GrassInteractionSourceData
            {
                float4 positionRadius;
                float4 velocityPush;
                float4 response;
            };

            StructuredBuffer<GrassInteractionSourceData> _GrassDynamicInteractionSources;
            float4 _GrassDynamicInteractionFieldParams; // x center x, y center z, z coverage, w enabled
            float4 _GrassDynamicInteractionStampParams; // x speed to wake, y source count

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
                nointerpolation uint sourceIndex : TEXCOORD1;
            };

            float2 GetQuadCorner(uint vertexID)
            {
                if (vertexID == 0) return float2(-1.0, -1.0);
                if (vertexID == 1) return float2(1.0, -1.0);
                if (vertexID == 2) return float2(1.0, 1.0);
                if (vertexID == 3) return float2(-1.0, -1.0);
                if (vertexID == 4) return float2(1.0, 1.0);
                return float2(-1.0, 1.0);
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                GrassInteractionSourceData source = _GrassDynamicInteractionSources[instanceID];

                float2 velocityXZ = source.velocityPush.xz;
                float speed = length(velocityXZ);
                float2 direction = speed > 0.0001 ? velocityXZ / speed : float2(0.0, 1.0);
                float radius = max(source.positionRadius.w, 0.001);
                float wakeLength = speed * source.response.z;
                float footprintRadius = radius + max(radius, wakeLength);
                float2 sourceXZ = source.positionRadius.xz;
                float2 footprintCenter = sourceXZ - direction * (wakeLength * 0.5);

                float2 worldXZ = footprintCenter + GetQuadCorner(vertexID) * footprintRadius;
                float coverage = max(_GrassDynamicInteractionFieldParams.z, 0.001);
                float2 fieldCenter = _GrassDynamicInteractionFieldParams.xy;
                float2 uv = ((worldXZ - fieldCenter) / coverage) + 0.5;
                float2 clipPosition = uv * 2.0 - 1.0;
                clipPosition.x = -clipPosition.x;

                Varyings output;
                output.positionCS = float4(clipPosition, 0.0, 1.0);
                output.worldXZ = worldXZ;
                output.sourceIndex = instanceID;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                GrassInteractionSourceData source = _GrassDynamicInteractionSources[input.sourceIndex];

                float2 sourceXZ = source.positionRadius.xz;
                float2 toPoint = input.worldXZ - sourceXZ;
                float distanceToSource = length(toPoint);
                float radius = max(source.positionRadius.w, 0.001);
                float contact01 = saturate(1.0 - distanceToSource / radius);
                contact01 = contact01 * contact01 * (3.0 - 2.0 * contact01);

                float2 velocityXZ = source.velocityPush.xz;
                float speed = length(velocityXZ);
                float2 direction = speed > 0.0001 ? velocityXZ / speed : float2(0.0, 1.0);
                float2 radialDirection = distanceToSource > 0.0001 ? toPoint / distanceToSource : -direction;

                float2 force = radialDirection * source.velocityPush.w * contact01;
                float flatten = source.response.x * contact01;
                float turbulence = 0.0;

                if (speed > 0.05 && source.response.y > 0.0)
                {
                    float2 sideDirection = float2(-direction.y, direction.x);
                    float behind = dot(toPoint, -direction);
                    float side = abs(dot(toPoint, sideDirection));
                    float wakeLength = max(radius, speed * source.response.z);
                    float wakeWidth = radius * lerp(1.25, 2.25, saturate(speed * 0.08));
                    float wakeAlong = saturate(1.0 - behind / max(wakeLength, 0.001));
                    float wakeSide = saturate(1.0 - side / max(wakeWidth, 0.001));
                    float wakeMask = step(0.0, behind) * wakeAlong * wakeSide;
                    wakeMask = wakeMask * wakeMask * (3.0 - 2.0 * wakeMask);

                    float speed01 = saturate(speed * _GrassDynamicInteractionStampParams.x);
                    force += direction * source.response.y * speed01 * wakeMask;
                    turbulence += source.response.w * speed01 * wakeMask;
                }

                return half4(force.x, force.y, flatten, turbulence);
            }
            ENDHLSL
        }
    }
}
