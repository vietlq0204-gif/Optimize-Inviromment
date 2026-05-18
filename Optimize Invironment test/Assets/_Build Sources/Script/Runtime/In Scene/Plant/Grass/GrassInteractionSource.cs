using UnityEngine;

/// <summary>
/// Legacy wrapper kept so existing scene references continue to work while the
/// interaction backend uses shape batching instead of particle writers.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("Grass/Grass Interaction Source (Legacy)")]
public sealed class GrassInteractionSource : EnvironmentInteractor
{
    [SerializeField, HideInInspector] private bool legacyStationaryContactUpgraded;

    protected override void OnEnable()
    {
        ApplyLegacyDefaults();
        base.OnEnable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ApplyLegacyDefaults();
    }

    private void ApplyLegacyDefaults()
    {
        if (legacyStationaryContactUpgraded)
        {
            return;
        }

        emitWhileStationary = true;
        legacyStationaryContactUpgraded = true;
    }
}
