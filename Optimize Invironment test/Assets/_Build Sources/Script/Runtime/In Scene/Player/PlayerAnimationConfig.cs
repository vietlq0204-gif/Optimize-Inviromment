using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAnimationConfig", menuName = "Game/Player/Animation Config")]
public sealed class PlayerAnimationConfig : ScriptableObject
{
    [Header("Animator")]
    [SerializeField] private string velocityParameter = "Velocity";
    [SerializeField] private float dampTime = 0.1f;

    [Header("Speed Thresholds")]
    [SerializeField] private float idleSpeedThreshold = 0.05f;
    [SerializeField] private float walkSpeedThreshold = 1.5f;
    [SerializeField] private float jogSpeedThreshold = 4f;
    [SerializeField] private float dashSpeedThreshold = 8f;

    public string VelocityParameter => velocityParameter;
    public float DampTime => dampTime;
    public float IdleSpeedThreshold => idleSpeedThreshold;
    public float WalkSpeedThreshold => walkSpeedThreshold;
    public float JogSpeedThreshold => jogSpeedThreshold;
    public float DashSpeedThreshold => dashSpeedThreshold;

#if UNITY_EDITOR
    private void OnValidate()
    {
        idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
        walkSpeedThreshold = Mathf.Max(idleSpeedThreshold + 0.01f, walkSpeedThreshold);
        jogSpeedThreshold = Mathf.Max(walkSpeedThreshold + 0.01f, jogSpeedThreshold);
        dashSpeedThreshold = Mathf.Max(jogSpeedThreshold + 0.01f, dashSpeedThreshold);
        dampTime = Mathf.Max(0f, dampTime);
    }
#endif
}
