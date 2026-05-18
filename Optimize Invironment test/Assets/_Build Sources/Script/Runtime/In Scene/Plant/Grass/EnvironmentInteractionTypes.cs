using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies which downstream systems should consume a shape.
/// </summary>
[System.Flags]
public enum InteractionTargetMask : byte
{
    None = 0,
    Vegetation = 1 << 0,
    Surface = 1 << 1,
    Water = 1 << 2,
}

/// <summary>
/// Supported top-down interaction primitives.
/// </summary>
public enum InteractionShapeType : byte
{
    Disc = 0,
    Capsule = 1,
}

/// <summary>
/// One interaction primitive written into the shared interaction map.
/// </summary>
public struct InteractionShape
{
    public InteractionShapeType Type;
    public InteractionTargetMask Targets;
    public Vector3 PointA;
    public Vector3 PointB;
    public Vector3 Velocity;
    public float Radius;
    public float Strength;
    public float Softness;
    public float DirectionalInfluence;
    public float RecoveryWeight;
}

/// <summary>
/// Read-only context passed from the interaction system to interactors during collection.
/// </summary>
public struct InteractionCollectContext
{
    public Vector3 FocusPosition;
    public float MaxDistance;
    public float DeltaTime;
}

/// <summary>
/// Shared runtime registry for active interaction sources.
/// </summary>
public static class EnvironmentInteractionRegistry
{
    private static readonly HashSet<EnvironmentInteractor> ActiveInteractors = new HashSet<EnvironmentInteractor>();

    public static IReadOnlyCollection<EnvironmentInteractor> Interactors => ActiveInteractors;

    public static void Register(EnvironmentInteractor interactor)
    {
        if (interactor == null)
        {
            return;
        }

        ActiveInteractors.Add(interactor);
    }

    public static void Unregister(EnvironmentInteractor interactor)
    {
        if (interactor == null)
        {
            return;
        }

        ActiveInteractors.Remove(interactor);
    }
}
