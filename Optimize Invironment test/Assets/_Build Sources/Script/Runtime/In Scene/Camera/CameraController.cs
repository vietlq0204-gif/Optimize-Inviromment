using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraController : MonoBehaviour
{
    private const float FocusProbeRadius = 0.35f;
    private const float FocusPadding = 0.15f;
    private const float FocusBlendSpeed = 8f;
    private const float DefaultAperture = 5.6f;
    private const float NearFocusAperture = 2.8f;
    private const float BokehFocalLength = 50f;
    private const int BokehBladeCount = 5;
    private const float BokehBladeCurvature = 1f;
    private const float BokehBladeRotation = 0f;

    [Header("Target")]
    public Transform target;
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0f, 2f, -4f);
    public float mouseSensitivity = 100f;
    public float smoothSpeed = 0.125f;
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float lookAtHeight = 1.5f;
    [SerializeField] private float mouseDeltaScale = 0.02f;

    [Header("Camera Blur")]
    [SerializeField] private bool enableCameraBlur = true;
    [SerializeField] private float blurDistance = 6f;
    [SerializeField] private float nearFocusDistance = 1.5f;
    [SerializeField] [Range(0.25f, 2f)] private float blurStrength = 1f;

    public float MinPitch
    {
        get => minPitch;
        set => minPitch = Mathf.Min(value, maxPitch);
    }

    public float MaxPitch
    {
        get => maxPitch;
        set => maxPitch = Mathf.Max(value, minPitch);
    }

    public float LookAtHeight
    {
        get => lookAtHeight;
        set => lookAtHeight = value;
    }

    public float MouseDeltaScale
    {
        get => mouseDeltaScale;
        set => mouseDeltaScale = Mathf.Max(0f, value);
    }

    public bool EnableCameraBlur
    {
        get => enableCameraBlur;
        set => enableCameraBlur = value;
    }

    public float BlurDistance
    {
        get => blurDistance;
        set => blurDistance = Mathf.Max(0.1f, value);
    }

    public float NearFocusDistance
    {
        get => nearFocusDistance;
        set => nearFocusDistance = Mathf.Max(0.1f, value);
    }

    public float BlurStrength
    {
        get => blurStrength;
        set => blurStrength = Mathf.Clamp(value, 0.25f, 2f);
    }

    private float yawRotation;
    private float pitchRotation;
    private Coroutine shakeRoutine;
    private Vector3 shakeOffset;
    private readonly Collider[] nearbyFocusColliders = new Collider[16];
    private readonly RaycastHit[] focusHits = new RaycastHit[16];
    private Volume blurVolumeInstance;
    private DepthOfField depthOfField;
    private float currentFocusDistance;
    private bool isBlurSetupInitialized;
    private bool isBlurOverrideActive;
    private bool hasCachedDepthOfFieldState;
    private DepthOfFieldState cachedDepthOfFieldState;

    private struct DepthOfFieldState
    {
        public bool active;
        public bool modeOverride;
        public DepthOfFieldMode mode;
        public bool gaussianStartOverride;
        public float gaussianStart;
        public bool gaussianEndOverride;
        public float gaussianEnd;
        public bool gaussianMaxRadiusOverride;
        public float gaussianMaxRadius;
        public bool highQualitySamplingOverride;
        public bool highQualitySampling;
        public bool focusDistanceOverride;
        public float focusDistance;
        public bool apertureOverride;
        public float aperture;
        public bool focalLengthOverride;
        public float focalLength;
        public bool bladeCountOverride;
        public int bladeCount;
        public bool bladeCurvatureOverride;
        public float bladeCurvature;
        public bool bladeRotationOverride;
        public float bladeRotation;
    }

    private void Awake()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        pitchRotation = NormalizePitch(eulerAngles.x);
        yawRotation = eulerAngles.y;
        currentFocusDistance = Mathf.Max(0.1f, blurDistance);
        ResolveInputReader();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            RestoreCameraBlurState();
            return;
        }

        ResolveInputReader();

        Vector2 lookInput = ReadLookInput();
        yawRotation += lookInput.x * mouseSensitivity * Time.deltaTime;
        pitchRotation -= lookInput.y * mouseSensitivity * Time.deltaTime;
        pitchRotation = Mathf.Clamp(pitchRotation, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitchRotation, yawRotation, 0f);
        Vector3 desiredPosition = target.position + rotation * offset;
        Vector3 currentBasePosition = transform.position - shakeOffset;
        Vector3 smoothedPosition = Vector3.Lerp(currentBasePosition, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition + shakeOffset;
        transform.LookAt(target.position + Vector3.up * lookAtHeight);

        UpdateCameraBlur();
    }

    private void OnDisable()
    {
        RestoreCameraBlurState();
    }

    private void OnValidate()
    {
        blurDistance = Mathf.Max(0.1f, blurDistance);
        nearFocusDistance = Mathf.Max(0.1f, nearFocusDistance);
        blurStrength = Mathf.Clamp(blurStrength, 0.25f, 2f);
    }

    private Vector2 ReadLookInput()
    {
        return inputReader != null ? inputReader.LookInput * mouseDeltaScale : Vector2.zero;
    }

    private void ResolveInputReader()
    {
        if (inputReader != null)
        {
            return;
        }

        if (target != null)
        {
            inputReader = target.GetComponent<PlayerInputReader>();
            if (inputReader != null)
            {
                return;
            }
        }

        if (Camera.main != null && Camera.main.transform != null)
        {
            inputReader = FindFirstObjectByType<PlayerInputReader>();
        }
    }

    private void UpdateCameraBlur()
    {
        if (!enableCameraBlur)
        {
            RestoreCameraBlurState();
            return;
        }

        if (!EnsureCameraBlurSetup())
        {
            return;
        }

        float targetFocusDistance = Mathf.Max(0.1f, blurDistance);
        bool hasNearFocus = TryGetNearFocusDistance(out float nearObjectFocusDistance);

        if (hasNearFocus)
        {
            targetFocusDistance = nearObjectFocusDistance;
        }

        currentFocusDistance = Mathf.MoveTowards(
            currentFocusDistance,
            targetFocusDistance,
            FocusBlendSpeed * Time.deltaTime);

        ApplyCameraBlur(currentFocusDistance, hasNearFocus);
    }

    private bool TryGetNearFocusDistance(out float focusDistance)
    {
        focusDistance = 0f;

        if (TryGetNearbyFocusDistance(out focusDistance))
        {
            return true;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            FocusProbeRadius,
            transform.forward,
            focusHits,
            nearFocusDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return false;
        }

        float nearestHitDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = focusHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (target != null && hitCollider.transform.IsChildOf(target))
            {
                continue;
            }

            float hitDistance = focusHits[i].distance;
            if (hitDistance < 0.001f || hitDistance >= nearestHitDistance)
            {
                continue;
            }

            nearestHitDistance = hitDistance;
        }

        if (float.IsPositiveInfinity(nearestHitDistance))
        {
            return false;
        }

        focusDistance = Mathf.Max(0.1f, nearestHitDistance + FocusPadding);
        return true;
    }

    private bool TryGetNearbyFocusDistance(out float focusDistance)
    {
        focusDistance = 0f;

        int colliderCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            nearFocusDistance,
            nearbyFocusColliders,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (colliderCount <= 0)
        {
            return false;
        }

        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider candidate = nearbyFocusColliders[i];
            if (candidate == null)
            {
                continue;
            }

            if (target != null && candidate.transform.IsChildOf(target))
            {
                continue;
            }

            float distance = GetNearbyColliderDistance(candidate, transform.position);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
        }

        if (float.IsPositiveInfinity(nearestDistance))
        {
            return false;
        }

        focusDistance = Mathf.Max(0.1f, nearestDistance + FocusPadding);
        return true;
    }

    private static float GetNearbyColliderDistance(Collider candidate, Vector3 point)
    {
        if (candidate is TerrainCollider terrainCollider)
        {
            return GetTerrainDistance(terrainCollider, point);
        }

        if (candidate is MeshCollider meshCollider && !meshCollider.convex)
        {
            return Mathf.Sqrt(candidate.bounds.SqrDistance(point));
        }

        Vector3 closestPoint = candidate.ClosestPoint(point);
        return Vector3.Distance(point, closestPoint);
    }

    private static float GetTerrainDistance(TerrainCollider terrainCollider, Vector3 point)
    {
        Terrain terrain = terrainCollider.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            return Mathf.Sqrt(terrainCollider.bounds.SqrDistance(point));
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        bool insideTerrainXZ =
            point.x >= terrainPosition.x &&
            point.x <= terrainPosition.x + terrainSize.x &&
            point.z >= terrainPosition.z &&
            point.z <= terrainPosition.z + terrainSize.z;

        if (!insideTerrainXZ)
        {
            return Mathf.Sqrt(terrainCollider.bounds.SqrDistance(point));
        }

        float terrainHeight = terrain.SampleHeight(point) + terrainPosition.y;
        return Mathf.Abs(point.y - terrainHeight);
    }

    private bool EnsureCameraBlurSetup()
    {
        if (isBlurSetupInitialized && depthOfField != null)
        {
            return true;
        }

        blurVolumeInstance = FindBestBlurVolume();
        if (blurVolumeInstance == null)
        {
            return false;
        }

        VolumeProfile runtimeProfile = blurVolumeInstance.profile;
        if (!runtimeProfile.TryGet(out depthOfField))
        {
            depthOfField = runtimeProfile.Add<DepthOfField>();
        }

        CacheDepthOfFieldState(depthOfField);
        isBlurSetupInitialized = true;
        return true;
    }

    private Volume FindBestBlurVolume()
    {
        int volumeMask = ~0;
        UniversalAdditionalCameraData cameraData = GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            volumeMask = cameraData.volumeLayerMask.value;
        }

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        Volume bestVolume = null;
        float bestPriority = float.NegativeInfinity;

        foreach (Volume candidate in volumes)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.isGlobal || candidate.weight <= 0f)
            {
                continue;
            }

            if ((volumeMask & (1 << candidate.gameObject.layer)) == 0)
            {
                continue;
            }

            if (bestVolume == null || candidate.priority > bestPriority)
            {
                bestVolume = candidate;
                bestPriority = candidate.priority;
            }
        }

        return bestVolume;
    }

    private void CacheDepthOfFieldState(DepthOfField dof)
    {
        cachedDepthOfFieldState = new DepthOfFieldState
        {
            active = dof.active,
            modeOverride = dof.mode.overrideState,
            mode = dof.mode.value,
            gaussianStartOverride = dof.gaussianStart.overrideState,
            gaussianStart = dof.gaussianStart.value,
            gaussianEndOverride = dof.gaussianEnd.overrideState,
            gaussianEnd = dof.gaussianEnd.value,
            gaussianMaxRadiusOverride = dof.gaussianMaxRadius.overrideState,
            gaussianMaxRadius = dof.gaussianMaxRadius.value,
            highQualitySamplingOverride = dof.highQualitySampling.overrideState,
            highQualitySampling = dof.highQualitySampling.value,
            focusDistanceOverride = dof.focusDistance.overrideState,
            focusDistance = dof.focusDistance.value,
            apertureOverride = dof.aperture.overrideState,
            aperture = dof.aperture.value,
            focalLengthOverride = dof.focalLength.overrideState,
            focalLength = dof.focalLength.value,
            bladeCountOverride = dof.bladeCount.overrideState,
            bladeCount = dof.bladeCount.value,
            bladeCurvatureOverride = dof.bladeCurvature.overrideState,
            bladeCurvature = dof.bladeCurvature.value,
            bladeRotationOverride = dof.bladeRotation.overrideState,
            bladeRotation = dof.bladeRotation.value
        };

        hasCachedDepthOfFieldState = true;
    }

    private void ApplyCameraBlur(float focusDistance, bool hasNearFocus)
    {
        if (!EnsureCameraBlurSetup())
        {
            return;
        }

        float blurStrength01 = Mathf.InverseLerp(0.25f, 2f, blurStrength);
        float farAperture = Mathf.Lerp(8f, DefaultAperture, blurStrength01);
        float nearAperture = Mathf.Lerp(DefaultAperture, NearFocusAperture, blurStrength01);
        float targetAperture = hasNearFocus ? nearAperture : farAperture;

        depthOfField.active = true;
        depthOfField.mode.overrideState = true;
        depthOfField.mode.value = DepthOfFieldMode.Bokeh;
        depthOfField.focusDistance.overrideState = true;
        depthOfField.focusDistance.value = focusDistance;
        depthOfField.aperture.overrideState = true;
        depthOfField.aperture.value = targetAperture;
        depthOfField.focalLength.overrideState = true;
        depthOfField.focalLength.value = BokehFocalLength;
        depthOfField.bladeCount.overrideState = true;
        depthOfField.bladeCount.value = BokehBladeCount;
        depthOfField.bladeCurvature.overrideState = true;
        depthOfField.bladeCurvature.value = BokehBladeCurvature;
        depthOfField.bladeRotation.overrideState = true;
        depthOfField.bladeRotation.value = BokehBladeRotation;

        isBlurOverrideActive = true;
    }

    private void RestoreCameraBlurState()
    {
        if (!isBlurSetupInitialized || !isBlurOverrideActive || depthOfField == null || !hasCachedDepthOfFieldState)
        {
            return;
        }

        depthOfField.active = cachedDepthOfFieldState.active;
        depthOfField.mode.overrideState = cachedDepthOfFieldState.modeOverride;
        depthOfField.mode.value = cachedDepthOfFieldState.mode;
        depthOfField.gaussianStart.overrideState = cachedDepthOfFieldState.gaussianStartOverride;
        depthOfField.gaussianStart.value = cachedDepthOfFieldState.gaussianStart;
        depthOfField.gaussianEnd.overrideState = cachedDepthOfFieldState.gaussianEndOverride;
        depthOfField.gaussianEnd.value = cachedDepthOfFieldState.gaussianEnd;
        depthOfField.gaussianMaxRadius.overrideState = cachedDepthOfFieldState.gaussianMaxRadiusOverride;
        depthOfField.gaussianMaxRadius.value = cachedDepthOfFieldState.gaussianMaxRadius;
        depthOfField.highQualitySampling.overrideState = cachedDepthOfFieldState.highQualitySamplingOverride;
        depthOfField.highQualitySampling.value = cachedDepthOfFieldState.highQualitySampling;
        depthOfField.focusDistance.overrideState = cachedDepthOfFieldState.focusDistanceOverride;
        depthOfField.focusDistance.value = cachedDepthOfFieldState.focusDistance;
        depthOfField.aperture.overrideState = cachedDepthOfFieldState.apertureOverride;
        depthOfField.aperture.value = cachedDepthOfFieldState.aperture;
        depthOfField.focalLength.overrideState = cachedDepthOfFieldState.focalLengthOverride;
        depthOfField.focalLength.value = cachedDepthOfFieldState.focalLength;
        depthOfField.bladeCount.overrideState = cachedDepthOfFieldState.bladeCountOverride;
        depthOfField.bladeCount.value = cachedDepthOfFieldState.bladeCount;
        depthOfField.bladeCurvature.overrideState = cachedDepthOfFieldState.bladeCurvatureOverride;
        depthOfField.bladeCurvature.value = cachedDepthOfFieldState.bladeCurvature;
        depthOfField.bladeRotation.overrideState = cachedDepthOfFieldState.bladeRotationOverride;
        depthOfField.bladeRotation.value = cachedDepthOfFieldState.bladeRotation;

        isBlurOverrideActive = false;
    }

    private static float NormalizePitch(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    public void ShakeCamera(float duration = 0.15f, float magnitude = 0.1f)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(Shake(duration, magnitude));
    }

    private IEnumerator Shake(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * magnitude,
                Random.Range(-1f, 1f) * magnitude,
                0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeOffset = Vector3.zero;
        shakeRoutine = null;
    }
}
