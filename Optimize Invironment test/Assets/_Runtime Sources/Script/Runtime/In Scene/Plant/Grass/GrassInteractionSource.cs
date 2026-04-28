using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drives grass interaction from contact and trail particle systems.
/// </summary>
[ExecuteAlways]
public sealed class GrassInteractionSource : MonoBehaviour
{
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private static readonly int RecoveryWeightId = Shader.PropertyToID("_RecoveryWeight");
    private static readonly int UseParticleColorEncodingId = Shader.PropertyToID("_UseParticleColorEncoding");
    private static readonly int DirectionalInfluenceId = Shader.PropertyToID("_DirectionalInfluence");

    [Header("Source")]
    [SerializeField] private GrassInteractionSourceProfile profile;
    [SerializeField] private int interactionLayer;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private bool autoCreateParticleSystems = true;

    [Header("Paint")]
    [SerializeField] private ParticleSystem contactParticles;
    [SerializeField] private ParticleSystem trailParticles;
    [SerializeField] private Shader interactionStampShader;

    private static Material sharedMaterial;
    private static bool missingShaderLogged;

    private MaterialPropertyBlock contactPropertyBlock;
    private MaterialPropertyBlock trailPropertyBlock;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastLossyScale;
    private Vector3 stableContactPosition;
    private Vector2 stablePlanarDirection = Vector2.up;
    private bool isStationary = true;
    private bool hasLastPosition;

    private void Reset()
    {
        AssignDefaultShaderIfMissing();
        EnsureParticleSystems();
        ApplyProfile();
    }

    private void OnEnable()
    {
        AssignDefaultShaderIfMissing();

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            ScheduleEditorRefresh();
#endif
            return;
        }

        EnsureParticleSystems();
        InitializeRuntimeState();
        ApplyProfile();
        PlaySystems();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= RefreshFromEditorDelay;
#endif
        StopSystems();
        hasLastPosition = false;
    }

    private void OnValidate()
    {
        interactionLayer = Mathf.Clamp(interactionLayer, 0, 31);
        heightOffset = Mathf.Max(-5f, heightOffset);
        AssignDefaultShaderIfMissing();

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (Application.isPlaying)
        {
            EnsureParticleSystems();
            ApplyProfile();
            return;
        }

#if UNITY_EDITOR
        ScheduleEditorRefresh();
#endif
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || profile == null)
        {
            return;
        }

        EnsureParticleSystems();
        if (contactParticles == null || trailParticles == null)
        {
            return;
        }

        PlaySystems();

        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        Vector3 currentLossyScale = transform.lossyScale;
        float deltaTime = GetDeltaTime();
        bool rootTransformChanged = HasRootTransformChanged(currentPosition, currentRotation, currentLossyScale);
        Vector2 planarDirection = GetFilteredPlanarDirection(currentPosition - lastPosition, deltaTime, out float planarSpeed);
        Vector3 contactPosition = GetStableContactPosition(currentPosition, planarSpeed, deltaTime);
        Vector3 paintPosition = contactPosition + Vector3.up * heightOffset;

        UpdateEmitterTransforms(paintPosition);
        UpdateParticleWriterState(planarDirection, planarSpeed);
        UpdateEmissionState(rootTransformChanged);

        lastPosition = currentPosition;
        lastRotation = currentRotation;
        lastLossyScale = currentLossyScale;
        hasLastPosition = true;
    }

    public void ApplyProfile()
    {
        if (profile == null)
        {
            return;
        }

        EnsureParticleSystems();
        if (contactParticles == null || trailParticles == null)
        {
            return;
        }

        ConfigureParticleSystem(contactParticles, false);
        ConfigureParticleSystem(trailParticles, true);
        UpdateParticleWriterState(stablePlanarDirection, 0f);
    }

    private void InitializeRuntimeState()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastLossyScale = transform.lossyScale;
        stableContactPosition = transform.position;
        stablePlanarDirection = Vector2.up;
        isStationary = true;
        hasLastPosition = false;
    }

    private void EnsureParticleSystems()
    {
        EnsureSharedMaterial(interactionStampShader);
        if (sharedMaterial == null)
        {
            return;
        }

        contactParticles = EnsureParticleSystem(contactParticles, "Grass Interaction Contact", false);
        trailParticles = EnsureParticleSystem(trailParticles, "Grass Interaction Trail", true);
    }

    private ParticleSystem EnsureParticleSystem(ParticleSystem particleSystem, string childName, bool trailSystem)
    {
        bool createdNewSystem = false;
        if (particleSystem == null)
        {
            if (!autoCreateParticleSystems)
            {
                return null;
            }

            Transform child = transform.Find(childName);
            GameObject childObject;
            if (child != null)
            {
                childObject = child.gameObject;
            }
            else
            {
                childObject = new GameObject(childName);
                childObject.transform.SetParent(transform, false);
            }

            childObject.layer = interactionLayer;

            particleSystem = childObject.GetComponent<ParticleSystem>();
            if (particleSystem == null)
            {
                particleSystem = childObject.AddComponent<ParticleSystem>();
                createdNewSystem = true;
            }

            if (childObject.GetComponent<ParticleSystemRenderer>() == null)
            {
                childObject.AddComponent<ParticleSystemRenderer>();
            }
        }

        particleSystem.gameObject.layer = interactionLayer;
        ApplyRendererDefaults(particleSystem);
        if (createdNewSystem)
        {
            ApplyParticleDefaults(particleSystem, trailSystem);
        }

        return particleSystem;
    }

    private void ApplyParticleDefaults(ParticleSystem particleSystem, bool trailSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startRotation = 0f;
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = trailSystem ? 192 : 64;
        main.startLifetime = trailSystem ? 0.95f : 0.2f;
        main.startSize = trailSystem ? 1.05f : 1.2f;
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = trailSystem ? 0f : 18f;
        emission.rateOverDistance = trailSystem ? 4.5f : 0f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAlphaFadeGradient(trailSystem));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve(trailSystem));
    }

    private void ConfigureParticleSystem(ParticleSystem particleSystem, bool trailSystem)
    {
        bool wasPlaying = particleSystem.isPlaying;
        if (wasPlaying && !Application.isPlaying)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        if (!particleSystem.isPlaying)
        {
            main.duration = Mathf.Max(main.duration, 1f);
        }
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startRotation = 0f;
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particleSystem.limitVelocityOverLifetime;
        limitVelocity.enabled = false;

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = false;

        ParticleSystem.TrailModule trails = particleSystem.trails;
        trails.enabled = false;

        if (particleSystem.colorOverLifetime.enabled == false)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAlphaFadeGradient(trailSystem));
        }

        if (particleSystem.sizeOverLifetime.enabled == false)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve(trailSystem));
        }

        ApplyRendererDefaults(particleSystem);

        if (wasPlaying && Application.isPlaying)
        {
            PlayIfNeeded(particleSystem);
        }
    }

    private void ApplyRendererDefaults(ParticleSystem particleSystem)
    {
        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = sharedMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private void UpdateEmitterTransforms(Vector3 paintPosition)
    {
        if (contactParticles != null)
        {
            contactParticles.transform.SetPositionAndRotation(paintPosition, Quaternion.identity);
        }

        if (trailParticles != null)
        {
            trailParticles.transform.SetPositionAndRotation(paintPosition, Quaternion.identity);
        }
    }

    private void UpdateParticleWriterState(Vector2 planarDirection, float planarSpeed)
    {
        Vector2 direction = planarSpeed >= Mathf.Max(profile.minimumDirectionalSpeed, 0.0001f) &&
                            planarDirection.sqrMagnitude > 0.0001f
            ? planarDirection.normalized
            : stablePlanarDirection;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        stablePlanarDirection = direction;

        UpdateRendererProperties(
            contactParticles,
            ref contactPropertyBlock,
            profile.contactSoftness,
            profile.contactDirectionalInfluence,
            profile.contactRecoveryWeight,
            direction);
        UpdateRendererProperties(
            trailParticles,
            ref trailPropertyBlock,
            profile.trailSoftness,
            profile.trailDirectionalInfluence,
            profile.trailRecoveryWeight,
            direction);
    }

    private static void UpdateRendererProperties(
        ParticleSystem particleSystem,
        ref MaterialPropertyBlock propertyBlock,
        float softness,
        float directionalInfluence,
        float recoveryWeight,
        Vector2 direction)
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetFloat(IntensityId, 1f);
        propertyBlock.SetFloat(SoftnessId, Mathf.Clamp01(softness));
        propertyBlock.SetFloat(DirectionalInfluenceId, Mathf.Clamp01(directionalInfluence));
        propertyBlock.SetFloat(RecoveryWeightId, Mathf.Clamp01(recoveryWeight));
        propertyBlock.SetFloat(UseParticleColorEncodingId, 0f);
        propertyBlock.SetVector(DirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void PlaySystems()
    {
        PlayIfNeeded(contactParticles);
        PlayIfNeeded(trailParticles);
    }

    private void StopSystems()
    {
        StopAndClear(contactParticles);
        StopAndClear(trailParticles);
    }

    private static void PlayIfNeeded(ParticleSystem particleSystem)
    {
        if (particleSystem != null && !particleSystem.isPlaying)
        {
            particleSystem.Play(true);
        }
    }

    private static void StopAndClear(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Clear(true);
    }

    private static Gradient CreateAlphaFadeGradient(bool trailSystem)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            trailSystem
                ? new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.82f, 0.14f),
                    new GradientAlphaKey(0.48f, 0.36f),
                    new GradientAlphaKey(0.2f, 0.62f),
                    new GradientAlphaKey(0.06f, 0.84f),
                    new GradientAlphaKey(0f, 0.94f),
                    new GradientAlphaKey(0f, 1f),
                }
                : new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0.22f, 0.84f),
                    new GradientAlphaKey(0f, 0.96f),
                    new GradientAlphaKey(0f, 1f),
                });
        return gradient;
    }

    private static AnimationCurve CreateSizeCurve(bool trailSystem)
    {
        return trailSystem
            ? new AnimationCurve(
                new Keyframe(0f, 0.92f),
                new Keyframe(0.5f, 1.04f),
                new Keyframe(1f, 1.12f))
            : new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 1.02f));
    }

    private Vector2 GetFilteredPlanarDirection(Vector3 delta, float deltaTime, out float planarSpeed)
    {
        Vector2 planarDelta = new(delta.x, delta.z);
        float rawDistance = planarDelta.magnitude;
        float rawSpeed = rawDistance / Mathf.Max(deltaTime, 0.0001f);
        float deadZone = 0.0025f;
        float speedEnterStationary = Mathf.Max(profile.minimumDirectionalSpeed * 2.75f, 0.12f);
        float speedExitStationary = speedEnterStationary * 1.35f;

        if (!hasLastPosition || rawDistance <= deadZone)
        {
            isStationary = true;
            planarSpeed = 0f;
            return Vector2.zero;
        }

        isStationary = isStationary ? rawSpeed < speedExitStationary : rawSpeed < speedEnterStationary;
        if (isStationary)
        {
            planarSpeed = 0f;
            return Vector2.zero;
        }

        Vector2 rawDirection = planarDelta / rawDistance;
        float smoothing = 1f - Mathf.Exp(-18f * deltaTime);
        stablePlanarDirection = Vector2.Lerp(stablePlanarDirection, rawDirection, smoothing);
        if (stablePlanarDirection.sqrMagnitude <= 0.0001f)
        {
            stablePlanarDirection = rawDirection;
        }
        else
        {
            stablePlanarDirection.Normalize();
        }

        planarSpeed = rawSpeed;
        return stablePlanarDirection;
    }

    private Vector3 GetStableContactPosition(Vector3 currentPosition, float planarSpeed, float deltaTime)
    {
        Vector3 targetPosition = currentPosition;
        float minimumSpeed = Mathf.Max(profile.minimumDirectionalSpeed, 0.0001f);
        if (!hasLastPosition)
        {
            stableContactPosition = targetPosition;
            return stableContactPosition;
        }

        if (planarSpeed < minimumSpeed)
        {
            float stationaryThreshold = 0.02f;
            Vector2 planarOffset = new(targetPosition.x - stableContactPosition.x, targetPosition.z - stableContactPosition.z);
            if (planarOffset.magnitude < stationaryThreshold)
            {
                targetPosition.x = stableContactPosition.x;
                targetPosition.z = stableContactPosition.z;
            }
        }

        float followSharpness = planarSpeed >= minimumSpeed ? 30f : 12f;
        float followT = 1f - Mathf.Exp(-followSharpness * deltaTime);
        stableContactPosition = Vector3.Lerp(stableContactPosition, targetPosition, followT);
        return stableContactPosition;
    }

    private static float GetDeltaTime()
    {
        return Application.isPlaying ? Mathf.Max(Time.deltaTime, 0.0001f) : (1f / 60f);
    }

    private void UpdateEmissionState(bool rootTransformChanged)
    {
        SetEmissionEnabled(contactParticles, rootTransformChanged);
        SetEmissionEnabled(trailParticles, rootTransformChanged);
    }

    private static void SetEmissionEnabled(ParticleSystem particleSystem, bool enabled)
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = enabled;
    }

    private bool HasRootTransformChanged(Vector3 currentPosition, Quaternion currentRotation, Vector3 currentLossyScale)
    {
        if (!hasLastPosition)
        {
            return false;
        }

        const float positionThreshold = 0.0005f;
        const float scaleThreshold = 0.0005f;
        const float rotationThreshold = 0.05f;

        bool positionChanged = Vector3.SqrMagnitude(currentPosition - lastPosition) > positionThreshold * positionThreshold;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastRotation) > rotationThreshold;
        bool scaleChanged = Vector3.SqrMagnitude(currentLossyScale - lastLossyScale) > scaleThreshold * scaleThreshold;
        return positionChanged || rotationChanged || scaleChanged;
    }

    private static void EnsureSharedMaterial(Shader defaultShader)
    {
        Shader shader = defaultShader != null ? defaultShader : Shader.Find("Hidden/Vit/GrassInteractionStamp");
        if (shader == null)
        {
            if (!missingShaderLogged)
            {
                Debug.LogWarning("GrassInteractionSource could not find shader 'Hidden/Vit/GrassInteractionStamp'.", null);
                missingShaderLogged = true;
            }

            return;
        }

        if (sharedMaterial != null && sharedMaterial.shader == shader)
        {
            return;
        }

        if (sharedMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(sharedMaterial);
            }
            else
            {
                DestroyImmediate(sharedMaterial);
            }
        }

        sharedMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        missingShaderLogged = false;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void AssignDefaultShaderIfMissing()
    {
#if UNITY_EDITOR
        if (interactionStampShader == null)
        {
            interactionStampShader = FindDefaultShader("Hidden/Vit/GrassInteractionStamp", "SG_GrassInteractionStamp.shader");
            if (interactionStampShader != null)
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
        string[] guids = AssetDatabase.FindAssets($"{searchName} t:Shader");
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

        StopSystems();
        EnsureParticleSystems();
        ApplyProfile();
    }
#endif
}
