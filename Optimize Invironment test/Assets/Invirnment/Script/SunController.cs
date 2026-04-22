using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[AddComponentMenu("Environment/Sun Controller")]
public sealed class SunController : MonoBehaviour
{
    [SerializeField]
    private MainClock clock;

    [SerializeField]
    private Transform targetTransform;

    [SerializeField]
    private bool useLocalRotation = true;

    [SerializeField]
    private float fixedYRotation;

    [SerializeField]
    private float fixedZRotation;

    private void Reset()
    {
        targetTransform = transform;
        CaptureCurrentYZRotation();
        AssignClockIfMissing();
    }

    private void OnEnable()
    {
        ApplyCurrentTime();
    }

    private void OnValidate()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        AssignClockIfMissing();
        fixedYRotation = NormalizeAngle(fixedYRotation);
        fixedZRotation = NormalizeAngle(fixedZRotation);
        ApplyCurrentTime();
    }

    private void LateUpdate()
    {
        ApplyCurrentTime();
    }

    public void ApplyCurrentTime()
    {
        if (!TryAssignReferences())
        {
            return;
        }

        Vector3 eulerAngles = new Vector3(
            EvaluateSunAngleX(clock.CurrentTimeHours),
            fixedYRotation,
            fixedZRotation);

        ApplyRotation(eulerAngles);
    }

    private void ApplyRotation(Vector3 eulerAngles)
    {
        Quaternion rotation = Quaternion.Euler(eulerAngles);

        if (useLocalRotation)
        {
            targetTransform.localRotation = rotation;
        }
        else
        {
            targetTransform.rotation = rotation;
        }

        SyncEulerHint(eulerAngles);
    }

    public static float EvaluateSunAngleX(float hours)
    {
        return (MainClock.WrapHours(hours) / MainClock.HoursPerDay * 360f) - 90f;
    }

    [ContextMenu("Capture Current YZ Rotation")]
    public void CaptureCurrentYZRotation()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        Vector3 currentEuler = GetCurrentEulerAnglesForCapture();
        fixedYRotation = NormalizeAngle(currentEuler.y);
        fixedZRotation = NormalizeAngle(currentEuler.z);
    }

    private bool TryAssignReferences()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        AssignClockIfMissing();
        return targetTransform != null && clock != null;
    }

    private void AssignClockIfMissing()
    {
        if (clock == null)
        {
            clock = FindAnyObjectByType<MainClock>();
        }
    }

    private Vector3 GetCurrentEulerAnglesForCapture()
    {
#if UNITY_EDITOR
        if (TryGetEulerHint(out Vector3 hintedEulerAngles))
        {
            return hintedEulerAngles;
        }
#endif

        return useLocalRotation ? targetTransform.localEulerAngles : targetTransform.eulerAngles;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle, 360f);
    }

#if UNITY_EDITOR
    private void SyncEulerHint(Vector3 eulerAngles)
    {
        if (!TryGetEulerHintProperty(out SerializedObject serializedTransform, out SerializedProperty eulerHintProperty))
        {
            return;
        }

        if ((eulerHintProperty.vector3Value - eulerAngles).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        eulerHintProperty.vector3Value = eulerAngles;
        serializedTransform.ApplyModifiedPropertiesWithoutUndo();
    }

    private bool TryGetEulerHint(out Vector3 hintedEulerAngles)
    {
        hintedEulerAngles = default;

        if (!TryGetEulerHintProperty(out _, out SerializedProperty eulerHintProperty))
        {
            return false;
        }

        hintedEulerAngles = eulerHintProperty.vector3Value;
        return true;
    }

    private bool TryGetEulerHintProperty(out SerializedObject serializedTransform, out SerializedProperty eulerHintProperty)
    {
        serializedTransform = null;
        eulerHintProperty = null;

        if (targetTransform == null)
        {
            return false;
        }

        if (!useLocalRotation && targetTransform.parent != null)
        {
            return false;
        }

        serializedTransform = new SerializedObject(targetTransform);
        serializedTransform.Update();
        eulerHintProperty = serializedTransform.FindProperty("m_LocalEulerAnglesHint");
        return eulerHintProperty != null;
    }
#else
    private void SyncEulerHint(Vector3 eulerAngles)
    {
    }
#endif
}
