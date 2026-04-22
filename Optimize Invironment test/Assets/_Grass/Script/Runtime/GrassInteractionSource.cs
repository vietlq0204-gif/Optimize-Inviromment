using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates a simple radial stamp so moving objects can bend nearby grass.
/// </summary>
[ExecuteAlways]
public sealed class GrassInteractionSource : MonoBehaviour
{
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    [Header("Stamp")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float intensity = 1f;
    [SerializeField] private float softness = 0.6f;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private int interactionLayer = 0;

    private static Mesh quadMesh;
    private static Material sharedMaterial;
    private static bool missingShaderLogged;

    private GameObject stampObject;
    private MeshRenderer stampRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        EnsureStamp();
        SyncStamp();
    }

    private void OnDisable()
    {
        DestroyStamp();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        intensity = Mathf.Max(0f, intensity);
        softness = Mathf.Clamp01(softness);

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureStamp();
        SyncStamp();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureStamp();
        SyncStamp();
    }

    private void EnsureStamp()
    {
        if (stampObject != null)
        {
            return;
        }

        EnsureSharedResources();
        if (quadMesh == null || sharedMaterial == null)
        {
            return;
        }

        stampObject = new GameObject("Grass Interaction Stamp");
        stampObject.transform.SetParent(transform, false);
        stampObject.hideFlags = HideFlags.HideAndDontSave;
        stampObject.layer = Mathf.Clamp(interactionLayer, 0, 31);

        MeshFilter meshFilter = stampObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = quadMesh;

        stampRenderer = stampObject.AddComponent<MeshRenderer>();
        stampRenderer.sharedMaterial = sharedMaterial;
        stampRenderer.shadowCastingMode = ShadowCastingMode.Off;
        stampRenderer.receiveShadows = false;
        stampRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        stampRenderer.lightProbeUsage = LightProbeUsage.Off;
        stampRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        stampRenderer.allowOcclusionWhenDynamic = false;

        propertyBlock = new MaterialPropertyBlock();
    }

    private void SyncStamp()
    {
        if (stampObject == null || stampRenderer == null)
        {
            return;
        }

        stampObject.layer = Mathf.Clamp(interactionLayer, 0, 31);
        stampObject.transform.SetPositionAndRotation(
            transform.position + Vector3.up * heightOffset,
            Quaternion.Euler(90f, 0f, 0f));
        stampObject.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        propertyBlock.SetFloat(IntensityId, intensity);
        propertyBlock.SetFloat(SoftnessId, softness);
        stampRenderer.SetPropertyBlock(propertyBlock);
    }

    private void DestroyStamp()
    {
        if (stampObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(stampObject);
        }
        else
        {
            DestroyImmediate(stampObject);
        }

        stampObject = null;
        stampRenderer = null;
        propertyBlock = null;
    }

    private static void EnsureSharedResources()
    {
        if (quadMesh == null)
        {
            quadMesh = CreateQuadMesh();
        }

        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Vit/GrassInteractionStamp");
            if (shader == null)
            {
                if (!missingShaderLogged)
                {
                    Debug.LogWarning("GrassInteractionSource could not find shader 'Hidden/Vit/GrassInteractionStamp'.", null);
                    missingShaderLogged = true;
                }

                return;
            }

            sharedMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            missingShaderLogged = false;
        }
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "Grass Interaction Quad",
            hideFlags = HideFlags.HideAndDontSave,
        };

        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
        });
        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }
}
