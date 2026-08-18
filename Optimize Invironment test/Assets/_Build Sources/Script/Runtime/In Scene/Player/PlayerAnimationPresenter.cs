using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerAnimationPresenter : MonoBehaviour
{
    private const float WalkBlendPoint = 0.33333334f;
    private const float JogBlendPoint = 0.6666667f;
    private const float RunBlendPoint = 1f;

    [Header("Config")]
    [SerializeField] private PlayerAnimationConfig animationConfig;

    [Header("Animator")]
    [SerializeField] private string velocityParameter = "Velocity";
    [SerializeField] private string groundedParameter = "Grounded";
    [SerializeField] private string jumpingParameter = "Jumping";
    [SerializeField] private float dampTime = 0.1f;

    [Header("Speed Thresholds")]
    [SerializeField] private float idleSpeedThreshold = 0.05f;
    [SerializeField] private float walkSpeedThreshold = 1.5f;
    [SerializeField] private float jogSpeedThreshold = 4f;
    [SerializeField] private float dashSpeedThreshold = 8f;

    private Animator animatorComponent;
    private Rigidbody rigidbodyComponent;
    private PlayerMotor playerMotor;
    private int velocityParameterHash;
    private int groundedParameterHash;
    private int jumpingParameterHash;
    private bool hasGroundedParameter;
    private bool hasJumpingParameter;

    protected virtual void Awake()
    {
        animatorComponent = GetComponent<Animator>();
        rigidbodyComponent = GetComponent<Rigidbody>();
        playerMotor = GetComponent<PlayerMotor>();
        RefreshAnimatorParameterHashes();
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
        walkSpeedThreshold = Mathf.Max(idleSpeedThreshold + 0.01f, walkSpeedThreshold);
        jogSpeedThreshold = Mathf.Max(walkSpeedThreshold + 0.01f, jogSpeedThreshold);
        dashSpeedThreshold = Mathf.Max(jogSpeedThreshold + 0.01f, dashSpeedThreshold);
        dampTime = Mathf.Max(0f, dampTime);
    }
#endif

    protected virtual void Update()
    {
        if (animatorComponent == null)
        {
            return;
        }

        float normalizedVelocity = CalculateBlendVelocity();
        animatorComponent.SetFloat(velocityParameterHash, normalizedVelocity, GetDampTimeSetting(), Time.deltaTime);

        if (playerMotor == null)
        {
            return;
        }

        if (hasGroundedParameter)
        {
            animatorComponent.SetBool(groundedParameterHash, playerMotor.IsGrounded);
        }

        if (hasJumpingParameter)
        {
            animatorComponent.SetBool(jumpingParameterHash, playerMotor.IsJumping);
        }
    }

    private float CalculateBlendVelocity()
    {
        float planarSpeed = GetPlanarSpeed();
        if (planarSpeed <= GetIdleSpeedThresholdSetting())
        {
            return 0f;
        }

        if (planarSpeed <= GetWalkSpeedThresholdSetting())
        {
            float t = Mathf.InverseLerp(GetIdleSpeedThresholdSetting(), GetWalkSpeedThresholdSetting(), planarSpeed);
            return Mathf.Lerp(0f, WalkBlendPoint, t);
        }

        if (planarSpeed <= GetJogSpeedThresholdSetting())
        {
            float t = Mathf.InverseLerp(GetWalkSpeedThresholdSetting(), GetJogSpeedThresholdSetting(), planarSpeed);
            return Mathf.Lerp(WalkBlendPoint, JogBlendPoint, t);
        }

        if (planarSpeed <= GetDashSpeedThresholdSetting())
        {
            float t = Mathf.InverseLerp(GetJogSpeedThresholdSetting(), GetDashSpeedThresholdSetting(), planarSpeed);
            return Mathf.Lerp(JogBlendPoint, RunBlendPoint, t);
        }

        return RunBlendPoint;
    }

    private float GetPlanarSpeed()
    {
        if (playerMotor != null)
        {
            return playerMotor.CurrentPlanarSpeed;
        }

        if (rigidbodyComponent == null)
        {
            return 0f;
        }

        Vector3 planarVelocity = rigidbodyComponent.linearVelocity;
        planarVelocity.y = 0f;
        return planarVelocity.magnitude;
    }

    private void RefreshAnimatorParameterHashes()
    {
        velocityParameterHash = Animator.StringToHash(GetVelocityParameterSetting());
        hasGroundedParameter = TryGetParameterHash(GetGroundedParameterSetting(), AnimatorControllerParameterType.Bool, out groundedParameterHash);
        hasJumpingParameter = TryGetParameterHash(GetJumpingParameterSetting(), AnimatorControllerParameterType.Bool, out jumpingParameterHash);
    }

    private bool TryGetParameterHash(string parameterName, AnimatorControllerParameterType expectedType, out int parameterHash)
    {
        parameterHash = 0;
        if (animatorComponent == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animatorComponent.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != expectedType || parameter.name != parameterName)
            {
                continue;
            }

            parameterHash = parameter.nameHash;
            return true;
        }

        return false;
    }

    private string GetVelocityParameterSetting() => animationConfig != null ? animationConfig.VelocityParameter : velocityParameter;
    private string GetGroundedParameterSetting() => animationConfig != null ? animationConfig.GroundedParameter : groundedParameter;
    private string GetJumpingParameterSetting() => animationConfig != null ? animationConfig.JumpingParameter : jumpingParameter;
    private float GetDampTimeSetting() => animationConfig != null ? animationConfig.DampTime : dampTime;
    private float GetIdleSpeedThresholdSetting() => animationConfig != null ? animationConfig.IdleSpeedThreshold : idleSpeedThreshold;
    private float GetWalkSpeedThresholdSetting() => animationConfig != null ? animationConfig.WalkSpeedThreshold : walkSpeedThreshold;
    private float GetJogSpeedThresholdSetting() => animationConfig != null ? animationConfig.JogSpeedThreshold : jogSpeedThreshold;
    private float GetDashSpeedThresholdSetting() => animationConfig != null ? animationConfig.DashSpeedThreshold : dashSpeedThreshold;
}
