using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Paints grass interaction into the global render texture using particle systems.
/// </summary>
[ExecuteAlways]
public sealed class GrassInteractionSource : MonoBehaviour
{
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private static readonly int DirectionalInfluenceId = Shader.PropertyToID("_DirectionalInfluence");

    [Header("Source")]
    [SerializeField] private GrassInteractionSourceProfile profile;
    [SerializeField] private int interactionLayer = 0;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private bool autoCreateParticleSystems = true;

    [Header("Paint")]
    [SerializeField] private ParticleSystem contactParticles;
    [SerializeField] private ParticleSystem trailParticles;

    private static Material sharedMaterial;
    private static bool missingShaderLogged;

    private MaterialPropertyBlock contactPropertyBlock;
    private MaterialPropertyBlock trailPropertyBlock;
    private float contactEmitAccumulator;
    private float trailDistanceAccumulator;
    private Vector3 lastPosition;
    private Vector3 lastPaintPosition;
    private Vector3 stableContactPosition;
    private Vector2 stablePlanarDirection = Vector2.up;
    private bool isStationary;
    private bool hasLastPosition;

    private void Reset()
    {
        EnsureParticleSystems();
        ApplyProfile();
    }

    private void OnEnable()
    {
        EnsureParticleSystems();
        ApplyProfile();

        lastPosition = transform.position;
        stableContactPosition = transform.position;
        lastPaintPosition = transform.position + Vector3.up * heightOffset;
        stablePlanarDirection = Vector2.up;
        isStationary = true;
        hasLastPosition = false;
        contactEmitAccumulator = 0f;
        trailDistanceAccumulator = 0f;

        PlaySystems();
    }

    private void OnDisable()
    {
        StopAndClear(contactParticles);
        StopAndClear(trailParticles);
        lastPaintPosition = transform.position + Vector3.up * heightOffset;
        stablePlanarDirection = Vector2.up;
        isStationary = true;
        hasLastPosition = false;
        contactEmitAccumulator = 0f;
        trailDistanceAccumulator = 0f;
    }

    private void OnValidate()
    {
        interactionLayer = Mathf.Clamp(interactionLayer, 0, 31);
        heightOffset = Mathf.Max(-5f, heightOffset);

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureParticleSystems();
        ApplyProfile();
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
        float deltaTime = GetDeltaTime();
        Vector3 delta = currentPosition - lastPosition;
        Vector2 planarDirection = GetFilteredPlanarDirection(delta, deltaTime, out float planarDistance, out float planarSpeed);
        Vector3 contactPosition = GetStableContactPosition(currentPosition, planarSpeed, deltaTime);
        Vector3 paintPosition = contactPosition + Vector3.up * heightOffset;

        EmitContactParticles(paintPosition, planarDirection, planarSpeed, deltaTime);
        EmitTrailParticles(lastPaintPosition, paintPosition, planarDirection, planarDistance, planarSpeed);

        lastPosition = currentPosition;
        lastPaintPosition = paintPosition;
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

        ConfigureParticleSystem(contactParticles, profile.contactMaxParticles, profile.contactSoftness, profile.contactDirectionalInfluence, false);
        ConfigureParticleSystem(trailParticles, profile.trailMaxParticles, profile.trailSoftness, profile.trailDirectionalInfluence, true);
    }

    private void EmitContactParticles(Vector3 paintPosition, Vector2 planarDirection, float planarSpeed, float deltaTime)
    {
        if (profile.contactRefreshRate <= 0f || profile.contactLifetime <= 0f || profile.contactIntensity <= 0f)
        {
            return;
        }

        float effectiveRefreshRate = GetStableContactRefreshRate();
        contactEmitAccumulator += deltaTime * effectiveRefreshRate;
        while (contactEmitAccumulator >= 1f)
        {
            contactEmitAccumulator -= 1f;
            EmitParticle(
                contactParticles,
                paintPosition,
                profile.contactSize,
                profile.contactLifetime,
                profile.contactIntensity,
                GetEncodedDirection(planarDirection, planarSpeed, profile.minimumDirectionalSpeed),
                0f);
        }
    }

    private float GetStableContactRefreshRate()
    {
        const float minimumOverlap = 5f;
        float profileRefresh = Mathf.Max(profile.contactRefreshRate, 0.01f);
        float overlapDrivenRefresh = minimumOverlap / Mathf.Max(profile.contactLifetime, 0.01f);
        return Mathf.Max(profileRefresh, overlapDrivenRefresh);
    }

    private void EmitTrailParticles(
        Vector3 previousPosition,
        Vector3 currentPosition,
        Vector2 planarDirection,
        float planarDistance,
        float planarSpeed)
    {
        if (!hasLastPosition ||
            profile.trailRateOverDistance <= 0f ||
            profile.trailLifetime <= 0f ||
            profile.trailIntensity <= 0f ||
            planarSpeed < profile.minimumDirectionalSpeed ||
            planarDistance <= 0.0001f)
        {
            if (!hasLastPosition)
            {
                trailDistanceAccumulator = 0f;
            }

            return;
        }

        float spacing = 1f / Mathf.Max(profile.trailRateOverDistance, 0.0001f);
        float nextDistance = spacing - trailDistanceAccumulator;
        while (nextDistance <= planarDistance)
        {
            float t = nextDistance / Mathf.Max(planarDistance, 0.0001f);
            Vector3 emitPosition = Vector3.Lerp(previousPosition, currentPosition, t);
            EmitParticle(
                trailParticles,
                emitPosition,
                profile.trailSize,
                profile.trailLifetime,
                profile.trailIntensity,
                GetEncodedDirection(planarDirection, planarSpeed, profile.minimumDirectionalSpeed),
                profile.trailRecoveryWeight);
            nextDistance += spacing;
        }

        trailDistanceAccumulator = (trailDistanceAccumulator + planarDistance) % spacing;
    }

    private void EmitParticle(
        ParticleSystem particleSystem,
        Vector3 worldPosition,
        float startSize,
        float lifetime,
        float intensity,
        Vector2 encodedDirection,
        float recoveryWeight)
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.EmitParams emitParams = new()
        {
            position = worldPosition,
            startLifetime = lifetime,
            startSize = startSize,
            startColor = new Color(
                encodedDirection.x,
                encodedDirection.y,
                Mathf.Clamp01(recoveryWeight),
                Mathf.Clamp01(intensity)),
            applyShapeToPosition = false,
        };

        particleSystem.Emit(emitParams, 1);
    }

    private void EnsureParticleSystems()
    {
        EnsureSharedMaterial();
        if (sharedMaterial == null)
        {
            return;
        }

        contactParticles = EnsureParticleSystem(contactParticles, "Grass Interaction Contact");
        trailParticles = EnsureParticleSystem(trailParticles, "Grass Interaction Trail");
    }

    private ParticleSystem EnsureParticleSystem(ParticleSystem particleSystem, string childName)
    {
        if (particleSystem != null)
        {
            particleSystem.gameObject.layer = interactionLayer;
            return particleSystem;
        }

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
        }

        ParticleSystemRenderer renderer = childObject.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            renderer = childObject.AddComponent<ParticleSystemRenderer>();
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

        return particleSystem;
    }

    private void ConfigureParticleSystem(
        ParticleSystem particleSystem,
        int maxParticles,
        float softness,
        float directionalInfluence,
        bool trailSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startRotation = 0f;
        main.maxParticles = Mathf.Max(maxParticles, 1);
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;

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

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAlphaFadeGradient(trailSystem));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve(trailSystem));

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = sharedMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;

            MaterialPropertyBlock propertyBlock = trailSystem ? trailPropertyBlock ??= new MaterialPropertyBlock() : contactPropertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetFloat(IntensityId, 1f);
            propertyBlock.SetFloat(SoftnessId, Mathf.Clamp01(softness));
            propertyBlock.SetFloat(DirectionalInfluenceId, Mathf.Clamp01(directionalInfluence));
            propertyBlock.SetVector(DirectionId, Vector4.zero);
            renderer.SetPropertyBlock(propertyBlock);
        }

        particleSystem.gameObject.layer = interactionLayer;
        if (!particleSystem.isPlaying)
        {
            particleSystem.Play(true);
        }
    }

    private void PlaySystems()
    {
        PlayIfNeeded(contactParticles);
        PlayIfNeeded(trailParticles);
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
                    new GradientAlphaKey(0.92f, 0.3f),
                    new GradientAlphaKey(0f, 1f),
                }
                : new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.75f),
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
                new Keyframe(1f, 1.15f))
            : new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 1.02f));
    }

    private static Vector2 GetEncodedDirection(Vector2 planarDirection, float planarSpeed, float minimumDirectionalSpeed)
    {
        if (planarSpeed < minimumDirectionalSpeed || planarDirection.sqrMagnitude <= 0.0001f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        Vector2 normalizedDirection = planarDirection.normalized;
        return normalizedDirection * 0.5f + Vector2.one * 0.5f;
    }

    private Vector2 GetFilteredPlanarDirection(Vector3 delta, float deltaTime, out float planarDistance, out float planarSpeed)
    {
        Vector2 planarDelta = new(delta.x, delta.z);
        float rawDistance = planarDelta.magnitude;
        float rawSpeed = rawDistance / Mathf.Max(deltaTime, 0.0001f);
        float distanceDeadZone = Mathf.Max(profile.contactSize * 0.015f, 0.0025f);
        float speedEnterStationary = Mathf.Max(profile.minimumDirectionalSpeed * 2.75f, 0.12f);
        float speedExitStationary = speedEnterStationary * 1.35f;

        if (!hasLastPosition || rawDistance <= distanceDeadZone)
        {
            isStationary = true;
            planarDistance = 0f;
            planarSpeed = 0f;
            return Vector2.zero;
        }

        if (isStationary)
        {
            isStationary = rawSpeed < speedExitStationary;
        }
        else
        {
            isStationary = rawSpeed < speedEnterStationary;
        }

        if (isStationary)
        {
            planarDistance = 0f;
            planarSpeed = 0f;
            return Vector2.zero;
        }

        Vector2 rawDirection = planarDelta / rawDistance;
        float directionSmoothing = 1f - Mathf.Exp(-18f * deltaTime);
        stablePlanarDirection = Vector2.Lerp(stablePlanarDirection, rawDirection, directionSmoothing);
        if (stablePlanarDirection.sqrMagnitude <= 0.0001f)
        {
            stablePlanarDirection = rawDirection;
        }
        else
        {
            stablePlanarDirection.Normalize();
        }

        planarDistance = rawDistance;
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
            float stationaryThreshold = Mathf.Max(profile.contactSize * 0.08f, 0.015f);
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

    private static void EnsureSharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return;
        }

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
