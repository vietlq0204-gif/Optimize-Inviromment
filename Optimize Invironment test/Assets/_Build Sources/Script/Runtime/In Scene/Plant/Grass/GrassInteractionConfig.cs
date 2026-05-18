using UnityEngine;

/// <summary>
/// Shared interaction tuning for interactors and the grass material response.
/// </summary>
[CreateAssetMenu(fileName = "New Grass Interaction Config", menuName = "Environment/Grass Interaction Config")]
public class GrassInteractionConfig : EnvironmentInteractionProfile
{
    [Header("Interactor Overrides")]
    [Min(-5f)] public float heightOffset = 0.05f;
    [Min(0.01f)] public float contactRadius = 0.6f;
    [Min(0f)] public float contactStrength = 1f;
    [Min(0.01f)] public float trailRadius = 0.45f;
    [Min(0f)] public float trailStrength = 1f;
    [Min(0f)] public float minimumTrailDistance = 0.02f;
    public bool emitWhileStationary = true;
    public bool suppressRecoveryWhileStationary = true;

    [Header("Material Overrides")]
    public bool overrideMaterialInteraction = true;
    public bool enableInteraction = true;
    [Min(0f)] public float interactionStrength = 1f;
    [Range(0f, 1f)] public float interactionPushAway = 0.1f;
    [Range(0f, 1f)] public float interactionFlatten;
    [Min(0f)] public float interactionRadiusMultiplier = 1.2f;
    [Min(0f)] public float interactionVerticalRange = 1f;
    [Range(0f, 1f)] public float interactionTrail = 0.3f;
    [Min(0f)] public float interactionRecoveryStrength = 0.5f;
    [Min(0f)] public float interactionRecoveryFrequency = 10f;
    [Min(0f)] public float interactionRecoveryNoiseScale = 5f;
}
