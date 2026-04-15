using UnityEngine;

/// <summary>
/// Optional scene-level settings for persistent grass trails.
/// </summary>
[DisallowMultipleComponent]
public sealed class GrassTrailSettings : MonoBehaviour
{
    [Header("Trail Lifetime")]
    [SerializeField, Range(0.02f, 1.5f)] private float decayPerSecond = 0.12f;
    [SerializeField, Min(128)] private int resolution = 1024;

    [Header("Bounds")]
    [SerializeField] private bool useActiveTerrainBounds = true;
    [SerializeField] private Vector3 manualWorldOrigin = new(-48f, 0f, -48f);
    [SerializeField] private Vector2 manualWorldSize = new(96f, 96f);

    internal float DecayPerSecond => decayPerSecond;
    internal int Resolution => Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 128, 4096));
    internal bool UseActiveTerrainBounds => useActiveTerrainBounds;
    internal Vector4 ManualBounds => new(
        manualWorldOrigin.x,
        manualWorldOrigin.z,
        Mathf.Max(manualWorldSize.x, 1f),
        Mathf.Max(manualWorldSize.y, 1f));

    private void OnDrawGizmosSelected()
    {
        if (useActiveTerrainBounds)
        {
            return;
        }

        Vector3 center = new(
            manualWorldOrigin.x + manualWorldSize.x * 0.5f,
            transform.position.y,
            manualWorldOrigin.z + manualWorldSize.y * 0.5f);

        Gizmos.color = new Color(0.12f, 0.7f, 0.9f, 0.85f);
        Gizmos.DrawWireCube(center, new Vector3(manualWorldSize.x, 0.1f, manualWorldSize.y));
    }
}
