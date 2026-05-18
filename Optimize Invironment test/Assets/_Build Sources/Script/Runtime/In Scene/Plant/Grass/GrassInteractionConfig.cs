using UnityEngine;

/// <summary>
/// Shared interaction tuning for interactors and the grass material response.
/// </summary>
[CreateAssetMenu(fileName = "New Grass Interaction Config", menuName = "Environment/Grass Interaction Config")]
public class GrassInteractionConfig : EnvironmentInteractionProfile
{
    [Header("Interactor Overrides")]
    [Tooltip("Độ lệch chiều cao của điểm ghi tương tác so với vị trí object. Dùng để đặt vùng đè sát mặt đất/cỏ.")]
    [Min(-5f)] public float heightOffset = 0.05f;
    [Tooltip("Bán kính vùng cỏ bị đè trực tiếp quanh object.")]
    [Min(0.01f)] public float contactRadius = 0.6f;
    [Tooltip("Cường độ vùng đè trực tiếp. 0 tắt vùng contact, giá trị cao làm cỏ phản ứng mạnh hơn.")]
    [Min(0f)] public float contactStrength = 1f;
    [Tooltip("Bán kính vệt cỏ phía sau khi object di chuyển.")]
    [Min(0.01f)] public float trailRadius = 0.45f;
    [Tooltip("Cường độ vệt cỏ khi object đi qua. 0 tắt trail.")]
    [Min(0f)] public float trailStrength = 1f;
    [Tooltip("Quãng đường tối thiểu giữa hai frame để bắt đầu vẽ vệt trail.")]
    [Min(0f)] public float minimumTrailDistance = 0.02f;
    [Tooltip("Vẫn ghi vùng contact khi object đứng yên. Bật để cỏ tiếp tục bị đè khi player đứng trên cỏ.")]
    public bool emitWhileStationary = true;
    [Tooltip("Khi object đứng yên, chặn recovery của vùng contact để cỏ không bật lại dưới chân.")]
    public bool suppressRecoveryWhileStationary = true;

    [Header("Material Overrides")]
    [Tooltip("Bật để config này ghi đè thông số interaction trên material cỏ bằng global shader values.")]
    public bool overrideMaterialInteraction = true;
    [Tooltip("Bật/tắt toàn bộ phản ứng interaction của cỏ.")]
    public bool enableInteraction = true;
    [Tooltip("Cường độ tổng của phản ứng cỏ trong shader.")]
    [Min(0f)] public float interactionStrength = 1f;
    [Tooltip("Độ cỏ bị đẩy ngang ra khỏi hướng tác động.")]
    [Range(0f, 1f)] public float interactionPushAway = 0.1f;
    [Tooltip("Độ cỏ bị ép xuống theo chiều dọc.")]
    [Range(0f, 1f)] public float interactionFlatten;
    [Tooltip("Hệ số mở rộng/thu hẹp bán kính phản ứng trong shader.")]
    [Min(0f)] public float interactionRadiusMultiplier = 1.2f;
    [Tooltip("Khoảng chiều cao quanh mặt đất được phép nhận tương tác.")]
    [Min(0f)] public float interactionVerticalRange = 1f;
    [Tooltip("Mức giữ vệt trail trong shader. Cao hơn làm vệt đi qua còn ảnh hưởng lâu và mềm hơn.")]
    [Range(0f, 1f)] public float interactionTrail = 0.3f;
    [Tooltip("Biên độ rung/bật lại khi cỏ đang hồi phục. Đặt 0 nếu không muốn hiệu ứng bật lại.")]
    [Min(0f)] public float interactionRecoveryStrength = 0.5f;
    [Tooltip("Tần số rung/bật lại khi cỏ hồi phục.")]
    [Min(0f)] public float interactionRecoveryFrequency = 10f;
    [Tooltip("Tỉ lệ noise làm lệch pha recovery giữa các bụi cỏ.")]
    [Min(0f)] public float interactionRecoveryNoiseScale = 5f;
}
