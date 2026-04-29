using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Custom material inspector for the grass wind shader.
/// </summary>
public sealed class PlantShaderGUI : ShaderGUI
{
    private static bool s_ShowCommon = true;
    private static bool s_ShowGrassShape = true;
    private static bool s_ShowDistanceBlur = true;
    private static bool s_ShowTransparentBlur = true;
    private static bool s_ShowGrassShadowNoise = true;
    private static bool s_ShowLighting = true;
    private static bool s_ShowWindShape = true;
    private static bool s_ShowWindTexture = true;
    private static bool s_ShowColor;
    private static bool s_ShowTerrain;
    private static bool s_ShowInteraction = true;
    private static bool s_ShowInteractionAdvanced;

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
        MaterialProperty cameraBendStrength = Find("_CameraBendStrength", properties);
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
        MaterialProperty grassShadowNoiseTex = Find("_GrassShadowNoiseTex", properties);
        MaterialProperty grassShadowNoiseStrength = Find("_GrassShadowNoiseStrength", properties);
        MaterialProperty grassShadowNoiseContrast = Find("_GrassShadowNoiseContrast", properties);
        MaterialProperty grassShadowNoiseScale = Find("_GrassShadowNoiseScale", properties);
        MaterialProperty grassShadowNoiseScrollSpeed = Find("_GrassShadowNoiseScrollSpeed", properties);

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
        MaterialProperty interactionRecoveryStrength = Find("_InteractionRecoveryStrength", properties);
        MaterialProperty interactionRecoveryFrequency = Find("_InteractionRecoveryFrequency", properties);
        MaterialProperty interactionRecoveryNoiseScale = Find("_InteractionRecoveryNoiseScale", properties);

        DrawCommon(materialEditor, ref s_ShowCommon, baseMap, baseColor, cutoff, windTexture, windSpeed, windDirection, cameraBendStrength);
        DrawGrassShape(
            materialEditor,
            ref s_ShowGrassShape,
            enableGrassConeShape,
            grassConeTipScale,
            enableGrassDistanceBlur,
            enableGrassTransparentBlurPath,
            grassDistanceBlurStart,
            grassDistanceBlurEnd,
            grassDistanceBlurRadius,
            grassDistanceBlurOpacity,
            grassDistanceBlurBrightness,
            grassDistanceBlurCutoffShift,
            enableGrassShadowNoise,
            grassShadowNoiseTex,
            grassShadowNoiseStrength,
            grassShadowNoiseContrast,
            grassShadowNoiseScale,
            grassShadowNoiseScrollSpeed);
        SyncGrassTransparentBlurState(materialEditor.targets, enableGrassDistanceBlur, enableGrassTransparentBlurPath);
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
            interactionTrail,
            interactionRecoveryStrength,
            interactionRecoveryFrequency,
            interactionRecoveryNoiseScale);
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
        MaterialProperty windDirection,
        MaterialProperty cameraBendStrength)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Common");
        if (foldout)
        {
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Base Map", "Texture mau co ban cua co. Kenh alpha dung de cat hinh la hoac blade."),
                baseMap);
            materialEditor.ShaderProperty(
                baseColor,
                MakeLabel("Base Color", "Mau phu nhan them vao Base Map de doi tong tong the cua co."));
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Wind Texture", "Texture gio trong world-space, quyet dinh vung nao gio manh va vung nao gio yeu."),
                windTexture);
            materialEditor.ShaderProperty(
                cutoff,
                MakeLabel("Alpha Cutoff", "Pixel co alpha thap hon nguong nay se bi cat bo."));
            materialEditor.ShaderProperty(
                windSpeed,
                MakeLabel("Grass Lean", "Do nghieng co ban cua co theo huong gio."));
            DrawNormalizedDirection2D(
                windDirection,
                MakeLabel("Wind Direction (XZ)", "Huong gio toan cuc tren mat phang XZ."));
            materialEditor.ShaderProperty(
                cameraBendStrength,
                MakeLabel("Camera Bend Strength", "Day nhe phan ngon theo huong camera de giam lo khe giua cac card co."));
            materialEditor.EnableInstancingField();
            EditorGUILayout.HelpBox(
                "Grass Lean quyet dinh do nghieng nen cua co. Camera Bend Strength la meo thi giac de giam lo khe voi quad grass. Chuyen dong ro rang den tu Wave Shape va Wind Texture.",
                MessageType.None);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawGrassShape(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty enableGrassConeShape,
        MaterialProperty grassConeTipScale,
        MaterialProperty enableGrassDistanceBlur,
        MaterialProperty enableGrassTransparentBlurPath,
        MaterialProperty grassDistanceBlurStart,
        MaterialProperty grassDistanceBlurEnd,
        MaterialProperty grassDistanceBlurRadius,
        MaterialProperty grassDistanceBlurOpacity,
        MaterialProperty grassDistanceBlurBrightness,
        MaterialProperty grassDistanceBlurCutoffShift,
        MaterialProperty enableGrassShadowNoise,
        MaterialProperty grassShadowNoiseTex,
        MaterialProperty grassShadowNoiseStrength,
        MaterialProperty grassShadowNoiseContrast,
        MaterialProperty grassShadowNoiseScale,
        MaterialProperty grassShadowNoiseScrollSpeed)
    {
        bool isEnabled = DrawToggleFoldoutHeader(
            ref foldout,
            enableGrassConeShape,
            MakeLabel("Grass Shape", "Bat hoac tat shape mo rong dan theo chieu cao cho grass."));
        if (foldout)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!isEnabled);
            materialEditor.ShaderProperty(
                grassConeTipScale,
                MakeLabel("Tip Scale", "He so scale ngang tai ngon. Gia tri 1 giu nguyen form goc, lon hon 1 se mo rong dan len phan ngon."));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(2);
            EditorGUI.indentLevel++;
            bool blurEnabled = DrawToggleFoldoutHeader(
                ref s_ShowDistanceBlur,
                enableGrassDistanceBlur,
                MakeLabel("Distance Blur", "Lam texture grass bi nhoe dan khi ra xa camera."));
            if (s_ShowDistanceBlur)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!blurEnabled);
                materialEditor.ShaderProperty(
                    grassDistanceBlurStart,
                    MakeLabel("Blur Start", "Khoang cach bat dau xuat hien nhoe."));
                materialEditor.ShaderProperty(
                    grassDistanceBlurEnd,
                    MakeLabel("Blur End", "Khoang cach dat muc nhoe toi da."));
                materialEditor.ShaderProperty(
                    grassDistanceBlurRadius,
                    MakeLabel("Blur Radius", "Ban kinh sample texture de tao cam giac bi boi nhoe."));
                materialEditor.ShaderProperty(
                    grassDistanceBlurOpacity,
                    MakeLabel("Blur Opacity", "Do day alpha cua vung smear. Giam xuong de vung blur trong va min hon."));
                materialEditor.ShaderProperty(
                    grassDistanceBlurBrightness,
                    MakeLabel("Blur Brightness", "Tang do sang cua vung smear de no khong bi nap thanh mang toi."));
                materialEditor.ShaderProperty(
                    grassDistanceBlurCutoffShift,
                    MakeLabel("Edge Softness", "Noi long alpha cutoff khi xa camera de mep grass mem hon."));
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(2);
                bool transparentBlurEnabled = DrawToggleFoldoutHeader(
                    ref s_ShowTransparentBlur,
                    enableGrassTransparentBlurPath,
                    MakeLabel("Transparent Blur", "Bat path dither/transparent rieng cho blur xa. Chi material nay moi doi render state."));
                if (s_ShowTransparentBlur)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(!blurEnabled || !transparentBlurEnabled);
                    EditorGUILayout.HelpBox(
                        "Path nay doi sang blur trong suot o xa de giam cam giac card grass va doi render state cua material.",
                        MessageType.None);
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            bool shadowNoiseEnabled = DrawToggleFoldoutHeader(
                ref s_ShowGrassShadowNoise,
                enableGrassShadowNoise,
                MakeLabel("Shadow Noise", "Gia lap cac mang bong chay tren mat grass bang noise world-space."));
            if (s_ShowGrassShadowNoise)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!shadowNoiseEnabled);
                materialEditor.TexturePropertySingleLine(
                    MakeLabel("Shadow Texture", "Noise world-space dung de ve cac mang toi chay tren mat co."),
                    grassShadowNoiseTex);
                materialEditor.ShaderProperty(
                    grassShadowNoiseStrength,
                    MakeLabel("Shadow Strength", "Muc do shadow-noise lam toi mau co."));
                materialEditor.ShaderProperty(
                    grassShadowNoiseContrast,
                    MakeLabel("Shadow Contrast", "Do net giua vung shadow va vung sang cua noise."));
                materialEditor.ShaderProperty(
                    grassShadowNoiseScale,
                    MakeLabel("Shadow Scale", "Do lon cua pattern shadow trong world-space XZ."));
                materialEditor.ShaderProperty(
                    grassShadowNoiseScrollSpeed,
                    MakeLabel("Shadow Scroll Speed", "Toc do shadow-noise troi theo huong gio."));
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
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
                MakeLabel("Enable Main Light", "Bat anh huong cua directional light chinh trong URP."));
            if (enableMainLight.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    mainLightIntensity,
                    MakeLabel("Main Intensity", "He so cuong do sang cua main light."));
            }

            materialEditor.ShaderProperty(
                enableAdditionalLights,
                MakeLabel("Enable Additional Lights", "Bat anh huong cua cac light phu nhu point, spot hoac directional bo sung."));
            if (enableAdditionalLights.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    additionalLightIntensity,
                    MakeLabel("Additional Intensity", "He so cuong do sang cua cac light phu."));
            }

            materialEditor.ShaderProperty(
                enableAmbient,
                MakeLabel("Enable Ambient", "Bat anh sang moi truong lay tu ambient lighting cua scene."));
            if (enableAmbient.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    ambientIntensity,
                    MakeLabel("Ambient Intensity", "He so cuong do cua anh sang moi truong."));
            }

            materialEditor.ShaderProperty(
                twoSidedLighting,
                MakeLabel("Two-Sided Lighting", "Tinh sang cho ca hai mat de mat sau cua foliage khong bi den."));
            materialEditor.ShaderProperty(
                receiveShadows,
                MakeLabel("Receive Shadows", "Cho phep material nhan bong realtime tu light trong URP."));
            if (receiveShadows != null && receiveShadows.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    shadowStrength,
                    MakeLabel("Shadow Strength", "Muc do bong realtime lam toi material manh toi dau."));
                materialEditor.ShaderProperty(
                    shadowFloor,
                    MakeLabel("Shadow Floor", "Luong sang toi thieu con giu lai trong vung bi bong che."));
            }

            EditorGUILayout.HelpBox(
                "Main Light giu lai khoi chinh duoi directional light. Additional Lights cho point va spot light tac dong len co. Ambient giup vung toi khong bi sup thanh den dac.",
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
                MakeLabel("Enable Wave Motion", "Bat hoac tat lop chuyen dong song procedural cua co."));
            EditorGUI.BeginDisabledGroup(enableWaveShape.floatValue < 0.5f);
            materialEditor.ShaderProperty(waveFrequency, MakeLabel("Wave Frequency", "Mat do song theo huong gio."));
            materialEditor.ShaderProperty(waveSpacingVariation, MakeLabel("Wave Spacing Variation", "Do ngau nhien cua khoang cach giua cac dai song."));
            materialEditor.ShaderProperty(waveSpeed, MakeLabel("Wave Speed", "Toc do di chuyen cua lop song."));
            materialEditor.ShaderProperty(waveStrength, MakeLabel("Wave Strength", "Bien do tong the cua chuyen dong song."));
            materialEditor.ShaderProperty(waveBodyInfluence, MakeLabel("Body Wave", "Muc anh huong cua song len phan than co."));
            materialEditor.ShaderProperty(waveTipInfluence, MakeLabel("Tip Wave", "Muc anh huong cua song len phan ngon co."));
            materialEditor.ShaderProperty(waveLateralInfluence, MakeLabel("Lateral Wave", "Do lac ngang trai phai cua song."));
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
            materialEditor.ShaderProperty(windTextureScale, MakeLabel("Texture Scale", "Do lap cua wind texture trong world-space."));
            materialEditor.ShaderProperty(windTextureScrollSpeed, MakeLabel("Texture Scroll Speed", "Toc do troi cua truong gio trong wind texture."));
            materialEditor.ShaderProperty(windTextureContrast, MakeLabel("Texture Contrast", "Khoang remap dung de tang hoac giam do tuong phan cua wind texture."));
            materialEditor.ShaderProperty(windTextureInfluence, MakeLabel("Lean Influence", "Muc do wind texture anh huong len do nghieng co ban cua co."));
            materialEditor.ShaderProperty(windTextureWaveInfluence, MakeLabel("Wave Influence", "Muc do wind texture dieu che lop song."));
            EditorGUILayout.HelpBox(
                "Wind Texture dong vai tro truong gio chinh. Wind Direction xoay huong sample, con Scroll Speed lam pattern gio troi qua be mat.",
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
            materialEditor.ShaderProperty(nearColor, MakeLabel("Near Color", "Mau tint ap dung cho co o gan camera."));
            materialEditor.ShaderProperty(farColor, MakeLabel("Far Color", "Mau tint ap dung cho co o xa camera."));
            materialEditor.ShaderProperty(nearFarRange, MakeLabel("Near/Far Range", "Khoang cach dung de blend giua Near Color va Far Color."));
            materialEditor.ShaderProperty(bottomColor, MakeLabel("Bottom Tint", "Mau tint ap dung o phan goc cua moi blade co."));
            materialEditor.ShaderProperty(heightBlend, MakeLabel("Height Blend", "Toc do mau o goc chuyen dan len mau binh thuong o phan ngon."));
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
            materialEditor.ShaderProperty(useTerrainColor, MakeLabel("Use Terrain Color", "Tron mau terrain vao mau cua co de co hoa nen tot hon."));
            if (useTerrainColor.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(terrainColor, MakeLabel("Terrain Color", "Mau terrain lay tu he terrain hoac dat tay de blend vao co."));
            }

            materialEditor.ShaderProperty(terrainBlendStrength, MakeLabel("Blend Strength", "Muc do mau terrain hoac terrain color map duoc tron vao co."));

            EditorGUILayout.HelpBox(
                "Trong URP, shader co the dung mau terrain dat tay hoac terrain color map toan cuc do GrassTerrainColorMapController cung cap.",
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
        MaterialProperty interactionTrail,
        MaterialProperty interactionRecoveryStrength,
        MaterialProperty interactionRecoveryFrequency,
        MaterialProperty interactionRecoveryNoiseScale)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Interaction");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                enableInteraction,
                MakeLabel("Enable Interaction", "Cho phép material phản ứng với RenderTexture interaction toàn cục của hệ cỏ."));

            EditorGUI.BeginDisabledGroup(enableInteraction.floatValue < 0.5f);
            materialEditor.ShaderProperty(interactionStrength, MakeLabel("Strength", "Hệ số tổng quyết định loại cỏ này phản ứng mạnh hay yếu trước cùng một nguồn tương tác."));
            materialEditor.ShaderProperty(interactionPushAway, MakeLabel("Push Away", "Độ nghiêng ngang ban đầu theo hướng tác động của player hoặc vật thể tương tác."));
            materialEditor.ShaderProperty(interactionFlatten, MakeLabel("Flatten", "Mức độ cỏ bị ép thấp xuống khi đang chịu tác động trực tiếp."));
            materialEditor.ShaderProperty(interactionVerticalRange, MakeLabel("Vertical Range", "Khoảng cao độ mà interaction còn có hiệu lực, giúp tránh ảnh hưởng nhầm lên tầng địa hình khác."));
            EditorGUI.EndDisabledGroup();

            s_ShowInteractionAdvanced = EditorGUILayout.Foldout(s_ShowInteractionAdvanced, "Advanced", true);
            if (s_ShowInteractionAdvanced)
            {
                EditorGUI.BeginDisabledGroup(enableInteraction.floatValue < 0.5f);
                materialEditor.ShaderProperty(interactionRadiusMultiplier, MakeLabel("Radius Multiplier", "Mở rộng hoặc thu hẹp vùng phản ứng khi shader giải mã interaction field từ RenderTexture."));
                materialEditor.ShaderProperty(interactionTrail, MakeLabel("Trail Response", "Điều khiển tốc độ nhả độ nghiêng cũ. Giá trị cao giữ độ nghiêng lâu hơn trước khi cỏ bắt đầu hồi rõ."));
                materialEditor.ShaderProperty(interactionRecoveryStrength, MakeLabel("Recovery Strength", "Biên độ rung hồi khi cỏ đang trả dần về trạng thái ban đầu."));
                materialEditor.ShaderProperty(interactionRecoveryFrequency, MakeLabel("Recovery Frequency", "Tốc độ dao động trong lúc hồi. Giá trị cao làm cỏ rung nhiều nhịp hơn trước khi dừng."));
                materialEditor.ShaderProperty(interactionRecoveryNoiseScale, MakeLabel("Recovery Noise Scale", "Độ lệch pha theo world-space để một bãi cỏ lớn không rung cùng nhịp như nhau."));
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.HelpBox(
                "Particle System quyết định dữ liệu đầu vào của interaction: emission quyết định mật độ, lifetime quyết định độ dài, alpha fade quyết định cường độ và quãng hồi. Material ở đây chủ yếu quyết định cỏ bị đè mạnh tới đâu, hồi nhanh hay chậm và rung mạnh hay nhẹ.",
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

    private static bool DrawToggleFoldoutHeader(ref bool foldout, MaterialProperty toggleProperty, GUIContent label)
    {
        Rect rect = EditorGUILayout.GetControlRect();
        rect = EditorGUI.IndentedRect(rect);

        int previousIndentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        Rect foldoutRect = new(rect.x, rect.y, 16f, rect.height);
        Rect toggleRect = new(rect.x + 16f, rect.y, 18f, rect.height);
        Rect labelRect = new(rect.x + 36f, rect.y, rect.width - 36f, rect.height);

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown && labelRect.Contains(currentEvent.mousePosition))
        {
            bool currentEnabled = toggleProperty.floatValue > 0.5f;
            toggleProperty.floatValue = currentEnabled ? 0f : 1f;
            currentEvent.Use();
        }

        foldout = EditorGUI.Foldout(foldoutRect, foldout, GUIContent.none, true);

        EditorGUI.showMixedValue = toggleProperty.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        bool isEnabled = EditorGUI.Toggle(toggleRect, toggleProperty.floatValue > 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            toggleProperty.floatValue = isEnabled ? 1f : 0f;
        }

        EditorGUI.showMixedValue = false;
        EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
        EditorGUI.indentLevel = previousIndentLevel;
        return isEnabled;
    }

    private static GUIContent MakeLabel(string text, string tooltip)
    {
        return new GUIContent(text, tooltip);
    }
}
