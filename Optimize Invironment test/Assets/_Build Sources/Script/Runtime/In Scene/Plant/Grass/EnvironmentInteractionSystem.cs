using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Writes shared environment interaction shapes into the global vegetation interaction map.
/// </summary>
[ExecuteAlways]
public class EnvironmentInteractionSystem : MonoBehaviour
{
    private const float IdleHistoryTailMultiplier = 4f;
    private const int MaxShapesPerBatch = 16;
    private const float DebugDrawHeightOffset = 0.04f;
    private static readonly Color NeutralInteractionClearColor = new Color(0.5f, 0.5f, 0f, 0f);

    protected enum InteractionResolution
    {
        Resolution256 = 256,
        Resolution512 = 512,
        Resolution1024 = 1024,
        Resolution2048 = 2048,
    }

    private static readonly int InteractionMapId = Shader.PropertyToID("_GrassInteractionMap");
    private static readonly int InteractionCameraId = Shader.PropertyToID("_GrassInteractionCameraPosition");
    private static readonly int InteractionParamsId = Shader.PropertyToID("_GrassInteractionParams");
    private static readonly int InteractionTexelSizeId = Shader.PropertyToID("_GrassInteractionMap_TexelSize");
    private static readonly int GrassCameraForwardId = Shader.PropertyToID("_GrassCameraForwardWS");
    private static readonly int InteractionRtAliasId = Shader.PropertyToID("_GrassInteractionRT");
    private static readonly int InteractionCameraAliasId = Shader.PropertyToID("_GrassInteractionCamera");
    private static readonly int GlobalInteractionConfigStateId = Shader.PropertyToID("_GlobalPlantInteractionConfigState");
    private static readonly int GlobalInteractionConfigAId = Shader.PropertyToID("_GlobalPlantInteractionConfigA");
    private static readonly int GlobalInteractionConfigBId = Shader.PropertyToID("_GlobalPlantInteractionConfigB");

    private static readonly int CurrentMapId = Shader.PropertyToID("_CurrentInteractionMap");
    private static readonly int PreviousMapId = Shader.PropertyToID("_PreviousInteractionMap");
    private static readonly int PersistenceId = Shader.PropertyToID("_HistoryPersistence");
    private static readonly int NeutralColorId = Shader.PropertyToID("_NeutralInteractionColor");
    private static readonly int PreviousUVOffsetId = Shader.PropertyToID("_PreviousUVOffset");

    private static readonly int BaseInteractionMapId = Shader.PropertyToID("_BaseInteractionMap");
    private static readonly int ShapeCountId = Shader.PropertyToID("_ShapeCount");
    private static readonly int InteractionRegionId = Shader.PropertyToID("_InteractionRegion");
    private static readonly int ShapeData0Id = Shader.PropertyToID("_ShapeData0");
    private static readonly int ShapeData1Id = Shader.PropertyToID("_ShapeData1");
    private static readonly int ShapeData2Id = Shader.PropertyToID("_ShapeData2");

    [Header("Tracking")]
    [Tooltip("Transform mà vùng interaction sẽ đi theo. Thường là Player hoặc camera target.")]
    [SerializeField] protected Transform followTarget;
    [Tooltip("Độ lệch vị trí vùng interaction so với Follow Target. Y thường đặt cao để camera/RT nhìn xuống vùng cỏ.")]
    [SerializeField] protected Vector3 worldOffset = new Vector3(0f, 18f, 0f);
    [Tooltip("Trong Edit Mode, vùng interaction đi theo Scene View camera nếu không chạy game.")]
    [SerializeField] protected bool followSceneViewInEditMode = true;

    [Header("Render")]
    [Tooltip("Layer được dùng khi render interaction map. Chỉ object thuộc layer này được camera interaction nhìn thấy nếu dùng path render layer.")]
    [SerializeField] protected LayerMask cullingMask;
    [Tooltip("Nửa kích thước vùng interaction tính theo world unit. Vùng đầy đủ có cạnh bằng Orthographic Size x 2.")]
    [SerializeField] protected float orthographicSize = 16f;
    [Tooltip("Cường độ tổng khi shader đọc interaction map.")]
    [SerializeField] protected float globalStrength = 1f;
    [Tooltip("Độ phân giải texture interaction. Cao hơn mịn hơn nhưng tốn GPU hơn.")]
    [SerializeField] protected InteractionResolution resolution = InteractionResolution.Resolution512;
    [Tooltip("Màu trạng thái trung lập của interaction map. Thường giữ mặc định 0.5, 0.5, 0, 0.")]
    [SerializeField] protected Color clearColor = new Color(0.5f, 0.5f, 0f, 0f);
    [Tooltip("Ẩn layer interaction khỏi camera game để chỉ hệ thống interaction sử dụng.")]
    [SerializeField] protected bool hideInteractionLayerFromGameCameras = true;

    [Header("History")]
    [Tooltip("Thời gian blend/history của vết cỏ bị đè. Tăng giá trị để vết đè hồi chậm hơn; đặt rất lớn để gần như không hồi trong lúc test.")]
    [SerializeField] protected float historyBlendSeconds = 0.12f;
    [Tooltip("Shader dùng để cộng dồn interaction map qua thời gian.")]
    [SerializeField] private Shader accumulationShader;

    [Header("Shape Writer")]
    [Tooltip("Shader dùng để ghi các shape contact/trail vào interaction map.")]
    [SerializeField] private Shader batchStampShader;

    [Header("Interaction Config")]
    [Tooltip("Config cỏ dùng chung cho toàn bộ interaction system. Nếu source không có config riêng, nó sẽ dùng config này.")]
    [SerializeField] private GrassInteractionConfig interactionConfig;

    [Header("Debug")]
    [Tooltip("Vẽ vùng interaction trong Scene view.")]
    [SerializeField] private bool drawDebugRegion = true;
    [Tooltip("Chỉ vẽ vùng debug khi chọn object.")]
    [SerializeField] private bool drawDebugOnlyWhenSelected = true;
    [Tooltip("Hiện label debug gồm kích thước, độ phân giải và số shape.")]
    [SerializeField] private bool drawDebugLabels = true;
    [Tooltip("Vẽ đường chữ thập ở tâm vùng interaction.")]
    [SerializeField] private bool drawDebugCross = true;
    [Tooltip("Màu gizmo của vùng interaction.")]
    [SerializeField] private Color debugRegionColor = new Color(0.1f, 0.85f, 1f, 0.9f);

    protected RenderTexture interactionTexture;
    protected RenderTexture interactionScratchTexture;
    protected RenderTexture interactionHistoryA;
    protected RenderTexture interactionHistoryB;

    private Material accumulationMaterial;
    private Material batchStampMaterial;
    private bool historyAIsCurrent = true;
    private bool historyContainsInteraction;
    private bool hasHistoryRegionCenter;
    private Vector3 historyRegionCenter;
    private float lastActiveShapeTime = float.NegativeInfinity;
    private int lastCollectedShapeCount;

    private readonly List<InteractionShape> collectedShapes = new List<InteractionShape>(64);
    private readonly Vector4[] shapeData0 = new Vector4[MaxShapesPerBatch];
    private readonly Vector4[] shapeData1 = new Vector4[MaxShapesPerBatch];
    private readonly Vector4[] shapeData2 = new Vector4[MaxShapesPerBatch];

    public static GrassInteractionConfig ActiveInteractionConfig { get; private set; }
    public GrassInteractionConfig InteractionConfig => interactionConfig;

    protected virtual void Reset()
    {
        AssignDefaultShadersIfMissing();
    }

    protected virtual void OnEnable()
    {
        historyContainsInteraction = false;
        hasHistoryRegionCenter = false;
        lastActiveShapeTime = float.NegativeInfinity;
        NormalizeClearColor();
        AssignDefaultShadersIfMissing();
        RefreshActiveInteractionConfig();
        EnsureResources();
        UpdateInteractionState();
    }

    protected virtual void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= RefreshFromEditorDelay;
#endif
        historyContainsInteraction = false;
        hasHistoryRegionCenter = false;
        lastActiveShapeTime = float.NegativeInfinity;
        if (ReferenceEquals(ActiveInteractionConfig, interactionConfig))
        {
            ActiveInteractionConfig = null;
        }
        ReleaseResources();
        ClearGlobals();
    }

    protected virtual void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        globalStrength = Mathf.Max(0f, globalStrength);
        historyBlendSeconds = Mathf.Max(0.01f, historyBlendSeconds);
        NormalizeClearColor();
        AssignDefaultShadersIfMissing();
        RefreshActiveInteractionConfig();

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureResources();
        if (Application.isPlaying)
        {
            UpdateInteractionState();
            return;
        }

#if UNITY_EDITOR
        ScheduleEditorRefresh();
#endif
    }

    protected virtual void LateUpdate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureResources();
        UpdateInteractionState();
    }

    protected virtual void EnsureResources()
    {
        EnsureInteractionTextures();
        EnsureHistoryTextures();
        EnsureAccumulationMaterial();
        EnsureBatchStampMaterial();
    }

    protected virtual void EnsureInteractionTextures()
    {
        int textureSize = (int)resolution;
        GraphicsFormat colorFormat = GetCompatibleColorFormat();
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(textureSize, textureSize)
        {
            msaaSamples = 1,
            volumeDepth = 1,
            graphicsFormat = colorFormat,
            depthStencilFormat = GraphicsFormat.None,
            sRGB = false,
            useMipMap = false,
            autoGenerateMips = false,
        };

        interactionTexture = EnsureTexture(interactionTexture, descriptor, "EnvironmentInteractionRT");
        interactionScratchTexture = EnsureTexture(interactionScratchTexture, descriptor, "EnvironmentInteractionScratchRT");
    }

    protected virtual void EnsureHistoryTextures()
    {
        int textureSize = (int)resolution;
        GraphicsFormat colorFormat = GetCompatibleColorFormat();
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(textureSize, textureSize)
        {
            msaaSamples = 1,
            volumeDepth = 1,
            graphicsFormat = colorFormat,
            depthStencilFormat = GraphicsFormat.None,
            sRGB = false,
            useMipMap = false,
            autoGenerateMips = false,
        };

        interactionHistoryA = EnsureTexture(interactionHistoryA, descriptor, "EnvironmentInteractionHistoryA");
        interactionHistoryB = EnsureTexture(interactionHistoryB, descriptor, "EnvironmentInteractionHistoryB");
    }

    protected virtual RenderTexture EnsureTexture(RenderTexture texture, RenderTextureDescriptor descriptor, string name)
    {
        bool needsNewTexture =
            texture == null ||
            !texture.IsCreated() ||
            texture.width != descriptor.width ||
            texture.height != descriptor.height ||
            texture.graphicsFormat != descriptor.graphicsFormat;

        if (!needsNewTexture)
        {
            return texture;
        }

        if (texture != null)
        {
            texture.Release();
            DestroyImmediateSafe(texture);
        }

        texture = new RenderTexture(descriptor)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        texture.Create();
        ClearRenderTexture(texture, clearColor);
        historyAIsCurrent = true;
        hasHistoryRegionCenter = false;
        return texture;
    }

    protected virtual void EnsureAccumulationMaterial()
    {
        if (accumulationMaterial != null)
        {
            return;
        }

        Shader shader = accumulationShader != null ? accumulationShader : Shader.Find("Hidden/Vit/GrassInteractionAccumulate");
        if (shader == null)
        {
            Debug.LogWarning("EnvironmentInteractionSystem could not find shader 'Hidden/Vit/GrassInteractionAccumulate'.", this);
            return;
        }

        accumulationMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    protected virtual void EnsureBatchStampMaterial()
    {
        if (batchStampMaterial != null)
        {
            return;
        }

        Shader shader = batchStampShader != null ? batchStampShader : Shader.Find("Hidden/Vit/EnvironmentInteractionBatch");
        if (shader == null)
        {
            Debug.LogWarning("EnvironmentInteractionSystem could not find shader 'Hidden/Vit/EnvironmentInteractionBatch'.", this);
            return;
        }

        batchStampMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    protected virtual void UpdateInteractionState()
    {
        if (interactionTexture == null)
        {
            return;
        }

        Vector3 followPosition = GetFollowPosition();
        Vector3 regionCenter = followPosition + worldOffset;
        float projectionHeight = followTarget != null ? followTarget.position.y : transform.position.y;
        bool hasShapes = CollectInteractionShapes(regionCenter);
        bool shouldKeepHistory = ShouldKeepHistoryAlive(hasShapes);

        RenderCurrentInteractionMap(regionCenter);

        if (hasShapes || shouldKeepHistory)
        {
            RenderInteractionHistory(regionCenter);
        }
        else if (historyContainsInteraction)
        {
            ClearInteractionHistory();
        }

        RenderTexture sampledInteraction = HasHistoryAccumulation() ? GetCurrentHistoryTexture() : interactionTexture;
        if (sampledInteraction == null)
        {
            sampledInteraction = interactionTexture;
        }

        Shader.SetGlobalTexture(InteractionMapId, sampledInteraction);
        Shader.SetGlobalTexture(InteractionRtAliasId, sampledInteraction);

        Vector4 cameraData = new Vector4(regionCenter.x, regionCenter.y, regionCenter.z, orthographicSize);
        Shader.SetGlobalVector(InteractionCameraId, cameraData);
        Shader.SetGlobalVector(InteractionCameraAliasId, cameraData);
        Shader.SetGlobalVector(InteractionParamsId, new Vector4(1f, projectionHeight, globalStrength, 0f));
        Shader.SetGlobalVector(
            InteractionTexelSizeId,
            new Vector4(
                1f / sampledInteraction.width,
                1f / sampledInteraction.height,
                sampledInteraction.width,
                sampledInteraction.height));

        Camera referenceCamera = GetReferenceCamera();
        Vector3 cameraForward = referenceCamera != null ? referenceCamera.transform.forward : Vector3.forward;
        if (cameraForward.sqrMagnitude > 0.0001f)
        {
            cameraForward.Normalize();
        }
        else
        {
            cameraForward = Vector3.forward;
        }

        Shader.SetGlobalVector(GrassCameraForwardId, new Vector4(cameraForward.x, cameraForward.y, cameraForward.z, 0f));
        ApplyGlobalInteractionConfig();
    }

    protected virtual bool CollectInteractionShapes(Vector3 regionCenter)
    {
        collectedShapes.Clear();
        float deltaTime = Application.isPlaying ? Mathf.Max(Time.deltaTime, 0.0001f) : (1f / 60f);
        InteractionCollectContext context = new InteractionCollectContext
        {
            FocusPosition = regionCenter,
            MaxDistance = orthographicSize * 1.5f,
            DeltaTime = deltaTime,
        };

        IReadOnlyCollection<EnvironmentInteractor> interactors = EnvironmentInteractionRegistry.Interactors;
        foreach (EnvironmentInteractor interactor in interactors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled)
            {
                continue;
            }

            interactor.CollectShapes(collectedShapes, context);
        }

        for (int i = collectedShapes.Count - 1; i >= 0; i--)
        {
            if ((collectedShapes[i].Targets & InteractionTargetMask.Vegetation) != 0)
            {
                continue;
            }

            collectedShapes.RemoveAt(i);
        }

        if (collectedShapes.Count > 0)
        {
            lastCollectedShapeCount = collectedShapes.Count;
            lastActiveShapeTime = Application.isPlaying ? Time.unscaledTime : 0f;
            historyContainsInteraction = true;
            return true;
        }

        lastCollectedShapeCount = 0;
        return false;
    }

    protected virtual bool ShouldKeepHistoryAlive(bool hasShapes)
    {
        if (hasShapes)
        {
            return true;
        }

        if (!historyContainsInteraction || !HasHistoryAccumulation())
        {
            return false;
        }

        if (!Application.isPlaying)
        {
            return false;
        }

        float idleHistoryTailSeconds = Mathf.Max(historyBlendSeconds * IdleHistoryTailMultiplier, 0.05f);
        return Time.unscaledTime - lastActiveShapeTime <= idleHistoryTailSeconds;
    }

    protected virtual void RenderCurrentInteractionMap(Vector3 regionCenter)
    {
        ClearRenderTexture(interactionTexture, clearColor);
        if (batchStampMaterial == null || interactionScratchTexture == null || collectedShapes.Count == 0)
        {
            return;
        }

        RenderTexture source = interactionTexture;
        RenderTexture destination = interactionScratchTexture;
        int shapeCount = collectedShapes.Count;

        for (int startIndex = 0; startIndex < shapeCount; startIndex += MaxShapesPerBatch)
        {
            int batchCount = Mathf.Min(MaxShapesPerBatch, shapeCount - startIndex);
            int actualBatchCount = FillShapeBatch(startIndex, batchCount);
            if (actualBatchCount <= 0)
            {
                continue;
            }

            batchStampMaterial.SetTexture(BaseInteractionMapId, source);
            batchStampMaterial.SetColor(NeutralColorId, clearColor);
            batchStampMaterial.SetFloat(ShapeCountId, actualBatchCount);
            batchStampMaterial.SetVector(InteractionRegionId, new Vector4(regionCenter.x, regionCenter.z, orthographicSize, 0f));
            batchStampMaterial.SetVectorArray(ShapeData0Id, shapeData0);
            batchStampMaterial.SetVectorArray(ShapeData1Id, shapeData1);
            batchStampMaterial.SetVectorArray(ShapeData2Id, shapeData2);

            Graphics.Blit(source, destination, batchStampMaterial, 0);
            RenderTexture swapTexture = source;
            source = destination;
            destination = swapTexture;
        }

        if (source != interactionTexture)
        {
            Graphics.Blit(source, interactionTexture);
        }
    }

    protected virtual int FillShapeBatch(int startIndex, int batchCount)
    {
        for (int i = 0; i < MaxShapesPerBatch; i++)
        {
            shapeData0[i] = Vector4.zero;
            shapeData1[i] = Vector4.zero;
            shapeData2[i] = Vector4.zero;
        }

        int writeIndex = 0;
        int endIndex = startIndex + batchCount;
        for (int i = startIndex; i < endIndex; i++)
        {
            InteractionShape shape = collectedShapes[i];
            if ((shape.Targets & InteractionTargetMask.Vegetation) == 0)
            {
                continue;
            }

            shapeData0[writeIndex] = new Vector4(shape.PointA.x, shape.PointA.z, shape.PointB.x, shape.PointB.z);
            shapeData1[writeIndex] = new Vector4(
                Mathf.Max(0.001f, shape.Radius),
                Mathf.Max(0f, shape.Strength),
                Mathf.Clamp01(shape.Softness),
                Mathf.Clamp01(shape.DirectionalInfluence));
            shapeData2[writeIndex] = new Vector4(
                shape.Velocity.x,
                shape.Velocity.z,
                Mathf.Clamp01(shape.RecoveryWeight),
                (float)shape.Type);
            writeIndex++;
        }

        return writeIndex;
    }

    protected virtual void RenderInteractionHistory(Vector3 regionCenter)
    {
        if (!HasHistoryAccumulation())
        {
            return;
        }

        RenderTexture previousHistory = GetCurrentHistoryTexture();
        RenderTexture nextHistory = historyAIsCurrent ? interactionHistoryB : interactionHistoryA;
        float deltaTime = Application.isPlaying ? Mathf.Max(Time.deltaTime, 0.0001f) : (1f / 60f);
        float blendSeconds = Mathf.Max(historyBlendSeconds, 0.0001f);
        float persistence = Mathf.Exp(-deltaTime / blendSeconds);
        Vector2 previousUVOffset = Vector2.zero;
        if (hasHistoryRegionCenter)
        {
            float textureWorldSize = Mathf.Max(orthographicSize * 2f, 0.0001f);
            previousUVOffset = new Vector2(
                (regionCenter.x - historyRegionCenter.x) / textureWorldSize,
                (regionCenter.z - historyRegionCenter.z) / textureWorldSize);
        }

        accumulationMaterial.SetTexture(CurrentMapId, interactionTexture);
        accumulationMaterial.SetTexture(PreviousMapId, previousHistory);
        accumulationMaterial.SetFloat(PersistenceId, persistence);
        accumulationMaterial.SetColor(NeutralColorId, clearColor);
        accumulationMaterial.SetVector(PreviousUVOffsetId, new Vector4(previousUVOffset.x, previousUVOffset.y, 0f, 0f));
        Graphics.Blit(null, nextHistory, accumulationMaterial, 0);

        historyAIsCurrent = !historyAIsCurrent;
        historyRegionCenter = regionCenter;
        hasHistoryRegionCenter = true;
    }

    protected virtual RenderTexture GetCurrentHistoryTexture()
    {
        return historyAIsCurrent ? interactionHistoryA : interactionHistoryB;
    }

    protected virtual bool HasHistoryAccumulation()
    {
        return accumulationMaterial != null && interactionHistoryA != null && interactionHistoryB != null;
    }

    protected virtual Vector3 GetFollowPosition()
    {
        if (followTarget != null)
        {
            return followTarget.position;
        }

        Camera referenceCamera = GetReferenceCamera();
        if (referenceCamera != null)
        {
            return referenceCamera.transform.position;
        }

        return transform.position;
    }

    protected virtual void ReleaseResources()
    {
        ReleaseTexture(ref interactionTexture);
        ReleaseTexture(ref interactionScratchTexture);
        ReleaseTexture(ref interactionHistoryA);
        ReleaseTexture(ref interactionHistoryB);

        if (accumulationMaterial != null)
        {
            DestroyImmediateSafe(accumulationMaterial);
            accumulationMaterial = null;
        }

        if (batchStampMaterial != null)
        {
            DestroyImmediateSafe(batchStampMaterial);
            batchStampMaterial = null;
        }
    }

    protected static void ClearGlobals()
    {
        Shader.SetGlobalTexture(InteractionMapId, Texture2D.blackTexture);
        Shader.SetGlobalTexture(InteractionRtAliasId, Texture2D.blackTexture);
        Shader.SetGlobalVector(InteractionCameraId, Vector4.zero);
        Shader.SetGlobalVector(InteractionCameraAliasId, Vector4.zero);
        Shader.SetGlobalVector(InteractionParamsId, Vector4.zero);
        Shader.SetGlobalVector(InteractionTexelSizeId, Vector4.zero);
        Shader.SetGlobalVector(GrassCameraForwardId, Vector4.zero);
        Shader.SetGlobalVector(GlobalInteractionConfigStateId, Vector4.zero);
        Shader.SetGlobalVector(GlobalInteractionConfigAId, Vector4.zero);
        Shader.SetGlobalVector(GlobalInteractionConfigBId, Vector4.zero);
    }

    protected static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        DestroyImmediateSafe(texture);
        texture = null;
    }

    protected static void DestroyImmediateSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    protected static GraphicsFormat GetCompatibleColorFormat()
    {
        GraphicsFormat preferredFormat = GraphicsFormat.R8G8B8A8_UNorm;
        if (SystemInfo.IsFormatSupported(preferredFormat, GraphicsFormatUsage.Render))
        {
            return preferredFormat;
        }

        GraphicsFormat fallbackFormat = SystemInfo.GetCompatibleFormat(preferredFormat, GraphicsFormatUsage.Render);
        return fallbackFormat != GraphicsFormat.None ? fallbackFormat : GraphicsFormat.B8G8R8A8_UNorm;
    }

    protected void NormalizeClearColor()
    {
        if (ApproximatelyColor(clearColor, Color.black))
        {
            clearColor = NeutralInteractionClearColor;
        }
    }

    protected Camera GetReferenceCamera()
    {
        if (Application.isPlaying)
        {
            return Camera.main;
        }

#if UNITY_EDITOR
        if (followSceneViewInEditMode && SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            return SceneView.lastActiveSceneView.camera;
        }
#endif

        return Camera.main;
    }

    protected static bool ApproximatelyColor(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) &&
               Mathf.Approximately(a.g, b.g) &&
               Mathf.Approximately(a.b, b.b) &&
               Mathf.Approximately(a.a, b.a);
    }

    protected static void ClearRenderTexture(RenderTexture texture, Color color)
    {
        if (texture == null)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, color);
        RenderTexture.active = previous;
    }

    protected void ClearInteractionHistory()
    {
        ClearRenderTexture(interactionTexture, clearColor);
        ClearRenderTexture(interactionScratchTexture, clearColor);
        ClearRenderTexture(interactionHistoryA, clearColor);
        ClearRenderTexture(interactionHistoryB, clearColor);

        historyAIsCurrent = true;
        historyContainsInteraction = false;
        hasHistoryRegionCenter = false;
    }

    protected void RefreshActiveInteractionConfig()
    {
        ActiveInteractionConfig = interactionConfig;
    }

    protected void ApplyGlobalInteractionConfig()
    {
        GrassInteractionConfig config = interactionConfig;
        if (config == null || !config.overrideMaterialInteraction)
        {
            Shader.SetGlobalVector(GlobalInteractionConfigStateId, Vector4.zero);
            Shader.SetGlobalVector(GlobalInteractionConfigAId, Vector4.zero);
            Shader.SetGlobalVector(GlobalInteractionConfigBId, Vector4.zero);
            return;
        }

        Shader.SetGlobalVector(
            GlobalInteractionConfigStateId,
            new Vector4(
                1f,
                config.enableInteraction ? 1f : 0f,
                config.interactionStrength,
                config.interactionPushAway));

        Shader.SetGlobalVector(
            GlobalInteractionConfigAId,
            new Vector4(
                config.interactionFlatten,
                config.interactionRadiusMultiplier,
                config.interactionVerticalRange,
                config.interactionTrail));

        Shader.SetGlobalVector(
            GlobalInteractionConfigBId,
            new Vector4(
                config.interactionRecoveryStrength,
                config.interactionRecoveryFrequency,
                config.interactionRecoveryNoiseScale,
                0f));
    }

    protected virtual void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!drawDebugRegion || drawDebugOnlyWhenSelected)
        {
            return;
        }

        DrawDebugRegionInternal(false);
#endif
    }

    protected virtual void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!drawDebugRegion)
        {
            return;
        }

        DrawDebugRegionInternal(true);
#endif
    }

#if UNITY_EDITOR
    private void DrawDebugRegionInternal(bool isSelected)
    {
        Vector3 followPosition = GetFollowPosition();
        Vector3 regionCenter = followPosition + worldOffset;
        float projectionHeight = followTarget != null ? followTarget.position.y : transform.position.y;
        Vector3 planeCenter = new Vector3(regionCenter.x, projectionHeight + DebugDrawHeightOffset, regionCenter.z);
        float extent = orthographicSize;
        Color outlineColor = GetDebugRegionColor(isSelected);
        Color fillColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a * 0.06f);

        Vector3[] quad =
        {
            planeCenter + new Vector3(-extent, 0f, -extent),
            planeCenter + new Vector3(-extent, 0f,  extent),
            planeCenter + new Vector3( extent, 0f,  extent),
            planeCenter + new Vector3( extent, 0f, -extent),
        };

        using (new Handles.DrawingScope(outlineColor))
        {
            Handles.DrawSolidRectangleWithOutline(quad, fillColor, outlineColor);

            if (drawDebugCross)
            {
                Handles.DrawLine(
                    planeCenter + new Vector3(-extent, 0f, 0f),
                    planeCenter + new Vector3(extent, 0f, 0f));
                Handles.DrawLine(
                    planeCenter + new Vector3(0f, 0f, -extent),
                    planeCenter + new Vector3(0f, 0f, extent));
            }

            if (drawDebugLabels && isSelected)
            {
                string label =
                    "Interaction Region\n" +
                    "size=" + (orthographicSize * 2f).ToString("0.0") + "m\n" +
                    "rt=" + ((int)resolution).ToString() + "\n" +
                    "shapes=" + lastCollectedShapeCount.ToString();
                Handles.Label(planeCenter + Vector3.up * 0.35f, label);
            }
        }
    }

    private Color GetDebugRegionColor(bool isSelected)
    {
        float alpha = isSelected ? debugRegionColor.a : debugRegionColor.a * 0.75f;
        return new Color(debugRegionColor.r, debugRegionColor.g, debugRegionColor.b, alpha);
    }
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void AssignDefaultShadersIfMissing()
    {
#if UNITY_EDITOR
        if (accumulationShader == null)
        {
            accumulationShader = FindDefaultShader("Hidden/Vit/GrassInteractionAccumulate", "SG_GrassInteractionAccumulate.shader");
            if (accumulationShader != null)
            {
                EditorUtility.SetDirty(this);
            }
        }

        if (batchStampShader == null)
        {
            batchStampShader = FindDefaultShader("Hidden/Vit/EnvironmentInteractionBatch", "SG_EnvironmentInteractionBatch.shader");
            if (batchStampShader != null)
            {
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }

#if UNITY_EDITOR
    private static Shader FindDefaultShader(string shaderName, string assetFileName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader != null)
        {
            return shader;
        }

        string searchName = Path.GetFileNameWithoutExtension(assetFileName);
        string[] guids = AssetDatabase.FindAssets(searchName + " t:Shader");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.Equals(Path.GetFileName(assetPath), assetFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private void ScheduleEditorRefresh()
    {
        EditorApplication.delayCall -= RefreshFromEditorDelay;
        EditorApplication.delayCall += RefreshFromEditorDelay;
    }

    private void RefreshFromEditorDelay()
    {
        EditorApplication.delayCall -= RefreshFromEditorDelay;
        if (this == null || !isActiveAndEnabled || Application.isPlaying)
        {
            return;
        }

        EnsureResources();
        UpdateInteractionState();
    }
#endif
}
