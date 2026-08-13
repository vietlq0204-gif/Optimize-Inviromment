using UnityEngine;

/// <summary>
/// Procedural leg animator inspired by planted-foot / stepping-foot workflow.
/// It borrows the main idea from FImpossible's Legs Animator:
/// keep feet planted while supported, detach only when target drift is large enough,
/// animate the swing step with curves, and shift hips toward the support leg.
/// </summary>
[DisallowMultipleComponent]
public sealed class LegAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody movementBody;
    [SerializeField] private Transform skeletonRoot;
    [SerializeField] private Transform hips;
    [SerializeField] private Transform leftUpperLeg;
    [SerializeField] private Transform leftLowerLeg;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightUpperLeg;
    [SerializeField] private Transform rightLowerLeg;
    [SerializeField] private Transform rightFoot;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float footRaycastHeight = 0.8f;
    [SerializeField] private float footRaycastDistance = 2.2f;
    [SerializeField] private float footPlantOffset = 0.02f;
    [SerializeField] private float footNormalBlend = 0.7f;

    [Header("Stride")]
    [SerializeField] private float maxPlanarSpeed = 6f;
    [SerializeField] private float minSpeedToAnimate = 0.05f;
    [SerializeField] private float baseStepFrequency = 4.5f;
    [SerializeField] private float minimumCadenceMultiplier = 0.55f;
    [SerializeField] private float maximumCadenceMultiplier = 1.5f;
    [SerializeField] private float minimumStrideScale = 0.15f;
    [SerializeField] private float maximumStrideScale = 1.25f;
    [SerializeField] private float forwardStrideDistance = 0.32f;
    [SerializeField] private float lateralStrideDistance = 0.28f;
    [SerializeField] private float stepTriggerDistance = 0.18f;
    [SerializeField] private float stepTriggerDistanceAtMaxSpeed = 0.34f;
    [SerializeField] private float leadLegAdvance = 0.18f;
    [SerializeField] private float minStepHeight = 0.06f;
    [SerializeField] private float maxStepHeight = 0.22f;
    [SerializeField] private float minStepDuration = 0.12f;
    [SerializeField] private float maxStepDuration = 0.34f;
    [SerializeField] private bool invertStrideDirection = true;

    [Header("Leg Motion")]
    [SerializeField] private float upperLegSwing = 34f;
    [SerializeField] private float supportUpperLegBack = 14f;
    [SerializeField] private float lowerLegBend = 42f;
    [SerializeField] private float supportKneeBend = 12f;
    [SerializeField] private float footReturnAngle = 18f;
    [SerializeField] private float sideSwing = 11f;
    [SerializeField] [Range(0.15f, 0.75f)] private float thighLeadRatio = 0.38f;
    [SerializeField] [Range(0.05f, 0.6f)] private float shinFollowDelay = 0.18f;

    [Header("Body Motion")]
    [SerializeField] private float hipsSideOffset = 0.05f;
    [SerializeField] private float supportHipDrop = 0.035f;
    [SerializeField] private float stepHipPush = 0.025f;
    [SerializeField] private float bodyLeanAngle = 8f;
    [SerializeField] private float idleDamping = 6f;
    [SerializeField] private float poseBlendSpeed = 12f;

    private LegState leftLegState;
    private LegState rightLegState;
    private Vector3 hipsLocalPosition;
    private Quaternion hipsLocalRotation;
    private float poseWeight;
    private float cadenceTimer;
    private float lastStepSide;

    private sealed class LegState
    {
        public Transform upperLeg;
        public Transform lowerLeg;
        public Transform foot;
        public Quaternion upperBaseRotation;
        public Quaternion lowerBaseRotation;
        public Quaternion footBaseRotation;
        public Vector3 defaultFootLocalPosition;
        public float sideSign;

        public bool initialized;
        public bool isStepping;
        public float stepProgress;
        public float stepDuration;
        public float stepHeight;
        public float stepBlend;
        public float supportWeight;
        public float compression;

        public Vector3 plantedWorldPosition;
        public Vector3 plantedWorldNormal = Vector3.up;
        public Vector3 currentWorldPosition;
        public Vector3 currentWorldNormal = Vector3.up;

        public Vector3 stepStartWorldPosition;
        public Vector3 stepTargetWorldPosition;
        public Vector3 stepArcWorldOffset;
        public Vector3 stepTargetNormal = Vector3.up;

        public Vector3 desiredWorldPosition;
        public Vector3 desiredWorldNormal = Vector3.up;
        public float desiredDistance;
        public float urgency;
        public float localForwardOffset;
        public float localSideOffset;
        public float groundPitch;
        public float groundRoll;
    }

    private void Reset()
    {
        AutoBindReferences();
        CacheRigDefaults();
        ResetLegStates(true);
    }

    private void Awake()
    {
        AutoBindReferences();
        CacheRigDefaults();
        ResetLegStates(true);
    }

    private void OnEnable()
    {
        ResetLegStates(false);
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    private void LateUpdate()
    {
        if (!HasValidRig())
        {
            return;
        }

        Vector3 planarVelocity = GetPlanarVelocity();
        float planarSpeed = planarVelocity.magnitude;
        float speedNormalized = Mathf.Clamp01(planarSpeed / Mathf.Max(0.001f, maxPlanarSpeed));
        float targetWeight = planarSpeed >= minSpeedToAnimate
            ? Mathf.InverseLerp(minSpeedToAnimate, maxPlanarSpeed, planarSpeed)
            : 0f;

        float blendRate = targetWeight > poseWeight ? poseBlendSpeed : idleDamping;
        poseWeight = Mathf.MoveTowards(poseWeight, targetWeight, blendRate * Time.deltaTime);

        Vector2 moveDirectionLocal = GetMoveDirectionLocal(planarVelocity);
        float strideScale = Mathf.Lerp(minimumStrideScale, maximumStrideScale, speedNormalized) * poseWeight;
        float cadenceMultiplier = Mathf.Lerp(minimumCadenceMultiplier, maximumCadenceMultiplier, speedNormalized);
        cadenceTimer += Time.deltaTime * baseStepFrequency * cadenceMultiplier * poseWeight;

        UpdateDesiredFootPlacement(leftLegState, moveDirectionLocal, strideScale);
        UpdateDesiredFootPlacement(rightLegState, moveDirectionLocal, strideScale);
        UpdateStepping(leftLegState, moveDirectionLocal, speedNormalized);
        UpdateStepping(rightLegState, moveDirectionLocal, speedNormalized);

        if (poseWeight > 0.0001f)
        {
            TryStartStep(moveDirectionLocal, speedNormalized);
        }

        UpdateCurrentFootPlacement(leftLegState);
        UpdateCurrentFootPlacement(rightLegState);
        ApplyBodyPose(moveDirectionLocal, speedNormalized);

        if (poseWeight <= 0.0001f && !leftLegState.isStepping && !rightLegState.isStepping)
        {
            RestoreDefaultPose();
            return;
        }

        ApplyLegPose(leftLegState, moveDirectionLocal, speedNormalized);
        ApplyLegPose(rightLegState, moveDirectionLocal, speedNormalized);
    }

    [ContextMenu("Auto Bind Bones")]
    private void AutoBindReferences()
    {
        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody>();
        }

        if (skeletonRoot == null)
        {
            skeletonRoot = FindChildByName(transform, "Skeleton");
        }

        Transform searchRoot = skeletonRoot != null ? skeletonRoot : transform;

        hips = hips != null ? hips : FindChildByName(searchRoot, "Hips");
        leftUpperLeg = leftUpperLeg != null ? leftUpperLeg : FindChildByName(searchRoot, "Left_UpperLeg");
        leftLowerLeg = leftLowerLeg != null ? leftLowerLeg : FindChildByName(searchRoot, "Left_LowerLeg");
        leftFoot = leftFoot != null ? leftFoot : FindChildByName(searchRoot, "Left_Foot");
        rightUpperLeg = rightUpperLeg != null ? rightUpperLeg : FindChildByName(searchRoot, "Right_UpperLeg");
        rightLowerLeg = rightLowerLeg != null ? rightLowerLeg : FindChildByName(searchRoot, "Right_LowerLeg");
        rightFoot = rightFoot != null ? rightFoot : FindChildByName(searchRoot, "Right_Foot");
    }

    private void CacheRigDefaults()
    {
        if (!HasValidRig())
        {
            return;
        }

        hipsLocalPosition = hips.localPosition;
        hipsLocalRotation = hips.localRotation;

        SetupLegState(leftLegState ??= new LegState(), leftUpperLeg, leftLowerLeg, leftFoot, -1f);
        SetupLegState(rightLegState ??= new LegState(), rightUpperLeg, rightLowerLeg, rightFoot, 1f);
    }

    private void SetupLegState(LegState leg, Transform upper, Transform lower, Transform footBone, float fallbackSide)
    {
        leg.upperLeg = upper;
        leg.lowerLeg = lower;
        leg.foot = footBone;
        leg.upperBaseRotation = upper.localRotation;
        leg.lowerBaseRotation = lower.localRotation;
        leg.footBaseRotation = footBone.localRotation;
        leg.defaultFootLocalPosition = transform.InverseTransformPoint(footBone.position);
        leg.sideSign = Mathf.Sign(Mathf.Abs(hips.InverseTransformPoint(upper.position).x) > 0.0001f
            ? hips.InverseTransformPoint(upper.position).x
            : fallbackSide);
        leg.initialized = true;
    }

    private void ResetLegStates(bool forceFromCurrentPose)
    {
        if (!HasValidRig())
        {
            return;
        }

        ResetLegState(leftLegState, forceFromCurrentPose);
        ResetLegState(rightLegState, forceFromCurrentPose);
        poseWeight = 0f;
        cadenceTimer = 0f;
        lastStepSide = 0f;
    }

    private void ResetLegState(LegState leg, bool forceFromCurrentPose)
    {
        if (leg == null || leg.foot == null)
        {
            return;
        }

        if (forceFromCurrentPose || !leg.initialized)
        {
            leg.plantedWorldPosition = leg.foot.position;
            leg.plantedWorldNormal = transform.up;
        }
        else
        {
            leg.plantedWorldPosition = leg.currentWorldPosition == Vector3.zero ? leg.foot.position : leg.currentWorldPosition;
        }

        leg.currentWorldPosition = leg.plantedWorldPosition;
        leg.currentWorldNormal = leg.plantedWorldNormal;
        leg.isStepping = false;
        leg.stepProgress = 0f;
        leg.stepDuration = maxStepDuration;
        leg.stepHeight = minStepHeight;
        leg.stepBlend = 0f;
        leg.supportWeight = 1f;
        leg.compression = 0f;
        leg.desiredDistance = 0f;
        leg.urgency = 0f;
        leg.localForwardOffset = 0f;
        leg.localSideOffset = 0f;
        leg.groundPitch = 0f;
        leg.groundRoll = 0f;
    }

    private Vector3 GetPlanarVelocity()
    {
        Vector3 velocity = movementBody != null ? movementBody.linearVelocity : Vector3.zero;
        return Vector3.ProjectOnPlane(velocity, transform.up);
    }

    private Vector2 GetMoveDirectionLocal(Vector3 planarVelocity)
    {
        if (planarVelocity.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
        Vector2 localPlanar = new Vector2(localVelocity.x, localVelocity.z);
        return localPlanar.sqrMagnitude > 0.0001f ? localPlanar.normalized : Vector2.zero;
    }

    private void UpdateDesiredFootPlacement(LegState leg, Vector2 moveDirectionLocal, float strideScale)
    {
        Vector3 desiredLocal = leg.defaultFootLocalPosition;
        float strideDirection = invertStrideDirection ? -1f : 1f;

        desiredLocal.z += moveDirectionLocal.y * forwardStrideDistance * strideScale * strideDirection;
        desiredLocal.x += moveDirectionLocal.x * lateralStrideDistance * strideScale;

        Vector3 desiredWorld = transform.TransformPoint(desiredLocal);
        Vector3 rayOrigin = desiredWorld + transform.up * footRaycastHeight;

        if (Physics.Raycast(rayOrigin, -transform.up, out RaycastHit hit, footRaycastDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            leg.desiredWorldPosition = hit.point + hit.normal * footPlantOffset;
            leg.desiredWorldNormal = hit.normal;
        }
        else
        {
            leg.desiredWorldPosition = desiredWorld;
            leg.desiredWorldNormal = transform.up;
        }

        leg.desiredDistance = Vector3.Distance(
            Vector3.ProjectOnPlane(leg.plantedWorldPosition, transform.up),
            Vector3.ProjectOnPlane(leg.desiredWorldPosition, transform.up));

        Vector3 desiredLocalFromWorld = transform.InverseTransformPoint(leg.desiredWorldPosition);
        leg.localForwardOffset = desiredLocalFromWorld.z - leg.defaultFootLocalPosition.z;
        leg.localSideOffset = desiredLocalFromWorld.x - leg.defaultFootLocalPosition.x;

        float supportBias = moveDirectionLocal.x * leg.sideSign * leadLegAdvance;
        float forwardBias = 0f;

        if (Mathf.Abs(moveDirectionLocal.y) > Mathf.Abs(moveDirectionLocal.x))
        {
            Vector3 plantedLocal = transform.InverseTransformPoint(leg.plantedWorldPosition);
            forwardBias = Vector2.Dot(
                new Vector2(plantedLocal.x - leg.defaultFootLocalPosition.x, plantedLocal.z - leg.defaultFootLocalPosition.z),
                new Vector2(moveDirectionLocal.x, moveDirectionLocal.y));
        }

        leg.urgency = leg.desiredDistance + supportBias - forwardBias * 0.2f;
    }

    private void UpdateStepping(LegState leg, Vector2 moveDirectionLocal, float speedNormalized)
    {
        if (!leg.isStepping)
        {
            leg.supportWeight = poseWeight;
            leg.compression = poseWeight * Mathf.Lerp(0.35f, 1f, speedNormalized);
            return;
        }

        leg.stepProgress = Mathf.MoveTowards(leg.stepProgress, 1f, Time.deltaTime / Mathf.Max(0.0001f, leg.stepDuration));
        leg.stepBlend = EvaluateMoveCurve(leg.stepProgress);
        leg.supportWeight = (1f - EvaluateRaiseCurve(leg.stepProgress)) * poseWeight;
        leg.compression = (1f - EvaluateRaiseCurve(leg.stepProgress)) * poseWeight * 0.35f;

        if (leg.stepProgress >= 0.9999f)
        {
            leg.isStepping = false;
            leg.plantedWorldPosition = leg.stepTargetWorldPosition;
            leg.plantedWorldNormal = leg.stepTargetNormal;
            leg.currentWorldPosition = leg.plantedWorldPosition;
            leg.currentWorldNormal = leg.plantedWorldNormal;
        }
    }

    private void TryStartStep(Vector2 moveDirectionLocal, float speedNormalized)
    {
        if (leftLegState.isStepping || rightLegState.isStepping)
        {
            return;
        }

        float triggerDistance = Mathf.Lerp(stepTriggerDistance, stepTriggerDistanceAtMaxSpeed, speedNormalized);
        bool leftReady = leftLegState.desiredDistance > triggerDistance;
        bool rightReady = rightLegState.desiredDistance > triggerDistance;

        if (!leftReady && !rightReady)
        {
            return;
        }

        LegState candidate = SelectCandidateLeg(moveDirectionLocal, leftReady, rightReady);
        if (candidate == null)
        {
            return;
        }

        StartStep(candidate, speedNormalized);
    }

    private LegState SelectCandidateLeg(Vector2 moveDirectionLocal, bool leftReady, bool rightReady)
    {
        if (leftReady && !rightReady)
        {
            return leftLegState;
        }

        if (!leftReady && rightReady)
        {
            return rightLegState;
        }

        float lateralPreference = moveDirectionLocal.x;
        if (Mathf.Abs(lateralPreference) > 0.2f)
        {
            return lateralPreference > 0f ? rightLegState : leftLegState;
        }

        float leftScore = leftLegState.urgency;
        float rightScore = rightLegState.urgency;

        if (Mathf.Abs(leftScore - rightScore) < 0.02f && lastStepSide != 0f)
        {
            return lastStepSide > 0f ? leftLegState : rightLegState;
        }

        return rightScore > leftScore ? rightLegState : leftLegState;
    }

    private void StartStep(LegState leg, float speedNormalized)
    {
        leg.isStepping = true;
        leg.stepProgress = 0f;
        leg.stepBlend = 0f;
        leg.stepStartWorldPosition = leg.plantedWorldPosition;
        leg.stepTargetWorldPosition = leg.desiredWorldPosition;
        leg.stepTargetNormal = leg.desiredWorldNormal;

        float distance01 = Mathf.Clamp01(leg.desiredDistance / Mathf.Max(0.001f, stepTriggerDistanceAtMaxSpeed));
        leg.stepDuration = Mathf.Lerp(maxStepDuration, minStepDuration, speedNormalized);
        leg.stepDuration *= Mathf.Lerp(1f, 0.82f, distance01);
        leg.stepHeight = Mathf.Lerp(minStepHeight, maxStepHeight, Mathf.Max(speedNormalized, distance01));

        Vector3 towards = leg.stepTargetWorldPosition - leg.stepStartWorldPosition;
        Vector3 planarTowards = Vector3.ProjectOnPlane(towards, transform.up);
        Vector3 sideAxis = planarTowards.sqrMagnitude > 0.0001f
            ? Vector3.Cross(planarTowards.normalized, transform.up).normalized
            : transform.right;

        float spherizeAmount = Mathf.Lerp(0.01f, 0.05f, distance01) * leg.sideSign;
        leg.stepArcWorldOffset = sideAxis * spherizeAmount;

        lastStepSide = -leg.sideSign;
    }

    private void UpdateCurrentFootPlacement(LegState leg)
    {
        if (!leg.isStepping)
        {
            leg.currentWorldPosition = leg.plantedWorldPosition;
            leg.currentWorldNormal = Vector3.Slerp(leg.currentWorldNormal, leg.plantedWorldNormal, poseBlendSpeed * Time.deltaTime);
            ComputeGroundAngles(leg);
            return;
        }

        float moveT = leg.stepBlend;
        float raiseT = EvaluateRaiseCurve(leg.stepProgress);
        float spherizeT = EvaluateSpherizeCurve(leg.stepProgress);

        Vector3 worldPosition = Vector3.LerpUnclamped(leg.stepStartWorldPosition, leg.stepTargetWorldPosition, moveT);
        worldPosition += transform.up * (leg.stepHeight * raiseT);
        worldPosition += leg.stepArcWorldOffset * spherizeT;

        leg.currentWorldPosition = worldPosition;
        leg.currentWorldNormal = Vector3.Slerp(transform.up, leg.stepTargetNormal, footNormalBlend * moveT);

        ComputeGroundAngles(leg);
    }

    private void ComputeGroundAngles(LegState leg)
    {
        Vector3 normal = leg.currentWorldNormal.normalized;
        float pitch = Vector3.SignedAngle(transform.up, normal, transform.right);
        float roll = -Vector3.SignedAngle(transform.up, normal, transform.forward);
        leg.groundPitch = pitch * footNormalBlend;
        leg.groundRoll = roll * footNormalBlend;
    }

    private void ApplyBodyPose(Vector2 moveDirectionLocal, float speedNormalized)
    {
        float supportTotal = leftLegState.supportWeight + rightLegState.supportWeight;
        float supportBias = 0f;

        if (supportTotal > 0.0001f)
        {
            supportBias = (leftLegState.supportWeight * leftLegState.sideSign + rightLegState.supportWeight * rightLegState.sideSign) / supportTotal;
        }

        float leftPush = leftLegState.isStepping ? EvaluateHipsPushCurve(leftLegState.stepProgress) : 0f;
        float rightPush = rightLegState.isStepping ? EvaluateHipsPushCurve(rightLegState.stepProgress) : 0f;
        float supportCompression = Mathf.Max(leftLegState.compression, rightLegState.compression);

        Vector3 targetPosition = hipsLocalPosition;
        targetPosition.x += supportBias * hipsSideOffset * poseWeight;
        targetPosition.y -= supportCompression * supportHipDrop;
        targetPosition.y -= Mathf.Max(leftPush, rightPush) * stepHipPush * poseWeight;

        float leanForward = -moveDirectionLocal.y * bodyLeanAngle * Mathf.Lerp(0.35f, 1f, speedNormalized) * poseWeight;
        float leanSide = (-supportBias * bodyLeanAngle * 0.75f + moveDirectionLocal.x * bodyLeanAngle * 0.3f) * poseWeight;
        float twistYaw = moveDirectionLocal.x * bodyLeanAngle * 0.18f * poseWeight;

        Quaternion targetRotation = hipsLocalRotation * Quaternion.Euler(leanForward, twistYaw, leanSide);

        hips.localPosition = Vector3.Lerp(hips.localPosition, targetPosition, poseBlendSpeed * Time.deltaTime);
        hips.localRotation = Quaternion.Slerp(hips.localRotation, targetRotation, poseBlendSpeed * Time.deltaTime);
    }

    private void ApplyLegPose(LegState leg, Vector2 moveDirectionLocal, float speedNormalized)
    {
        Vector3 targetLocal = transform.InverseTransformPoint(leg.currentWorldPosition);
        Vector3 offset = targetLocal - leg.defaultFootLocalPosition;

        float strideDirection = invertStrideDirection ? -1f : 1f;
        float forwardOffset = offset.z * strideDirection;
        float sideOffset = offset.x * leg.sideSign;
        float verticalOffset = Mathf.Max(0f, offset.y);

        float thighLead = leg.isStepping
            ? SmoothStep01(Mathf.Clamp01(leg.stepProgress / Mathf.Max(0.05f, thighLeadRatio)))
            : 0f;

        float shinFollow = leg.isStepping
            ? SmoothStep01(Mathf.Clamp01((leg.stepProgress - shinFollowDelay) / Mathf.Max(0.05f, 1f - shinFollowDelay)))
            : 0f;

        float upperPitch = forwardOffset * upperLegSwing * 2.2f * poseWeight;
        float supportBack = (!leg.isStepping ? -leg.supportWeight * supportUpperLegBack * poseWeight : 0f);
        upperPitch += supportBack;
        upperPitch += verticalOffset * upperLegSwing * 1.4f * thighLead;

        float upperRoll = (sideOffset * sideSwing * 1.8f + moveDirectionLocal.x * sideSwing * 0.25f * leg.sideSign) * poseWeight;

        float lowerPitch = (!leg.isStepping ? leg.compression * supportKneeBend : 0f) * poseWeight;
        lowerPitch += verticalOffset * lowerLegBend * 1.75f * shinFollow;
        lowerPitch += Mathf.Abs(forwardOffset) * lowerLegBend * 0.65f * shinFollow;

        float footPitch = leg.groundPitch;
        footPitch += EvaluateFootRotationCurve(leg.stepProgress) * footReturnAngle * poseWeight * (leg.isStepping ? 1f : 0f);
        footPitch += -lowerPitch * 0.22f;

        float footRoll = leg.groundRoll + upperRoll * 0.25f;

        Quaternion upperOffset = Quaternion.Euler(upperPitch, 0f, -upperRoll);
        Quaternion lowerOffset = Quaternion.Euler(lowerPitch, 0f, 0f);
        Quaternion footOffset = Quaternion.Euler(footPitch, 0f, footRoll);

        leg.upperLeg.localRotation = Quaternion.Slerp(
            leg.upperLeg.localRotation,
            leg.upperBaseRotation * upperOffset,
            poseBlendSpeed * Time.deltaTime);

        leg.lowerLeg.localRotation = Quaternion.Slerp(
            leg.lowerLeg.localRotation,
            leg.lowerBaseRotation * lowerOffset,
            poseBlendSpeed * Time.deltaTime);

        leg.foot.localRotation = Quaternion.Slerp(
            leg.foot.localRotation,
            leg.footBaseRotation * footOffset,
            poseBlendSpeed * Time.deltaTime);
    }

    private void RestoreDefaultPose()
    {
        hips.localPosition = Vector3.Lerp(hips.localPosition, hipsLocalPosition, idleDamping * Time.deltaTime);
        hips.localRotation = Quaternion.Slerp(hips.localRotation, hipsLocalRotation, idleDamping * Time.deltaTime);
        RestoreLegPose(leftLegState);
        RestoreLegPose(rightLegState);
    }

    private void RestoreLegPose(LegState leg)
    {
        leg.upperLeg.localRotation = Quaternion.Slerp(leg.upperLeg.localRotation, leg.upperBaseRotation, idleDamping * Time.deltaTime);
        leg.lowerLeg.localRotation = Quaternion.Slerp(leg.lowerLeg.localRotation, leg.lowerBaseRotation, idleDamping * Time.deltaTime);
        leg.foot.localRotation = Quaternion.Slerp(leg.foot.localRotation, leg.footBaseRotation, idleDamping * Time.deltaTime);
    }

    private bool HasValidRig()
    {
        return movementBody != null
            && hips != null
            && leftUpperLeg != null
            && leftLowerLeg != null
            && leftFoot != null
            && rightUpperLeg != null
            && rightLowerLeg != null
            && rightFoot != null;
    }

    private static float EvaluateMoveCurve(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.4885f
            ? Mathf.LerpUnclamped(0f, 0.8972f, SmoothStep01(t / 0.4885f))
            : Mathf.LerpUnclamped(0.8972f, 1f, SmoothStep01((t - 0.4885f) / 0.5115f));
    }

    private static float EvaluateRaiseCurve(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.2731f)
        {
            return Mathf.LerpUnclamped(0f, 0.45f, SmoothStep01(t / 0.2731f));
        }

        if (t < 0.5051f)
        {
            return Mathf.LerpUnclamped(0.45f, 0.5f, SmoothStep01((t - 0.2731f) / 0.232f));
        }

        if (t < 0.911f)
        {
            return Mathf.LerpUnclamped(0.5f, 0f, SmoothStep01((t - 0.5051f) / 0.4059f));
        }

        return Mathf.LerpUnclamped(0f, 0f, t);
    }

    private static float EvaluateSpherizeCurve(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.4f)
        {
            return Mathf.LerpUnclamped(0f, 0.3f, SmoothStep01(t / 0.4f));
        }

        if (t < 0.85f)
        {
            return Mathf.LerpUnclamped(0.3f, 0f, SmoothStep01((t - 0.4f) / 0.45f));
        }

        return 0f;
    }

    private static float EvaluateFootRotationCurve(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.4378f)
        {
            return Mathf.LerpUnclamped(0f, 0.2036f, SmoothStep01(t / 0.4378f));
        }

        if (t < 0.7841f)
        {
            return Mathf.LerpUnclamped(0.2036f, -0.1339f, SmoothStep01((t - 0.4378f) / 0.3463f));
        }

        return Mathf.LerpUnclamped(-0.1339f, 0f, SmoothStep01((t - 0.7841f) / 0.2159f));
    }

    private static float EvaluateHipsPushCurve(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.383f)
        {
            return Mathf.LerpUnclamped(0f, 0.3734f, SmoothStep01(t / 0.383f));
        }

        if (t < 0.7075f)
        {
            return Mathf.LerpUnclamped(0.3734f, 0.146f, SmoothStep01((t - 0.383f) / 0.3245f));
        }

        return Mathf.LerpUnclamped(0.146f, 0f, SmoothStep01((t - 0.7075f) / 0.2925f));
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform match = FindChildByName(child, targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
