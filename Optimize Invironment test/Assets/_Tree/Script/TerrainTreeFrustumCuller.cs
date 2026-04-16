using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TerrainTreeFrustumCuller : MonoBehaviour
{
    [Serializable]
    private struct TreeRecord
    {
        public int PrototypeIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    private sealed class PrototypeInfo
    {
        public string Name = string.Empty;
        public GameObject Prefab;
        public Matrix4x4 PrototypeLocalMatrix = Matrix4x4.identity;
        public readonly Queue<GameObject> Pool = new();
    }

    private sealed class LoadedTreeInstance
    {
        public int PrototypeIndex;
        public GameObject GameObject;
    }

    private sealed class TreeCell
    {
        public Bounds Bounds;
        public readonly List<TreeRecord> Trees = new();
        public readonly List<LoadedTreeInstance> LoadedInstances = new();
        public GameObject Root;
        public bool IsLoaded;
    }

    [Header("References")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainCollider terrainCollider;
    [SerializeField] private Transform activationTarget;
    [SerializeField] private Camera targetCamera;

    [Header("Culling")]
    [SerializeField] private float cellSize = 32f;
    [SerializeField] private float activationDistance = 80f;
    [SerializeField] private float unloadDistance = 100f;
    [SerializeField] private int refreshInterval = 5;
    [SerializeField] private int maxCellLoadsPerRefresh = 1;

    [Header("Runtime")]
    [SerializeField] private bool suppressBuiltInTerrainTrees = true;
    [SerializeField] private bool suppressBuiltInTerrainTreeColliders = true;
    [SerializeField] private bool logBuildStats = true;

    private readonly List<TreeCell> cells = new();
    private readonly List<PrototypeInfo> prototypes = new();

    private bool isBuilt;
    private float originalTreeDistance = -1f;
    private bool originalTreeCollidersEnabled;
    private bool didCaptureTreeColliderState;
    private PropertyInfo cachedEnableTreeCollidersProperty;
    private FieldInfo cachedEnableTreeCollidersField;
    private GameObject runtimeRoot;
    private GameObject poolRoot;

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

        Build();
        ApplyBuiltInTerrainSuppression(true);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UnloadAllCells();
        DestroyRuntimeRoots();
        ApplyBuiltInTerrainSuppression(false);
    }

    private void LateUpdate()
    {
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
            RefreshLoadedCells();
        }
    }

    [ContextMenu("Rebuild Tree Data")]
    public void Build()
    {
        UnloadAllCells();
        DestroyRuntimeRoots();
        cells.Clear();
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

        EnsureRuntimeRoots();

        TerrainData terrainData = terrain.terrainData;
        int[] prototypeRemap = CollectPrototypes(terrainData);
        if (prototypes.Count == 0)
        {
            Debug.LogWarning("TerrainTreeFrustumCuller found no valid tree prefabs in TerrainData.treePrototypes.", this);
            return;
        }

        CreateCells(terrainData);
        PopulateCells(terrainData, prototypeRemap);
        cells.RemoveAll(cell => cell.Trees.Count == 0);

        isBuilt = cells.Count > 0;
        ApplyBuiltInTerrainSuppression(true);

        if (logBuildStats)
        {
            int treeCount = 0;
            foreach (TreeCell cell in cells)
            {
                treeCount += cell.Trees.Count;
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

            remap[terrainPrototypeIndex] = prototypes.Count;
            prototypes.Add(new PrototypeInfo
            {
                Name = prefab.name,
                Prefab = prefab,
                PrototypeLocalMatrix = Matrix4x4.TRS(
                    prefab.transform.localPosition,
                    prefab.transform.localRotation,
                    prefab.transform.localScale),
            });
        }

        return remap;
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

                cells.Add(new TreeCell
                {
                    Bounds = new Bounds(
                        new Vector3(minX + sizeX * 0.5f, terrainPosition.y + boundsHeight * 0.5f, minZ + sizeZ * 0.5f),
                        new Vector3(sizeX, boundsHeight, sizeZ)),
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

            TreeRecord record = new TreeRecord
            {
                PrototypeIndex = prototypeIndex,
                Position = ExtractPosition(treeMatrix),
                Rotation = ExtractRotation(treeMatrix),
                Scale = ExtractScale(treeMatrix),
            };

            int cellX = Mathf.Clamp((int)((record.Position.x - terrainPosition.x) / clampedCellSize), 0, Mathf.Max(0, cellCountX - 1));
            int cellZ = Mathf.Clamp((int)((record.Position.z - terrainPosition.z) / clampedCellSize), 0, Mathf.Max(0, cellCountZ - 1));
            int cellIndex = cellZ * cellCountX + cellX;

            TreeCell cell = cells[cellIndex];
            cell.Trees.Add(record);
            cell.Bounds.Encapsulate(record.Position + Vector3.up * Mathf.Max(4f, record.Scale.y * 4f));
            cell.Bounds.Encapsulate(record.Position - Vector3.up * 2f);
        }
    }

    private void RefreshLoadedCells()
    {
        Vector3 anchorPosition = GetAnchorPosition();
        float activationDistanceSqr = activationDistance * activationDistance;
        float unloadDistanceSqr = Mathf.Max(unloadDistance, activationDistance + 5f) * Mathf.Max(unloadDistance, activationDistance + 5f);
        int remainingLoads = Mathf.Max(1, maxCellLoadsPerRefresh);

        for (int i = 0; i < cells.Count; i++)
        {
            TreeCell cell = cells[i];
            float sqrDistance = cell.Bounds.SqrDistance(anchorPosition);

            if (!cell.IsLoaded)
            {
                if (sqrDistance <= activationDistanceSqr && remainingLoads > 0)
                {
                    LoadCell(cell);
                    remainingLoads--;
                }

                continue;
            }

            if (sqrDistance > unloadDistanceSqr)
            {
                UnloadCell(cell);
            }
        }
    }

    private void LoadCell(TreeCell cell)
    {
        if (cell.IsLoaded)
        {
            return;
        }

        if (cell.Root == null)
        {
            cell.Root = new GameObject("TreeCell");
            cell.Root.transform.SetParent(runtimeRoot.transform, false);
        }

        cell.Root.SetActive(true);

        for (int i = 0; i < cell.Trees.Count; i++)
        {
            TreeRecord tree = cell.Trees[i];
            GameObject instance = AcquireInstance(tree.PrototypeIndex);
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(cell.Root.transform, false);
            instanceTransform.SetPositionAndRotation(tree.Position, tree.Rotation);
            instanceTransform.localScale = tree.Scale;
            instance.SetActive(true);

            cell.LoadedInstances.Add(new LoadedTreeInstance
            {
                PrototypeIndex = tree.PrototypeIndex,
                GameObject = instance,
            });
        }

        cell.IsLoaded = true;
    }

    private void UnloadCell(TreeCell cell)
    {
        if (!cell.IsLoaded)
        {
            return;
        }

        for (int i = 0; i < cell.LoadedInstances.Count; i++)
        {
            LoadedTreeInstance loaded = cell.LoadedInstances[i];
            if (loaded.GameObject == null)
            {
                continue;
            }

            ReleaseInstance(loaded.PrototypeIndex, loaded.GameObject);
        }

        cell.LoadedInstances.Clear();
        cell.IsLoaded = false;

        if (cell.Root != null)
        {
            cell.Root.SetActive(false);
        }
    }

    private void UnloadAllCells()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            UnloadCell(cells[i]);
        }
    }

    private GameObject AcquireInstance(int prototypeIndex)
    {
        PrototypeInfo prototype = prototypes[prototypeIndex];
        while (prototype.Pool.Count > 0)
        {
            GameObject pooled = prototype.Pool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }

        return Instantiate(prototype.Prefab);
    }

    private void ReleaseInstance(int prototypeIndex, GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(poolRoot.transform, false);
        prototypes[prototypeIndex].Pool.Enqueue(instance);
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

    private void EnsureRuntimeRoots()
    {
        if (runtimeRoot == null)
        {
            runtimeRoot = new GameObject("TerrainTreeRuntime");
            runtimeRoot.transform.SetParent(transform, false);
            runtimeRoot.transform.localPosition = Vector3.zero;
            runtimeRoot.transform.localRotation = Quaternion.identity;
            runtimeRoot.transform.localScale = Vector3.one;
        }

        if (poolRoot == null)
        {
            poolRoot = new GameObject("TerrainTreePool");
            poolRoot.transform.SetParent(transform, false);
            poolRoot.transform.localPosition = Vector3.zero;
            poolRoot.transform.localRotation = Quaternion.identity;
            poolRoot.transform.localScale = Vector3.one;
        }
    }

    private void DestroyRuntimeRoots()
    {
        if (runtimeRoot != null)
        {
            Destroy(runtimeRoot);
            runtimeRoot = null;
        }

        if (poolRoot != null)
        {
            Destroy(poolRoot);
            poolRoot = null;
        }
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

    private static Quaternion ExtractRotation(Matrix4x4 matrix)
    {
        Vector3 forward = new Vector3(matrix.m02, matrix.m12, matrix.m22);
        Vector3 up = new Vector3(matrix.m01, matrix.m11, matrix.m21);

        if (forward.sqrMagnitude <= 0.0001f || up.sqrMagnitude <= 0.0001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(forward.normalized, up.normalized);
    }
}
