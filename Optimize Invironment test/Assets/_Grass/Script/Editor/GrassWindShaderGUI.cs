using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom material inspector for the grass wind shader.
/// </summary>
public sealed class GrassWindShaderGUI : ShaderGUI
{
    private static bool s_ShowCommon = true;
    private static bool s_ShowLighting = true;
    private static bool s_ShowWindShape = true;
    private static bool s_ShowWindTexture = true;
    private static bool s_ShowColor;
    private static bool s_ShowTerrain;
    private static bool s_ShowInteraction = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty baseMap = Find("_BaseMap", properties);
        MaterialProperty baseColor = Find("_BaseColor", properties);
        MaterialProperty cutoff = Find("_Cutoff", properties);
        MaterialProperty receiveShadows = Find("_ReceiveShadows", properties);
        MaterialProperty shadowStrength = Find("_ShadowStrength", properties);
        MaterialProperty shadowFloor = Find("_ShadowFloor", properties);
        MaterialProperty enableMainLight = Find("_EnableMainLight", properties);
        MaterialProperty mainLightIntensity = Find("_MainLightIntensity", properties);
        MaterialProperty enableAdditionalLights = Find("_EnableAdditionalLights", properties);
        MaterialProperty additionalLightIntensity = Find("_AdditionalLightIntensity", properties);
        MaterialProperty enableAmbient = Find("_EnableAmbient", properties);
        MaterialProperty ambientIntensity = Find("_AmbientIntensity", properties);
        MaterialProperty twoSidedLighting = Find("_TwoSidedLighting", properties);

        MaterialProperty windTexture = Find("_WindTexture", properties);
        MaterialProperty windSpeed = Find("_WindSpeed", properties);
        MaterialProperty windDirection = Find("_WindDirection", properties);

        MaterialProperty enableWaveShape = Find("_EnableWaveShape", properties);
        MaterialProperty waveFrequency = Find("_WaveFrequency", properties);
        MaterialProperty waveSpacingVariation = Find("_WaveSpacingVariation", properties);
        MaterialProperty waveSpeed = Find("_WaveSpeed", properties);
        MaterialProperty waveStrength = Find("_WaveStrength", properties);
        MaterialProperty waveBodyInfluence = Find("_WaveBodyInfluence", properties);
        MaterialProperty waveTipInfluence = Find("_WaveTipInfluence", properties);
        MaterialProperty waveLateralInfluence = Find("_WaveLateralInfluence", properties);

        MaterialProperty windTextureScale = Find("_WindTextureScale", properties);
        MaterialProperty windTextureScrollSpeed = Find("_WindTextureScrollSpeed", properties);
        MaterialProperty windTextureContrast = Find("_WindTextureContrast", properties);
        MaterialProperty windTextureInfluence = Find("_WindTextureInfluence", properties);
        MaterialProperty windTextureWaveInfluence = Find("_WindTextureWaveInfluence", properties);

        MaterialProperty nearColor = Find("_NearColor", properties);
        MaterialProperty farColor = Find("_FarColor", properties);
        MaterialProperty nearFarRange = Find("_NearFarRange", properties);
        MaterialProperty bottomColor = Find("_BottomColor", properties);
        MaterialProperty heightBlend = Find("_HeightBlend", properties);

        MaterialProperty useTerrainColor = Find("_UseTerrainColor", properties);
        MaterialProperty terrainColor = Find("_TerrainColor", properties);
        MaterialProperty terrainBlendStrength = Find("_TerrainBlendStrength", properties);
        MaterialProperty enableInteraction = Find("_EnableInteraction", properties);
        MaterialProperty interactionStrength = Find("_InteractionStrength", properties);
        MaterialProperty interactionPushAway = Find("_InteractionPushAway", properties);
        MaterialProperty interactionFlatten = Find("_InteractionFlatten", properties);
        MaterialProperty interactionRadiusMultiplier = Find("_InteractionRadiusMultiplier", properties);
        MaterialProperty interactionVerticalRange = Find("_InteractionVerticalRange", properties);
        MaterialProperty interactionTrail = Find("_InteractionTrail", properties);

        DrawCommon(materialEditor, ref s_ShowCommon, baseMap, baseColor, cutoff, windTexture, windSpeed, windDirection);
        DrawLighting(
            materialEditor,
            ref s_ShowLighting,
            receiveShadows,
            shadowStrength,
            shadowFloor,
            enableMainLight,
            mainLightIntensity,
            enableAdditionalLights,
            additionalLightIntensity,
            enableAmbient,
            ambientIntensity,
            twoSidedLighting);
        DrawWindShape(
            materialEditor,
            ref s_ShowWindShape,
            enableWaveShape,
            waveFrequency,
            waveSpacingVariation,
            waveSpeed,
            waveStrength,
            waveBodyInfluence,
            waveTipInfluence,
            waveLateralInfluence);
        DrawWindTexture(
            materialEditor,
            ref s_ShowWindTexture,
            windTextureScale,
            windTextureScrollSpeed,
            windTextureContrast,
            windTextureInfluence,
            windTextureWaveInfluence);
        DrawColor(materialEditor, ref s_ShowColor, nearColor, farColor, nearFarRange, bottomColor, heightBlend);
        DrawTerrain(materialEditor, ref s_ShowTerrain, useTerrainColor, terrainColor, terrainBlendStrength);
        DrawInteraction(
            materialEditor,
            ref s_ShowInteraction,
            enableInteraction,
            interactionStrength,
            interactionPushAway,
            interactionFlatten,
            interactionRadiusMultiplier,
            interactionVerticalRange,
            interactionTrail);
        DrawBakeTools(materialEditor);
    }

    private static MaterialProperty Find(string name, MaterialProperty[] properties)
    {
        return FindProperty(name, properties, false);
    }

    private static void DrawCommon(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty baseMap,
        MaterialProperty baseColor,
        MaterialProperty cutoff,
        MaterialProperty windTexture,
        MaterialProperty windSpeed,
        MaterialProperty windDirection)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Common");
        if (foldout)
        {
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Base Map", "Base color texture. Alpha drives the cutout mask."),
                baseMap);
            materialEditor.ShaderProperty(
                baseColor,
                MakeLabel("Base Color", "Tint multiplied with the base texture."));
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Wind Texture", "World-space wind field texture that controls stronger and weaker gust areas."),
                windTexture);
            materialEditor.ShaderProperty(
                cutoff,
                MakeLabel("Alpha Cutoff", "Pixels below this alpha threshold are clipped."));
            materialEditor.ShaderProperty(
                windSpeed,
                MakeLabel("Grass Lean", "Base lean amount in the wind direction."));
            DrawNormalizedDirection2D(
                windDirection,
                MakeLabel("Wind Direction (XZ)", "Global wind direction in XZ space."));
            materialEditor.EnableInstancingField();
            EditorGUILayout.HelpBox(
                "Grass Lean controls the base bend. Visible motion comes from Wave Shape and Wind Texture scrolling.",
                MessageType.None);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawLighting(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty receiveShadows,
        MaterialProperty shadowStrength,
        MaterialProperty shadowFloor,
        MaterialProperty enableMainLight,
        MaterialProperty mainLightIntensity,
        MaterialProperty enableAdditionalLights,
        MaterialProperty additionalLightIntensity,
        MaterialProperty enableAmbient,
        MaterialProperty ambientIntensity,
        MaterialProperty twoSidedLighting)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Lighting");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                enableMainLight,
                MakeLabel("Enable Main Light", "Use the URP main directional light contribution."));
            if (enableMainLight.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    mainLightIntensity,
                    MakeLabel("Main Intensity", "Brightness multiplier for the main light."));
            }

            materialEditor.ShaderProperty(
                enableAdditionalLights,
                MakeLabel("Enable Additional Lights", "Use URP additional lights such as point, spot, and extra directional lights."));
            if (enableAdditionalLights.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    additionalLightIntensity,
                    MakeLabel("Additional Intensity", "Brightness multiplier for additional lights."));
            }

            materialEditor.ShaderProperty(
                enableAmbient,
                MakeLabel("Enable Ambient", "Use spherical harmonics ambient fill from the scene lighting."));
            if (enableAmbient.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    ambientIntensity,
                    MakeLabel("Ambient Intensity", "Brightness multiplier for ambient fill."));
            }

            materialEditor.ShaderProperty(
                twoSidedLighting,
                MakeLabel("Two-Sided Lighting", "Light both sides of foliage cards so the back face does not turn black."));
            materialEditor.ShaderProperty(
                receiveShadows,
                MakeLabel("Receive Shadows", "Enable real-time shadow reception from URP lights."));
            if (receiveShadows != null && receiveShadows.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    shadowStrength,
                    MakeLabel("Shadow Strength", "How strongly real-time shadows darken the material."));
                materialEditor.ShaderProperty(
                    shadowFloor,
                    MakeLabel("Shadow Floor", "Minimum lighting preserved inside shadowed areas."));
            }

            EditorGUILayout.HelpBox(
                "Main Light restores shape under the primary directional light. Additional Lights makes point and spot lights affect the material. Ambient keeps shaded foliage from collapsing to black.",
                MessageType.None);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawWindShape(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableWaveShape,
        MaterialProperty waveFrequency,
        MaterialProperty waveSpacingVariation,
        MaterialProperty waveSpeed,
        MaterialProperty waveStrength,
        MaterialProperty waveBodyInfluence,
        MaterialProperty waveTipInfluence,
        MaterialProperty waveLateralInfluence)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Wind Shape");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                enableWaveShape,
                MakeLabel("Enable Wave Motion", "Toggle the procedural wave layer."));
            EditorGUI.BeginDisabledGroup(enableWaveShape.floatValue < 0.5f);
            materialEditor.ShaderProperty(
                waveFrequency,
                MakeLabel("Wave Frequency", "Wave density along the wind direction."));
            materialEditor.ShaderProperty(
                waveSpacingVariation,
                MakeLabel("Wave Spacing Variation", "Irregular spacing between wave bands."));
            materialEditor.ShaderProperty(
                waveSpeed,
                MakeLabel("Wave Speed", "Travel speed of the wave layer."));
            materialEditor.ShaderProperty(
                waveStrength,
                MakeLabel("Wave Strength", "Overall wave amplitude."));
            materialEditor.ShaderProperty(
                waveBodyInfluence,
                MakeLabel("Body Wave", "Wave influence over the blade body."));
            materialEditor.ShaderProperty(
                waveTipInfluence,
                MakeLabel("Tip Wave", "Wave influence over the blade tip."));
            materialEditor.ShaderProperty(
                waveLateralInfluence,
                MakeLabel("Lateral Wave", "Side-to-side wave motion."));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawWindTexture(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty windTextureScale,
        MaterialProperty windTextureScrollSpeed,
        MaterialProperty windTextureContrast,
        MaterialProperty windTextureInfluence,
        MaterialProperty windTextureWaveInfluence)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Wind Texture");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                windTextureScale,
                MakeLabel("Texture Scale", "World-space tiling of the wind texture."));
            materialEditor.ShaderProperty(
                windTextureScrollSpeed,
                MakeLabel("Texture Scroll Speed", "How fast the wind field travels."));
            materialEditor.ShaderProperty(
                windTextureContrast,
                MakeLabel("Texture Contrast", "Remap range used to shape wind texture intensity."));
            materialEditor.ShaderProperty(
                windTextureInfluence,
                MakeLabel("Lean Influence", "How much the wind texture affects base leaning."));
            materialEditor.ShaderProperty(
                windTextureWaveInfluence,
                MakeLabel("Wave Influence", "How much the wind texture modulates the wave layer."));
            EditorGUILayout.HelpBox(
                "Wind Texture acts as the main gust field. Wind Direction rotates sampling, while Scroll Speed moves the gust pattern.",
                MessageType.None);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawColor(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty nearColor,
        MaterialProperty farColor,
        MaterialProperty nearFarRange,
        MaterialProperty bottomColor,
        MaterialProperty heightBlend)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Color");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                nearColor,
                MakeLabel("Near Color", "Tint applied near the camera."));
            materialEditor.ShaderProperty(
                farColor,
                MakeLabel("Far Color", "Tint applied at distance."));
            materialEditor.ShaderProperty(
                nearFarRange,
                MakeLabel("Near/Far Range", "Distance range used to blend near and far tint."));
            materialEditor.ShaderProperty(
                bottomColor,
                MakeLabel("Bottom Tint", "Tint applied near the base of each blade."));
            materialEditor.ShaderProperty(
                heightBlend,
                MakeLabel("Height Blend", "How quickly bottom tint fades toward the tip."));
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawTerrain(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty useTerrainColor,
        MaterialProperty terrainColor,
        MaterialProperty terrainBlendStrength)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Terrain");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                useTerrainColor,
                MakeLabel("Use Terrain Color", "Multiply terrain tint into the grass color."));
            if (useTerrainColor.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    terrainColor,
                    MakeLabel("Terrain Color", "Tint sampled from terrain settings or set manually."));
            }

            materialEditor.ShaderProperty(
                terrainBlendStrength,
                MakeLabel("Blend Strength", "How strongly terrain tint or terrain color maps are mixed into the grass."));

            EditorGUILayout.HelpBox(
                "For URP, the shader can use the manual terrain tint or a global terrain color map provided by GrassTerrainColorMapController.",
                MessageType.None);

            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawInteraction(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableInteraction,
        MaterialProperty interactionStrength,
        MaterialProperty interactionPushAway,
        MaterialProperty interactionFlatten,
        MaterialProperty interactionRadiusMultiplier,
        MaterialProperty interactionVerticalRange,
        MaterialProperty interactionTrail)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Interaction");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                enableInteraction,
                MakeLabel("Enable Interaction", "Allows the material to respond to the global grass interaction render texture."));

            EditorGUI.BeginDisabledGroup(enableInteraction.floatValue < 0.5f);
            materialEditor.ShaderProperty(
                interactionStrength,
                MakeLabel("Strength", "Master multiplier for interaction intensity."));
            materialEditor.ShaderProperty(
                interactionPushAway,
                MakeLabel("Push Away", "How far blade tips are pushed sideways by interaction."));
            materialEditor.ShaderProperty(
                interactionFlatten,
                MakeLabel("Flatten", "How much interaction pushes the blade downward."));
            materialEditor.ShaderProperty(
                interactionRadiusMultiplier,
                MakeLabel("Radius Multiplier", "Broadens the interaction gradient sampling radius."));
            materialEditor.ShaderProperty(
                interactionVerticalRange,
                MakeLabel("Vertical Range", "Limits interaction by height so distant levels are not affected."));
            materialEditor.ShaderProperty(
                interactionTrail,
                MakeLabel("Trail Response", "Shapes the falloff from sharp stamp response to softer lingering response."));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "Runtime interaction comes from GrassInteractionController + one or more GrassInteractionSource components on a dedicated interaction layer.",
                MessageType.None);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawBakeTools(MaterialEditor materialEditor)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        bool singleMaterialSelected = materialEditor.targets.Length == 1;
        Material material = materialEditor.target as Material;
        string reason = string.Empty;
        bool canBake = singleMaterialSelected && ShaderDefaultBakeUtility.CanBake(material, out reason);

        using (new EditorGUI.DisabledScope(!canBake))
        {
            if (GUILayout.Button("Bake Material Values To Shader Defaults"))
            {
                ShaderDefaultBakeUtility.BakeMaterialWithDialogs(material);
            }
        }

        if (!singleMaterialSelected)
        {
            EditorGUILayout.HelpBox(
                "Select exactly one material if you want to bake the current values back into the shader defaults.",
                MessageType.None);
            return;
        }

        if (!canBake)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "This writes the current float, color, and vector values into the shader defaults and syncs default textures through ShaderImporter. Texture scale and offset stay on the material.",
            MessageType.None);
    }

    private static void DrawNormalizedDirection2D(MaterialProperty property, GUIContent label)
    {
        Vector4 vector = property.vectorValue;
        Vector2 direction = new Vector2(vector.x, vector.y);

        EditorGUI.BeginChangeCheck();
        direction = EditorGUILayout.Vector2Field(label, direction);
        if (EditorGUI.EndChangeCheck())
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            property.vectorValue = new Vector4(direction.x, direction.y, 0f, 0f);
        }
    }

    private static GUIContent MakeLabel(string text, string tooltip)
    {
        return new GUIContent(text, tooltip);
    }
}
