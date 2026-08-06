using UnityEngine;

/// <summary>
/// Kiểm soát cách một đối tượng trong game ghi lại tương tác vào bản đồ môi trường được chia sẻ.
/// </summary>
public abstract class EnvironmentInteractionProfile : ScriptableObject
{
    [Header("Contact Writer")]
    [Tooltip("Độ mềm mép vùng tiếp xúc. Giá trị thấp tạo vùng đè sắc hơn, giá trị cao làm mép loang mềm hơn.")]
    [Range(0.01f, 1f)] public float contactSoftness = 0.45f;
    [Tooltip("Mức ảnh hưởng của hướng di chuyển lên vùng tiếp xúc đứng yên.")]
    [Range(0f, 1f)] public float contactDirectionalInfluence = 0.1f;
    [Tooltip("Trọng số hồi phục của vùng tiếp xúc. 0 giữ cỏ bị đè lâu hơn, 1 cho phép hồi phục mạnh hơn.")]
    [Range(0f, 1f)] public float contactRecoveryWeight = 0.16f;

    [Header("Trail Writer")]
    [Tooltip("Độ mềm mép vệt kéo phía sau khi đối tượng di chuyển qua cỏ.")]
    [Range(0.01f, 1f)] public float trailSoftness = 0.68f;
    [Tooltip("Mức ảnh hưởng của hướng di chuyển lên vệt cỏ bị kéo.")]
    [Range(0f, 1f)] public float trailDirectionalInfluence = 0.35f;
    [Tooltip("Trọng số hồi phục của vệt cỏ. Giảm xuống để vệt bị đè giữ lại lâu hơn.")]
    [Range(0f, 1f)] public float trailRecoveryWeight = 0.9f;

    [Header("Motion")]
    [Tooltip("Tốc độ phẳng tối thiểu để hệ thống xem đối tượng là đang di chuyển và tạo vệt.")]
    [Min(0f)] public float minimumDirectionalSpeed = 0.05f;
}
