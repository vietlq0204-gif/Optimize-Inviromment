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

        Dictionary<BatchKey, BatchBuilder> builders = new();
        Dictionary<GameObject, List<PrototypeRendererInfo>> rendererCache = new();

        int terrainCount = 0;
        int prototypeCount = 0;

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
                    continue;
                }

                if (!rendererCache.TryGetValue(prototype.prototype, out List<PrototypeRendererInfo> rendererInfos))
                {
                    rendererInfos = CollectPrototypeRenderers(prototype.prototype);
                    rendererCache.Add(prototype.prototype, rendererInfos);
                }

                if (rendererInfos.Count == 0)
                {
                    continue;
                }

                float density = prototype.useDensityScaling
                    ? terrain.detailObjectDensity * QualitySettings.terrainDetailDensityScale
                    : terrain.detailObjectDensity;
                density *= renderer.DensityScale;
                if (density <= 0.0001f)
                {
                    continue;
                }

                bool bakedPrototype = false;
                for (int patchY = 0; patchY < patchCount; patchY++)
                {
                    for (int patchX = 0; patchX < patchCount; patchX++)
                    {
                        Bounds patchBounds;
                        DetailInstanceTransform[] detailTransforms =
                            terrainData.ComputeDetailInstanceTransforms(patchX, patchY, layer, density,
                                out patchBounds);
                        if (detailTransforms == null || detailTransforms.Length == 0)
                        {
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
                            Matrix4x4 instanceMatrix = terrainMatrix * Matrix4x4.TRS(localPosition, rotation, scale);

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

                                if (!builders.TryGetValue(key, out BatchBuilder builder))
                                {
                                    builder = new BatchBuilder(key);
                                    builders.Add(key, builder);
                                }

                                builder.Matrices.Add(instanceMatrix * rendererInfo.LocalMatrix);
                            }
                        }
                    }
                }

                if (bakedPrototype)
                {
                    prototypeCount++;
                }
            }
        }

        List<BakedGrassData.Batch> bakedBatches = new();
        Bounds worldBounds = new(Vector3.zero, Vector3.one);
        bool hasWorldBounds = false;
        int chunkCount = 0;
        int instanceCount = 0;

        foreach (BatchBuilder builder in builders.Values)
        {
            if (builder.Matrices.Count == 0)
            {
                continue;
            }

            List<BakedGrassData.Chunk> bakedChunks = BuildChunks(builder, ref hasWorldBounds, ref worldBounds,
                ref chunkCount, ref instanceCount);
            if (bakedChunks.Count == 0)
            {
                continue;
            }

            BakedGrassData.Batch bakedBatch = new();
            bakedBatch.SetData(
                builder.Key.Mesh,
                builder.Key.Material,
                builder.Key.Layer,
                builder.Key.SubMeshIndex,
                builder.Key.ShadowCastingMode,
                builder.Key.ReceiveShadows,
                bakedChunks);
            bakedBatches.Add(bakedBatch);
        }

        if (!hasWorldBounds)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one);
        }

        Undo.RecordObject(data, "Bake Grass Data");
        data.SetData(bakedBatches, worldBounds, terrainCount, prototypeCount, chunkCount, instanceCount);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"BakedGrassRenderer baked {instanceCount} grass instance(s), {chunkCount} chunk(s), " +
            $"{prototypeCount} prototype(s) from {terrainCount} terrain(s) into {AssetDatabase.GetAssetPath(data)}.",
            renderer);
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
        ref bool hasWorldBounds,
        ref Bounds worldBounds,
        ref int chunkCount,
        ref int instanceCount)
    {
        const int MaxInstancesPerChunk = 1023;
        List<BakedGrassData.Chunk> bakedChunks = new();

        for (int start = 0; start < builder.Matrices.Count; start += MaxInstancesPerChunk)
        {
            int count = Mathf.Min(MaxInstancesPerChunk, builder.Matrices.Count - start);
            Matrix4x4[] chunkMatrices = new Matrix4x4[count];
            builder.Matrices.CopyTo(start, chunkMatrices, 0, count);

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
            $"Chunks: {data.ChunkCount}\n" +
            $"Instances: {data.InstanceCount}",
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