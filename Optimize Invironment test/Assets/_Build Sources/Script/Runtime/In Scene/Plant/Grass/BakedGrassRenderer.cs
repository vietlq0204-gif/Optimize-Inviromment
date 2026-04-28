using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Grass/Baked Grass Renderer")]
public sealed class BakedGrassRenderer : MonoBehaviour
{
    private const string DefaultBakedFolderName = "Baked";
    private const string FallbackBakedAssetFolder = "Assets/Grass/Baked";

    private enum ShadowCastingOverride
    {
        UseBakedData = 0,
        Off = 1,
    }

    private enum ReceiveShadowsOverride
    {
        UseBakedData = 0,
        Off = 1,
    }

    [Header("Runtime")]
    [SerializeField] private BakedGrassData bakedData;
    [SerializeField] private bool renderOnlyInWebGL = true;
    [SerializeField] private bool renderInEditor = false;
    [SerializeField] private bool useMainCameraFrustumCulling = true;

    [Header("Shadows")]
    [SerializeField] private ShadowCastingOverride shadowCasting = ShadowCastingOverride.UseBakedData;
    [SerializeField] private ReceiveShadowsOverride receiveShadows = ReceiveShadowsOverride.UseBakedData;

    [Header("Bake Source")]
    [SerializeField] private Terrain[] sourceTerrains = Array.Empty<Terrain>();
    [SerializeField] private bool useActiveTerrainsWhenSourceTerrainsEmpty = true;
    [SerializeField, Min(0f)] private float densityScale = 1f;
    [SerializeField] private string bakedAssetFolder;
    [SerializeField] private string bakedAssetName = "SampleScene_BakedGrassData";

    private readonly Plane[] frustumPlanes = new Plane[6];

    public BakedGrassData BakedData => bakedData;

    private void Reset()
    {
#if UNITY_EDITOR
        bakedAssetFolder = NormalizeBakedAssetFolder(bakedAssetFolder);
#endif
        if (TryGetComponent(out Terrain terrain))
        {
            sourceTerrains = new[] { terrain };
        }
    }

    private void OnValidate()
    {
        densityScale = Mathf.Max(0f, densityScale);

#if UNITY_EDITOR
        bakedAssetFolder = NormalizeBakedAssetFolder(bakedAssetFolder);
#endif
        if (string.IsNullOrWhiteSpace(bakedAssetFolder))
        {
            bakedAssetFolder = FallbackBakedAssetFolder;
        }

        if (string.IsNullOrWhiteSpace(bakedAssetName))
        {
            bakedAssetName = "SampleScene_BakedGrassData";
        }
    }

    private void LateUpdate()
    {
        if (!ShouldRender())
        {
            return;
        }

        Camera cullingCamera = useMainCameraFrustumCulling ? Camera.main : null;
        bool hasCullingCamera = cullingCamera != null;
        if (hasCullingCamera)
        {
            GeometryUtility.CalculateFrustumPlanes(cullingCamera, frustumPlanes);
        }

        bool supportsInstancing = SystemInfo.supportsInstancing;
        var batches = bakedData.Batches;
        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            BakedGrassData.Batch batch = batches[batchIndex];
            if (batch == null || batch.Mesh == null || batch.Material == null)
            {
                continue;
            }

            bool useInstancing = supportsInstancing && batch.Material.enableInstancing;
            ShadowCastingMode shadowCastingMode = ResolveShadowCastingMode(batch);
            bool shouldReceiveShadows = ResolveReceiveShadows(batch);
            var chunks = batch.Chunks;

            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                BakedGrassData.Chunk chunk = chunks[chunkIndex];
                if (chunk == null || chunk.Matrices == null || chunk.Count == 0)
                {
                    continue;
                }

                if (hasCullingCamera && !GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.Bounds))
                {
                    continue;
                }

                if (useInstancing)
                {
                    Graphics.DrawMeshInstanced(
                        batch.Mesh,
                        batch.SubMeshIndex,
                        batch.Material,
                        chunk.Matrices,
                        chunk.Count,
                        null,
                        shadowCastingMode,
                        shouldReceiveShadows,
                        batch.Layer);

                    continue;
                }

                for (int instanceIndex = 0; instanceIndex < chunk.Count; instanceIndex++)
                {
                    Graphics.DrawMesh(
                        batch.Mesh,
                        chunk.Matrices[instanceIndex],
                        batch.Material,
                        batch.Layer,
                        null,
                        batch.SubMeshIndex,
                        null,
                        shadowCastingMode != ShadowCastingMode.Off,
                        shouldReceiveShadows,
                        false);
                }
            }
        }
    }

    private bool ShouldRender()
    {
        if (bakedData == null || bakedData.IsEmpty)
        {
            return false;
        }

        if (!Application.isPlaying)
        {
            return renderInEditor;
        }

        if (renderOnlyInWebGL && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            return false;
        }

        return true;
    }

    private ShadowCastingMode ResolveShadowCastingMode(BakedGrassData.Batch batch)
    {
        return shadowCasting == ShadowCastingOverride.Off
            ? ShadowCastingMode.Off
            : batch.ShadowCastingMode;
    }

    private bool ResolveReceiveShadows(BakedGrassData.Batch batch)
    {
        return receiveShadows == ReceiveShadowsOverride.Off
            ? false
            : batch.ReceiveShadows;
    }

#if UNITY_EDITOR
    public static string NormalizeBakedAssetFolder(string folderPath)
    {
        string normalizedPath = NormalizeAssetPath(folderPath);
        string defaultFolder = GetDefaultBakedAssetFolder();
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return defaultFolder;
        }

        if (!AssetDatabase.IsValidFolder(normalizedPath) &&
            string.Equals(Path.GetFileName(normalizedPath), DefaultBakedFolderName, StringComparison.OrdinalIgnoreCase) &&
            AssetDatabase.IsValidFolder(defaultFolder))
        {
            return defaultFolder;
        }

        return normalizedPath;
    }

    internal static string GetDefaultBakedAssetFolder()
    {
        string scriptPath = FindAssetPath("BakedGrassRenderer.cs", "t:MonoScript");
        if (string.IsNullOrEmpty(scriptPath))
        {
            return FallbackBakedAssetFolder;
        }

        string runtimeFolder = NormalizeAssetPath(Path.GetDirectoryName(scriptPath));
        string scriptFolder = NormalizeAssetPath(Path.GetDirectoryName(runtimeFolder));
        string rootFolder = NormalizeAssetPath(Path.GetDirectoryName(scriptFolder));
        if (string.IsNullOrEmpty(rootFolder))
        {
            return FallbackBakedAssetFolder;
        }

        return NormalizeAssetPath(Path.Combine(rootFolder, DefaultBakedFolderName));
    }

    private static string FindAssetPath(string fileName, string typeFilter)
    {
        string searchName = Path.GetFileNameWithoutExtension(fileName);
        string[] guids = AssetDatabase.FindAssets($"{searchName} {typeFilter}");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.Equals(Path.GetFileName(assetPath), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeAssetPath(assetPath);
            }
        }

        return string.Empty;
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().Replace('\\', '/');
    }

    public Terrain[] SourceTerrains => sourceTerrains;
    public bool UseActiveTerrainsWhenSourceTerrainsEmpty => useActiveTerrainsWhenSourceTerrainsEmpty;
    public float DensityScale => densityScale;
    public string BakedAssetFolder => bakedAssetFolder;
    public string BakedAssetName => bakedAssetName;

    public void SetBakedData(BakedGrassData data)
    {
        bakedData = data;
    }

    public void SetBakedAssetFolder(string folderPath)
    {
        bakedAssetFolder = NormalizeBakedAssetFolder(folderPath);
    }

    public void SetSourceTerrains(Terrain[] terrains)
    {
        sourceTerrains = terrains ?? Array.Empty<Terrain>();
    }
#endif
}
