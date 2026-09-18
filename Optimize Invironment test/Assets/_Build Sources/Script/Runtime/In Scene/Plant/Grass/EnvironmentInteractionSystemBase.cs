using UnityEngine;

/// <summary>
/// Base type for concrete environment interaction systems such as grass,
/// water, or surface response systems.
/// </summary>
public abstract class EnvironmentInteractionSystemBase : MonoBehaviour
{
    public abstract InteractionTargetMask ConsumedTargets { get; }
}
