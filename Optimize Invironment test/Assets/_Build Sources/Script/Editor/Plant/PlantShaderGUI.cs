using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom material inspector for the plant shader.
/// </summary>
public sealed class PlantShaderGUI : ShaderGUI
{
    private static bool s_ShowCommon = true;
    private static bool s_ShowPlantShape = true;
    private static bool s_ShowLighting = true;
    private static bool s_ShowWind = true;
    private static bool s_ShowWindVibrate = true;
    private static bool s_ShowWindNoise = true;
    private static bool s_ShowColor = true;
    private static bool s_ShowTerrain = true;
    private const int TransparentRenderQueue = 3000;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty baseMap = Find("_BaseMap", properties);
        MaterialProperty baseColor = Find("_BaseColor", properties);
        MaterialProperty enableColor = Find("_EnableColor", properties);
        MaterialProperty cutoff = Find("_Cutoff", properties);

        MaterialProperty enableLighting = Find("_EnableLighting", properties);
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

        MaterialProperty enableWind = Find("_EnableWind", properties);
        MaterialProperty windTexture = Find("_WindTexture", properties);
        MaterialProperty windSpeed = Find("_WindSpeed", properties);
        MaterialProperty windDirection = Find("_WindDirection", properties);
        MaterialProperty windTextureScale = Find("_WindTextureScale", properties);
        MaterialProperty windTextureScrollSpeed = Find("_WindTextureScrollSpeed", properties);
        MaterialProperty windTextureContrast = Find("_WindTextureContrast", properties);
        MaterialProperty windTextureInfluence = Find("_WindTextureInfluence", properties);
        MaterialProperty windTextureWaveInfluence = Find("_WindTextureWaveInfluence", properties);

        MaterialProperty enableWaveShape = Find("_EnableWaveShape", properties);
        MaterialProperty waveFrequency = Find("_WaveFrequency", properties);
        MaterialProperty waveSpacingVariation = Find("_WaveSpacingVariation", properties);
        MaterialProperty waveSpeed = Find("_WaveSpeed", properties);
        MaterialProperty waveStrength = Find("_WaveStrength", properties);
        MaterialProperty waveBodyInfluence = Find("_WaveBodyInfluence", properties);
        MaterialProperty waveTipInfluence = Find("_WaveTipInfluence", properties);
        MaterialProperty waveLateralInfluence = Find("_WaveLateralInfluence", properties);

        MaterialProperty enablePlantConeShape = Find("_EnablePlantConeShape", properties);
        MaterialProperty plantConeTipScale = Find("_PlantConeTipScale", properties);
        MaterialProperty enablePlantShadowNoise = Find("_EnablePlantShadowNoise", properties);
        MaterialProperty plantShadowNoiseStrength = Find("_PlantShadowNoiseStrength", properties);
        MaterialProperty plantShadowNoiseContrast = Find("_PlantShadowNoiseContrast", properties);

        MaterialProperty nearColor = Find("_NearColor", properties);
        MaterialProperty farColor = Find("_FarColor", properties);
        MaterialProperty nearFarRange = Find("_NearFarRange", properties);
        MaterialProperty bottomColor = Find("_BottomColor", properties);
        MaterialProperty heightBlend = Find("_HeightBlend", properties);

        MaterialProperty enableTerrain = Find("_EnableTerrain", properties);
        MaterialProperty useTerrainColor = Find("_UseTerrainColor", properties);
        MaterialProperty terrainColor = Find("_TerrainColor", properties);
        MaterialProperty terrainBlendStrength = Find("_TerrainBlendStrength", properties);

        DrawCommon(materialEditor, ref s_ShowCommon, baseMap, cutoff);
        DrawColor(materialEditor, ref s_ShowColor, enableColor, baseColor, nearColor, farColor, nearFarRange, bottomColor, heightBlend);
        DrawLighting(
            materialEditor,
            ref s_ShowLighting,
            enableLighting,
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

        DrawWind(
            materialEditor,
            ref s_ShowWind,
            enableWind,
            windSpeed,
            windDirection,
            windTexture,
            windTextureScale,
            windTextureScrollSpeed,
            enableWaveShape,
            ref s_ShowWindVibrate,
            windTextureContrast,
            windTextureInfluence,
            windTextureWaveInfluence,
            waveFrequency,
            waveSpacingVariation,
            waveSpeed,
            waveStrength,
            waveBodyInfluence,
            waveTipInfluence,
            waveLateralInfluence,
            enablePlantShadowNoise,
            ref s_ShowWindNoise,
            plantShadowNoiseStrength,
            plantShadowNoiseContrast);

        DrawTerrain(materialEditor, ref s_ShowTerrain, enableTerrain, useTerrainColor, terrainColor, terrainBlendStrength);
        DrawPlantShape(materialEditor, ref s_ShowPlantShape, enablePlantConeShape, plantConeTipScale);
        DrawBakeTools(materialEditor);
        NormalizeCutoutRenderState(materialEditor.targets);
    }

    private static MaterialProperty Find(string name, MaterialProperty[] properties)
    {
        return FindProperty(name, properties, false);
    }

    private static void DrawCommon(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty baseMap,
        MaterialProperty cutoff)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Common");
        if (foldout)
        {
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Base Map", "Texture co ban cua plant. Alpha dung de cat hinh la hoac blade."),
                baseMap);
            materialEditor.ShaderProperty(
                cutoff,
                MakeLabel("Alpha Cutoff", "Pixel co alpha thap hon nguong nay se bi cat bo."));
            materialEditor.EnableInstancingField();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawPlantShape(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enablePlantConeShape,
        MaterialProperty plantConeTipScale)
    {
        bool shapeEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enablePlantConeShape,
            MakeLabel("Plant Shape", "Bat hoac tat shape mo rong dan theo chieu cao cho plant."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!shapeEnabled);
        materialEditor.ShaderProperty(
            plantConeTipScale,
            MakeLabel("Tip Scale", "Scale ngang tai ngon. Gia tri lon hon 1 se mo rong phan ngon."));
        EditorGUI.EndDisabledGroup();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2);
    }

    private static void DrawLighting(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableLighting,
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
        bool lightingEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableLighting,
            MakeLabel("Lighting", "Bat hoac tat toan bo anh sang cua plant."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!lightingEnabled);
        materialEditor.ShaderProperty(enableMainLight, MakeLabel("Enable Main Light", "Bat directional light chinh."));
        if (enableMainLight.floatValue > 0.5f)
        {
            materialEditor.ShaderProperty(mainLightIntensity, MakeLabel("Main Intensity", "He so cuong do cua main light."));
        }

        materialEditor.ShaderProperty(enableAdditionalLights, MakeLabel("Enable Additional Lights", "Bat cac light phu nhu point va spot."));
        if (enableAdditionalLights.floatValue > 0.5f)
        {
            materialEditor.ShaderProperty(additionalLightIntensity, MakeLabel("Additional Intensity", "He so cuong do cua cac light phu."));
        }

        materialEditor.ShaderProperty(enableAmbient, MakeLabel("Enable Ambient", "Bat anh sang moi truong."));
        if (enableAmbient.floatValue > 0.5f)
        {
            materialEditor.ShaderProperty(ambientIntensity, MakeLabel("Ambient Intensity", "He so cuong do cua ambient."));
        }

        materialEditor.ShaderProperty(twoSidedLighting, MakeLabel("Two-Sided Lighting", "Tinh sang cho ca hai mat."));
        materialEditor.ShaderProperty(receiveShadows, MakeLabel("Receive Shadows", "Cho phep material nhan bong realtime."));
        if (receiveShadows.floatValue > 0.5f)
        {
            materialEditor.ShaderProperty(shadowStrength, MakeLabel("Shadow Strength", "Muc do bong realtime lam toi material."));
            materialEditor.ShaderProperty(shadowFloor, MakeLabel("Shadow Floor", "Luong sang toi thieu trong vung bong."));
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.HelpBox(
            "Main Light giu khoi chinh, Additional Lights cho point va spot, Ambient giup vung toi khong bi sup thanh den dac.",
            MessageType.None);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }

    private static void NormalizeCutoutRenderState(Object[] targets)
    {
        foreach (Object target in targets)
        {
            if (target is not Material material)
            {
                continue;
            }

            bool hasOldTransparentTag = material.GetTag("RenderType", false, string.Empty) == "Transparent";
            if (!hasOldTransparentTag && material.renderQueue != TransparentRenderQueue)
            {
                continue;
            }

            material.SetOverrideTag("RenderType", string.Empty);
            material.renderQueue = -1;
        }
    }

    private static void DrawWind(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableWind,
        MaterialProperty windSpeed,
        MaterialProperty windDirection,
        MaterialProperty windTexture,
        MaterialProperty windTextureScale,
        MaterialProperty windTextureScrollSpeed,
        MaterialProperty enableWaveShape,
        ref bool showWindVibrate,
        MaterialProperty windTextureContrast,
        MaterialProperty windTextureInfluence,
        MaterialProperty windTextureWaveInfluence,
        MaterialProperty waveFrequency,
        MaterialProperty waveSpacingVariation,
        MaterialProperty waveSpeed,
        MaterialProperty waveStrength,
        MaterialProperty waveBodyInfluence,
        MaterialProperty waveTipInfluence,
        MaterialProperty waveLateralInfluence,
        MaterialProperty enablePlantShadowNoise,
        ref bool showWindNoise,
        MaterialProperty plantShadowNoiseStrength,
        MaterialProperty plantShadowNoiseContrast)
    {
        bool windEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableWind,
            MakeLabel("Wind", "Bat hoac tat toan bo lop gio va wind noise cua plant."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!windEnabled);
        materialEditor.ShaderProperty(windSpeed, MakeLabel("Lean", "Do nghieng co ban cua co theo huong gio."));
        DrawNormalizedDirection2D(windDirection, MakeLabel("Wind Direction (XZ)", "Huong gio toan cuc tren mat phang XZ."));
        materialEditor.TexturePropertySingleLine(
            MakeLabel("Noise Texture", "Noise world-space dung chung cho dao dong gio va shadow noise."),
            windTexture);
        materialEditor.ShaderProperty(windTextureScale, MakeLabel("Noise Scale", "Do lap cua truong noise trong world-space."));
        materialEditor.ShaderProperty(windTextureScrollSpeed, MakeLabel("Noise Scroll Speed", "Toc do troi cua truong noise."));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);
        bool windVibrateEnabled = DrawToggleFoldoutHeader(
            ref showWindVibrate,
            enableWaveShape,
            MakeLabel("Wind Vibrate", "Bat hoac tat lop dao dong procedural cua plant."),
            windEnabled);
        if (showWindVibrate)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!windEnabled || !windVibrateEnabled);
            materialEditor.ShaderProperty(waveFrequency, MakeLabel("Vibrate Frequency", "Mat do dao dong theo huong gio."));
            materialEditor.ShaderProperty(waveSpacingVariation, MakeLabel("Spacing Variation", "Do ngau nhien cua khoang cach giua cac dai dao dong."));
            materialEditor.ShaderProperty(waveSpeed, MakeLabel("Vibrate Speed", "Toc do di chuyen cua lop dao dong."));
            materialEditor.ShaderProperty(waveStrength, MakeLabel("Vibrate Strength", "Bien do tong the cua lop dao dong."));
            materialEditor.ShaderProperty(waveBodyInfluence, MakeLabel("Body Vibrate", "Muc anh huong len phan than co."));
            materialEditor.ShaderProperty(waveTipInfluence, MakeLabel("Tip Vibrate", "Muc anh huong len phan ngon co."));
            materialEditor.ShaderProperty(waveLateralInfluence, MakeLabel("Lateral Vibrate", "Do lac ngang trai phai cua dao dong."));
            materialEditor.ShaderProperty(windTextureContrast, MakeLabel("Noise Contrast", "Do tuong phan cua wind noise."));
            materialEditor.ShaderProperty(windTextureInfluence, MakeLabel("Noise To Lean", "Muc do noise dieu che do nghieng nen."));
            materialEditor.ShaderProperty(windTextureWaveInfluence, MakeLabel("Noise To Vibrate", "Muc do noise dieu che lop dao dong."));
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);
        bool windNoiseEnabled = DrawToggleFoldoutHeader(
            ref showWindNoise,
            enablePlantShadowNoise,
            MakeLabel("Wind Noise", "Bat hoac tat cac mang bong chay tren mat plant tu truong noise cua gio."),
            windEnabled);
        if (showWindNoise)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!windEnabled || !windNoiseEnabled);
            materialEditor.ShaderProperty(plantShadowNoiseStrength, MakeLabel("Strength", "Muc do wind noise lam toi mau co."));
            materialEditor.ShaderProperty(plantShadowNoiseContrast, MakeLabel("Contrast", "Do net giua vung shadow va vung sang."));
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }

    private static void DrawColor(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableColor,
        MaterialProperty baseColor,
        MaterialProperty nearColor,
        MaterialProperty farColor,
        MaterialProperty nearFarRange,
        MaterialProperty bottomColor,
        MaterialProperty heightBlend)
    {
        bool colorEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableColor,
            MakeLabel("Color", "Bat hoac tat cac lop tint mau cua plant."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!colorEnabled);
        materialEditor.ShaderProperty(baseColor, MakeLabel("Base Color", "Mau phu them vao Base Map."));
        materialEditor.ShaderProperty(nearColor, MakeLabel("Near Color", "Mau tint ap dung khi o gan camera."));
        materialEditor.ShaderProperty(farColor, MakeLabel("Far Color", "Mau tint ap dung khi o xa camera."));
        materialEditor.ShaderProperty(nearFarRange, MakeLabel("Near/Far Range", "Khoang cach dung de blend giua Near va Far."));
        materialEditor.ShaderProperty(bottomColor, MakeLabel("Bottom Tint", "Mau tint o goc blade."));
        materialEditor.ShaderProperty(heightBlend, MakeLabel("Height Blend", "Toc do mau o goc chuyen dan len phan ngon."));
        EditorGUI.EndDisabledGroup();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }

    private static void DrawTerrain(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableTerrain,
        MaterialProperty useTerrainColor,
        MaterialProperty terrainColor,
        MaterialProperty terrainBlendStrength)
    {
        bool terrainEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableTerrain,
            MakeLabel("Terrain", "Bat hoac tat terrain blend cua plant."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!terrainEnabled);
        materialEditor.ShaderProperty(useTerrainColor, MakeLabel("Use Terrain Color", "Tron mau terrain vao mau cua co."));
        if (useTerrainColor.floatValue > 0.5f)
        {
            materialEditor.ShaderProperty(terrainColor, MakeLabel("Terrain Color", "Mau terrain dat tay de blend vao co."));
        }

        materialEditor.ShaderProperty(terrainBlendStrength, MakeLabel("Blend Strength", "Muc do terrain duoc tron vao co."));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.HelpBox(
            "Shader co the dung mau terrain dat tay hoac terrain color map toan cuc do he terrain cung cap.",
            MessageType.None);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
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
                "Hay chon dung mot material neu ban muon ghi cac gia tri hien tai tro lai shader defaults.",
                MessageType.None);
            return;
        }

        if (!canBake)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "Thao tac nay ghi cac gia tri float, color va vector hien tai vao shader defaults, dong thoi dong bo texture mac dinh qua ShaderImporter. Texture scale va offset van nam tren material.",
            MessageType.None);
    }

    private static void DrawNormalizedDirection2D(MaterialProperty property, GUIContent label)
    {
        Vector4 vector = property.vectorValue;
        Vector2 direction = new(vector.x, vector.y);

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

    private static bool DrawToggleFoldoutHeader(ref bool foldout, MaterialProperty toggleProperty, GUIContent label, bool interactive = true)
    {
        Rect rect = EditorGUILayout.GetControlRect();
        rect = EditorGUI.IndentedRect(rect);

        int previousIndentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        Rect foldoutRect = new(rect.x, rect.y, 16f, rect.height);
        Rect toggleRect = new(rect.x + 16f, rect.y, 18f, rect.height);
        Rect labelRect = new(rect.x + 36f, rect.y, rect.width - 36f, rect.height);

        Event currentEvent = Event.current;
        if (interactive && currentEvent.type == EventType.MouseDown && labelRect.Contains(currentEvent.mousePosition))
        {
            bool currentEnabled = toggleProperty.floatValue > 0.5f;
            toggleProperty.floatValue = currentEnabled ? 0f : 1f;
            currentEvent.Use();
        }

        bool isEnabled = toggleProperty.floatValue > 0.5f;
        using (new EditorGUI.DisabledScope(!interactive))
        {
            foldout = EditorGUI.Foldout(foldoutRect, foldout, GUIContent.none, true);

            EditorGUI.showMixedValue = toggleProperty.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            isEnabled = EditorGUI.Toggle(toggleRect, toggleProperty.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                toggleProperty.floatValue = isEnabled ? 1f : 0f;
            }

            EditorGUI.showMixedValue = false;
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
        }

        EditorGUI.indentLevel = previousIndentLevel;
        return isEnabled;
    }

    private static GUIContent MakeLabel(string text, string tooltip)
    {
        return new GUIContent(text, tooltip);
    }
}
