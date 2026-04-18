using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TerrainGrassFrustumCuller : MonoBehaviour
{
    private enum RenderBackend
    {
        NativeTerrain,
        CustomInstanced,
    }

    private enum GrassLodBand
    {
        Hidden,
        Near,
        Mid,
        Far,
    }

    private enum GrassShadowMode
    {
        None,
        CastOnly,
        ReceiveOnly,
        Full,
    }

    [Serializable]
    private readonly struct GrassInstance
    {
        public GrassInstance(Matrix4x4 matrix, uint hash)
        {
            Matrix = matrix;
            Hash = hash;
        }

        public Matrix4x4 Matrix { get; }
        public uint Hash { get; }
    }

    [Serializable]
    private sealed class CellBatch
    {
        public readonly List<GrassInstance> Instances = new();
    }

    private sealed class GrassCell
    {
        public Bounds Bounds;
        public CellBatch[] Batches = Array.Empty<CellBatch>();
        public GrassLodBand LodBand = GrassLodBand.Hidden;
        public GrassShadowMode ShadowMode = GrassShadowMode.None;
    }

    private readonly struct RenderPass
    {
        public RenderPass(Mesh mesh, int subMeshIndex, Material material, bool shaderReceivesShadows)
        {
            Mesh = mesh;
            SubMeshIndex = subMeshIndex;
            Material = material;
            ShaderReceivesShadows = shaderReceivesShadows;
        }

        public Mesh Mesh { get; }
        public int SubMeshIndex { get; }
        public Material Material { get; }
        public bool ShaderReceivesShadows { get; }
    }

    private sealed class PrototypeInfo
    {
        public int DetailLayerIndex;
        public string Name = string.Empty;
        public Matrix4x4 PrototypeLocalMatrix = Matrix4x4.identity;
        public RenderPass[] Passes = Array.Empty<RenderPass>();
    }

    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private Camera targetCamera;

    [Header("Mode")]
    [SerializeField] private RenderBackend renderBackend = RenderBackend.CustomInstanced;

    [Header("Culling")]
    [SerializeField] private bool enableDistanceCulling = true;
    [SerializeField] private bool enableFrustumCulling = true;
    [SerializeField] private bool keepShadowsWhenFrustumCulled;
    [SerializeField] private bool keepShadowsWhenDistanceCulled;
    [SerializeField] private float shadowOnlyDistance;
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private float maxRenderDistance = 60f;
    [SerializeField] private bool clampMaxRenderDistanceToTerrain = true;
    [SerializeField] private int visibilityRefreshInterval = 3;

    [Header("LOD")]
    [SerializeField] private float nearDistance = 12f;
    [SerializeField] private float midDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float midDensity = 0.5f;
    [SerializeField, Range(0f, 1f)] private float farDensity = 0.25f;
    [SerializeField] private float castShadowDistance = 10f;
    [SerializeField] private float receiveShadowDistance = 16f;

    [Header("Build")]
    [SerializeField] private bool buildAsynchronously = true;
    [SerializeField] private int buildPatchesPerFrame = 8;
    [SerializeField] private int maxTotalInstances = 200000;

    [Header("Runtime")]
    [SerializeField] private bool suppressBuiltInTerrainDetails = true;
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool receiveShadows = true;
    [SerializeField] private bool logBuildStats = true;

    private readonly List<GrassCell> cells = new();
    private readonly List<PrototypeInfo> prototypes = new();
    private readonly List<GrassCell> visibleCells = new();
    private readonly Matrix4x4[] drawBuffer = new Matrix4x4[511];
    private MaterialPropertyBlock sharedMaterialPropertyBlock;

    private bool isBuilt;
    private bool isBuilding;
    private bool builtFromTerrainDetailData;
    private float originalDetailObjectDistance = -1f;
    private Coroutine buildRoutine;

    private static readonly int ReceiveShadowsPropertyId = Shader.PropertyToID("_ReceiveShadows");

    private void Reset()
    {
        terrain = GetComponent<Terrain>();
        targetCamera = Camera.main;
    }

    private void Awake()
    {
        EnsureMaterialPropertyBlock();

        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void EnsureMaterialPropertyBlock()
    {
        if (sharedMaterialPropertyBlock == null)
        {
            sharedMaterialPropertyBlock = new MaterialPropertyBlock();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (renderBackend == RenderBackend.NativeTerrain)
        {
            ApplyBuiltInTerrainDetailSuppression(false);
            return;
        }

        StartBuild();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        isBuilding = false;
        isBuilt = false;
        visibleCells.Clear();
        ApplyBuiltInTerrainDetailSuppression(false);
    }

    private void LateUpdate()
    {
        if (renderBackend == RenderBackend.NativeTerrain)
        {
            return;
        }

        if (!isBuilt && !isBuilding)
        {
            StartBuild();
        }

        if (!isBuilt || isBuilding)
        {
            return;
        }

        if (visibilityRefreshInterval <= 1 || Time.frameCount % visibilityRefreshInterval == 0)
        {
            UpdateVisibility();
        }

        RenderVisibleCells();
    }

    [ContextMenu("Rebuild Grass Data")]
    public void Build()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Rebuild Grass Data only runs in Play Mode.", this);
            return;
        }

        if (renderBackend == RenderBackend.NativeTerrain)
        {
            return;
        }

        StartBuild();
    }

    private void StartBuild()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        buildRoutine = StartCoroutine(BuildRoutine());
    }

    private System.Collections.IEnumerator BuildRoutine()
    {
        isBuilt = false;
        isBuilding = true;

        if (buildAsynchronously)
        {
            yield return null;
        }

        yield return BuildInternalAsync();

        buildRoutine = null;
        isBuilding = false;
    }

    private System.Collections.IEnumerator BuildInternalAsync()
    {
        isBuilt = false;
        builtFromTerrainDetailData = false;
        cells.Clear();
        prototypes.Clear();
        visibleCells.Clear();

        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogWarning("TerrainGrassFrustumCuller requires a Terrain reference.", this);
            yield break;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            Debug.LogWarning("TerrainGrassFrustumCuller could not find TerrainData.", this);
            yield break;
        }

        if (originalDetailObjectDistance < 0f)
        {
            originalDetailObjectDistance = terrain.detailObjectDistance;
        }

        CollectSupportedPrototypes(terrainData);
        if (prototypes.Count == 0)
        {
            Debug.LogWarning("TerrainGrassFrustumCuller found no mesh-based terrain detail prototypes to render.", this);
            yield break;
        }

        CreateCells(terrainData);
        yield return PopulateCellsFromTerrainDetailsAsync(terrainData);
        UpdateVisibility();

        isBuilt = true;
        ApplyBuiltInTerrainDetailSuppression(true);

        if (logBuildStats)
        {
            int instanceCount = 0;
            foreach (GrassCell cell in cells)
            {
                for (int i = 0; i < cell.Batches.Length; i++)
                {
                    instanceCount += cell.Batches[i].Instances.Count;
                }
            }

            Debug.Log(
                $"TerrainGrassFrustumCuller built {instanceCount} grass instances into {cells.Count} cells using {prototypes.Count} terrain detail prototypes.",
                this);
        }
    }

    private void CollectSupportedPrototypes(TerrainData terrainData)
    {
        DetailPrototype[] detailPrototypes = terrainData.detailPrototypes;
        for (int layerIndex = 0; layerIndex < detailPrototypes.Length; layerIndex++)
        {
            DetailPrototype detailPrototype = detailPrototypes[layerIndex];
            GameObject prototypeObject = detailPrototype.prototype;
            if (prototypeObject == null)
            {
                continue;
            }

            MeshFilter meshFilter = prototypeObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = prototypeObject.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Material[] sharedMaterials = meshRenderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                continue;
            }

            List<RenderPass> passes = new();
            int subMeshCount = Mathf.Min(meshFilter.sharedMesh.subMeshCount, sharedMaterials.Length);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = sharedMaterials[subMeshIndex];
                if (material == null)
                {
                    continue;
                }

                material.enableInstancing = true;
                bool shaderReceivesShadows = !material.HasProperty(ReceiveShadowsPropertyId) ||
                    material.GetFloat(ReceiveShadowsPropertyId) > 0.5f;
                passes.Add(new RenderPass(meshFilter.sharedMesh, subMeshIndex, material, shaderReceivesShadows));
            }

            if (passes.Count == 0)
            {
                continue;
            }

            prototypes.Add(new PrototypeInfo
            {
                DetailLayerIndex = layerIndex,
                Name = prototypeObject.name,
                PrototypeLocalMatrix = Matrix4x4.TRS(
                    prototypeObject.transform.localPosition,
                    prototypeObject.transform.localRotation,
                    prototypeObject.transform.localScale),
                Passes = passes.ToArray(),
            });
        }
    }

    private void CreateCells(TerrainData terrainData)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        float clampedCellSize = Mathf.Max(1f, cellSize);

        int cellCountX = Mathf.CeilToInt(terrainSize.x / clampedCellSize);
        int cellCountZ = Mathf.CeilToInt(terrainSize.z / clampedCellSize);
        float boundsHeight = Mathf.Max(terrainSize.y + 10f, 20f);

        cells.Capacity = cellCountX * cellCountZ;

        for (int z = 0; z < cellCountZ; z++)
        {
            for (int x = 0; x < cellCountX; x++)
            {
                float minX = terrainPosition.x + x * clampedCellSize;
                float minZ = terrainPosition.z + z * clampedCellSize;
                float sizeX = Mathf.Min(clampedCellSize, terrainSize.x - x * clampedCellSize);
                float sizeZ = Mathf.Min(clampedCellSize, terrainSize.z - z * clampedCellSize);

                CellBatch[] batches = new CellBatch[prototypes.Count];
                for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
                {
                    batches[prototypeIndex] = new CellBatch();
                }

                cells.Add(new GrassCell
                {
                    Bounds = new Bounds(
                        new Vector3(minX + sizeX * 0.5f, terrainPosition.y + boundsHeight * 0.5f, minZ + sizeZ * 0.5f),
                        new Vector3(sizeX, boundsHeight, sizeZ)),
                    Batches = batches,
                });
            }
        }
    }

    private System.Collections.IEnumerator PopulateCellsFromTerrainDetailsAsync(TerrainData terrainData)
    {
        int patchCount = terrainData.detailPatchCount;
        if (patchCount <= 0)
        {
            yield break;
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        float clampedCellSize = Mathf.Max(1f, cellSize);
        int clampedBuildPatchesPerFrame = buildAsynchronously ? Mathf.Max(1, buildPatchesPerFrame) : int.MaxValue;
        int clampedMaxTotalInstances = Mathf.Max(1000, maxTotalInstances);
        int cellCountX = Mathf.CeilToInt(terrainSize.x / clampedCellSize);
        int cellCountZ = Mathf.CeilToInt(terrainSize.z / clampedCellSize);
        int processedPatches = 0;
        int totalInstances = 0;
        bool reachedInstanceCap = false;
        float detailDensity = terrain.detailObjectDensity;

        for (int patchZ = 0; patchZ < patchCount; patchZ++)
        {
            for (int patchX = 0; patchX < patchCount; patchX++)
            {
                for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
                {
                    PrototypeInfo prototype = prototypes[prototypeIndex];
                    DetailInstanceTransform[] detailTransforms = terrainData.ComputeDetailInstanceTransforms(
                        patchX,
                        patchZ,
                        prototype.DetailLayerIndex,
                        detailDensity,
                        out Bounds localPatchBounds);

                    if (detailTransforms == null || detailTransforms.Length == 0)
                    {
                        continue;
                    }

                    Bounds worldPatchBounds = new Bounds(localPatchBounds.center + terrainPosition, localPatchBounds.size);

                    for (int transformIndex = 0; transformIndex < detailTransforms.Length; transformIndex++)
                    {
                        if (totalInstances >= clampedMaxTotalInstances)
                        {
                            reachedInstanceCap = true;
                            break;
                        }

                        Matrix4x4 matrix = CreateInstanceMatrix(prototype, terrainPosition, detailTransforms[transformIndex]);
                        Vector3 position = ExtractPosition(matrix);
                        int cellX = Mathf.Clamp((int)((position.x - terrainPosition.x) / clampedCellSize), 0, Mathf.Max(0, cellCountX - 1));
                        int cellZ = Mathf.Clamp((int)((position.z - terrainPosition.z) / clampedCellSize), 0, Mathf.Max(0, cellCountZ - 1));
                        int cellIndex = cellZ * cellCountX + cellX;

                        cells[cellIndex].Bounds.Encapsulate(worldPatchBounds);
                        cells[cellIndex].Batches[prototypeIndex].Instances.Add(new GrassInstance(matrix, StableHash(position)));
                        totalInstances++;
                    }

                    if (reachedInstanceCap)
                    {
                        break;
                    }
                }

                if (reachedInstanceCap)
                {
                    break;
                }

                processedPatches++;
                if (processedPatches >= clampedBuildPatchesPerFrame)
                {
                    processedPatches = 0;
                    yield return null;
                }
            }

            if (reachedInstanceCap)
            {
                break;
            }
        }

        if (reachedInstanceCap)
        {
            Debug.LogWarning(
                $"TerrainGrassFrustumCuller stopped building at {totalInstances} instances to keep Play Mode responsive. Increase Max Total Instances if you need a full terrain match.",
                this);
        }

        builtFromTerrainDetailData = totalInstances > 0;
    }

    private Matrix4x4 CreateInstanceMatrix(
        PrototypeInfo prototype,
        Vector3 terrainPosition,
        DetailInstanceTransform detailTransform)
    {
        Matrix4x4 terrainInstanceMatrix = Matrix4x4.TRS(
            terrainPosition + new Vector3(detailTransform.posX, detailTransform.posY, detailTransform.posZ),
            Quaternion.Euler(0f, detailTransform.rotationY * Mathf.Rad2Deg, 0f),
            new Vector3(detailTransform.scaleXZ, detailTransform.scaleY, detailTransform.scaleXZ));

        return terrainInstanceMatrix * prototype.PrototypeLocalMatrix;
    }

    private void UpdateVisibility()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        visibleCells.Clear();

        float effectiveMaxRenderDistance = GetEffectiveMaxRenderDistance();
        float clampedNearDistance = Mathf.Max(0f, nearDistance);
        float clampedMidDistance = Mathf.Max(clampedNearDistance, midDistance);
        if (!float.IsPositiveInfinity(effectiveMaxRenderDistance))
        {
            clampedNearDistance = Mathf.Min(clampedNearDistance, effectiveMaxRenderDistance);
            clampedMidDistance = Mathf.Min(clampedMidDistance, effectiveMaxRenderDistance);
        }

        float nearDistanceSqr = clampedNearDistance * clampedNearDistance;
        float midDistanceSqr = clampedMidDistance * clampedMidDistance;
        float maxDistanceSqr = float.IsPositiveInfinity(effectiveMaxRenderDistance)
            ? float.PositiveInfinity
            : effectiveMaxRenderDistance * effectiveMaxRenderDistance;
        float castShadowDistanceSqr = castShadows && castShadowDistance > 0f
            ? castShadowDistance * castShadowDistance
            : -1f;
        float receiveShadowDistanceSqr = receiveShadows && receiveShadowDistance > 0f
            ? receiveShadowDistance * receiveShadowDistance
            : -1f;

        if (targetCamera == null)
        {
            foreach (GrassCell cell in cells)
            {
                cell.LodBand = GrassLodBand.Near;
                cell.ShadowMode = ClassifyShadowMode(castShadows, receiveShadows);
                visibleCells.Add(cell);
            }

            return;
        }

        Plane[] frustumPlanes = enableFrustumCulling ? GeometryUtility.CalculateFrustumPlanes(targetCamera) : null;
        Vector3 cameraPosition = targetCamera.transform.position;

        foreach (GrassCell cell in cells)
        {
            float cameraDistanceSqr = cell.Bounds.SqrDistance(cameraPosition);
            bool withinDistance = !enableDistanceCulling || cameraDistanceSqr <= maxDistanceSqr;
            bool isInsideFrustum = !enableFrustumCulling ||
                frustumPlanes == null ||
                GeometryUtility.TestPlanesAABB(frustumPlanes, cell.Bounds);
            if (!withinDistance || !isInsideFrustum)
            {
                cell.LodBand = GrassLodBand.Hidden;
                cell.ShadowMode = GrassShadowMode.None;
                continue;
            }

            cell.LodBand = ClassifyLodBand(cameraDistanceSqr, nearDistanceSqr, midDistanceSqr);
            if (GetDensityRatio(cell.LodBand) <= 0f)
            {
                cell.LodBand = GrassLodBand.Hidden;
                cell.ShadowMode = GrassShadowMode.None;
                continue;
            }

            bool shouldCastShadows = castShadowDistanceSqr >= 0f && cameraDistanceSqr <= castShadowDistanceSqr;
            bool shouldReceiveShadows = receiveShadowDistanceSqr >= 0f && cameraDistanceSqr <= receiveShadowDistanceSqr;
            cell.ShadowMode = ClassifyShadowMode(shouldCastShadows, shouldReceiveShadows);
            visibleCells.Add(cell);
        }
    }

    private void RenderVisibleCells()
    {
        if (!builtFromTerrainDetailData || visibleCells.Count == 0)
        {
            return;
        }

        for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
        {
            RenderPass[] passes = prototypes[prototypeIndex].Passes;
            if (passes.Length == 0)
            {
                continue;
            }

            for (int passIndex = 0; passIndex < passes.Length; passIndex++)
            {
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Near, GrassShadowMode.Full);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Near, GrassShadowMode.CastOnly);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Near, GrassShadowMode.ReceiveOnly);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Near, GrassShadowMode.None);

                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Mid, GrassShadowMode.Full);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Mid, GrassShadowMode.CastOnly);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Mid, GrassShadowMode.ReceiveOnly);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Mid, GrassShadowMode.None);

                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Far, GrassShadowMode.Full);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Far, GrassShadowMode.CastOnly);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Far, GrassShadowMode.ReceiveOnly);
                RenderVisiblePrototypePass(prototypeIndex, passes[passIndex], GrassLodBand.Far, GrassShadowMode.None);
            }
        }
    }

    private void RenderVisiblePrototypePass(
        int prototypeIndex,
        RenderPass renderPass,
        GrassLodBand lodBand,
        GrassShadowMode shadowMode)
    {
        float densityRatio = GetDensityRatio(lodBand);
        if (densityRatio <= 0f)
        {
            return;
        }

        int bufferedCount = 0;
        bool hasBufferedBounds = false;
        Bounds bufferedBounds = default;

        for (int cellIndex = 0; cellIndex < visibleCells.Count; cellIndex++)
        {
            GrassCell cell = visibleCells[cellIndex];
            if (cell.LodBand != lodBand || cell.ShadowMode != shadowMode)
            {
                continue;
            }

            List<GrassInstance> instances = cell.Batches[prototypeIndex].Instances;
            if (instances.Count == 0)
            {
                continue;
            }

            bool hasCellBoundsInBuffer = false;
            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                GrassInstance instance = instances[instanceIndex];
                if (!KeepByDensity(instance.Hash, densityRatio))
                {
                    continue;
                }

                if (!hasCellBoundsInBuffer)
                {
                    if (hasBufferedBounds)
                    {
                        bufferedBounds.Encapsulate(cell.Bounds);
                    }
                    else
                    {
                        bufferedBounds = cell.Bounds;
                        hasBufferedBounds = true;
                    }

                    hasCellBoundsInBuffer = true;
                }

                drawBuffer[bufferedCount] = instance.Matrix;
                bufferedCount++;

                if (bufferedCount == drawBuffer.Length)
                {
                    DrawBufferedInstances(renderPass, bufferedBounds, bufferedCount, shadowMode);
                    bufferedCount = 0;
                    hasBufferedBounds = false;
                    hasCellBoundsInBuffer = false;
                }
            }
        }

        if (bufferedCount > 0 && hasBufferedBounds)
        {
            DrawBufferedInstances(renderPass, bufferedBounds, bufferedCount, shadowMode);
        }
    }

    private void DrawBufferedInstances(
        RenderPass renderPass,
        Bounds worldBounds,
        int count,
        GrassShadowMode shadowMode)
    {
        if (renderPass.Material == null || renderPass.Mesh == null)
        {
            return;
        }

        EnsureMaterialPropertyBlock();
        sharedMaterialPropertyBlock.Clear();

        bool shouldReceiveShadows = shadowMode == GrassShadowMode.Full || shadowMode == GrassShadowMode.ReceiveOnly;
        if (renderPass.Material.HasProperty(ReceiveShadowsPropertyId))
        {
            float effectiveReceiveShadows = shouldReceiveShadows && renderPass.ShaderReceivesShadows ? 1f : 0f;
            sharedMaterialPropertyBlock.SetFloat(ReceiveShadowsPropertyId, effectiveReceiveShadows);
        }

        RenderParams renderParams = new RenderParams(renderPass.Material)
        {
            worldBounds = worldBounds,
            shadowCastingMode = shadowMode == GrassShadowMode.Full || shadowMode == GrassShadowMode.CastOnly
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off,
            receiveShadows = shouldReceiveShadows,
            matProps = sharedMaterialPropertyBlock,
        };

        Graphics.RenderMeshInstanced(renderParams, renderPass.Mesh, renderPass.SubMeshIndex, drawBuffer, count);
    }

    private float GetEffectiveMaxRenderDistance()
    {
        float configuredDistance = maxRenderDistance > 0f ? maxRenderDistance : float.PositiveInfinity;
        if (!clampMaxRenderDistanceToTerrain || terrain == null)
        {
            return configuredDistance;
        }

        float terrainDistance = originalDetailObjectDistance > 0f
            ? originalDetailObjectDistance
            : terrain.detailObjectDistance;
        if (terrainDistance <= 0f)
        {
            return configuredDistance;
        }

        return Mathf.Min(configuredDistance, terrainDistance);
    }

    private float GetDensityRatio(GrassLodBand lodBand)
    {
        return lodBand switch
        {
            GrassLodBand.Near => 1f,
            GrassLodBand.Mid => Mathf.Clamp01(midDensity),
            GrassLodBand.Far => Mathf.Clamp01(farDensity),
            _ => 0f,
        };
    }

    private static GrassLodBand ClassifyLodBand(float cameraDistanceSqr, float nearDistanceSqr, float midDistanceSqr)
    {
        if (cameraDistanceSqr <= nearDistanceSqr)
        {
            return GrassLodBand.Near;
        }

        if (cameraDistanceSqr <= midDistanceSqr)
        {
            return GrassLodBand.Mid;
        }

        return GrassLodBand.Far;
    }

    private static GrassShadowMode ClassifyShadowMode(bool shouldCastShadows, bool shouldReceiveShadows)
    {
        if (shouldCastShadows)
        {
            return shouldReceiveShadows ? GrassShadowMode.Full : GrassShadowMode.CastOnly;
        }

        return shouldReceiveShadows ? GrassShadowMode.ReceiveOnly : GrassShadowMode.None;
    }

    private static bool KeepByDensity(uint hash, float densityRatio)
    {
        if (densityRatio >= 0.9999f)
        {
            return true;
        }

        if (densityRatio <= 0f)
        {
            return false;
        }

        return (hash & 0xFFFF) < densityRatio * 65535f;
    }

    private static uint StableHash(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x * 10f);
        int z = Mathf.RoundToInt(position.z * 10f);
        unchecked
        {
            return (uint)(x * 73856093 ^ z * 19349663);
        }
    }

    private static Vector3 ExtractPosition(Matrix4x4 matrix)
    {
        return new Vector3(matrix.m03, matrix.m13, matrix.m23);
    }

    private void ApplyBuiltInTerrainDetailSuppression(bool suppress)
    {
        if (!Application.isPlaying || !suppressBuiltInTerrainDetails || terrain == null)
        {
            return;
        }

        if (suppress)
        {
            if (originalDetailObjectDistance < 0f)
            {
                originalDetailObjectDistance = terrain.detailObjectDistance;
            }

            terrain.detailObjectDistance = 0f;
            return;
        }

        if (originalDetailObjectDistance >= 0f)
        {
            terrain.detailObjectDistance = originalDetailObjectDistance;
        }
    }
}
