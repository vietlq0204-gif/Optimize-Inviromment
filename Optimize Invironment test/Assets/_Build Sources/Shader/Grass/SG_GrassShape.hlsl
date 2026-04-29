#ifndef SG_GRASS_SHAPE_INCLUDED
#define SG_GRASS_SHAPE_INCLUDED

float3 ApplyGrassConeShapeOS(float3 positionOS, float bladeMask)
{
    if (GetToggle01(_EnableGrassConeShape) < 0.5)
    {
        return positionOS;
    }

    float heightT = saturate(bladeMask);
    float flareProfile = heightT * heightT;
    float coneScale = lerp(1.0, max(_GrassConeTipScale, 0.001), flareProfile);
    positionOS.xz *= coneScale;
    return positionOS;
}

#endif
