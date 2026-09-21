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
    private const int MaxInstancedDrawCount = 1023;

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

    private enum InstancedRenderBackend
    {
        RenderMeshInstanced = 0,
        DrawMeshInstanced = 1,
        CullOnly = 2,
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
        public int CheckedCells;
        public int SpatiallySkippedCells;
    }

    [Header("Runtime")]
    [SerializeField] private BakedGrassData bakedData;
    [SerializeField] private PlatformFilter platformFilter = PlatformFilter.MobileAndWindows;
    [SerializeField] private bool renderInEditor = false;
    [SerializeField] private bool useMainCameraFrustumCulling = true;
    [Tooltip("Disables Terrain detail density in Play Mode while this renderer is active, so baked grass replaces the built-in Terrain detail renderer instead of drawing on top of it.")]
    [SerializeField] private bool suppressTerrainDetailsWhileRendering = true;
    [Tooltip("Cull Only is a diagnostic mode: it keeps runtime culling/stats active but skips grass draw submission.")]
    [SerializeField] private InstancedRenderBackend instancedRenderBackend = InstancedRenderBackend.RenderMeshInstanced;

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
    private readonly Dictionary<int, InstancedDrawBuffer> instancedDrawBuffers = new();
    private readonly List<int> activeInstancedDrawBufferKeys = new();
    private readonly HashSet<int> activeInstancedDrawBufferKeySet = new();
    private readonly HashSet<Terrain> suppressedTerrainDetails = new();
    private readonly List<Terrain> terrainSuppressionScratch = new();
    private readonly List<Terrain> terrainSuppressionReleaseBuffer = new();
    private readonly Dictionary<RuntimeCellKey, int> cellIndexByKey = new();
    private static readonly Dictionary<Terrain, TerrainDetailSuppressionState> terrainDetailSuppressions = new();
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
        bool shouldRender = ShouldRender();
        SyncTerrainDetailSuppression(shouldRender);
        if (!shouldRender)
        {
            return;
        }

        if (cachedData != bakedData)
        {
            ClearCellCache();
            RebuildCellIndex();
            cachedData = bakedData;
        }

        frameIndex++;
        runtimeStats = default;
        BeginInstancedDrawBufferFrame();

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
            FlushInstancedDrawBuffers();
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
        ReleaseTerrainDetailSuppression();
        ClearCellCache();
    }

    private void OnDestroy()
    {
        ReleaseTerrainDetailSuppression();
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
                        batch.Layer,
                        null,
                        LightProbeUsage.Off);

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

        if (hasDistanceCamera && cellIndexByKey.Count > 0 && bakedData.SpatialCellSize > 0f)
        {
            RenderNearbyCellPayloads(
                supportsInstancing,
                hasFrustumCamera,
                hasDistanceCamera,
                cameraPosition,
                maxRenderDistanceSqr);
            return;
        }

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            RenderCellPayload(
                cellIndex,
                supportsInstancing,
                hasFrustumCamera,
                hasDistanceCamera,
                cameraPosition,
                maxRenderDistanceSqr);
        }
    }

    private void RenderNearbyCellPayloads(
        bool supportsInstancing,
        bool hasFrustumCamera,
        bool hasDistanceCamera,
        Vector3 cameraPosition,
        float maxRenderDistanceSqr)
    {
        float maxRenderDistance = Mathf.Sqrt(maxRenderDistanceSqr);
        float cellSize = Mathf.Max(bakedData.SpatialCellSize, 1f);
        int paddingCells = 1;
        int minX = Mathf.FloorToInt((cameraPosition.x - maxRenderDistance) / cellSize) - paddingCells;
        int maxX = Mathf.FloorToInt((cameraPosition.x + maxRenderDistance) / cellSize) + paddingCells;
        int minZ = Mathf.FloorToInt((cameraPosition.z - maxRenderDistance) / cellSize) - paddingCells;
        int maxZ = Mathf.FloorToInt((cameraPosition.z + maxRenderDistance) / cellSize) + paddingCells;

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!cellIndexByKey.TryGetValue(new RuntimeCellKey(x, z), out int cellIndex))
                {
                    continue;
                }

                RenderCellPayload(
                    cellIndex,
                    supportsInstancing,
                    hasFrustumCamera,
                    hasDistanceCamera,
                    cameraPosition,
                    maxRenderDistanceSqr);
            }
        }

        runtimeStats.SpatiallySkippedCells = Mathf.Max(0, runtimeStats.TotalCells - runtimeStats.CheckedCells);
    }

    private void RenderCellPayload(
        int cellIndex,
        bool supportsInstancing,
        bool hasFrustumCamera,
        bool hasDistanceCamera,
        Vector3 cameraPosition,
        float maxRenderDistanceSqr)
    {
        var cells = bakedData.Cells;
        if (cellIndex < 0 || cellIndex >= cells.Count)
        {
            return;
        }

        BakedGrassData.Cell cell = cells[cellIndex];
        if (cell == null || !HasCellPayload(cell) || cell.InstanceCount == 0)
        {
            return;
        }

        runtimeStats.CheckedCells++;

        if (hasFrustumCamera && !GeometryUtility.TestPlanesAABB(frustumPlanes, cell.Bounds))
        {
            runtimeStats.FrustumCulledChunks++;
            return;
        }

        if (hasDistanceCamera && cell.Bounds.SqrDistance(cameraPosition) > maxRenderDistanceSqr)
        {
            runtimeStats.DistanceCulledChunks++;
            return;
        }

        LoadedCell loadedCell = LoadCell(cellIndex, cell);
        if (loadedCell == null)
        {
            return;
        }

        runtimeStats.VisibleCells++;
        loadedCell.LastUsedFrame = frameIndex;

        for (int chunkIndex = 0; chunkIndex < loadedCell.Chunks.Count; chunkIndex++)
        {
            RuntimeDrawChunk chunk = loadedCell.Chunks[chunkIndex];
            DrawRuntimeChunk(chunk, supportsInstancing);
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

        if (instancedRenderBackend == InstancedRenderBackend.CullOnly)
        {
            return;
        }

        bool useInstancing = supportsInstancing && batch.Material.enableInstancing;
        if (useInstancing)
        {
            QueueInstancedDraw(chunk.BatchIndex, chunk.Matrices);
            return;
        }

        ShadowCastingMode shadowCastingMode = ResolveShadowCastingMode(batch);
        bool shouldReceiveShadows = ResolveReceiveShadows(batch);
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

    private void BeginInstancedDrawBufferFrame()
    {
        activeInstancedDrawBufferKeys.Clear();
        activeInstancedDrawBufferKeySet.Clear();
    }

    private void QueueInstancedDraw(int batchIndex, Matrix4x4[] matrices)
    {
        if (matrices == null || matrices.Length == 0)
        {
            return;
        }

        if (!instancedDrawBuffers.TryGetValue(batchIndex, out InstancedDrawBuffer buffer))
        {
            buffer = new InstancedDrawBuffer(MaxInstancedDrawCount);
            instancedDrawBuffers.Add(batchIndex, buffer);
        }

        if (activeInstancedDrawBufferKeySet.Add(batchIndex))
        {
            activeInstancedDrawBufferKeys.Add(batchIndex);
        }

        int sourceIndex = 0;
        while (sourceIndex < matrices.Length)
        {
            int copyCount = Mathf.Min(MaxInstancedDrawCount - buffer.Count, matrices.Length - sourceIndex);
            Array.Copy(matrices, sourceIndex, buffer.Matrices, buffer.Count, copyCount);
            buffer.Count += copyCount;
            sourceIndex += copyCount;

            if (buffer.Count >= MaxInstancedDrawCount)
            {
                FlushInstancedDrawBuffer(batchIndex, buffer);
            }
        }
    }

    private void FlushInstancedDrawBuffers()
    {
        for (int i = 0; i < activeInstancedDrawBufferKeys.Count; i++)
        {
            int batchIndex = activeInstancedDrawBufferKeys[i];
            if (instancedDrawBuffers.TryGetValue(batchIndex, out InstancedDrawBuffer buffer))
            {
                FlushInstancedDrawBuffer(batchIndex, buffer);
            }
        }

        activeInstancedDrawBufferKeys.Clear();
        activeInstancedDrawBufferKeySet.Clear();
    }

    private void FlushInstancedDrawBuffer(int batchIndex, InstancedDrawBuffer buffer)
    {
        if (buffer == null || buffer.Count == 0)
        {
            return;
        }

        var batches = bakedData.Batches;
        if (batchIndex < 0 || batchIndex >= batches.Count)
        {
            buffer.Count = 0;
            return;
        }

        BakedGrassData.Batch batch = batches[batchIndex];
        if (batch == null || batch.Mesh == null || batch.Material == null)
        {
            buffer.Count = 0;
            return;
        }

        if (instancedRenderBackend == InstancedRenderBackend.RenderMeshInstanced)
        {
            RenderParams renderParams = new(batch.Material)
            {
                layer = batch.Layer,
                lightProbeUsage = LightProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                receiveShadows = ResolveReceiveShadows(batch),
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                shadowCastingMode = ResolveShadowCastingMode(batch),
                worldBounds = bakedData.WorldBounds,
            };

            Graphics.RenderMeshInstanced(
                renderParams,
                batch.Mesh,
                batch.SubMeshIndex,
                buffer.Matrices,
                buffer.Count);
        }
        else
        {
            Graphics.DrawMeshInstanced(
                batch.Mesh,
                batch.SubMeshIndex,
                batch.Material,
                buffer.Matrices,
                buffer.Count,
                null,
                ResolveShadowCastingMode(batch),
                ResolveReceiveShadows(batch),
                batch.Layer,
                null,
                LightProbeUsage.Off);
        }

        runtimeStats.InstancedDrawCalls++;
        buffer.Count = 0;
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

    private void RebuildCellIndex()
    {
        cellIndexByKey.Clear();
        if (bakedData == null)
        {
            return;
        }

        var cells = bakedData.Cells;
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            BakedGrassData.Cell cell = cells[cellIndex];
            if (cell == null)
            {
                continue;
            }

            RuntimeCellKey key = new(cell.X, cell.Z);
            if (!cellIndexByKey.ContainsKey(key))
            {
                cellIndexByKey.Add(key, cellIndex);
            }
        }
    }

    private void SyncTerrainDetailSuppression(bool shouldRender)
    {
        if (!Application.isPlaying || !suppressTerrainDetailsWhileRendering || !shouldRender)
        {
            ReleaseTerrainDetailSuppression();
            return;
        }

        CollectRuntimeTerrains(terrainSuppressionScratch);
        for (int i = 0; i < terrainSuppressionScratch.Count; i++)
        {
            AcquireTerrainDetailSuppression(terrainSuppressionScratch[i]);
        }

        terrainSuppressionReleaseBuffer.Clear();
        foreach (Terrain terrain in suppressedTerrainDetails)
        {
            if (terrain == null || !terrainSuppressionScratch.Contains(terrain))
            {
                terrainSuppressionReleaseBuffer.Add(terrain);
            }
        }

        for (int i = 0; i < terrainSuppressionReleaseBuffer.Count; i++)
        {
            ReleaseTerrainDetailSuppression(terrainSuppressionReleaseBuffer[i]);
        }

        terrainSuppressionScratch.Clear();
        terrainSuppressionReleaseBuffer.Clear();
    }

    private void CollectRuntimeTerrains(List<Terrain> results)
    {
        results.Clear();
        if (sourceTerrains != null)
        {
            for (int i = 0; i < sourceTerrains.Length; i++)
            {
                AddUniqueTerrain(results, sourceTerrains[i]);
            }
        }

        if (results.Count > 0 || !useActiveTerrainsWhenSourceTerrainsEmpty)
        {
            return;
        }

        Terrain[] activeTerrains = Terrain.activeTerrains;
        if (activeTerrains == null)
        {
            return;
        }

        for (int i = 0; i < activeTerrains.Length; i++)
        {
            AddUniqueTerrain(results, activeTerrains[i]);
        }
    }

    private static void AddUniqueTerrain(List<Terrain> terrains, Terrain terrain)
    {
        if (terrain != null && !terrains.Contains(terrain))
        {
            terrains.Add(terrain);
        }
    }

    private void AcquireTerrainDetailSuppression(Terrain terrain)
    {
        if (terrain == null || suppressedTerrainDetails.Contains(terrain))
        {
            return;
        }

        if (!terrainDetailSuppressions.TryGetValue(terrain, out TerrainDetailSuppressionState state))
        {
            state = new TerrainDetailSuppressionState
            {
                OriginalDetailDensity = terrain.detailObjectDensity,
            };
            terrainDetailSuppressions.Add(terrain, state);
            terrain.detailObjectDensity = 0f;
        }

        state.Owners.Add(this);
        suppressedTerrainDetails.Add(terrain);
    }

    private void ReleaseTerrainDetailSuppression()
    {
        if (suppressedTerrainDetails.Count == 0)
        {
            return;
        }

        terrainSuppressionReleaseBuffer.Clear();
        foreach (Terrain terrain in suppressedTerrainDetails)
        {
            terrainSuppressionReleaseBuffer.Add(terrain);
        }

        for (int i = 0; i < terrainSuppressionReleaseBuffer.Count; i++)
        {
            ReleaseTerrainDetailSuppression(terrainSuppressionReleaseBuffer[i]);
        }

        terrainSuppressionReleaseBuffer.Clear();
    }

    private void ReleaseTerrainDetailSuppression(Terrain terrain)
    {
        if (terrain == null)
        {
            suppressedTerrainDetails.Remove(terrain);
            terrainDetailSuppressions.Remove(terrain);
            return;
        }

        if (!suppressedTerrainDetails.Remove(terrain))
        {
            return;
        }

        if (!terrainDetailSuppressions.TryGetValue(terrain, out TerrainDetailSuppressionState state))
        {
            return;
        }

        state.Owners.Remove(this);
        if (state.Owners.Count > 0)
        {
            return;
        }

        terrain.detailObjectDensity = state.OriginalDetailDensity;
        terrainDetailSuppressions.Remove(terrain);
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

    private sealed class InstancedDrawBuffer
    {
        public InstancedDrawBuffer(int capacity)
        {
            Matrices = new Matrix4x4[capacity];
        }

        public Matrix4x4[] Matrices { get; }
        public int Count { get; set; }
    }

    private sealed class TerrainDetailSuppressionState
    {
        public float OriginalDetailDensity;
        public HashSet<BakedGrassRenderer> Owners { get; } = new();
    }

    private readonly struct RuntimeCellKey : IEquatable<RuntimeCellKey>
    {
        public RuntimeCellKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public bool Equals(RuntimeCellKey other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }
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
