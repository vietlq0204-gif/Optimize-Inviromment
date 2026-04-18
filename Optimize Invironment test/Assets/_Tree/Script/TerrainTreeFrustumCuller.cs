using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TerrainTreeFrustumCuller : MonoBehaviour
{
    private enum RenderBackend
    {
        NativeTerrain,
        CustomInstanced,
    }

    private enum TreeCellRenderMode
    {
        Hidden,
        Near,
        Mid,
        ShadowOnly,
    }

    [Serializable]
    private sealed class CellBatch
    {
        public readonly List<Matrix4x4> Matrices = new();
    }

    private sealed class TreeCell
    {
        public Bounds Bounds;
        public CellBatch[] Batches = Array.Empty<CellBatch>();
        public TreeCellRenderMode RenderMode = TreeCellRenderMode.Hidden;
    }

    private readonly struct RenderPass
    {
        public RenderPass(
            Mesh mesh,
            int subMeshIndex,
            Material material,
            Matrix4x4 rendererLocalMatrix,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows)
        {
            Mesh = mesh;
            SubMeshIndex = subMeshIndex;
            Material = material;
            RendererLocalMatrix = rendererLocalMatrix;
            ShadowCastingMode = shadowCastingMode;
            ReceiveShadows = receiveShadows;
        }

        public Mesh Mesh { get; }
        public int SubMeshIndex { get; }
        public Material Material { get; }
        public Matrix4x4 RendererLocalMatrix { get; }
        public ShadowCastingMode ShadowCastingMode { get; }
        public bool ReceiveShadows { get; }
    }

    private sealed class PrototypeInfo
    {
        public string Name = string.Empty;
        public Matrix4x4 PrototypeLocalMatrix = Matrix4x4.identity;
        public RenderPass[] NearPasses = Array.Empty<RenderPass>();
        public RenderPass[] MidPasses = Array.Empty<RenderPass>();
    }

    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainCollider terrainCollider;
    [SerializeField] private Transform activationTarget;
    [SerializeField] private Camera targetCamera;

    [Header("Mode")]
    [SerializeField] private RenderBackend renderBackend = RenderBackend.CustomInstanced;

    [Header("Culling")]
    [SerializeField] private bool enableDistanceCulling = true;
    [SerializeField] private bool enableFrustumCulling = true;
    [SerializeField] private bool keepShadowsWhenFrustumCulled;
    [SerializeField] private bool keepShadowsWhenDistanceCulled;
    [SerializeField] private float shadowOnlyDistance = 80f;
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private float activationDistance = 80f;
    [SerializeField] private float unloadDistance = 100f;
    [SerializeField] private int refreshInterval = 5;
    [SerializeField] private int maxCellLoadsPerRefresh = 1;

    [Header("LOD")]
    [SerializeField] private float nearLodDistance = 25f;

    [Header("Runtime")]
    [SerializeField] private bool suppressBuiltInTerrainTrees = true;
    [SerializeField] private bool suppressBuiltInTerrainTreeColliders = true;
    [SerializeField] private bool logBuildStats = true;

    private readonly List<TreeCell> cells = new();
    private readonly List<TreeCell> renderableCells = new();
    private readonly List<PrototypeInfo> prototypes = new();
    private readonly Matrix4x4[] drawBuffer = new Matrix4x4[511];

    private bool isBuilt;
    private float originalTreeDistance = -1f;
    private bool originalTreeCollidersEnabled;
    private bool didCaptureTreeColliderState;
    private PropertyInfo cachedEnableTreeCollidersProperty;
    private FieldInfo cachedEnableTreeCollidersField;

    private void Reset()
    {
        terrain = GetComponent<Terrain>();
        terrainCollider = GetComponent<TerrainCollider>();
        targetCamera = Camera.main;
    }

    private void Awake()
    {
        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        if (terrainCollider == null)
        {
            terrainCollider = GetComponent<TerrainCollider>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (activationTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            activationTarget = player != null ? player.transform : targetCamera != null ? targetCamera.transform : transform;
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
            ApplyBuiltInTerrainSuppression(false);
            return;
        }

        Build();
        ApplyBuiltInTerrainSuppression(true);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        renderableCells.Clear();
        cells.Clear();
        prototypes.Clear();
        isBuilt = false;
        ApplyBuiltInTerrainSuppression(false);
    }

    private void LateUpdate()
    {
        if (renderBackend == RenderBackend.NativeTerrain)
        {
            return;
        }

        if (!isBuilt)
        {
            Build();
        }

        if (!isBuilt)
        {
            return;
        }

        if (refreshInterval <= 1 || Time.frameCount % refreshInterval == 0)
        {
            RefreshRenderableCells();
        }

        RenderVisibleCells();
    }

    [ContextMenu("Rebuild Tree Data")]
    public void Build()
    {
        if (renderBackend == RenderBackend.NativeTerrain)
        {
            return;
        }

        cells.Clear();
        renderableCells.Clear();
        prototypes.Clear();
        isBuilt = false;

        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        if (terrainCollider == null)
        {
            terrainCollider = GetComponent<TerrainCollider>();
        }

        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning("TerrainTreeFrustumCuller requires a Terrain with valid TerrainData.", this);
            return;
        }

        if (originalTreeDistance < 0f)
        {
            originalTreeDistance = terrain.treeDistance;
        }

        TerrainData terrainData = terrain.terrainData;
        int[] prototypeRemap = CollectPrototypes(terrainData);
        if (prototypes.Count == 0)
        {
            Debug.LogWarning("TerrainTreeFrustumCuller found no valid tree prefabs in TerrainData.treePrototypes.", this);
            return;
        }

        CreateCells(terrainData);
        PopulateCells(terrainData, prototypeRemap);
        cells.RemoveAll(IsCellEmpty);
        RefreshRenderableCells();

        isBuilt = cells.Count > 0;
        ApplyBuiltInTerrainSuppression(true);

        if (logBuildStats)
        {
            int treeCount = 0;
            foreach (TreeCell cell in cells)
            {
                for (int prototypeIndex = 0; prototypeIndex < cell.Batches.Length; prototypeIndex++)
                {
                    treeCount += cell.Batches[prototypeIndex].Matrices.Count;
                }
            }

            Debug.Log(
                $"TerrainTreeFrustumCuller built {treeCount} tree instances into {cells.Count} active cells using {prototypes.Count} tree prototypes.",
                this);
        }
    }

    private int[] CollectPrototypes(TerrainData terrainData)
    {
        TreePrototype[] treePrototypes = terrainData.treePrototypes;
        int[] remap = new int[treePrototypes.Length];
        for (int i = 0; i < remap.Length; i++)
        {
            remap[i] = -1;
        }

        for (int terrainPrototypeIndex = 0; terrainPrototypeIndex < treePrototypes.Length; terrainPrototypeIndex++)
        {
            GameObject prefab = treePrototypes[terrainPrototypeIndex].prefab;
            if (prefab == null)
            {
                continue;
            }

            CollectRenderPassSets(prefab, out RenderPass[] nearPasses, out RenderPass[] midPasses);
            if (nearPasses.Length == 0)
            {
                continue;
            }

            remap[terrainPrototypeIndex] = prototypes.Count;
            prototypes.Add(new PrototypeInfo
            {
                Name = prefab.name,
                PrototypeLocalMatrix = Matrix4x4.TRS(
                    prefab.transform.localPosition,
                    prefab.transform.localRotation,
                    prefab.transform.localScale),
                NearPasses = nearPasses,
                MidPasses = midPasses.Length > 0 ? midPasses : nearPasses,
            });
        }

        return remap;
    }

    private static void CollectRenderPassSets(
        GameObject prefab,
        out RenderPass[] nearPasses,
        out RenderPass[] midPasses)
    {
        List<RenderPass> near = new();
        List<RenderPass> mid = new();
        LODGroup lodGroup = prefab.GetComponent<LODGroup>();
        if (lodGroup != null)
        {
            LOD[] lods = lodGroup.GetLODs();
            near = CollectRenderPasses(prefab, GetLodRenderers(lods, 0));

            for (int lodIndex = lods.Length - 1; lodIndex >= 1; lodIndex--)
            {
                List<RenderPass> candidate = CollectRenderPasses(prefab, GetLodRenderers(lods, lodIndex));
                if (candidate.Count > 0)
                {
                    mid = candidate;
                    break;
                }
            }
        }

        if (near.Count == 0)
        {
            near = CollectRenderPasses(prefab, prefab.GetComponentsInChildren<Renderer>(true));
        }

        if (mid.Count == 0)
        {
            mid = near;
        }

        nearPasses = near.ToArray();
        midPasses = mid.ToArray();
    }

    private static Renderer[] GetLodRenderers(LOD[] lods, int index)
    {
        if (lods == null || index < 0 || index >= lods.Length || lods[index].renderers == null)
        {
            return Array.Empty<Renderer>();
        }

        return lods[index].renderers;
    }

    private static List<RenderPass> CollectRenderPasses(GameObject prefab, Renderer[] candidateRenderers)
    {
        List<RenderPass> passes = new();
        Transform prefabTransform = prefab.transform;
        for (int rendererIndex = 0; rendererIndex < candidateRenderers.Length; rendererIndex++)
        {
            if (candidateRenderers[rendererIndex] is not MeshRenderer meshRenderer)
            {
                continue;
            }

            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Material[] sharedMaterials = meshRenderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                continue;
            }

            Matrix4x4 rendererLocalMatrix = prefabTransform.worldToLocalMatrix * meshRenderer.transform.localToWorldMatrix;
            int subMeshCount = Mathf.Min(meshFilter.sharedMesh.subMeshCount, sharedMaterials.Length);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = sharedMaterials[subMeshIndex];
                if (material == null)
                {
                    continue;
                }

                material.enableInstancing = true;
                passes.Add(new RenderPass(
                    meshFilter.sharedMesh,
                    subMeshIndex,
                    material,
                    rendererLocalMatrix,
                    meshRenderer.shadowCastingMode,
                    meshRenderer.receiveShadows));
            }
        }

        return passes;
    }

    private void CreateCells(TerrainData terrainData)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        float clampedCellSize = Mathf.Max(4f, cellSize);
        int cellCountX = Mathf.CeilToInt(terrainSize.x / clampedCellSize);
        int cellCountZ = Mathf.CeilToInt(terrainSize.z / clampedCellSize);
        float boundsHeight = Mathf.Max(terrainSize.y + 20f, 40f);

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

                cells.Add(new TreeCell
                {
                    Bounds = new Bounds(
                        new Vector3(minX + sizeX * 0.5f, terrainPosition.y + boundsHeight * 0.5f, minZ + sizeZ * 0.5f),
                        new Vector3(sizeX, boundsHeight, sizeZ)),
                    Batches = batches,
                });
            }
        }
    }

    private void PopulateCells(TerrainData terrainData, int[] prototypeRemap)
    {
        TreeInstance[] treeInstances = terrainData.treeInstances;
        if (treeInstances == null || treeInstances.Length == 0)
        {
            return;
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        float clampedCellSize = Mathf.Max(4f, cellSize);
        int cellCountX = Mathf.CeilToInt(terrainSize.x / clampedCellSize);
        int cellCountZ = Mathf.CeilToInt(terrainSize.z / clampedCellSize);

        for (int i = 0; i < treeInstances.Length; i++)
        {
            TreeInstance tree = treeInstances[i];
            if (tree.prototypeIndex < 0 || tree.prototypeIndex >= prototypeRemap.Length)
            {
                continue;
            }

            int prototypeIndex = prototypeRemap[tree.prototypeIndex];
            if (prototypeIndex < 0 || prototypeIndex >= prototypes.Count)
            {
                continue;
            }

            Matrix4x4 treeMatrix = Matrix4x4.TRS(
                terrainPosition + Vector3.Scale(tree.position, terrainSize),
                Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f),
                new Vector3(tree.widthScale, tree.heightScale, tree.widthScale)) * prototypes[prototypeIndex].PrototypeLocalMatrix;

            Vector3 position = ExtractPosition(treeMatrix);
            Vector3 scale = ExtractScale(treeMatrix);
            int cellX = Mathf.Clamp((int)((position.x - terrainPosition.x) / clampedCellSize), 0, Mathf.Max(0, cellCountX - 1));
            int cellZ = Mathf.Clamp((int)((position.z - terrainPosition.z) / clampedCellSize), 0, Mathf.Max(0, cellCountZ - 1));
            int cellIndex = cellZ * cellCountX + cellX;

            TreeCell cell = cells[cellIndex];
            cell.Batches[prototypeIndex].Matrices.Add(treeMatrix);
            cell.Bounds.Encapsulate(position + Vector3.up * Mathf.Max(4f, scale.y * 4f));
            cell.Bounds.Encapsulate(position - Vector3.up * 2f);
        }
    }

    private void RefreshRenderableCells()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        renderableCells.Clear();

        Vector3 anchorPosition = GetAnchorPosition();
        Vector3 cameraPosition = targetCamera != null ? targetCamera.transform.position : anchorPosition;
        float effectiveActivationDistance = Mathf.Max(0f, activationDistance);
        float effectiveNearLodDistance = Mathf.Max(0f, nearLodDistance);
        if (effectiveActivationDistance > 0f)
        {
            effectiveNearLodDistance = Mathf.Min(effectiveNearLodDistance, effectiveActivationDistance);
        }

        float activationDistanceSqr = effectiveActivationDistance * effectiveActivationDistance;
        float nearLodDistanceSqr = effectiveNearLodDistance * effectiveNearLodDistance;
        float effectiveUnloadDistance = Mathf.Max(unloadDistance, effectiveActivationDistance + 5f);
        float unloadDistanceSqr = effectiveUnloadDistance * effectiveUnloadDistance;
        float effectiveShadowOnlyDistance = GetEffectiveShadowOnlyDistance();
        float shadowOnlyDistanceSqr = effectiveShadowOnlyDistance * effectiveShadowOnlyDistance;
        Plane[] frustumPlanes = enableFrustumCulling && targetCamera != null
            ? GeometryUtility.CalculateFrustumPlanes(targetCamera)
            : null;
        int remainingActivations = Mathf.Max(1, maxCellLoadsPerRefresh);

        foreach (TreeCell cell in cells)
        {
            float sqrDistance = cell.Bounds.SqrDistance(anchorPosition);
            float cameraDistanceSqr = cell.Bounds.SqrDistance(cameraPosition);
            bool insideVisibleDistance = !enableDistanceCulling || sqrDistance <= activationDistanceSqr;
            bool insideShadowRetentionDistance = !enableDistanceCulling || sqrDistance <= unloadDistanceSqr;
            bool isInsideFrustum = !enableFrustumCulling ||
                frustumPlanes == null ||
                GeometryUtility.TestPlanesAABB(frustumPlanes, cell.Bounds);

            bool shouldKeepShadowOnly = effectiveShadowOnlyDistance > 0f &&
                cameraDistanceSqr <= shadowOnlyDistanceSqr &&
                ((!isInsideFrustum && keepShadowsWhenFrustumCulled) ||
                 (!insideVisibleDistance && insideShadowRetentionDistance && keepShadowsWhenDistanceCulled));

            TreeCellRenderMode desiredRenderMode;
            if (insideVisibleDistance && isInsideFrustum)
            {
                desiredRenderMode = cameraDistanceSqr <= nearLodDistanceSqr
                    ? TreeCellRenderMode.Near
                    : TreeCellRenderMode.Mid;
            }
            else if (shouldKeepShadowOnly)
            {
                desiredRenderMode = TreeCellRenderMode.ShadowOnly;
            }
            else
            {
                desiredRenderMode = TreeCellRenderMode.Hidden;
            }

            if (cell.RenderMode == TreeCellRenderMode.Hidden &&
                desiredRenderMode != TreeCellRenderMode.Hidden)
            {
                if (remainingActivations <= 0)
                {
                    desiredRenderMode = TreeCellRenderMode.Hidden;
                }
                else
                {
                    remainingActivations--;
                }
            }

            cell.RenderMode = desiredRenderMode;
            if (cell.RenderMode != TreeCellRenderMode.Hidden)
            {
                renderableCells.Add(cell);
            }
        }
    }

    private float GetEffectiveShadowOnlyDistance()
    {
        if (shadowOnlyDistance <= 0f)
        {
            return 0f;
        }

        float qualityShadowDistance = QualitySettings.shadowDistance;
        if (qualityShadowDistance <= 0f)
        {
            return 0f;
        }

        return Mathf.Min(shadowOnlyDistance, qualityShadowDistance);
    }

    private void RenderVisibleCells()
    {
        if (renderableCells.Count == 0)
        {
            return;
        }

        for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
        {
            PrototypeInfo prototype = prototypes[prototypeIndex];
            RenderPrototypePasses(prototypeIndex, prototype.NearPasses, TreeCellRenderMode.Near);
            RenderPrototypePasses(prototypeIndex, prototype.MidPasses, TreeCellRenderMode.Mid);
            RenderPrototypePasses(prototypeIndex, prototype.MidPasses, TreeCellRenderMode.ShadowOnly);
        }
    }

    private void RenderPrototypePasses(int prototypeIndex, RenderPass[] renderPasses, TreeCellRenderMode renderMode)
    {
        if (renderPasses == null || renderPasses.Length == 0)
        {
            return;
        }

        for (int passIndex = 0; passIndex < renderPasses.Length; passIndex++)
        {
            RenderPrototypePass(prototypeIndex, renderPasses[passIndex], renderMode);
        }
    }

    private void RenderPrototypePass(int prototypeIndex, RenderPass renderPass, TreeCellRenderMode renderMode)
    {
        int bufferedCount = 0;
        bool hasBufferedBounds = false;
        Bounds bufferedBounds = default;

        for (int cellIndex = 0; cellIndex < renderableCells.Count; cellIndex++)
        {
            TreeCell cell = renderableCells[cellIndex];
            if (cell.RenderMode != renderMode)
            {
                continue;
            }

            List<Matrix4x4> matrices = cell.Batches[prototypeIndex].Matrices;
            if (matrices.Count == 0)
            {
                continue;
            }

            bool hasCellBoundsInBuffer = false;
            for (int sourceIndex = 0; sourceIndex < matrices.Count; sourceIndex++)
            {
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

                drawBuffer[bufferedCount] = matrices[sourceIndex] * renderPass.RendererLocalMatrix;
                bufferedCount++;

                if (bufferedCount == drawBuffer.Length)
                {
                    DrawBufferedInstances(renderPass, bufferedBounds, bufferedCount, renderMode);
                    bufferedCount = 0;
                    hasBufferedBounds = false;
                    hasCellBoundsInBuffer = false;
                }
            }
        }

        if (bufferedCount > 0 && hasBufferedBounds)
        {
            DrawBufferedInstances(renderPass, bufferedBounds, bufferedCount, renderMode);
        }
    }

    private void DrawBufferedInstances(
        RenderPass renderPass,
        Bounds worldBounds,
        int count,
        TreeCellRenderMode renderMode)
    {
        if (renderPass.Material == null || renderPass.Mesh == null)
        {
            return;
        }

        RenderParams renderParams = new RenderParams(renderPass.Material)
        {
            worldBounds = worldBounds,
            shadowCastingMode = renderMode == TreeCellRenderMode.ShadowOnly
                ? ShadowCastingMode.ShadowsOnly
                : renderPass.ShadowCastingMode,
            receiveShadows = renderMode != TreeCellRenderMode.ShadowOnly && renderPass.ReceiveShadows,
        };

        Graphics.RenderMeshInstanced(renderParams, renderPass.Mesh, renderPass.SubMeshIndex, drawBuffer, count);
    }

    private bool IsCellEmpty(TreeCell cell)
    {
        for (int prototypeIndex = 0; prototypeIndex < cell.Batches.Length; prototypeIndex++)
        {
            if (cell.Batches[prototypeIndex].Matrices.Count > 0)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetAnchorPosition()
    {
        if (activationTarget != null)
        {
            return activationTarget.position;
        }

        if (targetCamera != null)
        {
            return targetCamera.transform.position;
        }

        return transform.position;
    }

    private void ApplyBuiltInTerrainSuppression(bool suppress)
    {
        if (!Application.isPlaying || terrain == null)
        {
            return;
        }

        if (suppressBuiltInTerrainTrees)
        {
            if (suppress)
            {
                if (originalTreeDistance < 0f)
                {
                    originalTreeDistance = terrain.treeDistance;
                }

                terrain.treeDistance = 0f;
            }
            else if (originalTreeDistance >= 0f)
            {
                terrain.treeDistance = originalTreeDistance;
            }
        }

        if (!suppressBuiltInTerrainTreeColliders || terrainCollider == null)
        {
            return;
        }

        if (suppress)
        {
            if (!didCaptureTreeColliderState)
            {
                didCaptureTreeColliderState = TryGetTerrainTreeCollidersEnabled(out originalTreeCollidersEnabled);
            }

            SetTerrainTreeCollidersEnabled(false);
        }
        else if (didCaptureTreeColliderState)
        {
            SetTerrainTreeCollidersEnabled(originalTreeCollidersEnabled);
        }
    }

    private bool TryGetTerrainTreeCollidersEnabled(out bool value)
    {
        value = false;
        if (terrainCollider == null)
        {
            return false;
        }

        Type colliderType = typeof(TerrainCollider);
        cachedEnableTreeCollidersProperty ??= colliderType.GetProperty("enableTreeColliders", BindingFlags.Instance | BindingFlags.Public);
        if (cachedEnableTreeCollidersProperty != null && cachedEnableTreeCollidersProperty.PropertyType == typeof(bool))
        {
            value = (bool)cachedEnableTreeCollidersProperty.GetValue(terrainCollider);
            return true;
        }

        cachedEnableTreeCollidersField ??= colliderType.GetField("enableTreeColliders", BindingFlags.Instance | BindingFlags.Public);
        if (cachedEnableTreeCollidersField != null && cachedEnableTreeCollidersField.FieldType == typeof(bool))
        {
            value = (bool)cachedEnableTreeCollidersField.GetValue(terrainCollider);
            return true;
        }

        return false;
    }

    private void SetTerrainTreeCollidersEnabled(bool enabled)
    {
        if (terrainCollider == null)
        {
            return;
        }

        if (cachedEnableTreeCollidersProperty != null && cachedEnableTreeCollidersProperty.PropertyType == typeof(bool))
        {
            cachedEnableTreeCollidersProperty.SetValue(terrainCollider, enabled);
            return;
        }

        if (cachedEnableTreeCollidersField != null && cachedEnableTreeCollidersField.FieldType == typeof(bool))
        {
            cachedEnableTreeCollidersField.SetValue(terrainCollider, enabled);
        }
    }

    private static Vector3 ExtractPosition(Matrix4x4 matrix)
    {
        return new Vector3(matrix.m03, matrix.m13, matrix.m23);
    }

    private static Vector3 ExtractScale(Matrix4x4 matrix)
    {
        return new Vector3(
            new Vector3(matrix.m00, matrix.m10, matrix.m20).magnitude,
            new Vector3(matrix.m01, matrix.m11, matrix.m21).magnitude,
            new Vector3(matrix.m02, matrix.m12, matrix.m22).magnitude);
    }
}
