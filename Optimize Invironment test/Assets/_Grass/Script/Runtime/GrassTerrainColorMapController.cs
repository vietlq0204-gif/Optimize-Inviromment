using UnityEngine;

/// <summary>
/// Pushes a manually supplied terrain color map to the grass shader globals.
/// </summary>
[ExecuteAlways]
public sealed class GrassTerrainColorMapController : MonoBehaviour
{
    private static readonly int TerrainColorMapId = Shader.PropertyToID("_GrassTerrainColorMap");
    private static readonly int TerrainColorBoundsId = Shader.PropertyToID("_GrassTerrainColorMapWorldBounds");
    private static readonly int TerrainColorParamsId = Shader.PropertyToID("_GrassTerrainColorMapParams");

    [SerializeField] private Texture terrainColorMap;
    [SerializeField] private Vector2 worldMin;
    [SerializeField] private Vector2 worldSize = new(100f, 100f);
    [SerializeField] private float influence = 1f;

    private void OnEnable()
    {
        ApplyGlobals();
    }

    private void OnDisable()
    {
        ClearGlobals();
    }

    private void OnValidate()
    {
        worldSize.x = Mathf.Max(0.01f, worldSize.x);
        worldSize.y = Mathf.Max(0.01f, worldSize.y);
        influence = Mathf.Clamp01(influence);
        ApplyGlobals();
    }

    private void ApplyGlobals()
    {
        if (!isActiveAndEnabled || terrainColorMap == null)
        {
            ClearGlobals();
            return;
        }

        Shader.SetGlobalTexture(TerrainColorMapId, terrainColorMap);
        Shader.SetGlobalVector(TerrainColorBoundsId, new Vector4(worldMin.x, worldMin.y, worldSize.x, worldSize.y));
        Shader.SetGlobalVector(TerrainColorParamsId, new Vector4(1f, influence, 0f, 0f));
    }

    private static void ClearGlobals()
    {
        Shader.SetGlobalTexture(TerrainColorMapId, Texture2D.whiteTexture);
        Shader.SetGlobalVector(TerrainColorBoundsId, Vector4.zero);
        Shader.SetGlobalVector(TerrainColorParamsId, Vector4.zero);
    }
}
