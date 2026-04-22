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
        MaterialProperty cameraBendStrength = Find("_CameraBendStrength", properties);

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
                MakeLabel("Base Map", "Texture màu cơ bản của cỏ. Kênh alpha dùng để cắt hình lá hoặc blade."),
                baseMap);
            materialEditor.ShaderProperty(
                baseColor,
                MakeLabel("Base Color", "Màu phủ nhân thêm vào Base Map để đổi tông tổng thể của cỏ."));
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Wind Texture", "Texture gió trong world-space, quyết định vùng nào gió mạnh và vùng nào gió yếu."),
                windTexture);
            materialEditor.ShaderProperty(
                cutoff,
                MakeLabel("Alpha Cutoff", "Pixel có alpha thấp hơn ngưỡng này sẽ bị cắt bỏ."));
            materialEditor.ShaderProperty(
                windSpeed,
                MakeLabel("Grass Lean", "Độ nghiêng cơ bản của cỏ theo hướng gió."));
            DrawNormalizedDirection2D(
                windDirection,
                MakeLabel("Wind Direction (XZ)", "Hướng gió toàn cục trên mặt phẳng XZ."));
            materialEditor.ShaderProperty(
                cameraBendStrength,
                MakeLabel("Camera Bend Strength", "Đẩy nhẹ phần ngọn theo hướng camera để giảm lộ khe giữa các card cỏ."));
            materialEditor.EnableInstancingField();
            EditorGUILayout.HelpBox(
                "Grass Lean quyết định độ nghiêng nền của cỏ. Camera Bend Strength là mẹo thị giác để giảm lộ khe với quad grass. Chuyển động nhìn thấy rõ sẽ đến từ Wave Shape và phần scroll của Wind Texture.",
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
                MakeLabel("Enable Main Light", "Bật ảnh hưởng của directional light chính trong URP."));
            if (enableMainLight.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    mainLightIntensity,
                    MakeLabel("Main Intensity", "Hệ số cường độ sáng của main light."));
            }

            materialEditor.ShaderProperty(
                enableAdditionalLights,
                MakeLabel("Enable Additional Lights", "Bật ảnh hưởng của các light phụ như point, spot hoặc directional bổ sung."));
            if (enableAdditionalLights.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    additionalLightIntensity,
                    MakeLabel("Additional Intensity", "Hệ số cường độ sáng của các light phụ."));
            }

            materialEditor.ShaderProperty(
                enableAmbient,
                MakeLabel("Enable Ambient", "Bật ánh sáng môi trường lấy từ ambient lighting của scene."));
            if (enableAmbient.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    ambientIntensity,
                    MakeLabel("Ambient Intensity", "Hệ số cường độ của ánh sáng môi trường."));
            }

            materialEditor.ShaderProperty(
                twoSidedLighting,
                MakeLabel("Two-Sided Lighting", "Tính sáng cho cả hai mặt để mặt sau của foliage không bị đen."));
            materialEditor.ShaderProperty(
                receiveShadows,
                MakeLabel("Receive Shadows", "Cho phép material nhận bóng realtime từ light trong URP."));
            if (receiveShadows != null && receiveShadows.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    shadowStrength,
                    MakeLabel("Shadow Strength", "Mức độ bóng realtime làm tối material mạnh tới đâu."));
                materialEditor.ShaderProperty(
                    shadowFloor,
                    MakeLabel("Shadow Floor", "Lượng sáng tối thiểu còn giữ lại trong vùng bị bóng che."));
            }

            EditorGUILayout.HelpBox(
                "Main Light giữ lại khối chính dưới directional light. Additional Lights cho point và spot light tác động lên cỏ. Ambient giúp vùng tối không bị sụp thành đen đặc.",
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
                MakeLabel("Enable Wave Motion", "Bật hoặc tắt lớp chuyển động sóng procedural của cỏ."));
            EditorGUI.BeginDisabledGroup(enableWaveShape.floatValue < 0.5f);
            materialEditor.ShaderProperty(
                waveFrequency,
                MakeLabel("Wave Frequency", "Mật độ sóng theo hướng gió."));
            materialEditor.ShaderProperty(
                waveSpacingVariation,
                MakeLabel("Wave Spacing Variation", "Độ ngẫu nhiên của khoảng cách giữa các dải sóng."));
            materialEditor.ShaderProperty(
                waveSpeed,
                MakeLabel("Wave Speed", "Tốc độ di chuyển của lớp sóng."));
            materialEditor.ShaderProperty(
                waveStrength,
                MakeLabel("Wave Strength", "Biên độ tổng thể của chuyển động sóng."));
            materialEditor.ShaderProperty(
                waveBodyInfluence,
                MakeLabel("Body Wave", "Mức ảnh hưởng của sóng lên phần thân cỏ."));
            materialEditor.ShaderProperty(
                waveTipInfluence,
                MakeLabel("Tip Wave", "Mức ảnh hưởng của sóng lên phần ngọn cỏ."));
            materialEditor.ShaderProperty(
                waveLateralInfluence,
                MakeLabel("Lateral Wave", "Độ lắc ngang trái phải của sóng."));
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
                MakeLabel("Texture Scale", "Độ lặp của wind texture trong world-space."));
            materialEditor.ShaderProperty(
                windTextureScrollSpeed,
                MakeLabel("Texture Scroll Speed", "Tốc độ trôi của trường gió trong wind texture."));
            materialEditor.ShaderProperty(
                windTextureContrast,
                MakeLabel("Texture Contrast", "Khoảng remap dùng để tăng hoặc giảm độ tương phản của wind texture."));
            materialEditor.ShaderProperty(
                windTextureInfluence,
                MakeLabel("Lean Influence", "Mức độ wind texture ảnh hưởng lên độ nghiêng cơ bản của cỏ."));
            materialEditor.ShaderProperty(
                windTextureWaveInfluence,
                MakeLabel("Wave Influence", "Mức độ wind texture điều chế lớp sóng."));
            EditorGUILayout.HelpBox(
                "Wind Texture đóng vai trò trường gió chính. Wind Direction xoay hướng sample, còn Scroll Speed làm pattern gió trôi qua bề mặt.",
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
                MakeLabel("Near Color", "Màu tint áp dụng cho cỏ ở gần camera."));
            materialEditor.ShaderProperty(
                farColor,
                MakeLabel("Far Color", "Màu tint áp dụng cho cỏ ở xa camera."));
            materialEditor.ShaderProperty(
                nearFarRange,
                MakeLabel("Near/Far Range", "Khoảng cách dùng để blend giữa Near Color và Far Color."));
            materialEditor.ShaderProperty(
                bottomColor,
                MakeLabel("Bottom Tint", "Màu tint áp dụng ở phần gốc của mỗi blade cỏ."));
            materialEditor.ShaderProperty(
                heightBlend,
                MakeLabel("Height Blend", "Tốc độ màu ở gốc chuyển dần lên màu bình thường ở phần ngọn."));
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
                MakeLabel("Use Terrain Color", "Trộn màu terrain vào màu của cỏ để cỏ hòa nền tốt hơn."));
            if (useTerrainColor.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    terrainColor,
                    MakeLabel("Terrain Color", "Màu terrain lấy từ hệ terrain hoặc đặt tay để blend vào cỏ."));
            }

            materialEditor.ShaderProperty(
                terrainBlendStrength,
                MakeLabel("Blend Strength", "Mức độ màu terrain hoặc terrain color map được trộn vào cỏ."));

            EditorGUILayout.HelpBox(
                "Trong URP, shader có thể dùng màu terrain đặt tay hoặc terrain color map toàn cục do GrassTerrainColorMapController cung cấp.",
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
            materialEditor.ShaderProperty(
                interactionStrength,
                MakeLabel("Strength", "Hệ số tổng quyết định độ mạnh của interaction."));
            materialEditor.ShaderProperty(
                interactionPushAway,
                MakeLabel("Push Away", "Độ đẩy ngang của ngọn cỏ khi bị interaction tác động."));
            materialEditor.ShaderProperty(
                interactionFlatten,
                MakeLabel("Flatten", "Mức độ interaction ép cỏ chúi xuống dưới."));
            materialEditor.ShaderProperty(
                interactionRadiusMultiplier,
                MakeLabel("Radius Multiplier", "Mở rộng hoặc siết lại vùng phản ứng khi giải mã interaction field."));
            materialEditor.ShaderProperty(
                interactionVerticalRange,
                MakeLabel("Vertical Range", "Giới hạn interaction theo độ cao để tránh ảnh hưởng nhầm tầng khác."));
            materialEditor.ShaderProperty(
                interactionTrail,
                MakeLabel("Trail Response", "Điều khiển độ gắt hoặc độ mềm của dấu interaction còn lưu lại."));
            materialEditor.ShaderProperty(
                interactionRecoveryStrength,
                MakeLabel("Recovery Strength", "Độ rung hồi khi cỏ trở về trạng thái bình thường sau interaction."));
            materialEditor.ShaderProperty(
                interactionRecoveryFrequency,
                MakeLabel("Recovery Frequency", "Tốc độ rung hồi của cỏ khi đang trả về trạng thái ban đầu."));
            materialEditor.ShaderProperty(
                interactionRecoveryNoiseScale,
                MakeLabel("Recovery Noise Scale", "Độ biến thiên pha trong world-space để vùng cỏ lớn không rung cùng nhịp."));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "Interaction runtime lấy dữ liệu từ một GrassInteractionSystem và một hoặc nhiều GrassInteractionSource vẽ particle vào interaction RT. Trail particle sẽ tạo độ rung hồi thông qua nhóm recovery trong shader.",
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
                "Hãy chọn đúng một material nếu bạn muốn ghi các giá trị hiện tại trở lại shader defaults.",
                MessageType.None);
            return;
        }

        if (!canBake)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
            return;
        }

            EditorGUILayout.HelpBox(
            "Thao tác này ghi các giá trị float, color và vector hiện tại vào shader defaults, đồng thời đồng bộ texture mặc định qua ShaderImporter. Texture scale và offset vẫn nằm trên material.",
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
