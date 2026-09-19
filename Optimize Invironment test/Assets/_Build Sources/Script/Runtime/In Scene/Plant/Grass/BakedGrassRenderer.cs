using System;
using System.Collections.Generic;
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

    private enum PlatformFilter
    {
        MobileAndWindows = 0,
        All = 1,
        WebGLOnly = 2,
    }

    private enum GrassQualityProfile
    {
        Auto = 0,
        Mobile = 1,
        PC = 2,
        Custom = 3,
    }

    public struct RuntimeStats
    {
        public int TotalChunks;
        public int TotalInstances;
        public int VisibleChunks;
        public int DrawnInstances;
        public int FrustumCulledChunks;
        public int DistanceCulledChunks;
        public int InstancedDrawCalls;
        public int FallbackDrawCalls;
        public int TotalCells;
        public int VisibleCells;
        public int CachedCells;
        public int CellPayloadLoads;
    }

    [Header("Runtime")]
    [SerializeField] private BakedGrassData bakedData;
    [SerializeField] private PlatformFilter platformFilter = PlatformFilter.MobileAndWindows;
    [SerializeField] private bool renderInEditor = false;
    [SerializeField] private bool useMainCameraFrustumCulling = true;

    [Header("Quality")]
    [SerializeField] private GrassQualityProfile qualityProfile = GrassQualityProfile.Auto;
    [SerializeField, Min(0f)] private float mobileMaxRenderDistance = 60f;
    [SerializeField, Min(0f)] private float pcMaxRenderDistance = 120f;
    [SerializeField, Min(0f)] private float customMaxRenderDistance = 60f;
    [SerializeField] private bool qualityProfileControlsShadows = true;
    [SerializeField] private ShadowCastingOverride mobileShadowCasting = ShadowCastingOverride.Off;
    [SerializeField] private ReceiveShadowsOverride mobileReceiveShadows = ReceiveShadowsOverride.Off;
    [SerializeField] private ShadowCastingOverride pcShadowCasting = ShadowCastingOverride.UseBakedData;
    [SerializeField] private ReceiveShadowsOverride pcReceiveShadows = ReceiveShadowsOverride.UseBakedData;

    [Header("Shadows")]
    [SerializeField] private ShadowCastingOverride shadowCasting = ShadowCastingOverride.UseBakedData;
    [SerializeField] private ReceiveShadowsOverride receiveShadows = ReceiveShadowsOverride.UseBakedData;

    [Header("Bake Source")]
    [SerializeField] private Terrain[] sourceTerrains = Array.Empty<Terrain>();
    [SerializeField] private bool useActiveTerrainsWhenSourceTerrainsEmpty = true;
    [SerializeField, Min(0f)] private float densityScale = 1f;
    [SerializeField] private string bakedAssetFolder;
    [SerializeField] private string bakedAssetName = "SampleScene_BakedGrassData";

    [Header("Bake Chunking")]
    [SerializeField, Min(1f)] private float spatialCellSize = 16f;
    [SerializeField, Range(1, 1023)] private int maxInstancesPerChunk = 1023;

    [Header("Streaming")]
    [SerializeField, Min(0)] private int cellCacheRetainFrames = 30;
    [SerializeField, Min(0)] private int maxCachedCells = 256;

    [Header("Debug")]
    [SerializeField] private bool logRuntimeStats;
    [SerializeField, Min(0.25f)] private float runtimeStatsLogInterval = 1f;

    private readonly Plane[] frustumPlanes = new Plane[6];
    private readonly Dictionary<int, LoadedCell> loadedCells = new();
    private readonly HashSet<int> invalidCellPayloads = new();
    private readonly List<int> cacheRemovalBuffer = new();
    private RuntimeStats runtimeStats;
    private float nextRuntimeStatsLogTime;
    private int frameIndex;
    private BakedGrassData cachedData;

    public BakedGrassData BakedData => bakedData;
    public RuntimeStats LastStats => runtimeStats;

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
        mobileMaxRenderDistance = Mathf.Max(0f, mobileMaxRenderDistance);
        pcMaxRenderDistance = Mathf.Max(0f, pcMaxRenderDistance);
        customMaxRenderDistance = Mathf.Max(0f, customMaxRenderDistance);
        spatialCellSize = Mathf.Max(1f, spatialCellSize);
        maxInstancesPerChunk = Mathf.Clamp(maxInstancesPerChunk, 1, 1023);
        cellCacheRetainFrames = Mathf.Max(0, cellCacheRetainFrames);
        maxCachedCells = Mathf.Max(0, maxCachedCells);
        runtimeStatsLogInterval = Mathf.Max(0.25f, runtimeStatsLogInterval);

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

        if (cachedData != bakedData)
        {
            ClearCellCache();
            cachedData = bakedData;
        }

        frameIndex++;
        runtimeStats = default;

        float maxRenderDistance = ResolveMaxRenderDistance();
        float maxRenderDistanceSqr = maxRenderDistance * maxRenderDistance;
        Camera cullingCamera = (useMainCameraFrustumCulling || maxRenderDistance > 0f) ? Camera.main : null;
        bool hasFrustumCamera = useMainCameraFrustumCulling && cullingCamera != null;
        bool hasDistanceCamera = maxRenderDistance > 0f && cullingCamera != null;
        Vector3 cameraPosition = hasDistanceCamera ? cullingCamera.transform.position : Vector3.zero;
        if (hasFrustumCamera)
        {
            GeometryUtility.CalculateFrustumPlanes(cullingCamera, frustumPlanes);
        }

        bool supportsInstancing = SystemInfo.supportsInstancing;
        if (bakedData.HasCellPayloads)
        {
            RenderCellPayloads(
                supportsInstancing,
                hasFrustumCamera,
                hasDistanceCamera,
                cameraPosition,
                maxRenderDistanceSqr);
            PruneCellCache();
            runtimeStats.CachedCells = loadedCells.Count;
            LogRuntimeStatsIfNeeded();
            return;
        }

        RenderLegacyChunks(
            supportsInstancing,
            hasFrustumCamera,
            hasDistanceCamera,
            cameraPosition,
            maxRenderDistanceSqr);

        LogRuntimeStatsIfNeeded();
    }

    private void OnDisable()
    {
        ClearCellCache();
    }

    private void RenderLegacyChunks(
        bool supportsInstancing,
        bool hasFrustumCamera,
        bool hasDistanceCamera,
        Vector3 cameraPosition,
        float maxRenderDistanceSqr)
    {
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

                runtimeStats.TotalChunks++;
                runtimeStats.TotalInstances += chunk.Count;

                if (hasFrustumCamera && !GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.Bounds))
                {
                    runtimeStats.FrustumCulledChunks++;
                    continue;
                }

                if (hasDistanceCamera && chunk.Bounds.SqrDistance(cameraPosition) > maxRenderDistanceSqr)
                {
                    runtimeStats.DistanceCulledChunks++;
                    continue;
                }

                runtimeStats.VisibleChunks++;
                runtimeStats.DrawnInstances += chunk.Count;

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

                    runtimeStats.InstancedDrawCalls++;
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

                runtimeStats.FallbackDrawCalls += chunk.Count;
            }
        }
    }

    private void RenderCellPayloads(
        bool supportsInstancing,
        bool hasFrustumCamera,
        bool hasDistanceCamera,
        Vector3 cameraPosition,
        float maxRenderDistanceSqr)
    {
        var cells = bakedData.Cells;
        runtimeStats.TotalCells = cells.Count;

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            BakedGrassData.Cell cell = cells[cellIndex];
            if (cell == null || !HasCellPayload(cell) || cell.InstanceCount == 0)
            {
                continue;
            }

            if (hasFrustumCamera && !GeometryUtility.TestPlanesAABB(frustumPlanes, cell.Bounds))
            {
                runtimeStats.FrustumCulledChunks++;
                continue;
            }

            if (hasDistanceCamera && cell.Bounds.SqrDistance(cameraPosition) > maxRenderDistanceSqr)
            {
                runtimeStats.DistanceCulledChunks++;
                continue;
            }

            LoadedCell loadedCell = LoadCell(cellIndex, cell);
            if (loadedCell == null)
            {
                continue;
            }

            runtimeStats.VisibleCells++;
            loadedCell.LastUsedFrame = frameIndex;

            for (int chunkIndex = 0; chunkIndex < loadedCell.Chunks.Count; chunkIndex++)
            {
                RuntimeDrawChunk chunk = loadedCell.Chunks[chunkIndex];
                DrawRuntimeChunk(chunk, supportsInstancing);
            }
        }
    }

    private void DrawRuntimeChunk(RuntimeDrawChunk chunk, bool supportsInstancing)
    {
        var batches = bakedData.Batches;
        if (chunk.BatchIndex < 0 || chunk.BatchIndex >= batches.Count)
        {
            return;
        }

        BakedGrassData.Batch batch = batches[chunk.BatchIndex];
        if (batch == null || batch.Mesh == null || batch.Material == null || chunk.Matrices == null || chunk.Matrices.Length == 0)
        {
            return;
        }

        runtimeStats.TotalChunks++;
        runtimeStats.TotalInstances += chunk.Matrices.Length;
        runtimeStats.VisibleChunks++;
        runtimeStats.DrawnInstances += chunk.Matrices.Length;

        bool useInstancing = supportsInstancing && batch.Material.enableInstancing;
        ShadowCastingMode shadowCastingMode = ResolveShadowCastingMode(batch);
        bool shouldReceiveShadows = ResolveReceiveShadows(batch);

        if (useInstancing)
        {
            Graphics.DrawMeshInstanced(
                batch.Mesh,
                batch.SubMeshIndex,
                batch.Material,
                chunk.Matrices,
                chunk.Matrices.Length,
                null,
                shadowCastingMode,
                shouldReceiveShadows,
                batch.Layer);

            runtimeStats.InstancedDrawCalls++;
            return;
        }

        for (int instanceIndex = 0; instanceIndex < chunk.Matrices.Length; instanceIndex++)
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

        runtimeStats.FallbackDrawCalls += chunk.Matrices.Length;
    }

    private LoadedCell LoadCell(int cellIndex, BakedGrassData.Cell cell)
    {
        if (loadedCells.TryGetValue(cellIndex, out LoadedCell loadedCell))
        {
            return loadedCell;
        }

        if (invalidCellPayloads.Contains(cellIndex))
        {
            return null;
        }

        try
        {
            TextAsset payload = ResolveCellPayload(cell, out bool unloadPayload);
            if (payload == null)
            {
                invalidCellPayloads.Add(cellIndex);
                return null;
            }

            List<BakedGrassPayload.Record> records;
            try
            {
                records = BakedGrassPayload.ReadRecords(payload.bytes);
            }
            finally
            {
                if (unloadPayload)
                {
                    Resources.UnloadAsset(payload);
                }
            }

            loadedCell = new LoadedCell(cell.InstanceCount);
            for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                BakedGrassPayload.Record record = records[recordIndex];
                if (record.Matrices == null || record.Matrices.Length == 0)
                {
                    continue;
                }

                loadedCell.Chunks.Add(new RuntimeDrawChunk(record.BatchIndex, record.Matrices));
            }

            loadedCells.Add(cellIndex, loadedCell);
            runtimeStats.CellPayloadLoads++;
            return loadedCell;
        }
        catch (Exception exception)
        {
            invalidCellPayloads.Add(cellIndex);
            Debug.LogWarning($"Failed to load baked grass cell payload at [{cell.X}, {cell.Z}]: {exception.Message}", this);
            return null;
        }
    }

    private static bool HasCellPayload(BakedGrassData.Cell cell)
    {
        return cell.Payload != null || !string.IsNullOrWhiteSpace(cell.PayloadResourcePath);
    }

    private static TextAsset ResolveCellPayload(BakedGrassData.Cell cell, out bool unloadPayload)
    {
        unloadPayload = false;
        if (cell.Payload != null)
        {
            return cell.Payload;
        }

        if (string.IsNullOrWhiteSpace(cell.PayloadResourcePath))
        {
            return null;
        }

        unloadPayload = true;
        return Resources.Load<TextAsset>(cell.PayloadResourcePath);
    }

    private void PruneCellCache()
    {
        if (cellCacheRetainFrames > 0)
        {
            int staleFrame = frameIndex - cellCacheRetainFrames;
            cacheRemovalBuffer.Clear();

            foreach (KeyValuePair<int, LoadedCell> pair in loadedCells)
            {
                if (pair.Value.LastUsedFrame < staleFrame)
                {
                    cacheRemovalBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < cacheRemovalBuffer.Count; i++)
            {
                loadedCells.Remove(cacheRemovalBuffer[i]);
            }
        }

        if (maxCachedCells <= 0 || loadedCells.Count <= maxCachedCells)
        {
            return;
        }

        while (loadedCells.Count > maxCachedCells)
        {
            int oldestKey = -1;
            int oldestFrame = int.MaxValue;
            foreach (KeyValuePair<int, LoadedCell> pair in loadedCells)
            {
                if (pair.Value.LastUsedFrame < oldestFrame)
                {
                    oldestFrame = pair.Value.LastUsedFrame;
                    oldestKey = pair.Key;
                }
            }

            if (oldestKey < 0)
            {
                return;
            }

            loadedCells.Remove(oldestKey);
        }
    }

    private void ClearCellCache()
    {
        loadedCells.Clear();
        invalidCellPayloads.Clear();
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

        return IsRuntimePlatformAllowed(Application.platform);
    }

    private ShadowCastingMode ResolveShadowCastingMode(BakedGrassData.Batch batch)
    {
        return ResolveShadowCastingOverride() == ShadowCastingOverride.Off
            ? ShadowCastingMode.Off
            : batch.ShadowCastingMode;
    }

    private bool ResolveReceiveShadows(BakedGrassData.Batch batch)
    {
        return ResolveReceiveShadowsOverride() == ReceiveShadowsOverride.Off
            ? false
            : batch.ReceiveShadows;
    }

    private bool IsRuntimePlatformAllowed(RuntimePlatform platform)
    {
        return platformFilter switch
        {
            PlatformFilter.All => true,
            PlatformFilter.WebGLOnly => platform == RuntimePlatform.WebGLPlayer,
            PlatformFilter.MobileAndWindows => IsMobileOrWindowsPlatform(platform),
            _ => true,
        };
    }

    private static bool IsMobileOrWindowsPlatform(RuntimePlatform platform)
    {
        return platform == RuntimePlatform.Android ||
               platform == RuntimePlatform.IPhonePlayer ||
               platform == RuntimePlatform.WindowsPlayer ||
               platform == RuntimePlatform.WindowsEditor;
    }

    private GrassQualityProfile ResolveQualityProfile()
    {
        if (qualityProfile != GrassQualityProfile.Auto)
        {
            return qualityProfile;
        }

        RuntimePlatform platform = Application.platform;
        if (platform == RuntimePlatform.Android ||
            platform == RuntimePlatform.IPhonePlayer ||
            platform == RuntimePlatform.WebGLPlayer)
        {
            return GrassQualityProfile.Mobile;
        }

        return GrassQualityProfile.PC;
    }

    private float ResolveMaxRenderDistance()
    {
        return ResolveQualityProfile() switch
        {
            GrassQualityProfile.Mobile => mobileMaxRenderDistance,
            GrassQualityProfile.PC => pcMaxRenderDistance,
            GrassQualityProfile.Custom => customMaxRenderDistance,
            _ => customMaxRenderDistance,
        };
    }

    private ShadowCastingOverride ResolveShadowCastingOverride()
    {
        if (!qualityProfileControlsShadows)
        {
            return shadowCasting;
        }

        return ResolveQualityProfile() switch
        {
            GrassQualityProfile.Mobile => mobileShadowCasting,
            GrassQualityProfile.PC => pcShadowCasting,
            GrassQualityProfile.Custom => shadowCasting,
            _ => shadowCasting,
        };
    }

    private ReceiveShadowsOverride ResolveReceiveShadowsOverride()
    {
        if (!qualityProfileControlsShadows)
        {
            return receiveShadows;
        }

        return ResolveQualityProfile() switch
        {
            GrassQualityProfile.Mobile => mobileReceiveShadows,
            GrassQualityProfile.PC => pcReceiveShadows,
            GrassQualityProfile.Custom => receiveShadows,
            _ => receiveShadows,
        };
    }

    private void LogRuntimeStatsIfNeeded()
    {
        if (!logRuntimeStats || !Application.isPlaying || Time.unscaledTime < nextRuntimeStatsLogTime)
        {
            return;
        }

        nextRuntimeStatsLogTime = Time.unscaledTime + runtimeStatsLogInterval;
        Debug.Log(
            $"Grass stats: visible chunks {runtimeStats.VisibleChunks}/{runtimeStats.TotalChunks}, " +
            $"instances {runtimeStats.DrawnInstances}/{runtimeStats.TotalInstances}, " +
            $"visible cells {runtimeStats.VisibleCells}/{runtimeStats.TotalCells}, cached cells {runtimeStats.CachedCells}, " +
            $"frustum culled {runtimeStats.FrustumCulledChunks}, distance culled {runtimeStats.DistanceCulledChunks}, " +
            $"instanced draws {runtimeStats.InstancedDrawCalls}, fallback draws {runtimeStats.FallbackDrawCalls}, " +
            $"cell loads {runtimeStats.CellPayloadLoads}.",
            this);
    }

    private sealed class LoadedCell
    {
        public LoadedCell(int instanceCount)
        {
            InstanceCount = instanceCount;
        }

        public int InstanceCount { get; }
        public int LastUsedFrame { get; set; }
        public List<RuntimeDrawChunk> Chunks { get; } = new();
    }

    private readonly struct RuntimeDrawChunk
    {
        public RuntimeDrawChunk(int batchIndex, Matrix4x4[] matrices)
        {
            BatchIndex = batchIndex;
            Matrices = matrices;
        }

        public int BatchIndex { get; }
        public Matrix4x4[] Matrices { get; }
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
    public float SpatialCellSize => spatialCellSize;
    public int MaxInstancesPerChunk => maxInstancesPerChunk;

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
