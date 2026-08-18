using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimation : MonoBehaviour
{
    private const float WalkBlendPoint = 0.33333334f;
    private const float JogBlendPoint = 0.6666667f;
    private const float RunBlendPoint = 1f;

    [Header("Animator")]
    [SerializeField] private string velocityParameter = "Velocity";
    [SerializeField] private float dampTime = 0.1f;

    [Header("Speed Thresholds")]
    [SerializeField] private float idleSpeedThreshold = 0.05f;
    [SerializeField] private float walkSpeedThreshold = 1.5f;
    [SerializeField] private float jogSpeedThreshold = 4f;
    [SerializeField] private float dashSpeedThreshold = 8f;

    private Animator animatorComponent;
    private Rigidbody rigidbodyComponent;
    private PlayerController playerController;
    private int velocityParameterHash;

    private void Awake()
    {
        animatorComponent = GetComponent<Animator>();
        rigidbodyComponent = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
        velocityParameterHash = Animator.StringToHash(velocityParameter);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
        walkSpeedThreshold = Mathf.Max(idleSpeedThreshold + 0.01f, walkSpeedThreshold);
        jogSpeedThreshold = Mathf.Max(walkSpeedThreshold + 0.01f, jogSpeedThreshold);
        dashSpeedThreshold = Mathf.Max(jogSpeedThreshold + 0.01f, dashSpeedThreshold);
        dampTime = Mathf.Max(0f, dampTime);
    }
#endif

    private void Update()
    {
        float normalizedVelocity = CalculateBlendVelocity();
        animatorComponent.SetFloat(velocityParameterHash, normalizedVelocity, dampTime, Time.deltaTime);
    }

    private float CalculateBlendVelocity()
    {
        float planarSpeed = GetPlanarSpeed();
        if (planarSpeed <= idleSpeedThreshold)
        {
            return 0f;
        }

        if (planarSpeed <= walkSpeedThreshold)
        {
            float t = Mathf.InverseLerp(idleSpeedThreshold, walkSpeedThreshold, planarSpeed);
            return Mathf.Lerp(0f, WalkBlendPoint, t);
        }

        if (planarSpeed <= jogSpeedThreshold)
        {
            float t = Mathf.InverseLerp(walkSpeedThreshold, jogSpeedThreshold, planarSpeed);
            return Mathf.Lerp(WalkBlendPoint, JogBlendPoint, t);
        }

        if (planarSpeed <= dashSpeedThreshold)
        {
            float t = Mathf.InverseLerp(jogSpeedThreshold, dashSpeedThreshold, planarSpeed);
            return Mathf.Lerp(JogBlendPoint, RunBlendPoint, t);
        }

        return RunBlendPoint;
    }

    private float GetPlanarSpeed()
    {
        if (playerController != null)
        {
            return playerController.CurrentPlanarSpeed;
        }

        if (rigidbodyComponent == null)
        {
            return 0f;
        }

        Vector3 planarVelocity = rigidbodyComponent.linearVelocity;
        planarVelocity.y = 0f;
        return planarVelocity.magnitude;
    }
}
