using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom material inspector for the grass wind shader.
/// </summary>
/// <remarks>
/// Splits parameters into frequent and advanced groups.
/// Hidden properties are still editable here.
/// </remarks>
public sealed class GrassWindShaderGUI : ShaderGUI
{
    private static bool s_ShowCommon = true;
    private static bool s_ShowWindShape = true;
    private static bool s_ShowGustFront = true;
    private static bool s_ShowWindNoise = false;
    private static bool s_ShowBladeBend = false;
    private static bool s_ShowColor = false;
    private static bool s_ShowTerrain = false;

    /// <summary>
    /// Draws the custom inspector.
    /// </summary>
    /// <param name="materialEditor">Unity material editor.</param>
    /// <param name="properties">All material properties.</param>
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty baseMap = Find("_BaseMap", properties);
        MaterialProperty baseColor = Find("_BaseColor", properties);
        MaterialProperty cutoff = Find("_Cutoff", properties);

        MaterialProperty windTexture = Find("_WindTexture", properties);
        MaterialProperty enableNoiseField = Find("_EnableNoiseField", properties);
        MaterialProperty enableMacroWave = Find("_EnableMacroWave", properties);
        MaterialProperty enableWave = Find("_EnableWave", properties);
        MaterialProperty windSpeed = Find("_WindSpeed", properties);
        MaterialProperty windStrength = Find("_WindStrength", properties);
        MaterialProperty windDirection = Find("_WindDirection", properties);

        MaterialProperty waveFrequency = Find("_WaveFrequency", properties);
        MaterialProperty waveSharpness = Find("_WaveSharpness", properties);
        MaterialProperty macroWaveStrength = Find("_MacroWaveStrength", properties);
        MaterialProperty sideVariation = Find("_SideVariation", properties);

        MaterialProperty gustFrontStrength = Find("_GustFrontStrength", properties);
        MaterialProperty gustFrontSpeed = Find("_GustFrontSpeed", properties);
        MaterialProperty gustFrontSpacing = Find("_GustFrontSpacing", properties);
        MaterialProperty gustFrontWidth = Find("_GustFrontWidth", properties);
        MaterialProperty gustFrontTrail = Find("_GustFrontTrail", properties);
        MaterialProperty gustFrontCurvature = Find("_GustFrontCurvature", properties);
        MaterialProperty gustFrontOverlap = Find("_GustFrontOverlap", properties);
        MaterialProperty gustFrontBreakup = Find("_GustFrontBreakup", properties);
        MaterialProperty gustFrontWarp = Find("_GustFrontWarp", properties);
        MaterialProperty gustFrontLateralScale = Find("_GustFrontLateralScale", properties);

        MaterialProperty windScale = Find("_WindScale", properties);
        MaterialProperty windNoiseScale = Find("_WindNoiseScale", properties);
        MaterialProperty windNoiseSpeed = Find("_WindNoiseSpeed", properties);
        MaterialProperty windNoiseContrast = Find("_WindNoiseContrast", properties);
        MaterialProperty noiseFieldInfluence = Find("_NoiseFieldInfluence", properties);

        MaterialProperty topBend = Find("_TopBend", properties);
        MaterialProperty stemBend = Find("_StemBend", properties);
        MaterialProperty windHeight = Find("_WindHeight", properties);
        MaterialProperty downBend = Find("_DownBend", properties);
        MaterialProperty enableFlutter = Find("_EnableFlutter", properties);
        MaterialProperty detailStrength = Find("_DetailStrength", properties);
        MaterialProperty flutterSpeed = Find("_FlutterSpeed", properties);

        MaterialProperty nearColor = Find("_NearColor", properties);
        MaterialProperty farColor = Find("_FarColor", properties);
        MaterialProperty nearFarRange = Find("_NearFarRange", properties);
        MaterialProperty bottomColor = Find("_BottomColor", properties);
        MaterialProperty heightBlend = Find("_HeightBlend", properties);

        MaterialProperty useTerrainColor = Find("_UseTerrainColor", properties);
        MaterialProperty terrainColor = Find("_TerrainColor", properties);

        DrawCommon(materialEditor, ref s_ShowCommon, baseMap, baseColor, cutoff, windTexture, windSpeed, windStrength, windDirection);
        DrawWindShape(materialEditor, ref s_ShowWindShape, enableMacroWave, waveFrequency, waveSharpness, macroWaveStrength, sideVariation);
        DrawGustFront(
            materialEditor,
            ref s_ShowGustFront,
            enableWave,
            gustFrontStrength,
            gustFrontSpeed,
            gustFrontSpacing,
            gustFrontWidth,
            gustFrontTrail,
            gustFrontCurvature,
            gustFrontOverlap,
            gustFrontBreakup,
            gustFrontWarp,
            gustFrontLateralScale);
        DrawWindNoise(
            materialEditor,
            ref s_ShowWindNoise,
            enableNoiseField,
            windNoiseScale,
            windNoiseSpeed,
            windNoiseContrast,
            noiseFieldInfluence);
        DrawBladeBend(
            materialEditor,
            ref s_ShowBladeBend,
            windScale,
            topBend,
            stemBend,
            windHeight,
            downBend,
            enableFlutter,
            detailStrength,
            flutterSpeed);
        DrawColor(materialEditor, ref s_ShowColor, nearColor, farColor, nearFarRange, bottomColor, heightBlend);
        DrawTerrain(materialEditor, ref s_ShowTerrain, useTerrainColor, terrainColor);
    }

    /// <summary>
    /// Finds a material property safely.
    /// </summary>
    /// <param name="name">Property name in shader.</param>
    /// <param name="properties">All shader properties.</param>
    /// <returns>The material property.</returns>
    private static MaterialProperty Find(string name, MaterialProperty[] properties)
    {
        return FindProperty(name, properties, false);
    }

    /// <summary>
    /// Draws the common section.
    /// </summary>
    private static void DrawCommon(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty baseMap,
        MaterialProperty baseColor,
        MaterialProperty cutoff,
        MaterialProperty windTexture,
        MaterialProperty windSpeed,
        MaterialProperty windStrength,
        MaterialProperty windDirection)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Common");
        if (foldout)
        {
            materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
            materialEditor.TexturePropertySingleLine(new GUIContent("Wind Texture"), windTexture);
            materialEditor.ShaderProperty(cutoff, "Alpha Cutoff");
            materialEditor.ShaderProperty(windSpeed, "Wind Speed");
            materialEditor.ShaderProperty(windStrength, "Wind Strength");
            DrawNormalizedDirection2D(windDirection, "Wind Direction");
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws wave shape controls.
    /// </summary>
    private static void DrawWindShape(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableMacroWave,
        MaterialProperty waveFrequency,
        MaterialProperty waveSharpness,
        MaterialProperty macroWaveStrength,
        MaterialProperty sideVariation)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Wind Shape");
        if (foldout)
        {
            materialEditor.ShaderProperty(enableMacroWave, "Enable Macro Wave");
            EditorGUI.BeginDisabledGroup(enableMacroWave.floatValue < 0.5f);
            materialEditor.ShaderProperty(waveFrequency, "Wave Frequency");
            materialEditor.ShaderProperty(waveSharpness, "Wave Sharpness");
            materialEditor.ShaderProperty(macroWaveStrength, "Ocean Swell");
            materialEditor.ShaderProperty(sideVariation, "Side Variation");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws gust-front controls.
    /// </summary>
    private static void DrawGustFront(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableWave,
        MaterialProperty gustFrontStrength,
        MaterialProperty gustFrontSpeed,
        MaterialProperty gustFrontSpacing,
        MaterialProperty gustFrontWidth,
        MaterialProperty gustFrontTrail,
        MaterialProperty gustFrontCurvature,
        MaterialProperty gustFrontOverlap,
        MaterialProperty gustFrontBreakup,
        MaterialProperty gustFrontWarp,
        MaterialProperty gustFrontLateralScale)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Gust Front");
        if (foldout)
        {
            materialEditor.ShaderProperty(enableWave, "Enable Gust Front");
            EditorGUI.BeginDisabledGroup(enableWave.floatValue < 0.5f);
            materialEditor.ShaderProperty(gustFrontStrength, "Impact Strength");
            materialEditor.ShaderProperty(gustFrontSpeed, "Travel Speed");
            materialEditor.ShaderProperty(gustFrontSpacing, "Front Spacing");
            materialEditor.ShaderProperty(gustFrontWidth, "Wave Width");
            materialEditor.ShaderProperty(gustFrontTrail, "Recovery Tail");
            materialEditor.ShaderProperty(gustFrontCurvature, "Wave Curvature");
            materialEditor.ShaderProperty(gustFrontOverlap, "Front Overlap");
            materialEditor.ShaderProperty(gustFrontBreakup, "Breakup");
            materialEditor.ShaderProperty(gustFrontWarp, "Front Warp");
            materialEditor.ShaderProperty(gustFrontLateralScale, "Lane Scale");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws wind noise controls.
    /// </summary>
    private static void DrawWindNoise(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableNoiseField,
        MaterialProperty windNoiseScale,
        MaterialProperty windNoiseSpeed,
        MaterialProperty windNoiseContrast,
        MaterialProperty noiseFieldInfluence)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Wind Noise");
        if (foldout)
        {
            materialEditor.ShaderProperty(enableNoiseField, "Enable Noise Field");
            EditorGUI.BeginDisabledGroup(enableNoiseField.floatValue < 0.5f);
            materialEditor.ShaderProperty(windNoiseScale, "Noise Scale");
            materialEditor.ShaderProperty(windNoiseSpeed, "Noise Speed");
            materialEditor.ShaderProperty(windNoiseContrast, "Noise Contrast");
            materialEditor.ShaderProperty(noiseFieldInfluence, "Field Influence");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws blade bend controls.
    /// </summary>
    private static void DrawBladeBend(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty windScale,
        MaterialProperty topBend,
        MaterialProperty stemBend,
        MaterialProperty windHeight,
        MaterialProperty downBend,
        MaterialProperty enableFlutter,
        MaterialProperty detailStrength,
        MaterialProperty flutterSpeed)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Blade Bend");
        if (foldout)
        {
            materialEditor.ShaderProperty(topBend, "Tip Weight");
            materialEditor.ShaderProperty(stemBend, "Stem Flex");
            materialEditor.ShaderProperty(windHeight, "Wind Height");
            materialEditor.ShaderProperty(downBend, "Down Bend");
            materialEditor.ShaderProperty(windScale, "Flutter Scale");
            materialEditor.ShaderProperty(enableFlutter, "Enable Tip Flutter");
            EditorGUI.BeginDisabledGroup(enableFlutter.floatValue < 0.5f);
            materialEditor.ShaderProperty(detailStrength, "Tip Flutter");
            materialEditor.ShaderProperty(flutterSpeed, "Flutter Speed");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws color controls.
    /// </summary>
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
            materialEditor.ShaderProperty(nearColor, "Near Color");
            materialEditor.ShaderProperty(farColor, "Far Color");
            materialEditor.ShaderProperty(nearFarRange, "Near/Far Range");
            materialEditor.ShaderProperty(bottomColor, "Bottom Color");
            materialEditor.ShaderProperty(heightBlend, "Height Blend");
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws terrain blending controls.
    /// </summary>
    private static void DrawTerrain(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty useTerrainColor,
        MaterialProperty terrainColor)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Terrain");
        if (foldout)
        {
            materialEditor.ShaderProperty(useTerrainColor, "Use Terrain Color");
            if (useTerrainColor.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(terrainColor, "Terrain Color");
            }

            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Draws a normalized 2D direction field backed by a Vector4 property.
    /// </summary>
    /// <param name="property">Shader vector property.</param>
    /// <param name="label">Displayed label.</param>
    private static void DrawNormalizedDirection2D(MaterialProperty property, string label)
    {
        Vector4 v = property.vectorValue;
        Vector2 dir = new Vector2(v.x, v.y);

        EditorGUI.BeginChangeCheck();
        dir = EditorGUILayout.Vector2Field(label, dir);
        if (EditorGUI.EndChangeCheck())
        {
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }
            else
            {
                dir.Normalize();
            }

            property.vectorValue = new Vector4(dir.x, dir.y, 0f, 0f);
        }
    }
}
