using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Custom material inspector for the plant shader.
/// </summary>
public sealed class PlantShaderGUI : ShaderGUI
{
    private static bool s_ShowCommon = true;
    private static bool s_ShowGrassShape = true;
    private static bool s_ShowDistanceBlur = true;
    private static bool s_ShowTransparentBlur = true;
    private static bool s_ShowLighting = true;
    private static bool s_ShowWind = true;
    private static bool s_ShowWindVibrate = true;
    private static bool s_ShowWindNoise = true;
    private static bool s_ShowColor = true;
    private static bool s_ShowTerrain = true;
    private static bool s_ShowInteraction = true;
    private static bool s_ShowInteractionAdvanced;

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

        MaterialProperty enableGrassConeShape = Find("_EnableGrassConeShape", properties);
        MaterialProperty grassConeTipScale = Find("_GrassConeTipScale", properties);
        MaterialProperty enableGrassDistanceBlur = Find("_EnableGrassDistanceBlur", properties);
        MaterialProperty enableGrassTransparentBlurPath = Find("_EnableGrassTransparentBlurPath", properties);
        MaterialProperty grassDistanceBlurStart = Find("_GrassDistanceBlurStart", properties);
        MaterialProperty grassDistanceBlurEnd = Find("_GrassDistanceBlurEnd", properties);
        MaterialProperty grassDistanceBlurRadius = Find("_GrassDistanceBlurRadius", properties);
        MaterialProperty grassDistanceBlurOpacity = Find("_GrassDistanceBlurOpacity", properties);
        MaterialProperty grassDistanceBlurBrightness = Find("_GrassDistanceBlurBrightness", properties);
        MaterialProperty grassDistanceBlurCutoffShift = Find("_GrassDistanceBlurCutoffShift", properties);
        MaterialProperty enableGrassShadowNoise = Find("_EnableGrassShadowNoise", properties);
        MaterialProperty grassShadowNoiseStrength = Find("_GrassShadowNoiseStrength", properties);
        MaterialProperty grassShadowNoiseContrast = Find("_GrassShadowNoiseContrast", properties);

        MaterialProperty nearColor = Find("_NearColor", properties);
        MaterialProperty farColor = Find("_FarColor", properties);
        MaterialProperty nearFarRange = Find("_NearFarRange", properties);
        MaterialProperty bottomColor = Find("_BottomColor", properties);
        MaterialProperty heightBlend = Find("_HeightBlend", properties);

        MaterialProperty enableTerrain = Find("_EnableTerrain", properties);
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
        MaterialProperty interactionRecoveryStrength = Find("_InteractionRecoveryStrength", properties);
        MaterialProperty interactionRecoveryFrequency = Find("_InteractionRecoveryFrequency", properties);
        MaterialProperty interactionRecoveryNoiseScale = Find("_InteractionRecoveryNoiseScale", properties);

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

        DrawDistanceBlur(
            materialEditor,
            ref s_ShowDistanceBlur,
            enableGrassDistanceBlur,
            enableGrassTransparentBlurPath,
            grassDistanceBlurStart,
            grassDistanceBlurEnd,
            grassDistanceBlurRadius,
            grassDistanceBlurOpacity,
            grassDistanceBlurBrightness,
            grassDistanceBlurCutoffShift);
        SyncGrassTransparentBlurState(materialEditor.targets, enableGrassDistanceBlur, enableGrassTransparentBlurPath);

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
            enableGrassShadowNoise,
            ref s_ShowWindNoise,
            grassShadowNoiseStrength,
            grassShadowNoiseContrast);

        DrawInteraction(
            materialEditor,
            ref s_ShowInteraction,
            enableInteraction,
            interactionStrength,
            interactionPushAway,
            interactionFlatten,
            interactionRadiusMultiplier,
            interactionVerticalRange,
            interactionTrail,
            interactionRecoveryStrength,
            interactionRecoveryFrequency,
            interactionRecoveryNoiseScale);
        DrawTerrain(materialEditor, ref s_ShowTerrain, enableTerrain, useTerrainColor, terrainColor, terrainBlendStrength);
        DrawGrassShape(materialEditor, ref s_ShowGrassShape, enableGrassConeShape, grassConeTipScale);
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

    private static void DrawGrassShape(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableGrassConeShape,
        MaterialProperty grassConeTipScale)
    {
        bool shapeEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableGrassConeShape,
            MakeLabel("Grass Shape", "Bat hoac tat shape mo rong dan theo chieu cao cho grass."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!shapeEnabled);
        materialEditor.ShaderProperty(
            grassConeTipScale,
            MakeLabel("Tip Scale", "Scale ngang tai ngon. Gia tri lon hon 1 se mo rong phan ngon."));
        EditorGUI.EndDisabledGroup();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2);
    }

    private static void DrawDistanceBlur(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableGrassDistanceBlur,
        MaterialProperty enableGrassTransparentBlurPath,
        MaterialProperty grassDistanceBlurStart,
        MaterialProperty grassDistanceBlurEnd,
        MaterialProperty grassDistanceBlurRadius,
        MaterialProperty grassDistanceBlurOpacity,
        MaterialProperty grassDistanceBlurBrightness,
        MaterialProperty grassDistanceBlurCutoffShift)
    {
        bool blurEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableGrassDistanceBlur,
            MakeLabel("Distance Blur", "Lam texture grass bi nhoe dan khi ra xa camera."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!blurEnabled);
        materialEditor.ShaderProperty(grassDistanceBlurStart, MakeLabel("Blur Start", "Khoang cach bat dau xuat hien nhoe."));
        materialEditor.ShaderProperty(grassDistanceBlurEnd, MakeLabel("Blur End", "Khoang cach dat muc nhoe toi da."));
        materialEditor.ShaderProperty(grassDistanceBlurRadius, MakeLabel("Blur Radius", "Ban kinh sample texture de tao cam giac bi boi nhoe."));
        materialEditor.ShaderProperty(grassDistanceBlurOpacity, MakeLabel("Blur Opacity", "Do day alpha cua vung smear."));
        materialEditor.ShaderProperty(grassDistanceBlurBrightness, MakeLabel("Blur Brightness", "Tang do sang cua vung smear."));
        materialEditor.ShaderProperty(grassDistanceBlurCutoffShift, MakeLabel("Edge Softness", "Noi long alpha cutoff khi xa camera."));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);
        bool transparentBlurEnabled = DrawToggleFoldoutHeader(
            ref s_ShowTransparentBlur,
            enableGrassTransparentBlurPath,
            MakeLabel("Transparent Blur", "Bat path dither transparent rieng cho blur xa."),
            blurEnabled);
        if (s_ShowTransparentBlur)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!blurEnabled || !transparentBlurEnabled);
            EditorGUILayout.HelpBox(
                "Path nay doi sang blur trong suot o xa va doi render state cua material.",
                MessageType.None);
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2);
    }

    private static void SyncGrassTransparentBlurState(
        Object[] targets,
        MaterialProperty enableGrassDistanceBlur,
        MaterialProperty enableGrassTransparentBlurPath)
    {
        bool useTransparentBlurPath = enableGrassDistanceBlur.floatValue > 0.5f &&
                                      enableGrassTransparentBlurPath.floatValue > 0.5f;

        foreach (Object target in targets)
        {
            if (target is not Material material)
            {
                continue;
            }

            material.SetFloat("_PlantSrcBlend", useTransparentBlurPath ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_PlantDstBlend", useTransparentBlurPath ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.SetFloat("_PlantZWrite", useTransparentBlurPath ? 0f : 1f);
            material.renderQueue = useTransparentBlurPath ? (int)RenderQueue.Transparent : -1;
            material.SetOverrideTag("RenderType", useTransparentBlurPath ? "Transparent" : string.Empty);
        }
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
        MaterialProperty enableGrassShadowNoise,
        ref bool showWindNoise,
        MaterialProperty grassShadowNoiseStrength,
        MaterialProperty grassShadowNoiseContrast)
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
            enableGrassShadowNoise,
            MakeLabel("Wind Noise", "Bat hoac tat cac mang bong chay tren mat grass tu truong noise cua gio."),
            windEnabled);
        if (showWindNoise)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!windEnabled || !windNoiseEnabled);
            materialEditor.ShaderProperty(grassShadowNoiseStrength, MakeLabel("Strength", "Muc do wind noise lam toi mau co."));
            materialEditor.ShaderProperty(grassShadowNoiseContrast, MakeLabel("Contrast", "Do net giua vung shadow va vung sang."));
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

    private static void DrawInteraction(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableInteraction,
        MaterialProperty interactionStrength,
        MaterialProperty interactionPushAway,
        MaterialProperty interactionFlatten,
        MaterialProperty interactionRadiusMultiplier,
        MaterialProperty interactionVerticalRange,
        MaterialProperty interactionTrail,
        MaterialProperty interactionRecoveryStrength,
        MaterialProperty interactionRecoveryFrequency,
        MaterialProperty interactionRecoveryNoiseScale)
    {
        bool interactionEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableInteraction,
            MakeLabel("Interaction", "Bat hoac tat grass interaction cua plant."));
        if (!foldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginDisabledGroup(!interactionEnabled);
        materialEditor.ShaderProperty(interactionStrength, MakeLabel("Strength", "He so tong quyet dinh muc do phan ung."));
        materialEditor.ShaderProperty(interactionPushAway, MakeLabel("Push Away", "Do nghieng ngang ban dau theo huong tac dong."));
        materialEditor.ShaderProperty(interactionFlatten, MakeLabel("Flatten", "Muc do co bi ep thap xuong khi dang chiu tac dong."));
        materialEditor.ShaderProperty(interactionVerticalRange, MakeLabel("Vertical Range", "Khoang cao do ma interaction con co hieu luc."));

        s_ShowInteractionAdvanced = EditorGUILayout.Foldout(s_ShowInteractionAdvanced, "Advanced", true);
        if (s_ShowInteractionAdvanced)
        {
            materialEditor.ShaderProperty(interactionRadiusMultiplier, MakeLabel("Radius Multiplier", "Mo rong hoac thu hep vung phan ung."));
            materialEditor.ShaderProperty(interactionTrail, MakeLabel("Trail Response", "Dieu khien toc do nha do nghieng cu."));
            materialEditor.ShaderProperty(interactionRecoveryStrength, MakeLabel("Recovery Strength", "Bien do rung hoi khi co dang tra dan ve trang thai ban dau."));
            materialEditor.ShaderProperty(interactionRecoveryFrequency, MakeLabel("Recovery Frequency", "Toc do dao dong trong luc hoi."));
            materialEditor.ShaderProperty(interactionRecoveryNoiseScale, MakeLabel("Recovery Noise Scale", "Do lech pha theo world-space de bai co lon khong rung cung nhip."));
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.HelpBox(
                "Neu scene dang dung GrassInteractionConfig tren interaction system, cac gia tri interaction trong material nay chi la fallback. Luc do ban chinh interaction o mot SO duy nhat thay vi sua o source, config va material rieng le.",
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
