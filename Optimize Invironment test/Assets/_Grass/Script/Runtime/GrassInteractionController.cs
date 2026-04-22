using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Maintains the global render texture used by grass interaction shaders.
/// </summary>
[ExecuteAlways]
public sealed class GrassInteractionController : MonoBehaviour
{
    private enum InteractionResolution
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

    [Header("Tracking")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = new(0f, 18f, 0f);
    [SerializeField] private bool followSceneViewInEditMode = true;

    [Header("Render")]
    [SerializeField] private LayerMask cullingMask;
    [SerializeField] private float orthographicSize = 16f;
    [SerializeField] private float globalStrength = 1f;
    [SerializeField] private InteractionResolution resolution = InteractionResolution.Resolution512;
    [SerializeField] private Color clearColor = Color.black;

    private Camera interactionCamera;
    private RenderTexture interactionTexture;

    private void OnEnable()
    {
        EnsureResources();
        UpdateInteractionState();
    }

    private void OnDisable()
    {
        ReleaseResources();
        ClearGlobals();
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        globalStrength = Mathf.Max(0f, globalStrength);

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureResources();
        UpdateInteractionState();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureResources();
        UpdateInteractionState();
    }

    private void EnsureResources()
    {
        EnsureCamera();
        EnsureRenderTexture();
    }

    private void EnsureCamera()
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

    private void EnsureRenderTexture()
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
            Debug.LogError("GrassInteractionController failed to create the interaction RenderTexture.", this);
            return;
        }

        interactionCamera.targetTexture = interactionTexture;
    }

    private void UpdateInteractionState()
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
        Shader.SetGlobalVector(
            InteractionCameraId,
            new Vector4(
                interactionCamera.transform.position.x,
                interactionCamera.transform.position.y,
                interactionCamera.transform.position.z,
                interactionCamera.orthographicSize));
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
    }

    private Vector3 GetFollowPosition()
    {
        if (followTarget != null)
        {
            return followTarget.position;
        }

        if (Application.isPlaying)
        {
            if (Camera.main != null)
            {
                return Camera.main.transform.position;
            }

            return transform.position;
        }

#if UNITY_EDITOR
        if (followSceneViewInEditMode && SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            return SceneView.lastActiveSceneView.camera.transform.position;
        }
#endif

        if (Camera.main != null)
        {
            return Camera.main.transform.position;
        }

        return transform.position;
    }

    private void ReleaseResources()
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

    private static void ClearGlobals()
    {
        Shader.SetGlobalTexture(InteractionMapId, Texture2D.blackTexture);
        Shader.SetGlobalVector(InteractionCameraId, Vector4.zero);
        Shader.SetGlobalVector(InteractionParamsId, Vector4.zero);
        Shader.SetGlobalVector(InteractionTexelSizeId, Vector4.zero);
    }

    private static void DestroyImmediateSafe(Object target)
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

    private static GraphicsFormat GetCompatibleColorFormat()
    {
        GraphicsFormat preferredFormat = GraphicsFormat.R8G8B8A8_UNorm;
        if (SystemInfo.IsFormatSupported(preferredFormat, GraphicsFormatUsage.Render))
        {
            return preferredFormat;
        }

        GraphicsFormat fallbackFormat = SystemInfo.GetCompatibleFormat(preferredFormat, GraphicsFormatUsage.Render);
        return fallbackFormat != GraphicsFormat.None ? fallbackFormat : GraphicsFormat.B8G8R8A8_UNorm;
    }

    private static GraphicsFormat GetCompatibleDepthFormat()
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
}
