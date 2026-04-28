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

    private bool wantsCursorHidden;

    public static bool IsCursorCaptured
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Cursor.lockState == CursorLockMode.Locked;
#else
            return Cursor.lockState == CursorLockMode.Locked || !Cursor.visible;
#endif
        }
    }

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TryDisableStickyCursorLock();
#endif
    }

    private void Start()
    {
        wantsCursorHidden = hideCursorOnStart;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (wantsCursorHidden)
        {
            // Browsers only allow pointer lock after user interaction.
            ShowCursorInternal();
            return;
        }
#endif

        ApplyRequestedCursorState();
    }

    private void Update()
    {
        if (WasShowCursorPressed())
        {
            ShowCursor();
            return;
        }

        if (wantsCursorHidden)
        {
            if (!IsCursorCaptured)
            {
                SyncUnlockedCursorState();

                if (WasMouseClicked())
                {
                    ApplyHiddenCursorState();
                }
            }

            return;
        }

        if (hideCursorOnMouseClick && WasMouseClicked())
        {
            HideCursor();
        }
    }

    public void HideCursor()
    {
        wantsCursorHidden = true;
        ApplyHiddenCursorState();
    }

    public void ShowCursor()
    {
        wantsCursorHidden = false;
        ShowCursorInternal();
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

    private void ApplyRequestedCursorState()
    {
        if (wantsCursorHidden)
        {
            ApplyHiddenCursorState();
            return;
        }

        ShowCursorInternal();
    }

    private void ApplyHiddenCursorState()
    {
        Cursor.visible = false;
        Cursor.lockState = lockCursorWhenHidden ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private static void ShowCursorInternal()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void SyncUnlockedCursorState()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.visible = true;
        }
#endif
    }

    private static void TryDisableStickyCursorLock()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        System.Type webGlInputType = typeof(Cursor).Assembly.GetType("UnityEngine.WebGLInput");
        System.Reflection.PropertyInfo stickyCursorLockProperty =
            webGlInputType?.GetProperty("stickyCursorLock", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (stickyCursorLockProperty != null && stickyCursorLockProperty.CanWrite)
        {
            stickyCursorLockProperty.SetValue(null, false);
        }
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
