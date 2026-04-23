using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CursorVisibilityController : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private bool hideCursorOnStart = true;
    [SerializeField] private bool lockCursorWhenHidden = true;

    [Header("Controls")]
    [SerializeField] private bool hideCursorOnMouseClick = true;
#if ENABLE_LEGACY_INPUT_MANAGER
    [SerializeField] private KeyCode showCursorKey = KeyCode.Escape;
#endif
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key showCursorKeyInputSystem = Key.Escape;
#endif

    [Header("Safety")]
    [SerializeField] private bool showCursorOnDisable = true;

    private void Start()
    {
        if (hideCursorOnStart)
        {
            HideCursor();
            return;
        }

        ShowCursor();
    }

    private void Update()
    {
        if (WasShowCursorPressed())
        {
            ShowCursor();
        }

        if (Cursor.visible && hideCursorOnMouseClick && WasMouseClicked())
        {
            HideCursor();
        }
    }

    public void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = lockCursorWhenHidden ? CursorLockMode.Locked : CursorLockMode.None;
    }

    public void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private bool WasShowCursorPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[showCursorKeyInputSystem].wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(showCursorKey);
#else
        return false;
#endif
    }

    private static bool WasMouseClicked()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(2);
#else
        return false;
#endif
    }

    private void OnDisable()
    {
        if (showCursorOnDisable)
        {
            ShowCursor();
        }
    }
}
