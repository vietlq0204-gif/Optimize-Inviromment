using UnityEngine;

/// <summary>
/// Legacy asset kept so existing scene and prefab references keep working after the
/// interaction backend moved from particles to shape batching.
/// </summary>
[CreateAssetMenu(menuName = "Grass/Interaction Source Profile")]
public sealed class GrassInteractionSourceProfile : EnvironmentInteractionProfile
{
}
