#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SunShaftsInstaller
{
    [MenuItem("Tools/Environment/Install Sun Shafts On Active URP", priority = 2000)]
    private static void InstallOnActiveUrp()
    {
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            Debug.LogError("Current Render Pipeline is not a Universal Render Pipeline asset.");
            return;
        }

        if (!TryGetDefaultRendererData(urpAsset, out ScriptableRendererData rendererData))
        {
            Debug.LogError($"URP asset '{urpAsset.name}' has no default renderer data.");
            return;
        }

        SunShaftsRendererFeature feature = EnsureRendererFeature(rendererData);
        VolumeProfile volumeProfile = EnsureVolumeProfile(urpAsset);
        if (volumeProfile == null)
        {
            return;
        }

        SunShaftsVolume volume = EnsureVolumeComponent(volumeProfile);

        rendererData.SetDirty();
        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(urpAsset);
        EditorUtility.SetDirty(volumeProfile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = feature != null ? feature : rendererData;
        Debug.Log($"Sun Shafts installed on renderer '{rendererData.name}' and volume profile '{volumeProfile.name}'. Volume component active: {volume != null}.");
    }

    [MenuItem("Tools/Environment/Install Sun Shafts On Active URP", true)]
    private static bool ValidateInstallOnActiveUrp()
    {
        return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
    }

    private static SunShaftsRendererFeature EnsureRendererFeature(ScriptableRendererData rendererData)
    {
        if (rendererData.TryGetRendererFeature<SunShaftsRendererFeature>(out SunShaftsRendererFeature existingFeature))
        {
            return existingFeature;
        }

        SunShaftsRendererFeature feature = ScriptableObject.CreateInstance<SunShaftsRendererFeature>();
        feature.name = nameof(SunShaftsRendererFeature);
        Undo.RegisterCreatedObjectUndo(feature, "Add Sun Shafts Renderer Feature");

        if (EditorUtility.IsPersistent(rendererData))
        {
            AssetDatabase.AddObjectToAsset(feature, rendererData);
        }

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        SerializedObject serializedRendererData = new SerializedObject(rendererData);
        SerializedProperty rendererFeatures = serializedRendererData.FindProperty("m_RendererFeatures");
        SerializedProperty rendererFeatureMap = serializedRendererData.FindProperty("m_RendererFeatureMap");

        rendererFeatures.arraySize++;
        rendererFeatures.GetArrayElementAtIndex(rendererFeatures.arraySize - 1).objectReferenceValue = feature;

        rendererFeatureMap.arraySize++;
        rendererFeatureMap.GetArrayElementAtIndex(rendererFeatureMap.arraySize - 1).longValue = localId;

        serializedRendererData.ApplyModifiedProperties();

        feature.Create();
        return feature;
    }

    private static bool TryGetDefaultRendererData(UniversalRenderPipelineAsset urpAsset, out ScriptableRendererData rendererData)
    {
        rendererData = null;
        if (urpAsset == null)
        {
            return false;
        }

        SerializedObject serializedUrpAsset = new SerializedObject(urpAsset);
        SerializedProperty rendererDataList = serializedUrpAsset.FindProperty("m_RendererDataList");
        SerializedProperty defaultRendererIndex = serializedUrpAsset.FindProperty("m_DefaultRendererIndex");

        if (rendererDataList == null || !rendererDataList.isArray || rendererDataList.arraySize == 0)
        {
            return false;
        }

        int index = 0;
        if (defaultRendererIndex != null)
        {
            index = Mathf.Clamp(defaultRendererIndex.intValue, 0, rendererDataList.arraySize - 1);
        }

        rendererData = rendererDataList.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
        if (rendererData != null)
        {
            return true;
        }

        for (int i = 0; i < rendererDataList.arraySize; i++)
        {
            rendererData = rendererDataList.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (rendererData != null)
            {
                return true;
            }
        }

        return false;
    }

    private static VolumeProfile EnsureVolumeProfile(UniversalRenderPipelineAsset urpAsset)
    {
        URPDefaultVolumeProfileSettings volumeSettings = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>();
        if (volumeSettings == null)
        {
            Debug.LogError("URP default volume profile settings are not available in Graphics Settings.");
            return null;
        }

        if (volumeSettings.volumeProfile != null)
        {
            return volumeSettings.volumeProfile;
        }

        string urpPath = AssetDatabase.GetAssetPath(urpAsset);
        string directory = Path.GetDirectoryName(urpPath);
        string profilePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory ?? "Assets", $"{urpAsset.name}_SunShaftsProfile.asset"));

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        volumeSettings.volumeProfile = profile;

        RenderPipelineGlobalSettings globalSettings = GraphicsSettings.GetSettingsForRenderPipeline(typeof(UniversalRenderPipeline));
        if (globalSettings != null)
        {
            EditorUtility.SetDirty(globalSettings);
        }

        return profile;
    }

    private static SunShaftsVolume EnsureVolumeComponent(VolumeProfile profile)
    {
        if (profile.TryGet(out SunShaftsVolume existingVolume))
        {
            existingVolume.active = true;
            return existingVolume;
        }

        SunShaftsVolume volume = ScriptableObject.CreateInstance<SunShaftsVolume>();
        volume.name = nameof(SunShaftsVolume);
        volume.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        volume.SetAllOverridesTo(true);

        Undo.RegisterCreatedObjectUndo(volume, "Add Sun Shafts Volume Override");
        if (EditorUtility.IsPersistent(profile))
        {
            AssetDatabase.AddObjectToAsset(volume, profile);
        }

        profile.components.Add(volume);
        profile.Reset();
        volume.active = true;
        if (volume.intensity.value <= 0f)
        {
            volume.intensity.value = 0.85f;
        }

        return volume;
    }
}
#endif
