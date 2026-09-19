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
    [SerializeField] private float spatialCellSize;
    [SerializeField] private int maxInstancesPerChunk = 1023;
    [SerializeField] private List<Cell> cells = new();

    public IReadOnlyList<Batch> Batches => batches;
    public Bounds WorldBounds => worldBounds;
    public int TerrainCount => terrainCount;
    public int PrototypeCount => prototypeCount;
    public int ChunkCount => chunkCount;
    public int InstanceCount => instanceCount;
    public float SpatialCellSize => spatialCellSize;
    public int MaxInstancesPerChunk => maxInstancesPerChunk;
    public IReadOnlyList<Cell> Cells => cells != null
        ? (IReadOnlyList<Cell>)cells
        : (IReadOnlyList<Cell>)Array.Empty<Cell>();
    public bool HasCellPayloads => cells != null && cells.Count > 0;
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

    [Serializable]
    public sealed class Cell
    {
        [SerializeField] private int x;
        [SerializeField] private int z;
        [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);
        [SerializeField] private int instanceCount;
        [SerializeField] private int recordCount;
        [SerializeField] private TextAsset payload;
        [SerializeField] private string payloadResourcePath;

        public int X => x;
        public int Z => z;
        public Bounds Bounds => bounds;
        public int InstanceCount => instanceCount;
        public int RecordCount => recordCount;
        public TextAsset Payload => payload;
        public string PayloadResourcePath => payloadResourcePath;

#if UNITY_EDITOR
        public void SetData(
            int newX,
            int newZ,
            Bounds newBounds,
            int newInstanceCount,
            int newRecordCount,
            TextAsset newPayload,
            string newPayloadResourcePath)
        {
            x = newX;
            z = newZ;
            bounds = newBounds;
            instanceCount = newInstanceCount;
            recordCount = newRecordCount;
            payload = newPayload;
            payloadResourcePath = newPayloadResourcePath;
        }
#endif
    }

#if UNITY_EDITOR
    public void SetData(
        List<Batch> newBatches,
        List<Cell> newCells,
        Bounds newWorldBounds,
        int newTerrainCount,
        int newPrototypeCount,
        int newChunkCount,
        int newInstanceCount,
        float newSpatialCellSize,
        int newMaxInstancesPerChunk)
    {
        batches = newBatches;
        cells = newCells ?? new List<Cell>();
        worldBounds = newWorldBounds;
        terrainCount = newTerrainCount;
        prototypeCount = newPrototypeCount;
        chunkCount = newChunkCount;
        instanceCount = newInstanceCount;
        spatialCellSize = newSpatialCellSize;
        maxInstancesPerChunk = newMaxInstancesPerChunk;
    }

    public void SetData(
        List<Batch> newBatches,
        Bounds newWorldBounds,
        int newTerrainCount,
        int newPrototypeCount,
        int newChunkCount,
        int newInstanceCount,
        float newSpatialCellSize,
        int newMaxInstancesPerChunk)
    {
        SetData(
            newBatches,
            new List<Cell>(),
            newWorldBounds,
            newTerrainCount,
            newPrototypeCount,
            newChunkCount,
            newInstanceCount,
            newSpatialCellSize,
            newMaxInstancesPerChunk);
    }
#endif
}
