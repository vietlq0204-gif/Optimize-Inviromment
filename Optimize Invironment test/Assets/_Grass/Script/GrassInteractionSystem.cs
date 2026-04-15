using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pushes active grass interactors into shader globals and builds a persistent trail map.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class GrassInteractionSystem : MonoBehaviour
{
    private const int MaxInteractors = 8;
    private const int DefaultTrailMapResolution = 1024;
    private const float DefaultTrailDecayPerSecond = 0.12f;
    private const float FallbackBoundsSize = 96f;
    private const float FallbackBoundsPadding = 12f;
    private const string TrailMapShaderResource = "GrassTrailMap";

    private static readonly int GrassInteractorCountId = Shader.PropertyToID("_GrassInteractorCount");
    private static readonly int GrassInteractorDataId = Shader.PropertyToID("_GrassInteractorData");
    private static readonly int GrassInteractorVelocityId = Shader.PropertyToID("_GrassInteractorVelocity");
    private static readonly int GrassTrailMapId = Shader.PropertyToID("_GrassTrailMap");
    private static readonly int GrassTrailBoundsId = Shader.PropertyToID("_GrassTrailBounds");
    private static readonly int TrailWorldBoundsId = Shader.PropertyToID("_TrailWorldBounds");
    private static readonly int TrailFadeMultiplierId = Shader.PropertyToID("_FadeMultiplier");
    private static readonly int StampParamsId = Shader.PropertyToID("_StampParams");
    private static readonly int StampMotionId = Shader.PropertyToID("_StampMotion");

    private static readonly List<GrassInteractionSource> Sources = new();
    private static readonly Vector4[] InteractorData = new Vector4[MaxInteractors];
    private static readonly Vector4[] InteractorVelocity = new Vector4[MaxInteractors];

    private static GrassInteractionSystem instance;

    private Material trailMapMaterial;
    private RenderTexture trailMapFront;
    private RenderTexture trailMapBack;
    private Vector4 currentTrailBounds;
    private bool hasTrailBounds;
    private GrassTrailSettings trailSettings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Sources.Clear();
        Shader.SetGlobalInt(GrassInteractorCountId, 0);
        Shader.SetGlobalTexture(GrassTrailMapId, Texture2D.blackTexture);
        EnsureInstance();
    }

    internal static void Register(GrassInteractionSource source)
    {
        if (source == null)
        {
            return;
        }

        EnsureInstance();

        if (!Sources.Contains(source))
        {
            Sources.Add(source);
        }
    }

    internal static void Unregister(GrassInteractionSource source)
    {
        if (source == null)
        {
            return;
        }

        Sources.Remove(source);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindAnyObjectByType<GrassInteractionSystem>();
        if (instance != null)
        {
            return;
        }

        GameObject systemObject = new("[GrassInteractionSystem]");
        systemObject.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(systemObject);
        instance = systemObject.AddComponent<GrassInteractionSystem>();
    }

    private void LateUpdate()
    {
        UpdateInteractorGlobals();
        UpdateTrailMap();
    }

    private void UpdateInteractorGlobals()
    {
        int count = 0;

        for (int i = Sources.Count - 1; i >= 0; i--)
        {
            GrassInteractionSource source = Sources[i];
            if (source == null || !source.isActiveAndEnabled)
            {
                Sources.RemoveAt(i);
                continue;
            }

            if (count >= MaxInteractors)
            {
                continue;
            }

            InteractorData[count] = source.InteractorData;
            InteractorVelocity[count] = source.VelocityData;
            count++;
        }

        Shader.SetGlobalInt(GrassInteractorCountId, count);

        if (count > 0)
        {
            Shader.SetGlobalVectorArray(GrassInteractorDataId, InteractorData);
            Shader.SetGlobalVectorArray(GrassInteractorVelocityId, InteractorVelocity);
        }
    }

    private void UpdateTrailMap()
    {
        RefreshTrailSettings();

        if (!TryGetTrailBounds(out Vector4 trailBounds))
        {
            Shader.SetGlobalTexture(GrassTrailMapId, Texture2D.blackTexture);
            Shader.SetGlobalVector(GrassTrailBoundsId, Vector4.zero);
            return;
        }

        if (!EnsureTrailResources())
        {
            Shader.SetGlobalTexture(GrassTrailMapId, Texture2D.blackTexture);
            Shader.SetGlobalVector(GrassTrailBoundsId, trailBounds);
            return;
        }

        if (!hasTrailBounds || !AreBoundsSimilar(currentTrailBounds, trailBounds))
        {
            currentTrailBounds = trailBounds;
            hasTrailBounds = true;
            ClearRenderTexture(trailMapFront, Color.clear);
            ClearRenderTexture(trailMapBack, Color.clear);
        }

        Shader.SetGlobalVector(GrassTrailBoundsId, currentTrailBounds);
        trailMapMaterial.SetVector(TrailWorldBoundsId, currentTrailBounds);

        float decayPerSecond = trailSettings != null ? trailSettings.DecayPerSecond : DefaultTrailDecayPerSecond;
        float fadeMultiplier = Mathf.Exp(-decayPerSecond * Time.deltaTime);
        trailMapMaterial.SetFloat(TrailFadeMultiplierId, fadeMultiplier);
        BlitTrail(0);

        for (int i = 0; i < Sources.Count; i++)
        {
            GrassInteractionSource source = Sources[i];
            if (source == null || !source.isActiveAndEnabled || !source.EmitsPersistentTrail)
            {
                continue;
            }

            Vector4 stampData = source.TrailStampData;
            if (stampData.w <= 0.0001f || stampData.z <= 0.0001f)
            {
                continue;
            }

            trailMapMaterial.SetVector(StampParamsId, stampData);
            trailMapMaterial.SetVector(StampMotionId, source.TrailMotionData);
            BlitTrail(1);
        }

        Shader.SetGlobalTexture(GrassTrailMapId, trailMapFront);
    }

    private bool EnsureTrailResources()
    {
        if (trailMapMaterial == null)
        {
            Shader trailShader = Resources.Load<Shader>(TrailMapShaderResource);
            if (trailShader == null)
            {
                Debug.LogWarning(
                    $"Grass trail map shader resource '{TrailMapShaderResource}' was not found. Persistent trails are disabled.");
                return false;
            }

            trailMapMaterial = new Material(trailShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        int resolution = trailSettings != null ? trailSettings.Resolution : DefaultTrailMapResolution;
        EnsureTrailRenderTexture(ref trailMapFront, "_GrassTrailMapFront", resolution);
        EnsureTrailRenderTexture(ref trailMapBack, "_GrassTrailMapBack", resolution);
        return true;
    }

    private static void EnsureTrailRenderTexture(ref RenderTexture renderTexture, string textureName, int resolution)
    {
        if (renderTexture != null &&
            renderTexture.IsCreated() &&
            renderTexture.width == resolution &&
            renderTexture.height == resolution)
        {
            return;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Object.Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave
        };

        renderTexture.Create();
        ClearRenderTexture(renderTexture, Color.clear);
    }

    private void BlitTrail(int pass)
    {
        Graphics.Blit(trailMapFront, trailMapBack, trailMapMaterial, pass);
        (trailMapFront, trailMapBack) = (trailMapBack, trailMapFront);
    }

    private void RefreshTrailSettings()
    {
        if (trailSettings != null && trailSettings.isActiveAndEnabled)
        {
            return;
        }

        trailSettings = FindAnyObjectByType<GrassTrailSettings>();
    }

    private bool TryGetTrailBounds(out Vector4 trailBounds)
    {
        if (trailSettings != null && !trailSettings.UseActiveTerrainBounds)
        {
            trailBounds = trailSettings.ManualBounds;
            return true;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            trailBounds = new Vector4(terrainPosition.x, terrainPosition.z, terrainSize.x, terrainSize.z);
            return true;
        }

        if (trailSettings != null)
        {
            trailBounds = trailSettings.ManualBounds;
            return true;
        }

        bool foundSource = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        for (int i = 0; i < Sources.Count; i++)
        {
            GrassInteractionSource source = Sources[i];
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            Vector4 stampData = source.TrailStampData;
            Vector2 position = new(stampData.x, stampData.y);
            float radius = stampData.z + FallbackBoundsPadding;

            if (!foundSource)
            {
                min = position - Vector2.one * radius;
                max = position + Vector2.one * radius;
                foundSource = true;
                continue;
            }

            min = Vector2.Min(min, position - Vector2.one * radius);
            max = Vector2.Max(max, position + Vector2.one * radius);
        }

        if (foundSource)
        {
            Vector2 size = Vector2.Max(max - min, Vector2.one * FallbackBoundsSize);
            trailBounds = new Vector4(min.x, min.y, size.x, size.y);
            return true;
        }

        trailBounds = new Vector4(-FallbackBoundsSize * 0.5f, -FallbackBoundsSize * 0.5f, FallbackBoundsSize, FallbackBoundsSize);
        return true;
    }

    private static bool AreBoundsSimilar(Vector4 a, Vector4 b)
    {
        const float epsilon = 0.01f;
        return Mathf.Abs(a.x - b.x) < epsilon &&
               Mathf.Abs(a.y - b.y) < epsilon &&
               Mathf.Abs(a.z - b.z) < epsilon &&
               Mathf.Abs(a.w - b.w) < epsilon;
    }

    private static void ClearRenderTexture(RenderTexture renderTexture, Color clearColor)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(false, true, clearColor);
        RenderTexture.active = previous;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Shader.SetGlobalInt(GrassInteractorCountId, 0);
            Shader.SetGlobalTexture(GrassTrailMapId, Texture2D.blackTexture);
        }

        ReleaseTrailResources();
    }

    private void ReleaseTrailResources()
    {
        if (trailMapFront != null)
        {
            trailMapFront.Release();
            Object.Destroy(trailMapFront);
            trailMapFront = null;
        }

        if (trailMapBack != null)
        {
            trailMapBack.Release();
            Object.Destroy(trailMapBack);
            trailMapBack = null;
        }

        if (trailMapMaterial != null)
        {
            Object.Destroy(trailMapMaterial);
            trailMapMaterial = null;
        }
    }
}
