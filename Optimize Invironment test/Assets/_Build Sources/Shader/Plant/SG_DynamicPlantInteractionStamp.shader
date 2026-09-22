Shader "Hidden/Vit/DynamicPlantInteractionStamp"
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

            struct PlantInteractionSourceData
            {
                float4 positionRadius;
                float4 velocityPush;
                float4 response;
                float4 wakeShape;
            };

            StructuredBuffer<PlantInteractionSourceData> _PlantDynamicInteractionSources;
            float4 _PlantDynamicInteractionFieldParams; // x center x, y center z, z coverage, w enabled
            float4 _PlantDynamicInteractionStampParams; // x speed to wake, y source count

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
                PlantInteractionSourceData source = _PlantDynamicInteractionSources[instanceID];

                float2 velocityXZ = source.velocityPush.xz;
                float speed = length(velocityXZ);
                float2 direction = speed > 0.0001 ? velocityXZ / speed : float2(0.0, 1.0);
                float2 sideDirection = float2(-direction.y, direction.x);
                float radius = max(source.positionRadius.w, 0.001);
                float wakeLength = speed > 0.05 && source.response.y > 0.0 ? max(source.response.z, 0.0) : 0.0;
                float footprintAlong = radius + wakeLength * 0.5;
                float footprintSide = max(radius, max(source.wakeShape.x, source.wakeShape.y));
                float2 sourceXZ = source.positionRadius.xz;
                float2 footprintCenter = sourceXZ - direction * (wakeLength * 0.5);
                float2 corner = GetQuadCorner(vertexID);

                float2 worldXZ = footprintCenter + sideDirection * corner.x * footprintSide + direction * corner.y * footprintAlong;
                float coverage = max(_PlantDynamicInteractionFieldParams.z, 0.001);
                float2 fieldCenter = _PlantDynamicInteractionFieldParams.xy;
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
                PlantInteractionSourceData source = _PlantDynamicInteractionSources[input.sourceIndex];

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

                if (speed > 0.05 && source.response.y > 0.0 && source.response.z > 0.001)
                {
                    float2 sideDirection = float2(-direction.y, direction.x);
                    float behind = dot(toPoint, -direction);
                    float side = abs(dot(toPoint, sideDirection));
                    float wakeLength = source.response.z;
                    float speed01 = saturate(speed * _PlantDynamicInteractionStampParams.x);

                    float wakeProgress = saturate(behind / max(wakeLength, 0.001));
                    float coneStart = min(saturate(source.wakeShape.w), 0.999);
                    float coneProgress = smoothstep(coneStart, 1.0, wakeProgress);
                    float startWidth = max(source.wakeShape.x, 0.0);
                    float tailWidthScale = max(source.wakeShape.y, source.wakeShape.x);
                    float endWidth = tailWidthScale;
                    float wakeWidth = max(lerp(startWidth, endWidth, coneProgress), max(radius * 0.01, 0.001));
                    float edgeFeather = max(wakeWidth * max(source.wakeShape.z, 0.01), max(radius * 0.01, 0.001));
                    float innerWidth = max(wakeWidth - edgeFeather, wakeWidth * 0.05);
                    float wakeSide = 1.0 - smoothstep(innerWidth, wakeWidth, side);
                    float tailFade = pow(saturate(1.0 - wakeProgress), 0.65);
                    float headFade = smoothstep(0.0, 0.08, wakeProgress);
                    float wakeMask = step(0.0, behind) * wakeSide * tailFade * headFade;

                    force += direction * source.response.y * speed01 * wakeMask;
                    turbulence += source.response.w * speed01 * wakeMask;
                }

                return half4(force.x, force.y, flatten, turbulence);
            }
            ENDHLSL
        }
    }
}
