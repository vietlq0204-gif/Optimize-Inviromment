using UnityEngine;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages the global interaction camera, render texture, and shader parameters used by grass materials.
/// </summary>
[ExecuteAlways]
public class GrassInteractionSystem : MonoBehaviour
{
    private static readonly Color NeutralInteractionClearColor = new(0.5f, 0.5f, 0f, 0f);

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

    [Header("Tracking")]
    [SerializeField] protected Transform followTarget;
    [SerializeField] protected Vector3 worldOffset = new(0f, 18f, 0f);
    [SerializeField] protected bool followSceneViewInEditMode = true;

    [Header("Render")]
    [SerializeField] protected LayerMask cullingMask;
    [SerializeField] protected float orthographicSize = 16f;
    [SerializeField] protected float globalStrength = 1f;
    [SerializeField] protected InteractionResolution resolution = InteractionResolution.Resolution512;
    [SerializeField] protected Color clearColor = new(0.5f, 0.5f, 0f, 0f);
    [SerializeField] protected bool hideInteractionLayerFromGameCameras = true;

    protected Camera interactionCamera;
    protected RenderTexture interactionTexture;
    private readonly Dictionary<Camera, int> overriddenCameraMasks = new();

    protected virtual void OnEnable()
    {
        NormalizeClearColor();
        EnsureResources();
        UpdateInteractionState();
    }

    protected virtual void OnDisable()
    {
        RestoreGameplayCameraMasks();
        ReleaseResources();
        ClearGlobals();
    }

    protected virtual void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        globalStrength = Mathf.Max(0f, globalStrength);
        NormalizeClearColor();

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureResources();
        UpdateInteractionState();
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

    protected void EnsureResources()
    {
        EnsureCamera();
        EnsureRenderTexture();
    }

    protected virtual void EnsureCamera()
    {
        if (interactionCamera != null)
        {
            return;
        }

        Transform existingChild = transform.Find("Grass Interaction Camera");
        GameObject cameraObject;
        if (existingChild != null)
        {
            cameraObject = existingChild.gameObject;
        }
        else
        {
            cameraObject = new GameObject("Grass Interaction Camera");
            cameraObject.transform.SetParent(transform, false);
        }

        cameraObject.hideFlags = HideFlags.HideAndDontSave;

        interactionCamera = cameraObject.GetComponent<Camera>();
        if (interactionCamera == null)
        {
            interactionCamera = cameraObject.AddComponent<Camera>();
        }

        interactionCamera.enabled = true;
        interactionCamera.orthographic = true;
        interactionCamera.clearFlags = CameraClearFlags.SolidColor;
        interactionCamera.backgroundColor = clearColor;
        interactionCamera.allowHDR = false;
        interactionCamera.allowMSAA = false;
        interactionCamera.useOcclusionCulling = false;
        interactionCamera.depth = -100f;
        interactionCamera.nearClipPlane = 0.01f;
        interactionCamera.farClipPlane = 128f;
    }

    protected virtual void EnsureRenderTexture()
    {
        int textureSize = (int)resolution;
        GraphicsFormat colorFormat = GetCompatibleColorFormat();
        GraphicsFormat depthFormat = GetCompatibleDepthFormat();
        bool needsNewTexture =
            interactionTexture == null ||
            !interactionTexture.IsCreated() ||
            interactionTexture.width != textureSize ||
            interactionTexture.height != textureSize ||
            interactionTexture.graphicsFormat != colorFormat ||
            interactionTexture.depthStencilFormat != depthFormat;

        if (!needsNewTexture)
        {
            interactionCamera.targetTexture = interactionTexture;
            return;
        }

        if (interactionTexture != null)
        {
            interactionCamera.targetTexture = null;
            interactionTexture.Release();
            DestroyImmediateSafe(interactionTexture);
        }

        RenderTextureDescriptor descriptor = new(textureSize, textureSize)
        {
            msaaSamples = 1,
            volumeDepth = 1,
            graphicsFormat = colorFormat,
            depthStencilFormat = depthFormat,
            sRGB = false,
            useMipMap = false,
            autoGenerateMips = false,
        };

        interactionTexture = new RenderTexture(descriptor)
        {
            name = "GrassInteractionRT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        interactionTexture.Create();
        if (!interactionTexture.IsCreated())
        {
            Debug.LogError("GrassInteractionSystem failed to create the interaction RenderTexture.", this);
            return;
        }

        interactionCamera.targetTexture = interactionTexture;
    }

    protected virtual void UpdateInteractionState()
    {
        Vector3 followPosition = GetFollowPosition();
        float projectionHeight = followTarget != null ? followTarget.position.y : transform.position.y;

        interactionCamera.transform.SetPositionAndRotation(
            followPosition + worldOffset,
            Quaternion.Euler(90f, 0f, 0f));
        interactionCamera.orthographicSize = orthographicSize;
        interactionCamera.cullingMask = cullingMask;
        interactionCamera.backgroundColor = clearColor;

        Shader.SetGlobalTexture(InteractionMapId, interactionTexture);
        Shader.SetGlobalTexture(InteractionRtAliasId, interactionTexture);

        Vector4 cameraData = new(
            interactionCamera.transform.position.x,
            interactionCamera.transform.position.y,
            interactionCamera.transform.position.z,
            interactionCamera.orthographicSize);

        Shader.SetGlobalVector(InteractionCameraId, cameraData);
        Shader.SetGlobalVector(InteractionCameraAliasId, cameraData);
        Shader.SetGlobalVector(
            InteractionParamsId,
            new Vector4(1f, projectionHeight, globalStrength, 0f));
        Shader.SetGlobalVector(
            InteractionTexelSizeId,
            new Vector4(
                1f / interactionTexture.width,
                1f / interactionTexture.height,
                interactionTexture.width,
                interactionTexture.height));

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

        Shader.SetGlobalVector(
            GrassCameraForwardId,
            new Vector4(cameraForward.x, cameraForward.y, cameraForward.z, 0f));

        SyncGameplayCameraMasks();
    }

    protected Vector3 GetFollowPosition()
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
        if (interactionCamera != null)
        {
            interactionCamera.targetTexture = null;
        }

        if (interactionTexture != null)
        {
            interactionTexture.Release();
            DestroyImmediateSafe(interactionTexture);
            interactionTexture = null;
        }

        if (interactionCamera != null)
        {
            DestroyImmediateSafe(interactionCamera.gameObject);
            interactionCamera = null;
        }
    }

    private void SyncGameplayCameraMasks()
    {
        if (!hideInteractionLayerFromGameCameras)
        {
            RestoreGameplayCameraMasks();
            return;
        }

        int interactionMaskValue = cullingMask.value;
        if (interactionMaskValue == 0 || (interactionMaskValue & 1) != 0)
        {
            RestoreGameplayCameraMasks();
            return;
        }

        Camera[] cameras = Camera.allCameras;
        HashSet<Camera> activeCameras = new();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == interactionCamera || camera.cameraType != CameraType.Game)
            {
                continue;
            }

            activeCameras.Add(camera);
            if (!overriddenCameraMasks.ContainsKey(camera))
            {
                overriddenCameraMasks.Add(camera, camera.cullingMask);
            }

            int originalMask = overriddenCameraMasks[camera];
            camera.cullingMask = originalMask & ~interactionMaskValue;
        }

        if (overriddenCameraMasks.Count == 0)
        {
            return;
        }

        List<Camera> camerasToRestore = new();
        foreach (KeyValuePair<Camera, int> entry in overriddenCameraMasks)
        {
            if (entry.Key == null || !activeCameras.Contains(entry.Key))
            {
                if (entry.Key != null)
                {
                    entry.Key.cullingMask = entry.Value;
                }

                camerasToRestore.Add(entry.Key);
            }
        }

        for (int i = 0; i < camerasToRestore.Count; i++)
        {
            overriddenCameraMasks.Remove(camerasToRestore[i]);
        }
    }

    private void RestoreGameplayCameraMasks()
    {
        if (overriddenCameraMasks.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Camera, int> entry in overriddenCameraMasks)
        {
            if (entry.Key != null)
            {
                entry.Key.cullingMask = entry.Value;
            }
        }

        overriddenCameraMasks.Clear();
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

    protected static GraphicsFormat GetCompatibleDepthFormat()
    {
        GraphicsFormat preferredFormat = GraphicsFormat.D24_UNorm_S8_UInt;
        if (SystemInfo.IsFormatSupported(preferredFormat, GraphicsFormatUsage.Render))
        {
            return preferredFormat;
        }

        GraphicsFormat fallbackFormat = SystemInfo.GetCompatibleFormat(preferredFormat, GraphicsFormatUsage.Render);
        if (fallbackFormat != GraphicsFormat.None)
        {
            return fallbackFormat;
        }

        return GraphicsFormat.D16_UNorm;
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
}
