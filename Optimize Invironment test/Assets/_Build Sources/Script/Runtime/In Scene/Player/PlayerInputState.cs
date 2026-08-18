using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class PlayerInputState : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string crouchActionName = "Crouch";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string previousActionName = "Previous";
    [SerializeField] private string nextActionName = "Next";
    [SerializeField] private string sprintActionName = "Sprint";
#endif

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool attackPressed;
    private bool interactPressed;
    private bool crouchPressed;
    private bool jumpPressed;
    private bool previousPressed;
    private bool nextPressed;
    private bool sprintPressed;
    private int attackTriggeredFrame = -1;
    private int interactTriggeredFrame = -1;
    private int crouchTriggeredFrame = -1;
    private int jumpTriggeredFrame = -1;
    private int previousTriggeredFrame = -1;
    private int nextTriggeredFrame = -1;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction attackAction;
    private InputAction interactAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private InputAction previousAction;
    private InputAction nextAction;
    private InputAction sprintAction;
#endif
    
    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;
    public bool IsSprinting => sprintPressed;
    public bool IsCrouching => crouchPressed;
    public bool IsAttacking => attackPressed;
    public bool IsInteracting => interactPressed;
    public bool IsJumpPressed => jumpPressed;
    public bool IsPreviousPressed => previousPressed;
    public bool IsNextPressed => nextPressed;
    public bool AttackTriggeredThisFrame => attackTriggeredFrame == Time.frameCount;
    public bool InteractTriggeredThisFrame => interactTriggeredFrame == Time.frameCount;
    public bool CrouchTriggeredThisFrame => crouchTriggeredFrame == Time.frameCount;
    public bool JumpTriggeredThisFrame => jumpTriggeredFrame == Time.frameCount;
    public bool PreviousTriggeredThisFrame => previousTriggeredFrame == Time.frameCount;
    public bool NextTriggeredThisFrame => nextTriggeredFrame == Time.frameCount;

    /// <summary>
    /// Hàm Awake được gọi khi script được tải.
    /// </summary>
    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
        CacheInputActions();
#endif
    }

    /// <summary>
    /// Được gọi khi đối tượng được kích hoạt.
    /// </summary>
    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        CacheInputActions();
#endif
    }
    
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        RefreshInputState();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    
    /// <summary>
    /// Lưu trữ các hành động input để truy cập nhanh.
    /// </summary>
    private void CacheInputActions()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput == null || playerInput.actions == null)
        {
            moveAction = null;
            lookAction = null;
            attackAction = null;
            interactAction = null;
            crouchAction = null;
            jumpAction = null;
            previousAction = null;
            nextAction = null;
            sprintAction = null;
            return;
        }

        InputActionMap actionMap = playerInput.currentActionMap;
        if (actionMap == null && !string.IsNullOrWhiteSpace(actionMapName))
        {
            actionMap = playerInput.actions.FindActionMap(actionMapName, false);
        }

        moveAction = FindAction(actionMap, moveActionName);
        lookAction = FindAction(actionMap, lookActionName);
        attackAction = FindAction(actionMap, attackActionName);
        interactAction = FindAction(actionMap, interactActionName);
        crouchAction = FindAction(actionMap, crouchActionName);
        jumpAction = FindAction(actionMap, jumpActionName);
        previousAction = FindAction(actionMap, previousActionName);
        nextAction = FindAction(actionMap, nextActionName);
        sprintAction = FindAction(actionMap, sprintActionName);
    }

    /// <summary>
    /// Làm mới trạng thái của các input.
    /// </summary>
    private void RefreshInputState()
    {
        if (moveAction == null && playerInput != null)
        {
            CacheInputActions();
        }

        moveInput = ReadVector2(moveAction);
        lookInput = ReadVector2(lookAction);

        attackPressed = IsActionPressed(attackAction);
        interactPressed = IsActionPressed(interactAction);
        crouchPressed = IsActionPressed(crouchAction);
        jumpPressed = IsActionPressed(jumpAction);
        previousPressed = IsActionPressed(previousAction);
        nextPressed = IsActionPressed(nextAction);
        sprintPressed = IsActionPressed(sprintAction);

        if (WasActionPressedThisFrame(attackAction))
        {
            attackTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(interactAction))
        {
            interactTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(crouchAction))
        {
            crouchTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(jumpAction))
        {
            jumpTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(previousAction))
        {
            previousTriggeredFrame = Time.frameCount;
        }

        if (WasActionPressedThisFrame(nextAction))
        {
            nextTriggeredFrame = Time.frameCount;
        }
    }

    /// <summary>
    /// Tìm một hành động input trong một action map.
    /// </summary>
    private static InputAction FindAction(InputActionMap actionMap, string actionName)
    {
        if (actionMap == null || string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        return actionMap.FindAction(actionName, false);
    }

    /// <summary>
    /// Đọc giá trị Vector2 từ một hành động input.
    /// </summary>
    private static Vector2 ReadVector2(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    /// <summary>
    /// Kiểm tra xem một hành động có đang được nhấn giữ không.
    /// </summary>
    private static bool IsActionPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    /// <summary>
    /// Kiểm tra xem một hành động có được nhấn trong frame này không.
    /// </summary>
    private static bool WasActionPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }
    
    /// <summary>
    /// Cập nhật trạng thái của một nút (đang nhấn, đã nhấn).
    /// </summary>
    private static bool UpdateButtonState(InputValue value, ref bool pressedState, ref int triggeredFrame)
    {
        bool isPressed = value != null && value.isPressed;
        if (isPressed && !pressedState)
        {
            triggeredFrame = Time.frameCount;
        }

        pressedState = isPressed;
        return isPressed;
    }

    /// <summary>
    /// Xử lý sự kiện input di chuyển.
    /// </summary>
    public void OnMove(InputValue value)
    {
        moveInput = value != null ? value.Get<Vector2>() : Vector2.zero;
    }
    
    /// <summary>
    /// Xử lý sự kiện input cúi người.
    /// </summary>
    public void OnCrouch(InputValue value)
    {
        UpdateButtonState(value, ref crouchPressed, ref crouchTriggeredFrame);
    }

    /// <summary>
    /// Xử lý sự kiện input nhảy.
    /// </summary>
    public void OnJump(InputValue value)
    {
        UpdateButtonState(value, ref jumpPressed, ref jumpTriggeredFrame);
    }
    
    /// <summary>
    /// Xử lý sự kiện input chạy nước rút.
    /// </summary>
    public void OnSprint(InputValue value)
    {
        sprintPressed = value != null && value.isPressed;
    }

    /// <summary>
    /// Xử lý sự kiện input tấn công.
    /// </summary>
    public void OnAttack(InputValue value)
    {
        UpdateButtonState(value, ref attackPressed, ref attackTriggeredFrame);
    }

    /// <summary>
    /// Xử lý sự kiện input tương tác.
    /// </summary>
    public void OnInteract(InputValue value)
    {
        UpdateButtonState(value, ref interactPressed, ref interactTriggeredFrame);
    }
    
    /// <summary>
    /// Xử lý sự kiện input nhìn.
    /// </summary>
    public void OnLook(InputValue value)
    {
        lookInput = value != null ? value.Get<Vector2>() : Vector2.zero;
    }

    /// <summary>
    /// Xử lý sự kiện input "trước đó".
    /// </summary>
    public void OnPrevious(InputValue value)
    {
        UpdateButtonState(value, ref previousPressed, ref previousTriggeredFrame);
    }

    /// <summary>
    /// Xử lý sự kiện input "tiếp theo".
    /// </summary>
    public void OnNext(InputValue value)
    {
        UpdateButtonState(value, ref nextPressed, ref nextTriggeredFrame);
    }
#endif
}
