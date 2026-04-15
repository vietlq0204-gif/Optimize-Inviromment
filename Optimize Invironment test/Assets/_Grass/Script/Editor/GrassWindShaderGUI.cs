using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom material inspector for the grass wind shader.
/// </summary>
public sealed class GrassWindShaderGUI : ShaderGUI
{
    private static bool s_ShowCommon = true;
    private static bool s_ShowWindShape = true;
    private static bool s_ShowWindTexture = true;
    private static bool s_ShowColor = false;
    private static bool s_ShowTerrain = false;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty baseMap = Find("_BaseMap", properties);
        MaterialProperty baseColor = Find("_BaseColor", properties);
        MaterialProperty cutoff = Find("_Cutoff", properties);

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

        DrawCommon(materialEditor, ref s_ShowCommon, baseMap, baseColor, cutoff, windTexture, windSpeed, windDirection);
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
        DrawTerrain(materialEditor, ref s_ShowTerrain, useTerrainColor, terrainColor);
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
                MakeLabel("Base Map", "Texture màu gốc của cỏ. Alpha của texture quyết định vùng nào bị cắt bởi Alpha Cutoff."),
                baseMap);
            materialEditor.ShaderProperty(
                baseColor,
                MakeLabel("Base Color", "Màu nhân thêm lên Base Map. Có thể dùng để chỉnh tông tổng thể của cỏ."));
            materialEditor.TexturePropertySingleLine(
                MakeLabel("Wind Texture", "Texture mô tả hình dạng trường gió. Vùng sáng/tối trong texture quyết định chỗ nào cỏ ngả mạnh hay nhẹ."),
                windTexture);
            materialEditor.ShaderProperty(
                cutoff,
                MakeLabel("Alpha Cutoff", "Ngưỡng cắt alpha. Tăng giá trị này thì lá cỏ sẽ mỏng hơn vì nhiều pixel bị loại bỏ hơn."));
            materialEditor.ShaderProperty(
                windSpeed,
                MakeLabel("Grass Lean", "Độ ngả nền của cỏ theo hướng gió. Giá trị này không làm cỏ tự dao động, chỉ quyết định mức nghiêng tổng thể."));
            DrawNormalizedDirection2D(
                windDirection,
                MakeLabel("Wind Direction (XZ)", "Hướng gió toàn cục trên mặt phẳng XZ. Tất cả bụi cỏ sẽ cùng đổ theo hướng này."));
            EditorGUILayout.HelpBox(
                "Grass Lean chỉ điều khiển độ ngả nền. Cỏ chỉ dao động khi bạn bật Wave Shape hoặc cho Wind Texture chạy bằng Scroll Speed.",
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
                MakeLabel("Enable Wave Motion", "Bật hoặc tắt lớp dao động dạng sóng. Tắt đi thì chỉ còn độ ngả nền và ảnh hưởng từ Wind Texture."));
            EditorGUI.BeginDisabledGroup(enableWaveShape.floatValue < 0.5f);
            materialEditor.ShaderProperty(
                waveFrequency,
                MakeLabel("Wave Frequency", "Mật độ sóng theo chiều gió. Giá trị cao tạo nhiều gợn sóng hơn trên cùng một khoảng cách."));
            materialEditor.ShaderProperty(
                waveSpacingVariation,
                MakeLabel("Wave Spacing Variation", "Độ lệch không đều giữa các bước sóng. Tăng lên để các dải sóng tách ra dày thưa khác nhau thay vì lặp quá đều."));
            materialEditor.ShaderProperty(
                waveSpeed,
                MakeLabel("Wave Speed", "Tốc độ di chuyển của sóng dọc theo hướng gió."));
            materialEditor.ShaderProperty(
                waveStrength,
                MakeLabel("Wave Strength", "Cường độ tổng của lớp sóng. Tăng lên để dao động thấy rõ hơn."));
            materialEditor.ShaderProperty(
                waveBodyInfluence,
                MakeLabel("Body Wave", "Mức ảnh hưởng của sóng lên phần thân cỏ. Tăng lên thì cả thân cùng lượn nhiều hơn."));
            materialEditor.ShaderProperty(
                waveTipInfluence,
                MakeLabel("Tip Wave", "Mức ảnh hưởng của sóng lên đầu ngọn cỏ. Tăng lên thì phần ngọn rung/lượn rõ hơn phần gốc."));
            materialEditor.ShaderProperty(
                waveLateralInfluence,
                MakeLabel("Lateral Wave", "Lượng lệch ngang của sóng sang hai bên, giúp chuyển động bớt cứng và đỡ một chiều."));
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
                MakeLabel("Texture Scale", "Tỉ lệ world-space của Wind Texture. Giá trị lớn làm pattern rộng hơn, giá trị nhỏ làm pattern dày hơn."));
            materialEditor.ShaderProperty(
                windTextureScrollSpeed,
                MakeLabel("Texture Scroll Speed", "Tốc độ trôi của trường gió theo hướng gió. Dùng để tạo cảm giác các dải gió đang quét qua đồng cỏ."));
            materialEditor.ShaderProperty(
                windTextureContrast,
                MakeLabel("Texture Contrast", "Khoảng remap sáng/tối của Wind Texture. Siết khoảng này để tăng độ phân biệt giữa vùng gió mạnh và nhẹ."));
            materialEditor.ShaderProperty(
                windTextureInfluence,
                MakeLabel("Lean Influence", "Mức độ Wind Texture ảnh hưởng tới độ ngả nền. 0 là bỏ qua texture, 1 là dùng texture đầy đủ."));
            materialEditor.ShaderProperty(
                windTextureWaveInfluence,
                MakeLabel("Wave Influence", "Mức độ Wind Texture tham gia điều chế lớp sóng. Tăng lên để sóng đi theo pattern của texture nhiều hơn."));
            EditorGUILayout.HelpBox(
                "Wind Texture bây giờ là trường gió chính. Wind Direction xoay hệ sample của texture, còn Scroll Speed làm pattern gió di chuyển xuyên qua thảm cỏ.",
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
                MakeLabel("Near Color", "Màu áp cho cỏ ở gần camera."));
            materialEditor.ShaderProperty(
                farColor,
                MakeLabel("Far Color", "Màu áp cho cỏ ở xa camera. Dùng để giảm chói hoặc đồng nhất màu ở xa."));
            materialEditor.ShaderProperty(
                nearFarRange,
                MakeLabel("Near/Far Range", "Khoảng cách bắt đầu và kết thúc việc chuyển màu từ Near Color sang Far Color."));
            materialEditor.ShaderProperty(
                bottomColor,
                MakeLabel("Bottom Tint", "Màu nhuộm ở phần gốc cỏ."));
            materialEditor.ShaderProperty(
                heightBlend,
                MakeLabel("Height Blend", "Độ chuyển màu từ gốc lên ngọn. Giá trị lớn làm màu gốc chuyển sang màu ngọn nhanh hơn."));
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawTerrain(
        MaterialEditor materialEditor,
        ref bool foldout,
        MaterialProperty useTerrainColor,
        MaterialProperty terrainColor)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, "Terrain");
        if (foldout)
        {
            materialEditor.ShaderProperty(
                useTerrainColor,
                MakeLabel("Use Terrain Color", "Bật để nhân thêm màu terrain lên cỏ, giúp cỏ hòa màu với nền đất."));
            if (useTerrainColor.floatValue > 0.5f)
            {
                materialEditor.ShaderProperty(
                    terrainColor,
                    MakeLabel("Terrain Color", "Màu terrain được nhân thêm lên cỏ khi Use Terrain Color đang bật."));
            }

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
            EditorGUILayout.HelpBox("Hãy chọn đúng một material nếu muốn ghi các giá trị hiện tại về mặc định của shader.", MessageType.None);
            return;
        }

        if (!canBake)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "Công cụ này sẽ ghi các giá trị float/color/vector hiện tại vào file .shader làm mặc định, đồng thời đồng bộ texture mặc định qua ShaderImporter. Scale/offset của texture vẫn chỉ nằm ở material.",
            MessageType.None);
    }

    private static void DrawNormalizedDirection2D(MaterialProperty property, GUIContent label)
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

    private static GUIContent MakeLabel(string text, string tooltip)
    {
        return new GUIContent(text, tooltip);
    }
}
