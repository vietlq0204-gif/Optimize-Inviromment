using UnityEngine;

/// <summary>
/// Controls how a gameplay object paints grass interaction into the global render texture.
/// </summary>
[CreateAssetMenu(menuName = "Grass/Interaction Source Profile")]
public sealed class GrassInteractionSourceProfile : ScriptableObject
{
    [Header("Contact")]
    [Min(0f)] public float contactRefreshRate = 12f;
    [Min(0.01f)] public float contactLifetime = 0.18f;
    [Min(0.01f)] public float contactSize = 1.35f;
    [Range(0f, 2f)] public float contactIntensity = 1f;
    [Range(0.01f, 1f)] public float contactSoftness = 0.55f;
    [Range(0f, 1f)] public float contactDirectionalInfluence = 0.2f;
    [Range(1, 256)] public int contactMaxParticles = 32;

    [Header("Trail")]
    [Min(0f)] public float trailRateOverDistance = 3f;
    [Min(0.05f)] public float trailLifetime = 0.75f;
    [Min(0.01f)] public float trailSize = 1.2f;
    [Range(0f, 2f)] public float trailIntensity = 0.85f;
    [Range(0.01f, 1f)] public float trailSoftness = 0.65f;
    [Range(0f, 1f)] public float trailDirectionalInfluence = 1f;
    [Range(0f, 1f)] public float trailRecoveryWeight = 1f;
    [Range(1, 2048)] public int trailMaxParticles = 160;

    [Header("Motion")]
    [Min(0f)] public float minimumDirectionalSpeed = 0.05f;
}
