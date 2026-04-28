using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Grass/Baked Grass Data", fileName = "BakedGrassData")]
public sealed class BakedGrassData : ScriptableObject
{
    [SerializeField] private List<Batch> batches = new();
    [SerializeField] private Bounds worldBounds = new(Vector3.zero, Vector3.one);
    [SerializeField] private int terrainCount;
    [SerializeField] private int prototypeCount;
    [SerializeField] private int chunkCount;
    [SerializeField] private int instanceCount;

    public IReadOnlyList<Batch> Batches => batches;
    public Bounds WorldBounds => worldBounds;
    public int TerrainCount => terrainCount;
    public int PrototypeCount => prototypeCount;
    public int ChunkCount => chunkCount;
    public int InstanceCount => instanceCount;
    public bool IsEmpty => batches == null || batches.Count == 0;

    [Serializable]
    public sealed class Batch
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField] private int layer;
        [SerializeField] private int subMeshIndex;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;
        [SerializeField] private List<Chunk> chunks = new();

        public Mesh Mesh => mesh;
        public Material Material => material;
        public int Layer => layer;
        public int SubMeshIndex => subMeshIndex;
        public ShadowCastingMode ShadowCastingMode => shadowCastingMode;
        public bool ReceiveShadows => receiveShadows;
        public IReadOnlyList<Chunk> Chunks => chunks;

#if UNITY_EDITOR
        public void SetData(
            Mesh newMesh,
            Material newMaterial,
            int newLayer,
            int newSubMeshIndex,
            ShadowCastingMode newShadowCastingMode,
            bool newReceiveShadows,
            List<Chunk> newChunks)
        {
            mesh = newMesh;
            material = newMaterial;
            layer = newLayer;
            subMeshIndex = newSubMeshIndex;
            shadowCastingMode = newShadowCastingMode;
            receiveShadows = newReceiveShadows;
            chunks = newChunks;
        }
#endif
    }

    [Serializable]
    public sealed class Chunk
    {
        [SerializeField] private Matrix4x4[] matrices = Array.Empty<Matrix4x4>();
        [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);

        public Matrix4x4[] Matrices => matrices;
        public Bounds Bounds => bounds;
        public int Count => matrices != null ? matrices.Length : 0;

#if UNITY_EDITOR
        public void SetData(Matrix4x4[] newMatrices, Bounds newBounds)
        {
            matrices = newMatrices;
            bounds = newBounds;
        }
#endif
    }

#if UNITY_EDITOR
    public void SetData(
        List<Batch> newBatches,
        Bounds newWorldBounds,
        int newTerrainCount,
        int newPrototypeCount,
        int newChunkCount,
        int newInstanceCount)
    {
        batches = newBatches;
        worldBounds = newWorldBounds;
        terrainCount = newTerrainCount;
        prototypeCount = newPrototypeCount;
        chunkCount = newChunkCount;
        instanceCount = newInstanceCount;
    }
#endif
}
