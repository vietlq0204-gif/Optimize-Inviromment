using UnityEngine;

[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class PlayerController : PlayerMotor
{
    public Vector2 LookInput => InputReader != null ? InputReader.LookInput : Vector2.zero;
    public bool IsAttacking => InputReader != null && InputReader.IsAttacking;
    public bool IsInteracting => InputReader != null && InputReader.IsInteracting;
    public bool IsJumpPressed => InputReader != null && InputReader.IsJumpPressed;
    public bool IsPreviousPressed => InputReader != null && InputReader.IsPreviousPressed;
    public bool IsNextPressed => InputReader != null && InputReader.IsNextPressed;
    public bool AttackTriggeredThisFrame => InputReader != null && InputReader.AttackTriggeredThisFrame;
    public bool InteractTriggeredThisFrame => InputReader != null && InputReader.InteractTriggeredThisFrame;
    public bool CrouchTriggeredThisFrame => InputReader != null && InputReader.CrouchTriggeredThisFrame;
    public bool JumpTriggeredThisFrame => InputReader != null && InputReader.JumpTriggeredThisFrame;
    public bool PreviousTriggeredThisFrame => InputReader != null && InputReader.PreviousTriggeredThisFrame;
    public bool NextTriggeredThisFrame => InputReader != null && InputReader.NextTriggeredThisFrame;
}
