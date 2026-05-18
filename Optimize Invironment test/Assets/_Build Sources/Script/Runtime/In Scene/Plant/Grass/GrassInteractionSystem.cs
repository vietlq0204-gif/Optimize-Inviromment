using UnityEngine;

/// <summary>
/// Legacy wrapper kept so existing scene references continue to work while the
/// interaction backend uses shape batching instead of a dedicated render camera.
/// </summary>
[ExecuteAlways]
public class GrassInteractionSystem : EnvironmentInteractionSystem
{
}
