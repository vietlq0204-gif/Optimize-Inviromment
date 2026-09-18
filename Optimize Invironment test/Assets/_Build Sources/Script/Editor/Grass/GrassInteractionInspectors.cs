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
            "Config nay dieu khien tuong tac co. Neu gan vao GrassInteractionSystem, phan Material Overrides se ghi de thong so tren material co bang global shader values.",
            MessageType.Info);

        DrawSection("Contact Writer");
        Draw("contactSoftness", "Contact Softness", "Do mem mep vung de.");
        Draw("contactDirectionalInfluence", "Contact Direction", "Muc anh huong cua huong di chuyen len vung contact.");
        Draw("contactRecoveryWeight", "Contact Recovery", "Trong so phuc hoi cua vung contact.");

        DrawSection("Trail Writer");
        Draw("trailSoftness", "Trail Softness", "Do mem mep vet co.");
        Draw("trailDirectionalInfluence", "Trail Direction", "Muc anh huong cua huong di chuyen len vet co.");
        Draw("trailRecoveryWeight", "Trail Recovery", "Trong so phuc hoi cua vet co.");

        DrawSection("Motion");
        Draw("minimumDirectionalSpeed", "Minimum Direction Speed", "Toc do phang toi thieu de xem object dang di chuyen.");

        DrawSection("Source / Interactor");
        Draw("heightOffset", "Lech do cao", "Do lech diem ghi interaction so voi vi tri object.");
        Draw("contactRadius", "Ban kinh tiep xuc", "Ban kinh vung co bi de truc tiep quanh object.");
        Draw("contactStrength", "Luc tiep xuc", "Cuong do vung de truc tiep.");
        Draw("trailRadius", "Ban kinh vet", "Ban kinh vet co phia sau khi object di chuyen.");
        Draw("trailStrength", "Luc vet", "Cuong do vet co.");
        Draw("minimumTrailDistance", "Khoang tao vet toi thieu", "Quang duong toi thieu giua hai frame de tao trail.");
        Draw("emitWhileStationary", "Ghi khi dung yen", "Van ghi contact khi object dung yen tren co.");
        Draw("suppressRecoveryWhileStationary", "Chan hoi khi dung yen", "Ngan co hoi lai khi object dung yen tren co.");

        DrawSection("Material / Shader");
        Draw("overrideMaterialInteraction", "Ghi de material", "Cho phep system day config nay vao shader bang global values.");
        Draw("enableInteraction", "Bat interaction", "Bat hoac tat phan ung interaction cua shader co.");
        Draw("interactionStrength", "Cuong do tong", "Cuong do tong cua phan ung co trong shader.");
        Draw("interactionPushAway", "Day ngang", "Do co bi day ngang ra khoi tam hoac huong tac dong.");
        Draw("interactionFlatten", "Ep xuong", "Do co bi ep xuong theo chieu doc.");
        Draw("interactionRadiusMultiplier", "He so ban kinh", "He so thay doi ban kinh phan ung trong shader.");
        Draw("interactionVerticalRange", "Vung cao nhan tac dong", "Khoang chieu cao quanh mat dat duoc nhan interaction.");
        Draw("interactionTrail", "Do giu vet", "Muc giu vet trong shader.");
        Draw("interactionRecoveryStrength", "Luc bat lai", "Bien do rung hoac bat lai khi co hoi phuc.");
        Draw("interactionRecoveryFrequency", "Tan so bat lai", "Tan so rung hoac bat lai khi co hoi phuc.");
        Draw("interactionRecoveryNoiseScale", "Nhieu pha hoi phuc", "Ti le noise lam lech pha hoi phuc giua cac cum co.");

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

        SerializedProperty configProperty = serializedObject.FindProperty("interactionConfig");
        bool hasLocalConfig = configProperty != null &&
                              !configProperty.hasMultipleDifferentValues &&
                              configProperty.objectReferenceValue != null;
        GrassInteractionConfig localConfig = hasLocalConfig
            ? configProperty.objectReferenceValue as GrassInteractionConfig
            : null;
        SerializedObject valueObject = localConfig != null ? new SerializedObject(localConfig) : serializedObject;
        if (valueObject != serializedObject)
        {
            valueObject.Update();
        }

        using (new EditorGUI.DisabledScope(localConfig == null))
        {
            if (GUILayout.Button("Luu Config dang gan"))
            {
                SaveAttachedConfigs();
            }
        }

        DrawSection("Config");
        GrassInteractionConfigEditor.DrawProperty(
            serializedObject,
            "interactionConfig",
            "Config rieng",
            "Config rieng cho source nay. Neu co config, cac gia tri ben duoi se sua truc tiep vao SO do.");

        EditorGUILayout.HelpBox(
            localConfig != null
                ? "Dang sua truc tiep height/radius/strength trong GrassInteractionConfig dang gan."
                : "Chua co GrassInteractionConfig rieng, cac gia tri ben duoi duoc luu tren component.",
            MessageType.None);

        DrawSection("Target");
        GrassInteractionConfigEditor.DrawProperty(
            serializedObject,
            "targets",
            "He nhan tac dong",
            "He thong se nhan interaction nay. Voi co, giu Vegetation.");

        DrawSection("Contact Shape");
        GrassInteractionConfigEditor.DrawProperty(
            serializedObject,
            "emitContactShape",
            "Bat vung tiep xuc",
            "Bat hoac tat vung de truc tiep quanh object.");

        EditorGUI.BeginChangeCheck();
        GrassInteractionConfigEditor.DrawProperty(valueObject, "heightOffset", "Lech do cao", "Do lech diem ghi interaction so voi vi tri object.");
        GrassInteractionConfigEditor.DrawProperty(valueObject, "contactRadius", "Ban kinh tiep xuc", "Ban kinh vung co bi de truc tiep.");
        GrassInteractionConfigEditor.DrawProperty(valueObject, "contactStrength", "Luc tiep xuc", "Cuong do vung de truc tiep.");
        bool configValuesChanged = EditorGUI.EndChangeCheck();

        DrawSection("Trail Shape");
        GrassInteractionConfigEditor.DrawProperty(
            serializedObject,
            "emitTrailShape",
            "Bat vet di chuyen",
            "Bat hoac tat vet co khi object di chuyen.");

        EditorGUI.BeginChangeCheck();
        GrassInteractionConfigEditor.DrawProperty(valueObject, "trailRadius", "Ban kinh vet", "Ban kinh vet co phia sau object.");
        GrassInteractionConfigEditor.DrawProperty(valueObject, "trailStrength", "Luc vet", "Cuong do vet co.");
        GrassInteractionConfigEditor.DrawProperty(valueObject, "minimumTrailDistance", "Khoang tao vet toi thieu", "Quang duong toi thieu de ve trail.");
        configValuesChanged |= EditorGUI.EndChangeCheck();

        DrawSection("Behavior");
        EditorGUI.BeginChangeCheck();
        GrassInteractionConfigEditor.DrawProperty(valueObject, "emitWhileStationary", "Ghi khi dung yen", "Van ghi contact khi object dung yen.");
        GrassInteractionConfigEditor.DrawProperty(valueObject, "suppressRecoveryWhileStationary", "Chan hoi khi dung yen", "Ngan co hoi lai khi object dung tren co.");
        configValuesChanged |= EditorGUI.EndChangeCheck();

        if (valueObject != serializedObject)
        {
            valueObject.ApplyModifiedProperties();
            if (configValuesChanged)
            {
                EditorUtility.SetDirty(localConfig);
            }
        }

        DrawSection("Debug");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugShapes", "Ve vung debug", "Ve gizmo contact hoac trail trong Scene view.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugOnlyWhenSelected", "Chi ve khi chon", "Chi hien gizmo khi object duoc chon.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugVelocity", "Ve huong di chuyen", "Ve mui ten huong hoac toc do di chuyen.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugLabels", "Hien nhan debug", "Hien label thong so debug khi chon object.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugContactColor", "Mau contact", "Mau gizmo vung contact.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugTrailColor", "Mau trail", "Mau gizmo vung trail.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugVelocityColor", "Mau van toc", "Mau gizmo huong van toc.");

        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.labelWidth = previousLabelWidth;
    }

    private void SaveAttachedConfigs()
    {
        int updatedCount = 0;
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget == null)
            {
                continue;
            }

            SerializedObject interactorObject = new SerializedObject(selectedTarget);
            SerializedProperty configProperty = interactorObject.FindProperty("interactionConfig");
            GrassInteractionConfig config = configProperty != null
                ? configProperty.objectReferenceValue as GrassInteractionConfig
                : null;

            if (config == null)
            {
                continue;
            }

            EditorUtility.SetDirty(config);
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
}

[CustomEditor(typeof(GrassInteractionSystem), true)]
[CanEditMultipleObjects]
public sealed class GrassInteractionSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 190f;

        GrassInteractionConfigEditor.DrawScriptField(serializedObject);

        if (target != null && target.GetType().Name == "EnvironmentInteractionSystem")
        {
            EditorGUILayout.HelpBox(
                "EnvironmentInteractionSystem chi con la legacy alias. Hay dung GrassInteractionSystem cho setup moi.",
                MessageType.Warning);
        }

        DrawSection("Tracking");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "followTarget", "Doi tuong theo doi", "Transform ma vung interaction se di theo. Thuong la Player.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "worldOffset", "Lech vung ghi", "Do lech vi tri vung interaction so voi doi tuong theo doi.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "followSceneViewInEditMode", "Theo Scene View khi edit", "Trong Edit Mode, vung interaction di theo Scene View camera neu co.");

        DrawSection("Render Interaction Map");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "orthographicSize", "Kich thuoc vung", "Nua kich thuoc vung interaction theo world unit.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "globalStrength", "Cuong do global", "Cuong do tong khi shader doc interaction map.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "resolution", "Do phan giai", "Do phan giai texture interaction.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "clearColor", "Mau trung lap", "Mau trang thai trung lap cua interaction map.");

        DrawSection("History");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "historyBlendSeconds", "Thoi gian giu vet", "Thoi gian blend/history cua vet co bi de.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "accumulationShader", "Shader cong don", "Shader dung de cong don interaction map theo thoi gian.");

        DrawSection("Shape Writer");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "batchStampShader", "Shader ghi shape", "Shader dung de ghi contact/trail vao interaction map.");

        DrawSection("Shared Config");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "interactionConfig", "Config chung", "Config co dung chung cho toan bo interaction system.");

        DrawSection("Debug");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugRegion", "Ve vung debug", "Ve vung interaction trong Scene view.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugOnlyWhenSelected", "Chi ve khi chon", "Chi ve debug khi chon object.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugLabels", "Hien nhan debug", "Hien label kich thuoc, do phan giai va so shape.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "drawDebugCross", "Ve dau cong", "Ve duong chu thap o tam vung interaction.");
        GrassInteractionConfigEditor.DrawProperty(serializedObject, "debugRegionColor", "Mau vung debug", "Mau gizmo cua vung interaction.");

        DrawSection("Legacy");
        EditorGUILayout.HelpBox(
            "Hai field ben duoi thuoc path render camera cu. Backend hien tai ghi shape truc tiep vao texture nen chung khong con anh huong den co.",
            MessageType.Warning);
        using (new EditorGUI.DisabledScope(true))
        {
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "cullingMask", "Layer render cu", "Khong con tac dung trong backend shape batching hien tai.");
            GrassInteractionConfigEditor.DrawProperty(serializedObject, "hideInteractionLayerFromGameCameras", "An layer render cu", "Khong con tac dung trong backend shape batching hien tai.");
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
