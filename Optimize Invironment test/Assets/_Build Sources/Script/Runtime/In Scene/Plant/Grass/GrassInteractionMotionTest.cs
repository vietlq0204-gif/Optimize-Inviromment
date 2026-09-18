using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Grass/Test/Grass Interaction Motion Test")]
public sealed class GrassInteractionMotionTest : MonoBehaviour
{
    public enum MotionMode
    {
        Circle = 0,
        Square = 1,
        LeftRight = 2,
        Chaotic = 3,
    }

    [Header("Target")]
    [Tooltip("Vật thể cần điều khiển. Nếu để trống, script sẽ tự lấy child đầu tiên của object cha.")]
    [SerializeField] private Transform movingObject;
    [Tooltip("Giữ nguyên độ cao local Y ban đầu của vật thể khi chạy trên mặt phẳng XZ.")]
    [SerializeField] private bool keepInitialLocalY = true;
    [Tooltip("Xoay vật thể theo hướng đang di chuyển.")]
    [SerializeField] private bool rotateToMovement = true;
    [Tooltip("Tốc độ xoay theo hướng di chuyển.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;

    [Header("Motion")]
    [Tooltip("Kiểu chuyển động quanh tâm. Tâm là GameObject đang gắn script này.")]
    [SerializeField] private MotionMode mode = MotionMode.Circle;
    [Tooltip("Vận tốc di chuyển theo world unit mỗi giây.")]
    [SerializeField, Min(0f)] private float speed = 4f;
    [Tooltip("Bán kính hoặc nửa kích thước vùng chạy quanh tâm.")]
    [SerializeField, Min(0.01f)] private float radius = 5f;
    [Tooltip("Hướng chạy vòng tròn hoặc chạy quanh hình vuông.")]
    [SerializeField] private bool clockwise = true;

    [Header("Chaotic")]
    [Tooltip("Khoảng cách đủ gần điểm đích để chọn điểm hỗn loạn mới.")]
    [SerializeField, Min(0.01f)] private float chaoticArrivalDistance = 0.25f;
    [Tooltip("Thời gian tối thiểu trước khi chọn điểm hỗn loạn mới.")]
    [SerializeField, Min(0f)] private float chaoticRetargetInterval = 0.35f;

    private float initialLocalY;
    private float circleAngle;
    private float squareDistance;
    private float leftRightDistance;
    private float leftRightDirection = 1f;
    private float chaoticRetargetTimer;
    private Vector3 chaoticTargetLocal;
    private Vector3 lastWorldPosition;

    private void Reset()
    {
        ResolveMovingObject();
    }

    private void OnEnable()
    {
        ResolveMovingObject();
        if (movingObject == null)
        {
            return;
        }

        initialLocalY = movingObject.localPosition.y;
        Vector3 localPosition = movingObject.localPosition;
        circleAngle = Mathf.Atan2(localPosition.z, localPosition.x);
        squareDistance = 0f;
        leftRightDistance = Mathf.Clamp(localPosition.x, -radius, radius);
        chaoticTargetLocal = ClampToRadius(localPosition);
        chaoticRetargetTimer = 0f;
        lastWorldPosition = movingObject.position;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        radius = Mathf.Max(0.01f, radius);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        chaoticArrivalDistance = Mathf.Max(0.01f, chaoticArrivalDistance);
        chaoticRetargetInterval = Mathf.Max(0f, chaoticRetargetInterval);
    }

    private void Update()
    {
        if (movingObject == null)
        {
            ResolveMovingObject();
            if (movingObject == null)
            {
                return;
            }
        }

        float deltaTime = Time.deltaTime;
        Vector3 nextLocalPosition = mode switch
        {
            MotionMode.Circle => EvaluateCircle(deltaTime),
            MotionMode.Square => EvaluateSquare(deltaTime),
            MotionMode.LeftRight => EvaluateLeftRight(deltaTime),
            MotionMode.Chaotic => EvaluateChaotic(deltaTime),
            _ => movingObject.localPosition,
        };

        if (keepInitialLocalY)
        {
            nextLocalPosition.y = initialLocalY;
        }

        movingObject.localPosition = nextLocalPosition;
        UpdateRotation(deltaTime);
        lastWorldPosition = movingObject.position;
    }

    private Vector3 EvaluateCircle(float deltaTime)
    {
        float angularSpeed = speed / Mathf.Max(radius, 0.0001f);
        circleAngle += angularSpeed * deltaTime * (clockwise ? -1f : 1f);
        return new Vector3(Mathf.Cos(circleAngle) * radius, initialLocalY, Mathf.Sin(circleAngle) * radius);
    }

    private Vector3 EvaluateSquare(float deltaTime)
    {
        float perimeter = radius * 8f;
        float direction = clockwise ? -1f : 1f;
        squareDistance = Mathf.Repeat(squareDistance + speed * deltaTime * direction, perimeter);

        float edgeLength = radius * 2f;
        float edgeProgress = squareDistance / edgeLength;
        int edge = Mathf.FloorToInt(edgeProgress) & 3;
        float t = edgeProgress - Mathf.Floor(edgeProgress);

        return edge switch
        {
            0 => new Vector3(Mathf.Lerp(-radius, radius, t), initialLocalY, radius),
            1 => new Vector3(radius, initialLocalY, Mathf.Lerp(radius, -radius, t)),
            2 => new Vector3(Mathf.Lerp(radius, -radius, t), initialLocalY, -radius),
            _ => new Vector3(-radius, initialLocalY, Mathf.Lerp(-radius, radius, t)),
        };
    }

    private Vector3 EvaluateLeftRight(float deltaTime)
    {
        leftRightDistance += speed * deltaTime * leftRightDirection;
        if (leftRightDistance > radius)
        {
            leftRightDistance = radius;
            leftRightDirection = -1f;
        }
        else if (leftRightDistance < -radius)
        {
            leftRightDistance = -radius;
            leftRightDirection = 1f;
        }

        return new Vector3(leftRightDistance, initialLocalY, 0f);
    }

    private Vector3 EvaluateChaotic(float deltaTime)
    {
        chaoticRetargetTimer -= deltaTime;
        Vector3 currentLocal = movingObject.localPosition;
        currentLocal.y = initialLocalY;

        float distanceToTarget = Vector3.Distance(currentLocal, chaoticTargetLocal);
        if (chaoticRetargetTimer <= 0f || distanceToTarget <= chaoticArrivalDistance)
        {
            chaoticTargetLocal = RandomPointInRadius();
            chaoticRetargetTimer = chaoticRetargetInterval;
        }

        return Vector3.MoveTowards(currentLocal, chaoticTargetLocal, speed * deltaTime);
    }

    private Vector3 RandomPointInRadius()
    {
        Vector2 random = Random.insideUnitCircle * radius;
        return new Vector3(random.x, initialLocalY, random.y);
    }

    private Vector3 ClampToRadius(Vector3 localPosition)
    {
        Vector2 xz = new Vector2(localPosition.x, localPosition.z);
        if (xz.sqrMagnitude > radius * radius)
        {
            xz = xz.normalized * radius;
        }

        return new Vector3(xz.x, keepInitialLocalY ? initialLocalY : localPosition.y, xz.y);
    }

    private void UpdateRotation(float deltaTime)
    {
        if (!rotateToMovement || rotationSpeed <= 0f)
        {
            return;
        }

        Vector3 velocity = movingObject.position - lastWorldPosition;
        velocity.y = 0f;
        if (velocity.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        movingObject.rotation = Quaternion.Slerp(movingObject.rotation, targetRotation, rotationSpeed * deltaTime);
    }

    private void ResolveMovingObject()
    {
        if (movingObject != null)
        {
            return;
        }

        if (transform.childCount > 0)
        {
            movingObject = transform.GetChild(0);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
        Vector3 center = transform.position;
        float y = movingObject != null && keepInitialLocalY ? movingObject.position.y : center.y;

        switch (mode)
        {
            case MotionMode.Circle:
            case MotionMode.Chaotic:
                DrawCircle(new Vector3(center.x, y, center.z), radius);
                break;
            case MotionMode.Square:
                Vector3 size = new Vector3(radius * 2f, 0f, radius * 2f);
                Gizmos.DrawWireCube(new Vector3(center.x, y, center.z), size);
                break;
            case MotionMode.LeftRight:
                Vector3 left = transform.TransformPoint(new Vector3(-radius, keepInitialLocalY ? initialLocalY : 0f, 0f));
                Vector3 right = transform.TransformPoint(new Vector3(radius, keepInitialLocalY ? initialLocalY : 0f, 0f));
                Gizmos.DrawLine(left, right);
                break;
        }
    }

    private static void DrawCircle(Vector3 center, float circleRadius)
    {
        const int Segments = 48;
        Vector3 previous = center + new Vector3(circleRadius, 0f, 0f);
        for (int i = 1; i <= Segments; i++)
        {
            float angle = (i / (float)Segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
