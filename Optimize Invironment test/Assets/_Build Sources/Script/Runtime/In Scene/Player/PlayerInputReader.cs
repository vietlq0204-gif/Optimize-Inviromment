using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputReader : MonoBehaviour
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

    protected virtual void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
        CacheInputActions();
#endif
    }

    protected virtual void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        CacheInputActions();
#endif
    }

    protected virtual void Update()
    {
#if ENABLE_INPUT_SYSTEM
        RefreshInputState();
#endif
    }

#if ENABLE_INPUT_SYSTEM
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

        CaptureTrigger(attackAction, ref attackTriggeredFrame);
        CaptureTrigger(interactAction, ref interactTriggeredFrame);
        CaptureTrigger(crouchAction, ref crouchTriggeredFrame);
        CaptureTrigger(jumpAction, ref jumpTriggeredFrame);
        CaptureTrigger(previousAction, ref previousTriggeredFrame);
        CaptureTrigger(nextAction, ref nextTriggeredFrame);
    }

    private static void CaptureTrigger(InputAction action, ref int triggeredFrame)
    {
        if (action != null && action.WasPressedThisFrame())
        {
            triggeredFrame = Time.frameCount;
        }
    }

    private static InputAction FindAction(InputActionMap actionMap, string actionName)
    {
        if (actionMap == null || string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        return actionMap.FindAction(actionName, false);
    }

    private static Vector2 ReadVector2(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    private static bool IsActionPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }
#endif
}
