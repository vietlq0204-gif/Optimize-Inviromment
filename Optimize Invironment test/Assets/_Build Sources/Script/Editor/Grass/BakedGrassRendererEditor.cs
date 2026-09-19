using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(BakedGrassRenderer))]
public sealed class BakedGrassRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Use Active Terrains"))
            {
                AssignActiveTerrains((BakedGrassRenderer)target);
            }

            if (GUILayout.Button("Bake Terrain Details To Asset"))
            {
                BakeRenderer((BakedGrassRenderer)target);
            }
        }

        DrawBakedStats((BakedGrassRenderer)target);
        DrawRuntimeStats((BakedGrassRenderer)target);
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        
        DrawDefaultInspector();
    }

    private static void AssignActiveTerrains(BakedGrassRenderer renderer)
    {
        Terrain[] activeTerrains = Terrain.activeTerrains ?? Array.Empty<Terrain>();
        Undo.RecordObject(renderer, "Assign Active Terrains");
        renderer.SetSourceTerrains(activeTerrains);
        EditorUtility.SetDirty(renderer);
    }

    private static void BakeRenderer(BakedGrassRenderer renderer)
    {
        List<Terrain> terrains = GetEffectiveTerrains(renderer);
        if (terrains.Count == 0)
        {
            EditorUtility.DisplayDialog("Bake Grass", "Khong tim thay Terrain nao de bake.", "OK");
            return;
        }

        BakedGrassData data = EnsureDataAsset(renderer);
        if (data == null)
        {
            EditorUtility.DisplayDialog("Bake Grass", "Khong the tao asset cho baked grass.", "OK");
            return;
        }

        string payloadFolder = AssetDatabase.GenerateUniqueAssetPath(GetPayloadFolderPath(data));
        StreamingBakeContext bakeContext = null;

        Dictionary<BatchKey, int> batchIndexByKey = new();
        List<BatchKey> batchKeys = new();
        Dictionary<GameObject, List<PrototypeRendererInfo>> rendererCache = new();

        int terrainCount = 0;
        int prototypeCount = 0;
        int processedPatches = 0;
        int totalPatches = CountDetailPatches(terrains);
        bool canceled = false;
        bool bakeSucceeded = false;

        try
        {
            EnsureFolderExists(payloadFolder);
            bakeContext = new StreamingBakeContext(
                payloadFolder,
                renderer.SpatialCellSize,
                renderer.MaxInstancesPerChunk);

            for (int terrainIndex = 0; terrainIndex < terrains.Count; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                TerrainData terrainData = terrain.terrainData;
                DetailPrototype[] prototypes = terrainData.detailPrototypes;
                if (prototypes == null || prototypes.Length == 0)
                {
                    continue;
                }

                terrainCount++;
                int patchCount = terrainData.detailPatchCount;
                Matrix4x4 terrainMatrix = terrain.transform.localToWorldMatrix;

                for (int layer = 0; layer < prototypes.Length; layer++)
                {
                    DetailPrototype prototype = prototypes[layer];
                    if (prototype == null || prototype.prototype == null)
                    {
                        processedPatches += patchCount * patchCount;
                        continue;
                    }

                    if (!rendererCache.TryGetValue(prototype.prototype, out List<PrototypeRendererInfo> rendererInfos))
                    {
                        rendererInfos = CollectPrototypeRenderers(prototype.prototype);
                        rendererCache.Add(prototype.prototype, rendererInfos);
                    }

                    if (rendererInfos.Count == 0)
                    {
                        processedPatches += patchCount * patchCount;
                        continue;
                    }

                    float density = prototype.useDensityScaling
                        ? terrain.detailObjectDensity * QualitySettings.terrainDetailDensityScale
                        : terrain.detailObjectDensity;
                    density *= renderer.DensityScale;
                    if (density <= 0.0001f)
                    {
                        processedPatches += patchCount * patchCount;
                        continue;
                    }

                    bool bakedPrototype = false;
                    for (int patchY = 0; patchY < patchCount; patchY++)
                    {
                        for (int patchX = 0; patchX < patchCount; patchX++)
                        {
                            float progress = totalPatches > 0 ? processedPatches / (float)totalPatches : 0f;
                            if (EditorUtility.DisplayCancelableProgressBar(
                                    "Bake Grass",
                                    $"Terrain {terrainIndex + 1}/{terrains.Count}, prototype {layer + 1}/{prototypes.Length}, patch {patchX},{patchY}",
                                    progress))
                            {
                                canceled = true;
                                throw new OperationCanceledException("Grass bake canceled.");
                            }

                            Bounds patchBounds;
                            DetailInstanceTransform[] detailTransforms =
                                terrainData.ComputeDetailInstanceTransforms(patchX, patchY, layer, density,
                                    out patchBounds);
                            if (detailTransforms == null || detailTransforms.Length == 0)
                            {
                                processedPatches++;
                                continue;
                            }

                            bakedPrototype = true;

                            for (int detailIndex = 0; detailIndex < detailTransforms.Length; detailIndex++)
                            {
                                DetailInstanceTransform detailTransform = detailTransforms[detailIndex];
                                Vector3 localPosition = new(detailTransform.posX, detailTransform.posY,
                                    detailTransform.posZ);
                                Quaternion rotation =
                                    Quaternion.AngleAxis(detailTransform.rotationY * Mathf.Rad2Deg, Vector3.up);
                                Vector3 scale = new(detailTransform.scaleXZ, detailTransform.scaleY,
                                    detailTransform.scaleXZ);
                                Matrix4x4 instanceMatrix =
                                    terrainMatrix * Matrix4x4.TRS(localPosition, rotation, scale);

                                for (int rendererIndex = 0; rendererIndex < rendererInfos.Count; rendererIndex++)
                                {
                                    PrototypeRendererInfo rendererInfo = rendererInfos[rendererIndex];
                                    BatchKey key = new(
                                        rendererInfo.Mesh,
                                        rendererInfo.Material,
                                        rendererInfo.Layer,
                                        rendererInfo.SubMeshIndex,
                                        rendererInfo.ShadowCastingMode,
                                        rendererInfo.ReceiveShadows);

                                    if (!batchIndexByKey.TryGetValue(key, out int batchIndex))
                                    {
                                        batchIndex = batchKeys.Count;
                                        batchKeys.Add(key);
                                        batchIndexByKey.Add(key, batchIndex);
                                    }

                                    Matrix4x4 finalMatrix = instanceMatrix * rendererInfo.LocalMatrix;
                                    bakeContext.AddInstance(batchIndex, rendererInfo.Mesh.bounds, finalMatrix);
                                }
                            }

                            bakeContext.FlushIfNeeded();
                            processedPatches++;
                        }
                    }

                    if (bakedPrototype)
                    {
                        prototypeCount++;
                    }
                }
            }

            bakeContext.FlushPending();
            AssetDatabase.Refresh();

            List<BakedGrassData.Batch> bakedBatches = BuildBatches(batchKeys);
            List<BakedGrassData.Cell> bakedCells = bakeContext.BuildCells(payloadFolder);
            Bounds worldBounds = bakeContext.HasWorldBounds
                ? bakeContext.WorldBounds
                : new Bounds(Vector3.zero, Vector3.one);

            Undo.RecordObject(data, "Bake Grass Data");
            data.SetData(
                bakedBatches,
                bakedCells,
                worldBounds,
                terrainCount,
                prototypeCount,
                bakeContext.RecordCount,
                bakeContext.InstanceCount,
                renderer.SpatialCellSize,
                renderer.MaxInstancesPerChunk);
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            bakeSucceeded = true;

            Debug.Log(
                $"BakedGrassRenderer baked {bakeContext.InstanceCount} grass instance(s), " +
                $"{bakeContext.CellCount} cell(s), {bakeContext.RecordCount} draw record(s), " +
                $"{prototypeCount} prototype(s) from {terrainCount} terrain(s), " +
                $"cell size {renderer.SpatialCellSize:0.##}, max {renderer.MaxInstancesPerChunk} per record " +
                $"into {AssetDatabase.GetAssetPath(data)}. Payload folder: {payloadFolder}",
                renderer);
        }
        catch (OperationCanceledException)
        {
            if (!canceled)
            {
                throw;
            }

            Debug.LogWarning("BakedGrassRenderer bake canceled. Existing baked data was left unchanged.", renderer);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            bakeContext?.Dispose();
            if (!bakeSucceeded)
            {
                DeleteAssetFolderIfExists(payloadFolder);
            }
        }
    }

    private static int CountDetailPatches(List<Terrain> terrains)
    {
        int total = 0;
        for (int terrainIndex = 0; terrainIndex < terrains.Count; terrainIndex++)
        {
            Terrain terrain = terrains[terrainIndex];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainData terrainData = terrain.terrainData;
            DetailPrototype[] prototypes = terrainData.detailPrototypes;
            if (prototypes == null || prototypes.Length == 0)
            {
                continue;
            }

            int patchCount = terrainData.detailPatchCount;
            total += patchCount * patchCount * prototypes.Length;
        }

        return total;
    }

    private static List<BakedGrassData.Batch> BuildBatches(List<BatchKey> batchKeys)
    {
        List<BakedGrassData.Batch> batches = new(batchKeys.Count);
        for (int i = 0; i < batchKeys.Count; i++)
        {
            BatchKey key = batchKeys[i];
            BakedGrassData.Batch batch = new();
            batch.SetData(
                key.Mesh,
                key.Material,
                key.Layer,
                key.SubMeshIndex,
                key.ShadowCastingMode,
                key.ReceiveShadows,
                new List<BakedGrassData.Chunk>());
            batches.Add(batch);
        }

        return batches;
    }

    private static string GetPayloadFolderPath(BakedGrassData data)
    {
        string dataPath = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            return "Assets/Resources/BakedGrass/Cells";
        }

        string name = Path.GetFileNameWithoutExtension(dataPath);
        string guid = AssetDatabase.AssetPathToGUID(dataPath);
        string suffix = string.IsNullOrEmpty(guid) ? string.Empty : $"_{guid.Substring(0, Mathf.Min(8, guid.Length))}";
        return NormalizeAssetPath(Path.Combine("Assets/Resources/BakedGrass", $"{name}{suffix}_Cells"));
    }

    private static string GetResourcesPath(string assetPath)
    {
        string normalizedPath = NormalizeAssetPath(assetPath);
        const string nestedMarker = "/Resources/";
        const string rootMarker = "Assets/Resources/";

        int startIndex = -1;
        int markerIndex = normalizedPath.IndexOf(nestedMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            startIndex = markerIndex + nestedMarker.Length;
        }
        else if (normalizedPath.StartsWith(rootMarker, StringComparison.OrdinalIgnoreCase))
        {
            startIndex = rootMarker.Length;
        }

        if (startIndex < 0)
        {
            throw new InvalidDataException($"Baked grass payload must be under a Resources folder: {assetPath}");
        }

        string resourcePath = normalizedPath.Substring(startIndex);
        int extensionIndex = resourcePath.LastIndexOf('.');
        if (extensionIndex >= 0)
        {
            resourcePath = resourcePath.Substring(0, extensionIndex);
        }

        return resourcePath;
    }

    private static void DeleteAssetFolderIfExists(string assetFolder)
    {
        if (string.IsNullOrWhiteSpace(assetFolder))
        {
            return;
        }

        assetFolder = NormalizeAssetPath(assetFolder);
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            AssetDatabase.DeleteAsset(assetFolder);
            return;
        }

        string fullPath = AssetPathToFullPath(assetFolder);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().Replace('\\', '/');
    }

    private static List<Terrain> GetEffectiveTerrains(BakedGrassRenderer renderer)
    {
        List<Terrain> terrains = new();
        HashSet<Terrain> uniqueTerrains = new();

        Terrain[] sourceTerrains = renderer.SourceTerrains;
        if (sourceTerrains != null)
        {
            for (int i = 0; i < sourceTerrains.Length; i++)
            {
                Terrain terrain = sourceTerrains[i];
                if (terrain != null && uniqueTerrains.Add(terrain))
                {
                    terrains.Add(terrain);
                }
            }
        }

        if (terrains.Count > 0 || !renderer.UseActiveTerrainsWhenSourceTerrainsEmpty)
        {
            return terrains;
        }

        Terrain[] activeTerrains = Terrain.activeTerrains;
        if (activeTerrains == null)
        {
            return terrains;
        }

        for (int i = 0; i < activeTerrains.Length; i++)
        {
            Terrain terrain = activeTerrains[i];
            if (terrain != null && uniqueTerrains.Add(terrain))
            {
                terrains.Add(terrain);
            }
        }

        return terrains;
    }

    private static BakedGrassData EnsureDataAsset(BakedGrassRenderer renderer)
    {
        if (renderer.BakedData != null)
        {
            return renderer.BakedData;
        }

        string folderPath = BakedGrassRenderer.NormalizeBakedAssetFolder(renderer.BakedAssetFolder);
        if (!string.Equals(renderer.BakedAssetFolder, folderPath, StringComparison.Ordinal))
        {
            Undo.RecordObject(renderer, "Update Baked Grass Folder");
            renderer.SetBakedAssetFolder(folderPath);
            EditorUtility.SetDirty(renderer);
        }

        EnsureFolderExists(folderPath);

        string assetName = string.IsNullOrWhiteSpace(renderer.BakedAssetName)
            ? "BakedGrassData"
            : renderer.BakedAssetName.Trim();
        if (!assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            assetName += ".asset";
        }

        string assetPath =
            AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName).Replace('\\', '/'));
        BakedGrassData data = ScriptableObject.CreateInstance<BakedGrassData>();
        AssetDatabase.CreateAsset(data, assetPath);

        Undo.RecordObject(renderer, "Assign Baked Grass Data");
        renderer.SetBakedData(data);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        string normalizedPath = folderPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return;
        }

        string[] segments = normalizedPath.Split('/');
        if (segments.Length == 0)
        {
            return;
        }

        string currentPath = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string nextPath = $"{currentPath}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, segments[i]);
            }

            currentPath = nextPath;
        }
    }

    private static List<PrototypeRendererInfo> CollectPrototypeRenderers(GameObject prototypeRoot)
    {
        List<PrototypeRendererInfo> rendererInfos = new();
        MeshRenderer[] renderers = prototypeRoot.GetComponentsInChildren<MeshRenderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            MeshRenderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                continue;
            }

            Matrix4x4 localMatrix = prototypeRoot.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            int subMeshCount = Mathf.Min(meshFilter.sharedMesh.subMeshCount, materials.Length);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = materials[subMeshIndex];
                if (material == null)
                {
                    continue;
                }

                if (!material.enableInstancing)
                {
                    material.enableInstancing = true;
                    EditorUtility.SetDirty(material);
                }

                rendererInfos.Add(new PrototypeRendererInfo
                {
                    Mesh = meshFilter.sharedMesh,
                    Material = material,
                    LocalMatrix = localMatrix,
                    Layer = renderer.gameObject.layer,
                    SubMeshIndex = subMeshIndex,
                    ShadowCastingMode = renderer.shadowCastingMode,
                    ReceiveShadows = renderer.receiveShadows,
                });
            }
        }

        return rendererInfos;
    }

    private static List<BakedGrassData.Chunk> BuildChunks(
        BatchBuilder builder,
        float spatialCellSize,
        int maxInstancesPerChunk,
        ref bool hasWorldBounds,
        ref Bounds worldBounds,
        ref int chunkCount,
        ref int instanceCount)
    {
        spatialCellSize = Mathf.Max(1f, spatialCellSize);
        maxInstancesPerChunk = Mathf.Clamp(maxInstancesPerChunk, 1, 1023);
        List<BakedGrassData.Chunk> bakedChunks = new();
        Dictionary<CellKey, List<Matrix4x4>> cells = new();

        for (int matrixIndex = 0; matrixIndex < builder.Matrices.Count; matrixIndex++)
        {
            Matrix4x4 matrix = builder.Matrices[matrixIndex];
            CellKey key = CellKey.FromMatrix(matrix, spatialCellSize);
            if (!cells.TryGetValue(key, out List<Matrix4x4> cellMatrices))
            {
                cellMatrices = new List<Matrix4x4>();
                cells.Add(key, cellMatrices);
            }

            cellMatrices.Add(matrix);
        }

        List<CellKey> sortedKeys = new(cells.Keys);
        sortedKeys.Sort();

        for (int keyIndex = 0; keyIndex < sortedKeys.Count; keyIndex++)
        {
            List<Matrix4x4> cellMatrices = cells[sortedKeys[keyIndex]];
            for (int start = 0; start < cellMatrices.Count; start += maxInstancesPerChunk)
            {
                int count = Mathf.Min(maxInstancesPerChunk, cellMatrices.Count - start);
                Matrix4x4[] chunkMatrices = new Matrix4x4[count];
                cellMatrices.CopyTo(start, chunkMatrices, 0, count);

                Bounds chunkBounds = CalculateChunkBounds(builder.Key.Mesh.bounds, chunkMatrices);
                BakedGrassData.Chunk bakedChunk = new();
                bakedChunk.SetData(chunkMatrices, chunkBounds);
                bakedChunks.Add(bakedChunk);

                if (!hasWorldBounds)
                {
                    worldBounds = chunkBounds;
                    hasWorldBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(chunkBounds);
                }

                chunkCount++;
                instanceCount += count;
            }
        }

        return bakedChunks;
    }

    private static Bounds CalculateChunkBounds(Bounds meshBounds, Matrix4x4[] matrices)
    {
        Bounds bounds = TransformBounds(meshBounds, matrices[0]);
        for (int i = 1; i < matrices.Length; i++)
        {
            bounds.Encapsulate(TransformBounds(meshBounds, matrices[i]));
        }

        return bounds;
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;

        Vector3 axisX = new(matrix.m00, matrix.m10, matrix.m20);
        Vector3 axisY = new(matrix.m01, matrix.m11, matrix.m21);
        Vector3 axisZ = new(matrix.m02, matrix.m12, matrix.m22);

        axisX *= extents.x;
        axisY *= extents.y;
        axisZ *= extents.z;

        Vector3 worldExtents = new(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

        return new Bounds(center, worldExtents * 2f);
    }

    private static void DrawBakedStats(BakedGrassRenderer renderer)
    {
        BakedGrassData data = renderer.BakedData;
        if (data == null)
        {
            EditorGUILayout.HelpBox("Chua co baked data asset.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            $"Terrains: {data.TerrainCount}\n" +
            $"Prototypes: {data.PrototypeCount}\n" +
            $"Cells: {data.Cells.Count}\n" +
            $"Draw Records: {data.ChunkCount}\n" +
            $"Instances: {data.InstanceCount}\n" +
            $"Spatial Cell: {(data.SpatialCellSize > 0f ? data.SpatialCellSize.ToString("0.##") : "Legacy")}\n" +
            $"Max Instances/Chunk: {(data.MaxInstancesPerChunk > 0 ? data.MaxInstancesPerChunk.ToString() : "Legacy")}",
            MessageType.None);
    }

    private static void DrawRuntimeStats(BakedGrassRenderer renderer)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BakedGrassRenderer.RuntimeStats stats = renderer.LastStats;
        EditorGUILayout.HelpBox(
            $"Runtime Grass\n" +
            $"Visible Cells: {stats.VisibleCells}/{stats.TotalCells}\n" +
            $"Cached Cells: {stats.CachedCells}\n" +
            $"Visible Chunks: {stats.VisibleChunks}/{stats.TotalChunks}\n" +
            $"Drawn Instances: {stats.DrawnInstances}/{stats.TotalInstances}\n" +
            $"Frustum Culled Chunks: {stats.FrustumCulledChunks}\n" +
            $"Distance Culled Chunks: {stats.DistanceCulledChunks}\n" +
            $"Instanced Draw Calls: {stats.InstancedDrawCalls}\n" +
            $"Fallback Draw Calls: {stats.FallbackDrawCalls}\n" +
            $"Cell Payload Loads: {stats.CellPayloadLoads}",
            MessageType.None);
    }

    private sealed class PrototypeRendererInfo
    {
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 LocalMatrix;
        public int Layer;
        public int SubMeshIndex;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
    }

    private readonly struct BatchKey : IEquatable<BatchKey>
    {
        public BatchKey(
            Mesh mesh,
            Material material,
            int layer,
            int subMeshIndex,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows)
        {
            Mesh = mesh;
            Material = material;
            Layer = layer;
            SubMeshIndex = subMeshIndex;
            ShadowCastingMode = shadowCastingMode;
            ReceiveShadows = receiveShadows;
        }

        public Mesh Mesh { get; }
        public Material Material { get; }
        public int Layer { get; }
        public int SubMeshIndex { get; }
        public ShadowCastingMode ShadowCastingMode { get; }
        public bool ReceiveShadows { get; }

        public bool Equals(BatchKey other)
        {
            return Mesh == other.Mesh &&
                   Material == other.Material &&
                   Layer == other.Layer &&
                   SubMeshIndex == other.SubMeshIndex &&
                   ShadowCastingMode == other.ShadowCastingMode &&
                   ReceiveShadows == other.ReceiveShadows;
        }

        public override bool Equals(object obj)
        {
            return obj is BatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Mesh != null ? Mesh.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Material != null ? Material.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Layer;
                hashCode = (hashCode * 397) ^ SubMeshIndex;
                hashCode = (hashCode * 397) ^ (int)ShadowCastingMode;
                hashCode = (hashCode * 397) ^ (ReceiveShadows ? 1 : 0);
                return hashCode;
            }
        }
    }

    private readonly struct CellKey : IEquatable<CellKey>, IComparable<CellKey>
    {
        private CellKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public static CellKey FromMatrix(Matrix4x4 matrix, float cellSize)
        {
            float safeCellSize = Mathf.Max(cellSize, 0.001f);
            int x = Mathf.FloorToInt(matrix.m03 / safeCellSize);
            int z = Mathf.FloorToInt(matrix.m23 / safeCellSize);
            return new CellKey(x, z);
        }

        public int CompareTo(CellKey other)
        {
            int zCompare = Z.CompareTo(other.Z);
            return zCompare != 0 ? zCompare : X.CompareTo(other.X);
        }

        public bool Equals(CellKey other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }
    }

    private readonly struct PendingRecordKey : IEquatable<PendingRecordKey>
    {
        public PendingRecordKey(CellKey cell, int batchIndex)
        {
            Cell = cell;
            BatchIndex = batchIndex;
        }

        public CellKey Cell { get; }
        public int BatchIndex { get; }

        public bool Equals(PendingRecordKey other)
        {
            return Cell.Equals(other.Cell) && BatchIndex == other.BatchIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is PendingRecordKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Cell.GetHashCode() * 397) ^ BatchIndex;
            }
        }
    }

    private sealed class StreamingBakeContext : IDisposable
    {
        private const int FlushThreshold = 32768;

        private readonly string payloadAssetFolder;
        private readonly float spatialCellSize;
        private readonly int maxInstancesPerRecord;
        private readonly Dictionary<PendingRecordKey, List<Matrix4x4>> pendingRecords = new();
        private readonly Dictionary<CellKey, CellMetadata> cells = new();
        private int pendingMatrixCount;

        public StreamingBakeContext(string payloadAssetFolder, float spatialCellSize, int maxInstancesPerRecord)
        {
            this.payloadAssetFolder = NormalizeAssetPath(payloadAssetFolder);
            this.spatialCellSize = Mathf.Max(1f, spatialCellSize);
            this.maxInstancesPerRecord = Mathf.Clamp(maxInstancesPerRecord, 1, 1023);
        }

        public int InstanceCount { get; private set; }
        public int RecordCount { get; private set; }
        public int CellCount => cells.Count;
        public bool HasWorldBounds { get; private set; }
        public Bounds WorldBounds { get; private set; }

        public void AddInstance(int batchIndex, Bounds meshBounds, Matrix4x4 matrix)
        {
            CellKey cellKey = CellKey.FromMatrix(matrix, spatialCellSize);
            PendingRecordKey recordKey = new(cellKey, batchIndex);
            if (!pendingRecords.TryGetValue(recordKey, out List<Matrix4x4> matrices))
            {
                matrices = new List<Matrix4x4>(maxInstancesPerRecord);
                pendingRecords.Add(recordKey, matrices);
            }

            matrices.Add(matrix);
            pendingMatrixCount++;
            InstanceCount++;

            Bounds instanceBounds = TransformBounds(meshBounds, matrix);
            UpdateBounds(cellKey, instanceBounds);

            if (matrices.Count >= maxInstancesPerRecord)
            {
                FlushRecord(recordKey, matrices);
            }
        }

        public void FlushIfNeeded()
        {
            if (pendingMatrixCount >= FlushThreshold)
            {
                FlushPending();
            }
        }

        public void FlushPending()
        {
            foreach (KeyValuePair<PendingRecordKey, List<Matrix4x4>> pair in pendingRecords)
            {
                FlushRecord(pair.Key, pair.Value);
            }

            pendingRecords.Clear();
            pendingMatrixCount = 0;
        }

        public List<BakedGrassData.Cell> BuildCells(string finalPayloadFolder)
        {
            List<CellKey> sortedKeys = new(cells.Keys);
            sortedKeys.Sort();

            List<BakedGrassData.Cell> bakedCells = new(sortedKeys.Count);
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                CellKey key = sortedKeys[i];
                CellMetadata metadata = cells[key];
                if (metadata.RecordCount == 0 || metadata.InstanceCount == 0)
                {
                    continue;
                }

                string finalPayloadPath = NormalizeAssetPath(Path.Combine(finalPayloadFolder, GetCellFileName(key)));
                string resourcePath = GetResourcesPath(finalPayloadPath);

                BakedGrassData.Cell cell = new();
                cell.SetData(
                    key.X,
                    key.Z,
                    metadata.Bounds,
                    metadata.InstanceCount,
                    metadata.RecordCount,
                    null,
                    resourcePath);
                bakedCells.Add(cell);
            }

            return bakedCells;
        }

        public void Dispose()
        {
            pendingRecords.Clear();
            pendingMatrixCount = 0;
        }

        private void UpdateBounds(CellKey cellKey, Bounds instanceBounds)
        {
            CellMetadata metadata = GetOrCreateCell(cellKey);
            if (!metadata.HasBounds)
            {
                metadata.Bounds = instanceBounds;
                metadata.HasBounds = true;
            }
            else
            {
                metadata.Bounds.Encapsulate(instanceBounds);
            }

            metadata.InstanceCount++;

            if (!HasWorldBounds)
            {
                WorldBounds = instanceBounds;
                HasWorldBounds = true;
            }
            else
            {
                WorldBounds.Encapsulate(instanceBounds);
            }
        }

        private void FlushRecord(PendingRecordKey recordKey, List<Matrix4x4> matrices)
        {
            if (matrices == null || matrices.Count == 0)
            {
                return;
            }

            CellMetadata metadata = GetOrCreateCell(recordKey.Cell);
            string fullPath = AssetPathToFullPath(metadata.AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? string.Empty);

            bool writeHeader = !File.Exists(fullPath) || new FileInfo(fullPath).Length == 0;
            using FileStream stream = new(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new(stream);
            if (writeHeader)
            {
                BakedGrassPayload.WriteHeader(writer);
            }

            int count = matrices.Count;
            BakedGrassPayload.WriteRecord(writer, recordKey.BatchIndex, matrices);
            metadata.RecordCount++;
            RecordCount++;
            pendingMatrixCount -= count;
            matrices.Clear();
        }

        private CellMetadata GetOrCreateCell(CellKey cellKey)
        {
            if (cells.TryGetValue(cellKey, out CellMetadata metadata))
            {
                return metadata;
            }

            metadata = new CellMetadata
            {
                AssetPath = NormalizeAssetPath(Path.Combine(payloadAssetFolder, GetCellFileName(cellKey))),
            };
            cells.Add(cellKey, metadata);
            return metadata;
        }

        private static string GetCellFileName(CellKey cellKey)
        {
            return $"cell_x{cellKey.X}_z{cellKey.Z}{BakedGrassPayload.FileExtension}";
        }

        private sealed class CellMetadata
        {
            public string AssetPath;
            public Bounds Bounds;
            public bool HasBounds;
            public int InstanceCount;
            public int RecordCount;
        }
    }

    private sealed class BatchBuilder
    {
        public BatchBuilder(BatchKey key)
        {
            Key = key;
        }

        public BatchKey Key { get; }
        public List<Matrix4x4> Matrices { get; } = new();
    }
}
