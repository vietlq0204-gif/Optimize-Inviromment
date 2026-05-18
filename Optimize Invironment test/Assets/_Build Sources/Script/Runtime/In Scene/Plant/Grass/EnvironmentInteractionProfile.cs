using UnityEngine;

/// <summary>
/// Controls how a gameplay object writes interaction into the shared environment map.
/// </summary>
[CreateAssetMenu(menuName = "Environment/Interaction Profile")]
public class EnvironmentInteractionProfile : ScriptableObject
{
    [Header("Contact Writer")]
    [Range(0.01f, 1f)] public float contactSoftness = 0.45f;
    [Range(0f, 1f)] public float contactDirectionalInfluence = 0.1f;
    [Range(0f, 1f)] public float contactRecoveryWeight = 0.16f;

    [Header("Trail Writer")]
    [Range(0.01f, 1f)] public float trailSoftness = 0.68f;
    [Range(0f, 1f)] public float trailDirectionalInfluence = 0.35f;
    [Range(0f, 1f)] public float trailRecoveryWeight = 0.9f;

    [Header("Motion")]
    [Min(0f)] public float minimumDirectionalSpeed = 0.05f;
}
