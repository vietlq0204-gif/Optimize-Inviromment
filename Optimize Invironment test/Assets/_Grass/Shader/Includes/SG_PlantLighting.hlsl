#ifndef SG_PLANT_LIGHTING_INCLUDED
#define SG_PLANT_LIGHTING_INCLUDED

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

#endif
