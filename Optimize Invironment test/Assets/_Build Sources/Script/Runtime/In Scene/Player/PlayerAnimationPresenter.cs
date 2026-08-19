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
    [SerializeField] private string lowIdleLandingTrigger = "LandLowIdle";
    [SerializeField] private string highIdleLandingTrigger = "LandHighIdle";
    [SerializeField] private string lowRunLandingTrigger = "LandLowRun";
    [SerializeField] private string highRunLandingTrigger = "LandHighRun";
    [SerializeField] private string runEndTrigger = "RunEnd";
    [SerializeField] private string runTurn180LeftTrigger = "RunTurn180Left";
    [SerializeField] private string runTurn180RightTrigger = "RunTurn180Right";
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
    private int lowIdleLandingTriggerHash;
    private int highIdleLandingTriggerHash;
    private int lowRunLandingTriggerHash;
    private int highRunLandingTriggerHash;
    private int runEndTriggerHash;
    private int runTurn180LeftTriggerHash;
    private int runTurn180RightTriggerHash;
    private bool hasGroundedParameter;
    private bool hasJumpingParameter;
    private bool hasLowIdleLandingTrigger;
    private bool hasHighIdleLandingTrigger;
    private bool hasLowRunLandingTrigger;
    private bool hasHighRunLandingTrigger;
    private bool hasRunEndTrigger;
    private bool hasRunTurn180LeftTrigger;
    private bool hasRunTurn180RightTrigger;
    private int lastConsumedLandingEventVersion;
    private int lastConsumedRunEndEventVersion;
    private int lastConsumedRunTurnEventVersion;

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

        if (playerMotor.LandingEventVersion == lastConsumedLandingEventVersion)
        {
            if (playerMotor.RunEndEventVersion == lastConsumedRunEndEventVersion)
            {
                if (playerMotor.RunTurnEventVersion == lastConsumedRunTurnEventVersion)
                {
                    return;
                }
            }
        }

        if (playerMotor.LandingEventVersion != lastConsumedLandingEventVersion)
        {
            lastConsumedLandingEventVersion = playerMotor.LandingEventVersion;
            TriggerLandingAnimation(playerMotor.LastLandingAnimationType);
        }

        bool hasNewRunTurnEvent = playerMotor.RunTurnEventVersion != lastConsumedRunTurnEventVersion;
        if (hasNewRunTurnEvent)
        {
            lastConsumedRunTurnEventVersion = playerMotor.RunTurnEventVersion;
            TriggerRunTurnAnimation(playerMotor.LastRunTurnAnimationType);
        }

        if (playerMotor.RunEndEventVersion != lastConsumedRunEndEventVersion)
        {
            lastConsumedRunEndEventVersion = playerMotor.RunEndEventVersion;
            if (!hasNewRunTurnEvent)
            {
                TriggerRunEndAnimation();
            }
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
        hasLowIdleLandingTrigger = TryGetParameterHash(GetLowIdleLandingTriggerSetting(), AnimatorControllerParameterType.Trigger, out lowIdleLandingTriggerHash);
        hasHighIdleLandingTrigger = TryGetParameterHash(GetHighIdleLandingTriggerSetting(), AnimatorControllerParameterType.Trigger, out highIdleLandingTriggerHash);
        hasLowRunLandingTrigger = TryGetParameterHash(GetLowRunLandingTriggerSetting(), AnimatorControllerParameterType.Trigger, out lowRunLandingTriggerHash);
        hasHighRunLandingTrigger = TryGetParameterHash(GetHighRunLandingTriggerSetting(), AnimatorControllerParameterType.Trigger, out highRunLandingTriggerHash);
        hasRunEndTrigger = TryGetParameterHash(GetRunEndTriggerSetting(), AnimatorControllerParameterType.Trigger, out runEndTriggerHash);
        hasRunTurn180LeftTrigger = TryGetParameterHash(GetRunTurn180LeftTriggerSetting(), AnimatorControllerParameterType.Trigger, out runTurn180LeftTriggerHash);
        hasRunTurn180RightTrigger = TryGetParameterHash(GetRunTurn180RightTriggerSetting(), AnimatorControllerParameterType.Trigger, out runTurn180RightTriggerHash);
    }

    private void TriggerLandingAnimation(PlayerMotor.LandingAnimationType landingAnimationType)
    {
        ResetLandingTriggers();

        switch (landingAnimationType)
        {
            case PlayerMotor.LandingAnimationType.LowIdle:
                if (hasLowIdleLandingTrigger)
                {
                    animatorComponent.SetTrigger(lowIdleLandingTriggerHash);
                }

                break;
            case PlayerMotor.LandingAnimationType.HighIdle:
                if (hasHighIdleLandingTrigger)
                {
                    animatorComponent.SetTrigger(highIdleLandingTriggerHash);
                }

                break;
            case PlayerMotor.LandingAnimationType.LowRun:
                if (hasLowRunLandingTrigger)
                {
                    animatorComponent.SetTrigger(lowRunLandingTriggerHash);
                }

                break;
            case PlayerMotor.LandingAnimationType.HighRun:
                if (hasHighRunLandingTrigger)
                {
                    animatorComponent.SetTrigger(highRunLandingTriggerHash);
                }

                break;
        }
    }

    private void ResetLandingTriggers()
    {
        if (hasLowIdleLandingTrigger)
        {
            animatorComponent.ResetTrigger(lowIdleLandingTriggerHash);
        }

        if (hasHighIdleLandingTrigger)
        {
            animatorComponent.ResetTrigger(highIdleLandingTriggerHash);
        }

        if (hasLowRunLandingTrigger)
        {
            animatorComponent.ResetTrigger(lowRunLandingTriggerHash);
        }

        if (hasHighRunLandingTrigger)
        {
            animatorComponent.ResetTrigger(highRunLandingTriggerHash);
        }
    }

    private void TriggerRunEndAnimation()
    {
        if (!hasRunEndTrigger)
        {
            return;
        }

        animatorComponent.ResetTrigger(runEndTriggerHash);
        animatorComponent.SetTrigger(runEndTriggerHash);
    }

    private void TriggerRunTurnAnimation(PlayerMotor.RunTurnAnimationType runTurnAnimationType)
    {
        ResetRunTurnTriggers();

        if (hasRunEndTrigger)
        {
            animatorComponent.ResetTrigger(runEndTriggerHash);
        }

        switch (runTurnAnimationType)
        {
            case PlayerMotor.RunTurnAnimationType.Left180:
                if (hasRunTurn180LeftTrigger)
                {
                    animatorComponent.SetTrigger(runTurn180LeftTriggerHash);
                }

                break;
            case PlayerMotor.RunTurnAnimationType.Right180:
                if (hasRunTurn180RightTrigger)
                {
                    animatorComponent.SetTrigger(runTurn180RightTriggerHash);
                }

                break;
        }
    }

    private void ResetRunTurnTriggers()
    {
        if (hasRunTurn180LeftTrigger)
        {
            animatorComponent.ResetTrigger(runTurn180LeftTriggerHash);
        }

        if (hasRunTurn180RightTrigger)
        {
            animatorComponent.ResetTrigger(runTurn180RightTriggerHash);
        }
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
    private string GetLowIdleLandingTriggerSetting() => animationConfig != null ? animationConfig.LowIdleLandingTrigger : lowIdleLandingTrigger;
    private string GetHighIdleLandingTriggerSetting() => animationConfig != null ? animationConfig.HighIdleLandingTrigger : highIdleLandingTrigger;
    private string GetLowRunLandingTriggerSetting() => animationConfig != null ? animationConfig.LowRunLandingTrigger : lowRunLandingTrigger;
    private string GetHighRunLandingTriggerSetting() => animationConfig != null ? animationConfig.HighRunLandingTrigger : highRunLandingTrigger;
    private string GetRunEndTriggerSetting() => animationConfig != null ? animationConfig.RunEndTrigger : runEndTrigger;
    private string GetRunTurn180LeftTriggerSetting() => animationConfig != null ? animationConfig.RunTurn180LeftTrigger : runTurn180LeftTrigger;
    private string GetRunTurn180RightTriggerSetting() => animationConfig != null ? animationConfig.RunTurn180RightTrigger : runTurn180RightTrigger;
    private float GetDampTimeSetting() => animationConfig != null ? animationConfig.DampTime : dampTime;
    private float GetIdleSpeedThresholdSetting() => animationConfig != null ? animationConfig.IdleSpeedThreshold : idleSpeedThreshold;
    private float GetWalkSpeedThresholdSetting() => animationConfig != null ? animationConfig.WalkSpeedThreshold : walkSpeedThreshold;
    private float GetJogSpeedThresholdSetting() => animationConfig != null ? animationConfig.JogSpeedThreshold : jogSpeedThreshold;
    private float GetDashSpeedThresholdSetting() => animationConfig != null ? animationConfig.DashSpeedThreshold : dashSpeedThreshold;
}
