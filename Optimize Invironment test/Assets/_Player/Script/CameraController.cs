using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0f, 2f, -4f);
    public float mouseSensitivity = 100f;
    public float smoothSpeed = 0.125f;
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float lookAtHeight = 1.5f;
    [SerializeField] private float mouseDeltaScale = 0.02f;

    private float yawRotation;
    private float pitchRotation;
    private Coroutine shakeRoutine;
    private Vector3 shakeOffset;

    private void Awake()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        pitchRotation = NormalizePitch(eulerAngles.x);
        yawRotation = eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 lookInput = ReadLookInput();
        yawRotation += lookInput.x * mouseSensitivity * Time.deltaTime;
        pitchRotation -= lookInput.y * mouseSensitivity * Time.deltaTime;
        pitchRotation = Mathf.Clamp(pitchRotation, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitchRotation, yawRotation, 0f);
        Vector3 desiredPosition = target.position + rotation * offset;
        Vector3 currentBasePosition = transform.position - shakeOffset;
        Vector3 smoothedPosition = Vector3.Lerp(currentBasePosition, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition + shakeOffset;
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
    }

    private Vector2 ReadLookInput()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 look = Vector2.zero;

        if (Mouse.current != null)
        {
            look = Mouse.current.delta.ReadValue() * mouseDeltaScale;
        }

        if (look.sqrMagnitude <= 0.0001f && Gamepad.current != null)
        {
            look = Gamepad.current.rightStick.ReadValue();
        }

        return look;
#else
        return Vector2.zero;
#endif
    }

    private static float NormalizePitch(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    public void ShakeCamera(float duration = 0.15f, float magnitude = 0.1f)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(Shake(duration, magnitude));
    }

    private IEnumerator Shake(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * magnitude,
                Random.Range(-1f, 1f) * magnitude,
                0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeOffset = Vector3.zero;
        shakeRoutine = null;
    }
}
