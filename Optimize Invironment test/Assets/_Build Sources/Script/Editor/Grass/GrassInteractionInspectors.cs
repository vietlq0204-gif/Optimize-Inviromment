using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassInteractionConfig))]
[CanEditMultipleObjects]
public sealed class GrassInteractionConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 190f;

        DrawScriptField(serializedObject);

        EditorGUILayout.HelpBox(
            "Config này là nơi chính để chỉnh hành vi đè cỏ. Nếu gắn config này vào GrassInteractionSystem, phần Material Overrides sẽ ghi đè thông số trên material cỏ.",
            MessageType.Info);

        DrawSection("Ghi vùng tiếp xúc");
        Draw("contactSoftness", "Độ mềm vùng tiếp xúc", "Độ mềm mép vùng đè. Thấp hơn tạo mép sắc hơn, cao hơn làm vùng đè loang mềm hơn.");
        Draw("contactDirectionalInfluence", "Hướng tiếp xúc", "Mức ảnh hưởng của hướng di chuyển lên hướng cỏ bị đẩy. Chỉ thấy rõ khi Đẩy ngang lớn hơn 0 và object đang di chuyển.");
        Draw("contactRecoveryWeight", "Hồi phục tiếp xúc", "Trọng số hồi phục của vùng tiếp xúc. Giảm về 0 để giảm hiệu ứng cỏ bật lại sau khi bị đè.");

        DrawSection("Ghi vệt đi qua");
        Draw("trailSoftness", "Độ mềm vệt", "Độ mềm mép vệt cỏ phía sau khi object di chuyển.");
        Draw("trailDirectionalInfluence", "Hướng vệt", "Mức ảnh hưởng của hướng di chuyển lên vệt cỏ bị kéo.");
        Draw("trailRecoveryWeight", "Hồi phục vệt", "Trọng số hồi phục của vệt đi qua. Giảm về 0 để giảm hiệu ứng cỏ bật lại trong vệt.");

        DrawSection("Chuyển động");
        Draw("minimumDirectionalSpeed", "Tốc độ tối thiểu", "Tốc độ phẳng tối thiểu để hệ thống xem object là đang di chuyển và tạo vệt trail.");

        DrawSection("Source / Interactor");
        Draw("heightOffset", "Lệch độ cao", "Độ lệch độ cao của điểm ghi interaction so với vị trí object.");
        Draw("contactRadius", "Bán kính tiếp xúc", "Bán kính vùng cỏ bị đè trực tiếp quanh object.");
        Draw("contactStrength", "Lực tiếp xúc", "Cường độ vùng đè trực tiếp. Đặt 0 để tắt vùng contact.");
        Draw("trailRadius", "Bán kính vệt", "Bán kính vệt cỏ phía sau khi object di chuyển.");
        Draw("trailStrength", "Lực vệt", "Cường độ vệt cỏ. Đặt 0 để tắt trail.");
        Draw("minimumTrailDistance", "Khoảng tạo vệt tối thiểu", "Quãng đường tối thiểu giữa hai frame để tạo vệt trail.");
        Draw("emitWhileStationary", "Ghi khi đứng yên", "Vẫn ghi vùng contact khi object đứng yên trên cỏ.");
        Draw("suppressRecoveryWhileStationary", "Chặn hồi khi đứng yên", "Khi object đứng yên trên cỏ, ngăn cỏ bật lại dưới chân.");

        DrawSection("Material / Shader");
        Draw("overrideMaterialInteraction", "Ghi đè material", "Bật để GrassInteractionSystem đưa các thông số Material bên dưới vào shader bằng global values.");
        Draw("enableInteraction", "Bật interaction", "Bật hoặc tắt toàn bộ phản ứng interaction của shader cỏ.");
        Draw("interactionStrength", "Cường độ tổng", "Cường độ tổng của phản ứng cỏ trong shader.");
        Draw("interactionPushAway", "Đẩy ngang", "Độ cỏ bị đẩy ngang ra khỏi tâm hoặc hướng tác động.");
        Draw("interactionFlatten", "Ép xuống", "Độ cỏ bị ép xuống theo chiều dọc.");
        Draw("interactionRadiusMultiplier", "Hệ số bán kính", "Hệ số thay đổi bán kính phản ứng trong shader.");
        Draw("interactionVerticalRange", "Vùng cao nhận tác động", "Khoảng chiều cao quanh mặt đất được phép nhận interaction.");
        Draw("interactionTrail", "Độ giữ vệt", "Mức giữ vệt trong shader. Tăng để vệt đi qua ảnh hưởng lâu hoặc mềm hơn.");
        Draw("interactionRecoveryStrength", "Lực bật lại", "Biên độ rung hoặc bật lại khi cỏ hồi phục. Đặt 0 nếu không muốn hiệu ứng bật lại.");
        Draw("interactionRecoveryFrequency", "Tần số bật lại", "Tần số rung hoặc bật lại khi cỏ hồi phục.");
        Draw("interactionRecoveryNoiseScale", "Nhiễu pha hồi phục", "Tỉ lệ noise làm lệch pha hồi phục giữa các cụm cỏ.");

        EditorGUILayout.HelpBox(
            "Nếu muốn vệt cỏ bị đè hồi chậm hoặc gần như không hồi, chỉnh GrassInteractionSystem > Thời gian giữ vệt. Các thông số Recovery ở đây chủ yếu điều khiển hiệu ứng rung hoặc bật lại.",
            MessageType.None);

        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.labelWidth = previousLabelWidth;
    }

    private void Draw(string propertyName, string label, string tooltip)
    {
        DrawProperty(serializedObject, propertyName, label, tooltip);
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    internal static void DrawScriptField(SerializedObject serializedObject)
    {
        SerializedProperty script = serializedObject.FindProperty("m_Script");
        if (script == null)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(script);
        }
    }

    internal static void DrawProperty(SerializedObject serializedObject, string propertyName, string label, string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }
}

[CustomEditor(typeof(EnvironmentInteractor), true)]
[CanEditMultipleObjects]
public sealed class EnvironmentInteractorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 190f;

        GrassInteractionConfigEditor.DrawScriptField(serializedObject);

        SerializedProperty config = serializedObject.FindProperty("interactionConfig");
        bool hasLocalConfig = config != null && !config.hasMultipleDifferentValues && config.objectReferenceValue != null;

        DrawSection("Config");
        GrassInteractionConfigEditor.DrawProperty(
            serializedObject,
            "interactionConfig",
            "Config riêng",
            "Config riêng cho source này. Nếu có giá trị, các field fallback bên dưới như bán kính hoặc lực sẽ bị override bởi config.");

        if (hasLocalConfig)
        {
            EditorGUILayout.HelpBox(
                "Source này đang dùng GrassInteractionConfig. Các field fallback về bán kính, lực và hành vi bên dưới được khóa vì thay đổi chúng sẽ không có tác dụng.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Nếu GrassInteractionSystem có Config chung, source này sẽ dùng config chung khi chạy. Khi đó các field fallback về bán kính hoặc lực có thể không còn tác dụng.",
                MessageType.None);
        }

        DrawSection("Đối tượng nhận");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "targets", "Hệ nhận tác động", "Hệ thống sẽ nhận interaction này. Với cỏ, giữ Vegetation.");

        DrawSection("Vùng tiếp xúc");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "emitContactShape", "Bật vùng tiếp xúc", "Bật hoặc tắt vùng đè trực tiếp quanh object.");
        using (new EditorGUI.DisabledScope(hasLocalConfig))
        {
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "heightOffset", "Lệch độ cao", "Fallback khi không có config. Độ lệch điểm ghi interaction so với vị trí object.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "contactRadius", "Bán kính tiếp xúc", "Fallback khi không có config. Bán kính vùng cỏ bị đè trực tiếp.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "contactStrength", "Lực tiếp xúc", "Fallback khi không có config. Cường độ vùng đè trực tiếp.");
        }

        DrawSection("Vệt di chuyển");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "emitTrailShape", "Bật vệt di chuyển", "Bật hoặc tắt vệt cỏ khi object di chuyển.");
        using (new EditorGUI.DisabledScope(hasLocalConfig))
        {
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "trailRadius", "Bán kính vệt", "Fallback khi không có config. Bán kính vệt cỏ phía sau object.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "trailStrength", "Lực vệt", "Fallback khi không có config. Cường độ vệt cỏ.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "minimumTrailDistance", "Khoảng tạo vệt tối thiểu", "Fallback khi không có config. Quãng đường tối thiểu để vẽ trail.");
        }

        DrawSection("Hành vi");
        using (new EditorGUI.DisabledScope(hasLocalConfig))
        {
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "emitWhileStationary", "Ghi khi đứng yên", "Fallback khi không có config. Vẫn ghi contact khi object đứng yên.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "suppressRecoveryWhileStationary", "Chặn hồi khi đứng yên", "Fallback khi không có config. Ngăn cỏ hồi lại khi object đứng trên cỏ.");
        }

        DrawSection("Debug");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugShapes", "Vẽ vùng debug", "Vẽ gizmo contact hoặc trail trong Scene view.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugOnlyWhenSelected", "Chỉ vẽ khi chọn", "Chỉ hiện gizmo khi object được chọn.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugVelocity", "Vẽ hướng di chuyển", "Vẽ mũi tên hướng hoặc tốc độ di chuyển.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugLabels", "Hiện nhãn debug", "Hiện label thông số debug khi chọn object.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugContactColor", "Màu contact", "Màu gizmo vùng contact.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugTrailColor", "Màu trail", "Màu gizmo vùng trail.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugVelocityColor", "Màu vận tốc", "Màu gizmo hướng vận tốc.");

        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.labelWidth = previousLabelWidth;
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
}

[CustomEditor(typeof(EnvironmentInteractionSystem), true)]
[CanEditMultipleObjects]
public sealed class EnvironmentInteractionSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 190f;

        GrassInteractionConfigEditor.DrawScriptField(serializedObject);

        DrawSection("Theo dõi vùng cỏ");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "followTarget", "Đối tượng theo dõi", "Transform mà vùng interaction sẽ đi theo. Thường là Player.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "worldOffset", "Lệch vùng ghi", "Độ lệch vị trí vùng interaction so với đối tượng theo dõi.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "followSceneViewInEditMode", "Theo Scene View khi edit", "Trong Edit Mode, vùng interaction đi theo Scene View camera nếu có.");

        DrawSection("Render interaction map");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "orthographicSize", "Kích thước vùng", "Nửa kích thước vùng interaction theo world unit. Vùng đầy đủ có cạnh bằng giá trị này x 2.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "globalStrength", "Cường độ global", "Cường độ tổng khi shader đọc interaction map.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "resolution", "Độ phân giải", "Độ phân giải texture interaction. Cao hơn mịn hơn nhưng tốn GPU hơn.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "clearColor", "Màu trung lập", "Màu trạng thái trung lập của interaction map. Thường giữ mặc định 0.5, 0.5, 0, 0.");

        DrawSection("Lịch sử vệt cỏ");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "historyBlendSeconds", "Thời gian giữ vệt", "Thời gian blend hoặc history của vệt cỏ bị đè. Tăng giá trị để cỏ hồi chậm hơn; đặt rất lớn để gần như không hồi khi test.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "accumulationShader", "Shader cộng dồn", "Shader dùng để cộng dồn interaction map qua thời gian.");

        DrawSection("Ghi shape");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "batchStampShader", "Shader ghi shape", "Shader dùng để ghi các shape contact hoặc trail vào interaction map.");

        DrawSection("Config chung");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "interactionConfig", "Config chung", "Config cỏ dùng chung cho toàn bộ interaction system. Source không có config riêng sẽ dùng config này; Material Overrides cũng được đẩy vào shader từ đây.");

        DrawSection("Debug");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugRegion", "Vẽ vùng debug", "Vẽ vùng interaction trong Scene view.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugOnlyWhenSelected", "Chỉ vẽ khi chọn", "Chỉ vẽ debug khi chọn object.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugLabels", "Hiện nhãn debug", "Hiện label kích thước, độ phân giải và số shape.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugCross", "Vẽ dấu cộng", "Vẽ đường chữ thập ở tâm vùng interaction.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugRegionColor", "Màu vùng debug", "Màu gizmo của vùng interaction.");

        DrawSection("Legacy không còn dùng");
        EditorGUILayout.HelpBox(
            "Hai field bên dưới thuộc path render camera cũ. Backend hiện tại ghi shape trực tiếp vào texture nên thay đổi chúng không ảnh hưởng đến cỏ.",
            MessageType.Warning);
        using (new EditorGUI.DisabledScope(true))
        {
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "cullingMask", "Layer render cũ", "Không còn tác dụng trong backend shape batching hiện tại.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "hideInteractionLayerFromGameCameras", "Ẩn layer render cũ", "Không còn tác dụng trong backend shape batching hiện tại.");
        }

        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.labelWidth = previousLabelWidth;
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
}
