using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Plant/Dynamic Plant Interaction Field")]
public sealed class GrassInteractionField : MonoBehaviour
{
    private const int ThreadGroupSize = 8;
    private const int SourceStride = sizeof(float) * 16;

    private static readonly int StateMapId = Shader.PropertyToID("_PlantDynamicInteractionStateMap");
    private static readonly int FlattenMapId = Shader.PropertyToID("_PlantDynamicInteractionFlattenMap");
    private static readonly int FieldParamsId = Shader.PropertyToID("_PlantDynamicInteractionFieldParams");
    private static readonly int RenderParamsId = Shader.PropertyToID("_PlantDynamicInteractionRenderParams");
    private static readonly int StampSourcesId = Shader.PropertyToID("_PlantDynamicInteractionSources");
    private static readonly int StampParamsId = Shader.PropertyToID("_PlantDynamicInteractionStampParams");
    private static readonly int ForceMapId = Shader.PropertyToID("_ForceMap");
    private static readonly int ForceMapWriteId = Shader.PropertyToID("_ForceMapWrite");
    private static readonly int StateReadId = Shader.PropertyToID("_StateRead");
    private static readonly int StateWriteId = Shader.PropertyToID("_StateWrite");
    private static readonly int FlattenReadId = Shader.PropertyToID("_FlattenRead");
    private static readonly int FlattenWriteId = Shader.PropertyToID("_FlattenWrite");
    private static readonly int SimulationParamsId = Shader.PropertyToID("_SimulationParams");
    private static readonly int LimitParamsId = Shader.PropertyToID("_LimitParams");
    private static readonly int TurbulenceParamsId = Shader.PropertyToID("_TurbulenceParams");
    private static readonly int RecenterParamsId = Shader.PropertyToID("_RecenterParams");
    private static readonly int StampRegionParamsId = Shader.PropertyToID("_PlantDynamicInteractionStampRegionParams");
    private static readonly int StampRegionMaxParamsId = Shader.PropertyToID("_PlantDynamicInteractionStampRegionMaxParams");
    private static readonly int DebugColorId = Shader.PropertyToID("_PlantDynamicInteractionDebugColor");
    private static readonly int DebugParamsId = Shader.PropertyToID("_PlantDynamicInteractionDebugParams");

    [Header("Follow")]
    [Tooltip("Tâm của vùng mô phỏng cỏ. Thường đặt là Player để chỉ mô phỏng cỏ quanh người chơi. Vật thể tương tác ở ngoài vùng Coverage sẽ không ảnh hưởng cỏ.")]
    [SerializeField] private Transform followTarget;
    [Tooltip("Dịch tâm vùng mô phỏng so với Follow Target theo tọa độ world. Dùng khi muốn vùng cỏ nằm lệch về phía trước camera/player. Tăng X/Z để đẩy vùng theo trục world tương ứng.")]
    [SerializeField] private Vector3 worldOffset;
    [Tooltip("Nếu Follow Target trống, tự dùng Camera.main làm tâm vùng mô phỏng. Bật khi scene không có player cố định; tắt nếu muốn field đứng tại vị trí GameObject này.")]
    [SerializeField] private bool useMainCameraFallback = true;
    [Tooltip("Cho phép chạy mô phỏng trong Edit Mode. Bật để xem thử trong Scene view khi không Play; tắt để tránh tốn GPU và tránh thay đổi preview ngoài ý muốn.")]
    [SerializeField] private bool simulateInEditMode;

    [Header("Field")]
    [Tooltip("Kích thước vùng cỏ có thể tương tác, tính theo mét, dạng hình vuông quanh Follow Target. Tăng để vật thể xa player vẫn tương tác; giảm để tiết kiệm GPU và tăng mật độ chi tiết trên mỗi mét.")]
    [SerializeField, Min(8f)] private float coverage = 32f;
    [Tooltip("Độ phân giải texture mô phỏng. Tăng để vùng đè/cong sắc nét hơn; giảm để nhẹ GPU hơn nhưng vùng tương tác sẽ thô và dễ bị răng cưa.")]
    [SerializeField, Range(64, 1024)] private int resolution = 512;
    [Tooltip("Số GrassInteractionSource tối đa được xử lý mỗi frame. Tăng nếu có nhiều vật thể cùng lúc đè cỏ; giảm để giới hạn chi phí GPU. Source vượt quá giới hạn sẽ bị bỏ qua trong frame đó.")]
    [SerializeField, Range(1, 512)] private int maxSources = 256;
    [Tooltip("Số lần cập nhật mô phỏng mỗi giây. Tăng để phản ứng mượt hơn với vật thể nhanh; giảm để tiết kiệm GPU. Cỏ thường ổn ở 20-30Hz.")]
    [SerializeField, Range(1f, 60f)] private float simulationFrequency = 30f;
    [Tooltip("Số bước mô phỏng tối đa được chạy trong một frame khi game bị tụt FPS. Tăng giúp mô phỏng bắt kịp thời gian tốt hơn; giảm để tránh một frame bị quá nặng.")]
    [SerializeField, Range(1, 4)] private int maxStepsPerFrame = 2;
    [Tooltip("Thời gian tiếp tục chạy mô phỏng sau khi không còn source trong vùng. Tăng để cỏ có thời gian rung/hồi lại tự nhiên; giảm để hệ thống tắt nhanh hơn sau khi hết tương tác.")]
    [SerializeField, Min(0f)] private float recoveryWakeSeconds = 3f;

    [Header("Simulation")]
    [Tooltip("Độ mạnh lực đẩy ngang làm cỏ nghiêng ra xa vật thể. Tăng nếu cỏ bị đẩy quá nhẹ; giảm nếu cỏ cong quá mạnh hoặc văng xa.")]
    [SerializeField, Min(0f)] private float forceStrength = 18f;
    [Tooltip("Độ cứng kéo cỏ bị cong trở về vị trí ban đầu. Tăng để cỏ hồi nhanh và bật mạnh hơn; giảm để cỏ mềm, chậm trở lại hơn.")]
    [SerializeField, Min(0f)] private float bendSpring = 42f;
    [Tooltip("Độ giảm rung của chuyển động cong. Tăng để cỏ hết rung nhanh và ít lắc; giảm để cỏ rung lâu hơn sau khi bị tác động.")]
    [SerializeField, Min(0f)] private float bendDamping = 9f;
    [Tooltip("Giới hạn độ lệch ngang tối đa của cỏ trong mô phỏng, tính theo world unit. Tăng nếu muốn cỏ có thể nghiêng xa hơn; giảm để tránh cỏ bị kéo quá lố.")]
    [SerializeField, Min(0f)] private float maxBend = 0.9f;
    [Tooltip("Độ mạnh lực đè làm cỏ hạ thấp xuống. Tăng nếu vật thể đè cỏ chưa đủ rõ; giảm nếu cỏ bị lún quá sâu.")]
    [SerializeField, Min(0f)] private float flattenForceStrength = 14f;
    [Tooltip("Độ cứng kéo trạng thái bị đè về bình thường. Tăng để cỏ bật dậy nhanh hơn; giảm để vết đè tồn tại lâu và mềm hơn.")]
    [SerializeField, Min(0f)] private float flattenSpring = 36f;
    [Tooltip("Độ giảm rung của trạng thái đè bẹp. Tăng để cỏ dừng dao động nhanh khi bật dậy; giảm để cỏ nhún/rung lâu hơn.")]
    [SerializeField, Min(0f)] private float flattenDamping = 8f;
    [Tooltip("Giới hạn độ hạ thấp tối đa khi cỏ bị đè, tính theo world unit. Tăng để cỏ có thể nằm sát đất hơn; giảm để giữ cỏ không bị lún quá nhiều.")]
    [SerializeField, Min(0f)] private float maxFlatten = 0.55f;
    [Tooltip("Độ nhiễu phụ trong vùng wake phía sau vật thể đang chạy. Tăng để vệt gió rung hỗn loạn hơn; giảm để chuyển động sạch và ít loạn hơn.")]
    [SerializeField, Min(0f)] private float turbulenceStrength = 2f;

    [Header("Air Wake")]
    [Tooltip("Độ mạnh wake dùng chung cho mọi GrassInteractionSource. Tăng để vật thể đang chạy quạt cỏ phía sau mạnh hơn; giảm nếu wake quá rõ.")]
    [SerializeField, Min(0f)] private float airWakeStrength = 0.2f;
    [Tooltip("Quy đổi tốc độ của source thành cường độ wake. Tăng để vật chạy nhanh tác động wake mạnh hơn; giảm nếu wake quá nhạy với tốc độ.")]
    [SerializeField, Min(0f)] private float speedToWake = 0.12f;
    [Tooltip("Chiều dài wake tối đa theo tốc độ của source. Tăng để vật chạy nhanh để lại vệt dài hơn; giảm để wake ngắn hơn.")]
    [SerializeField, Min(0f)] private float wakeLengthPerSpeed = 0.35f;
    [Tooltip("Tốc độ wake dài ra theo quãng đường vật thể đã đi. 1 nghĩa là wake dài thêm đúng theo khoảng cách vật đã chạy; tăng để vệt mọc nhanh hơn; giảm để vệt mọc chậm hơn.")]
    [SerializeField, Min(0f)] private float wakeBuildDistanceMultiplier = 1f;
    [Tooltip("Ngưỡng phát hiện rẽ gấp để reset wake. Cao hơn sẽ reset với góc rẽ nhỏ hơn; thấp hơn chỉ reset khi đổi hướng rất mạnh. 0.7 gần tương đương rẽ hơn khoảng 45 độ.")]
    [SerializeField, Range(-1f, 1f)] private float wakeDirectionResetDot = 0.7f;
    [Tooltip("Hệ số nhân độ rộng đầu wake lấy từ scale X/Z của source. Đầu wake tự rộng theo kích thước object; tăng nếu muốn đầu wake rộng hơn cho mọi object.")]
    [SerializeField, Min(0f)] private float wakeHeadWidthObjectScale = 1f;
    [Tooltip("Độ rộng đuôi wake tăng theo vận tốc source, tính bằng world unit trên mỗi unit/giây. Tăng để vật chạy nhanh mở đuôi wake rộng hơn; giảm để đuôi hẹp hơn.")]
    [SerializeField, Min(0f)] private float wakeTailWidthPerSpeed = 0.06f;
    [Tooltip("Độ mềm/mờ hai bên mép wake. Tăng để mép wake mờ rộng hơn; giảm để mép sắc hơn.")]
    [SerializeField, Range(0.01f, 1f)] private float wakeEdgeFeather = 0.22f;
    [Tooltip("Vị trí dọc wake bắt đầu mở rộng thành hình nón. Tăng để wake giữ dạng đường thẳng lâu hơn rồi mới mở; giảm để mở rộng sớm hơn.")]
    [SerializeField, Range(0f, 1f)] private float wakeConeStart = 0.12f;
    [Tooltip("Độ mạnh nhiễu loạn ngẫu nhiên trong wake. Tăng để cỏ trong vệt rung hỗn loạn hơn; giảm để wake mượt hơn.")]
    [SerializeField, Min(0f)] private float wakeTurbulenceStrength = 0.2f;

    [Header("Render Response")]
    [Tooltip("Hệ số nhân hiệu ứng cong khi render. Tăng để nhìn thấy cỏ nghiêng mạnh hơn mà không đổi mô phỏng; giảm để làm hiệu ứng cong nhẹ hơn.")]
    [SerializeField, Min(0f)] private float shaderBendScale = 1f;
    [Tooltip("Hệ số nhân hiệu ứng đè thấp khi render. Tăng để cỏ nhìn lún sâu hơn; giảm để vết đè nông hơn.")]
    [SerializeField, Min(0f)] private float shaderFlattenScale = 1f;

    [Header("Shaders")]
    [Tooltip("Compute shader chính dùng để stamp source, mô phỏng cong, vận tốc và đè thấp của cỏ. Chỉ đổi nếu thay shader hệ thống.")]
    [SerializeField] private ComputeShader simulationShader;
    [Tooltip("Shader stamp cũ, giữ lại để tương thích dữ liệu scene/prefab. Runtime hiện dùng compute shader để stamp chính xác theo world-space.")]
    [SerializeField] private Shader stampShader;

    [Header("Debug")]
    [Tooltip("Vẽ debug trong Scene view. Khung xanh là vùng Coverage; vòng đỏ/cam là source đang được xử lý; vòng xám là source ngoài vùng hoặc vượt Max Sources.")]
    [SerializeField] private bool drawDebugGizmos = true;
    [Tooltip("Chỉ vẽ debug khi chọn GameObject có GrassInteractionField. Bật nếu Scene view bị rối; tắt nếu muốn luôn thấy vùng field.")]
    [SerializeField] private bool drawDebugOnlyWhenSelected;
    [Tooltip("Độ cao nâng gizmo lên khỏi mặt đất để dễ nhìn. Tăng nếu đường debug bị cỏ/terrain che; giảm nếu muốn bám sát mặt đất hơn.")]
    [SerializeField] private float debugGizmoHeight = 0.05f;
    [Tooltip("Màu khung vùng Coverage trong Scene view.")]
    [SerializeField] private Color debugCoverageColor = new Color(0.1f, 0.85f, 1f, 0.8f);
    [Tooltip("Màu vòng debug của source đang nằm trong field và sẽ được stamp vào mô phỏng.")]
    [SerializeField] private Color debugInteractingColor = new Color(1f, 0.25f, 0.05f, 0.85f);
    [Tooltip("Màu đường/vòng wake phía sau source đang di chuyển nhanh.")]
    [SerializeField] private Color debugWakeColor = new Color(1f, 0.65f, 0.05f, 0.75f);
    [Tooltip("Màu source không được xử lý, thường do nằm ngoài Coverage hoặc vượt quá Max Sources.")]
    [SerializeField] private Color debugOutsideColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
    [Tooltip("Tô màu trực tiếp lên cỏ đang bị tương tác trong Game/Scene view. Bật để kiểm tra vùng ảnh hưởng thật; tắt khi không cần debug.")]
    [SerializeField] private bool tintInteractedGrass = true;
    [Tooltip("Màu dùng để tô cỏ đang bị tương tác. Chỉ ảnh hưởng debug tint, không đổi material gốc.")]
    [SerializeField] private Color interactedGrassTint = new Color(1f, 0.18f, 0.03f, 1f);
    [Tooltip("Độ đậm của màu tô debug. Tăng để vùng tương tác nổi bật hơn; giảm để màu pha nhẹ hơn với màu cỏ thật.")]
    [SerializeField, Range(0f, 4f)] private float interactedGrassTintStrength = 1.4f;
    [Tooltip("Độ nhạy tô màu dựa trên độ cong ngang. Tăng nếu cỏ bị cong nhẹ nhưng chưa hiện màu; giảm nếu vùng màu quá rộng do rung nhỏ.")]
    [SerializeField, Min(0f)] private float debugBendSensitivity = 2.5f;
    [Tooltip("Độ nhạy tô màu dựa trên độ đè thấp. Tăng nếu vết đè chưa hiện rõ; giảm nếu cỏ chỉ lún nhẹ cũng bị tô quá nhiều.")]
    [SerializeField, Min(0f)] private float debugFlattenSensitivity = 3.5f;

    private readonly List<GrassInteractionSource> visibleSources = new List<GrassInteractionSource>(128);

    private GpuSource[] sourceData = Array.Empty<GpuSource>();
    private ComputeBuffer sourceBuffer;
    private RenderTexture forceMap;
    private RenderTexture stateA;
    private RenderTexture stateB;
    private RenderTexture flattenA;
    private RenderTexture flattenB;
    private bool stateAIsCurrent = true;
    private float simulationAccumulator;
    private float recoveryTimer;
    private int simulateKernel = -1;
    private int recenterKernel = -1;
    private int stampKernel = -1;
    private Vector3 fieldCenter;
    private bool fieldCenterInitialized;

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuSource
    {
        public Vector4 PositionRadius;
        public Vector4 VelocityPush;
        public Vector4 Response;
        public Vector4 WakeShape;
    }

    private void Reset()
    {
#if UNITY_EDITOR
        simulationShader = FindDefaultComputeShader("CS_DynamicPlantInteraction");
        stampShader = Shader.Find("Hidden/Vit/DynamicPlantInteractionStamp");
#endif
    }

    private void OnEnable()
    {
        EnsureResources();
        fieldCenter = GetSnappedFieldCenter(GetFieldCenter());
        fieldCenterInitialized = true;
        UpdateShaderGlobals(false);
    }

    private void OnDisable()
    {
        ReleaseResources();
        ClearShaderGlobals();
    }

    private void OnValidate()
    {
        coverage = Mathf.Max(8f, coverage);
        resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 64, 1024);
        maxSources = Mathf.Clamp(maxSources, 1, 512);
        simulationFrequency = Mathf.Clamp(simulationFrequency, 1f, 60f);
        maxStepsPerFrame = Mathf.Clamp(maxStepsPerFrame, 1, 4);
        recoveryWakeSeconds = Mathf.Max(0f, recoveryWakeSeconds);
        forceStrength = Mathf.Max(0f, forceStrength);
        bendSpring = Mathf.Max(0f, bendSpring);
        bendDamping = Mathf.Max(0f, bendDamping);
        maxBend = Mathf.Max(0f, maxBend);
        flattenForceStrength = Mathf.Max(0f, flattenForceStrength);
        flattenSpring = Mathf.Max(0f, flattenSpring);
        flattenDamping = Mathf.Max(0f, flattenDamping);
        maxFlatten = Mathf.Max(0f, maxFlatten);
        turbulenceStrength = Mathf.Max(0f, turbulenceStrength);
        airWakeStrength = Mathf.Max(0f, airWakeStrength);
        wakeLengthPerSpeed = Mathf.Max(0f, wakeLengthPerSpeed);
        wakeBuildDistanceMultiplier = Mathf.Max(0f, wakeBuildDistanceMultiplier);
        wakeDirectionResetDot = Mathf.Clamp(wakeDirectionResetDot, -1f, 1f);
        wakeHeadWidthObjectScale = Mathf.Max(0f, wakeHeadWidthObjectScale);
        wakeTailWidthPerSpeed = Mathf.Max(0f, wakeTailWidthPerSpeed);
        wakeEdgeFeather = Mathf.Clamp(wakeEdgeFeather, 0.01f, 1f);
        wakeConeStart = Mathf.Clamp01(wakeConeStart);
        wakeTurbulenceStrength = Mathf.Max(0f, wakeTurbulenceStrength);
        shaderBendScale = Mathf.Max(0f, shaderBendScale);
        shaderFlattenScale = Mathf.Max(0f, shaderFlattenScale);
        speedToWake = Mathf.Max(0f, speedToWake);
        debugGizmoHeight = Mathf.Max(0f, debugGizmoHeight);
        debugBendSensitivity = Mathf.Max(0f, debugBendSensitivity);
        debugFlattenSensitivity = Mathf.Max(0f, debugFlattenSensitivity);

#if UNITY_EDITOR
        if (simulationShader == null)
        {
            simulationShader = FindDefaultComputeShader("CS_DynamicPlantInteraction");
        }

        if (stampShader == null)
        {
            stampShader = Shader.Find("Hidden/Vit/DynamicPlantInteractionStamp");
        }
#endif

        if (isActiveAndEnabled)
        {
            EnsureResources();
        }
    }

    private void LateUpdate()
    {
        bool canSimulate = Application.isPlaying || simulateInEditMode;
        if (!canSimulate || !EnsureResources())
        {
            UpdateShaderGlobals(false);
            return;
        }

        float frameDeltaTime = GetFrameDeltaTime();
        UpdateSourceWakeStates(frameDeltaTime);

        Vector3 nextFieldCenter = GetFieldCenter();
        RecenterFieldIfNeeded(nextFieldCenter);
        int sourceCount = CollectVisibleSources(fieldCenter);
        if (sourceCount > 0)
        {
            recoveryTimer = recoveryWakeSeconds;
        }
        else
        {
            recoveryTimer = Mathf.Max(0f, recoveryTimer - frameDeltaTime);
        }

        float stepDeltaTime = 1f / Mathf.Max(simulationFrequency, 1f);
        simulationAccumulator += frameDeltaTime;
        int steps = 0;

        while (simulationAccumulator >= stepDeltaTime && steps < maxStepsPerFrame)
        {
            bool shouldRunSimulation = sourceCount > 0 || recoveryTimer > 0f;
            SimulateStep(sourceCount, stepDeltaTime, shouldRunSimulation);
            simulationAccumulator -= stepDeltaTime;
            steps++;
        }

        UpdateShaderGlobals(true);
    }

    private bool EnsureResources()
    {
        if (simulationShader == null || !SystemInfo.supportsComputeShaders)
        {
            return false;
        }

        if (!simulationShader.HasKernel("Simulate") || !simulationShader.HasKernel("Recenter") || !simulationShader.HasKernel("StampSourceRegion"))
        {
            return false;
        }

        if (simulateKernel < 0)
        {
            simulateKernel = simulationShader.FindKernel("Simulate");
        }

        if (recenterKernel < 0)
        {
            recenterKernel = simulationShader.FindKernel("Recenter");
        }

        if (stampKernel < 0)
        {
            stampKernel = simulationShader.FindKernel("StampSourceRegion");
        }

        if (sourceData.Length != maxSources)
        {
            sourceData = new GpuSource[maxSources];
        }

        if (sourceBuffer == null || sourceBuffer.count != maxSources)
        {
            sourceBuffer?.Release();
            sourceBuffer = new ComputeBuffer(maxSources, SourceStride, ComputeBufferType.Structured);
        }

        GraphicsFormat vectorFormat = SelectFormat(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.R32G32B32A32_SFloat);
        GraphicsFormat flattenFormat = SelectFormat(GraphicsFormat.R16G16_SFloat, GraphicsFormat.R32G32_SFloat);

        forceMap = EnsureRenderTexture(forceMap, vectorFormat, "PlantInteractionForce");
        stateA = EnsureRenderTexture(stateA, vectorFormat, "PlantInteractionStateA");
        stateB = EnsureRenderTexture(stateB, vectorFormat, "PlantInteractionStateB");
        flattenA = EnsureRenderTexture(flattenA, flattenFormat, "PlantInteractionFlattenA");
        flattenB = EnsureRenderTexture(flattenB, flattenFormat, "PlantInteractionFlattenB");
        return true;
    }

    private RenderTexture EnsureRenderTexture(RenderTexture texture, GraphicsFormat format, string textureName)
    {
        if (texture != null &&
            texture.width == resolution &&
            texture.height == resolution &&
            texture.graphicsFormat == format)
        {
            return texture;
        }

        ReleaseTexture(ref texture);

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution)
        {
            graphicsFormat = format,
            depthBufferBits = 0,
            msaaSamples = 1,
            sRGB = false,
            useMipMap = false,
            autoGenerateMips = false,
            enableRandomWrite = true,
        };

        texture = new RenderTexture(descriptor)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.Create();
        ClearRenderTexture(texture);
        return texture;
    }

    private int CollectVisibleSources(Vector3 center)
    {
        visibleSources.Clear();
        float halfCoverage = coverage * 0.5f;

        foreach (GrassInteractionSource source in GrassInteractionSource.Sources)
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 sourcePosition = source.transform.position;
            float effectiveRadius = Mathf.Max(source.EffectiveRadius, source.Radius);
            if (Mathf.Abs(sourcePosition.x - center.x) > halfCoverage + effectiveRadius ||
                Mathf.Abs(sourcePosition.z - center.z) > halfCoverage + effectiveRadius)
            {
                continue;
            }

            visibleSources.Add(source);
            if (visibleSources.Count >= maxSources)
            {
                break;
            }
        }

        for (int i = 0; i < visibleSources.Count; i++)
        {
            GrassInteractionSource source = visibleSources[i];
            Vector3 position = source.transform.position;
            Vector3 velocity = source.Velocity;
            float planarSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float wakeHeadWidth = source.PlanarHalfScale * wakeHeadWidthObjectScale;
            float wakeTailWidth = Mathf.Max(wakeHeadWidth, wakeHeadWidth + planarSpeed * wakeTailWidthPerSpeed);
            sourceData[i] = new GpuSource
            {
                PositionRadius = new Vector4(position.x, position.y, position.z, source.Radius),
                VelocityPush = new Vector4(velocity.x, velocity.y, velocity.z, source.PushStrength),
                Response = new Vector4(source.FlattenStrength, airWakeStrength, source.CurrentWakeLength, wakeTurbulenceStrength),
                WakeShape = new Vector4(wakeHeadWidth, wakeTailWidth, wakeEdgeFeather, wakeConeStart),
            };
        }

        return visibleSources.Count;
    }

    private void UpdateSourceWakeStates(float deltaTime)
    {
        foreach (GrassInteractionSource source in GrassInteractionSource.Sources)
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            source.UpdateWakeState(deltaTime, wakeLengthPerSpeed, wakeBuildDistanceMultiplier, wakeDirectionResetDot);
        }
    }

    private void SimulateStep(int sourceCount, float deltaTime, bool shouldRunSimulation)
    {
        if (!shouldRunSimulation && sourceCount <= 0)
        {
            return;
        }

        ClearRenderTexture(forceMap);
        if (sourceCount > 0)
        {
            StampSources(sourceCount);
        }

        if (!shouldRunSimulation)
        {
            return;
        }

        RenderTexture stateRead = stateAIsCurrent ? stateA : stateB;
        RenderTexture stateWrite = stateAIsCurrent ? stateB : stateA;
        RenderTexture flattenRead = stateAIsCurrent ? flattenA : flattenB;
        RenderTexture flattenWrite = stateAIsCurrent ? flattenB : flattenA;

        simulationShader.SetTexture(simulateKernel, ForceMapId, forceMap);
        simulationShader.SetTexture(simulateKernel, StateReadId, stateRead);
        simulationShader.SetTexture(simulateKernel, StateWriteId, stateWrite);
        simulationShader.SetTexture(simulateKernel, FlattenReadId, flattenRead);
        simulationShader.SetTexture(simulateKernel, FlattenWriteId, flattenWrite);
        simulationShader.SetVector(SimulationParamsId, new Vector4(deltaTime, bendSpring, bendDamping, forceStrength));
        simulationShader.SetVector(LimitParamsId, new Vector4(maxBend, flattenForceStrength, flattenSpring, flattenDamping));
        simulationShader.SetVector(TurbulenceParamsId, new Vector4(maxFlatten, turbulenceStrength, Time.time, 0f));

        int groups = Mathf.CeilToInt(resolution / (float)ThreadGroupSize);
        simulationShader.Dispatch(simulateKernel, groups, groups, 1);
        stateAIsCurrent = !stateAIsCurrent;
    }

    private void RecenterFieldIfNeeded(Vector3 nextFieldCenter)
    {
        nextFieldCenter = GetSnappedFieldCenter(nextFieldCenter);

        if (!fieldCenterInitialized)
        {
            fieldCenter = nextFieldCenter;
            fieldCenterInitialized = true;
            return;
        }

        Vector2 centerDelta = new Vector2(nextFieldCenter.x - fieldCenter.x, nextFieldCenter.z - fieldCenter.z);
        if (centerDelta.sqrMagnitude <= 0.00000001f)
        {
            fieldCenter = nextFieldCenter;
            return;
        }

        RenderTexture stateRead = stateAIsCurrent ? stateA : stateB;
        RenderTexture stateWrite = stateAIsCurrent ? stateB : stateA;
        RenderTexture flattenRead = stateAIsCurrent ? flattenA : flattenB;
        RenderTexture flattenWrite = stateAIsCurrent ? flattenB : flattenA;
        if (stateRead == null || stateWrite == null || flattenRead == null || flattenWrite == null)
        {
            fieldCenter = nextFieldCenter;
            return;
        }

        simulationShader.SetTexture(recenterKernel, StateReadId, stateRead);
        simulationShader.SetTexture(recenterKernel, StateWriteId, stateWrite);
        simulationShader.SetTexture(recenterKernel, FlattenReadId, flattenRead);
        simulationShader.SetTexture(recenterKernel, FlattenWriteId, flattenWrite);
        simulationShader.SetVector(RecenterParamsId, new Vector4(centerDelta.x / coverage, centerDelta.y / coverage, 0f, 0f));

        int groups = Mathf.CeilToInt(resolution / (float)ThreadGroupSize);
        simulationShader.Dispatch(recenterKernel, groups, groups, 1);
        stateAIsCurrent = !stateAIsCurrent;
        fieldCenter = nextFieldCenter;
    }

    private void StampSources(int sourceCount)
    {
        sourceBuffer.SetData(sourceData, 0, 0, sourceCount);
        simulationShader.SetBuffer(stampKernel, StampSourcesId, sourceBuffer);
        simulationShader.SetTexture(stampKernel, ForceMapWriteId, forceMap);
        simulationShader.SetVector(FieldParamsId, new Vector4(fieldCenter.x, fieldCenter.z, coverage, 1f));
        simulationShader.SetVector(StampParamsId, new Vector4(speedToWake, sourceCount, 0f, 0f));

        for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
        {
            if (!TryGetStampPixelBounds(sourceData[sourceIndex], out Vector2Int minPixel, out Vector2Int maxPixel))
            {
                continue;
            }

            simulationShader.SetVector(StampRegionParamsId, new Vector4(sourceIndex, minPixel.x, minPixel.y, 0f));
            simulationShader.SetVector(StampRegionMaxParamsId, new Vector4(maxPixel.x, maxPixel.y, 0f, 0f));

            int dispatchWidth = maxPixel.x - minPixel.x;
            int dispatchHeight = maxPixel.y - minPixel.y;
            int groupsX = Mathf.CeilToInt(dispatchWidth / (float)ThreadGroupSize);
            int groupsY = Mathf.CeilToInt(dispatchHeight / (float)ThreadGroupSize);
            simulationShader.Dispatch(stampKernel, groupsX, groupsY, 1);
        }
    }

    private bool TryGetStampPixelBounds(GpuSource source, out Vector2Int minPixel, out Vector2Int maxPixel)
    {
        Vector2 sourceXZ = new Vector2(source.PositionRadius.x, source.PositionRadius.z);
        Vector2 velocityXZ = new Vector2(source.VelocityPush.x, source.VelocityPush.z);
        float speed = velocityXZ.magnitude;
        Vector2 direction = speed > 0.0001f ? velocityXZ / speed : Vector2.up;
        Vector2 sideDirection = new Vector2(-direction.y, direction.x);

        float radius = Mathf.Max(source.PositionRadius.w, 0.001f);
        float wakeLength = speed > 0.05f && source.Response.y > 0f ? Mathf.Max(source.Response.z, 0f) : 0f;
        float footprintAlong = radius + wakeLength * 0.5f;
        float footprintSide = Mathf.Max(radius, Mathf.Max(source.WakeShape.x, source.WakeShape.y));
        Vector2 footprintCenter = sourceXZ - direction * (wakeLength * 0.5f);

        Vector2 a = footprintCenter - sideDirection * footprintSide - direction * footprintAlong;
        Vector2 b = footprintCenter + sideDirection * footprintSide - direction * footprintAlong;
        Vector2 c = footprintCenter - sideDirection * footprintSide + direction * footprintAlong;
        Vector2 d = footprintCenter + sideDirection * footprintSide + direction * footprintAlong;

        Vector2 minWorld = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        Vector2 maxWorld = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));

        Vector2 fieldCenterXZ = new Vector2(fieldCenter.x, fieldCenter.z);
        Vector2 minUv = ((minWorld - fieldCenterXZ) / coverage) + Vector2.one * 0.5f;
        Vector2 maxUv = ((maxWorld - fieldCenterXZ) / coverage) + Vector2.one * 0.5f;

        int minX = Mathf.Clamp(Mathf.FloorToInt(minUv.x * resolution) - 1, 0, resolution);
        int minY = Mathf.Clamp(Mathf.FloorToInt(minUv.y * resolution) - 1, 0, resolution);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(maxUv.x * resolution) + 1, 0, resolution);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(maxUv.y * resolution) + 1, 0, resolution);

        minPixel = new Vector2Int(minX, minY);
        maxPixel = new Vector2Int(maxX, maxY);
        return maxX > minX && maxY > minY;
    }

    private Vector3 GetFieldCenter()
    {
        if (followTarget != null)
        {
            return followTarget.position + worldOffset;
        }

        if (useMainCameraFallback && Camera.main != null)
        {
            return Camera.main.transform.position + worldOffset;
        }

        return transform.position + worldOffset;
    }

    private Vector3 GetSnappedFieldCenter(Vector3 center)
    {
        float texelSize = coverage / Mathf.Max(resolution, 1);
        if (texelSize <= 0f)
        {
            return center;
        }

        center.x = Mathf.Round(center.x / texelSize) * texelSize;
        center.z = Mathf.Round(center.z / texelSize) * texelSize;
        return center;
    }

    private void OnDrawGizmos()
    {
        if (drawDebugOnlyWhenSelected)
        {
            return;
        }

        DrawDebugGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugOnlyWhenSelected)
        {
            return;
        }

        DrawDebugGizmos(true);
    }

    private void DrawDebugGizmos(bool selected)
    {
        if (!drawDebugGizmos || (drawDebugOnlyWhenSelected && !selected))
        {
            return;
        }

        Vector3 debugCenter = fieldCenterInitialized ? fieldCenter : GetFieldCenter();
        debugCenter.y += debugGizmoHeight;
        DrawCoverageGizmo(debugCenter);
        DrawSourceGizmos(debugCenter);
    }

    private void DrawCoverageGizmo(Vector3 center)
    {
        float halfCoverage = coverage * 0.5f;
        Vector3 a = center + new Vector3(-halfCoverage, 0f, -halfCoverage);
        Vector3 b = center + new Vector3(halfCoverage, 0f, -halfCoverage);
        Vector3 c = center + new Vector3(halfCoverage, 0f, halfCoverage);
        Vector3 d = center + new Vector3(-halfCoverage, 0f, halfCoverage);

        Gizmos.color = debugCoverageColor;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(center + Vector3.left, center + Vector3.right);
        Gizmos.DrawLine(center + Vector3.back, center + Vector3.forward);
    }

    private void DrawSourceGizmos(Vector3 debugCenter)
    {
        float halfCoverage = coverage * 0.5f;
        IEnumerable<GrassInteractionSource> sources = GrassInteractionSource.Sources;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            sources = UnityEngine.Object.FindObjectsByType<GrassInteractionSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }
#endif

        int stampedSourceCount = 0;
        foreach (GrassInteractionSource source in sources)
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 sourcePosition = source.transform.position;
            float effectiveRadius = Mathf.Max(source.EffectiveRadius, source.Radius);
            bool insideField = Mathf.Abs(sourcePosition.x - debugCenter.x) <= halfCoverage + effectiveRadius &&
                Mathf.Abs(sourcePosition.z - debugCenter.z) <= halfCoverage + effectiveRadius;
            bool willBeStamped = insideField && stampedSourceCount < maxSources;
            if (insideField)
            {
                stampedSourceCount++;
            }

            Vector3 drawPosition = new Vector3(sourcePosition.x, debugCenter.y, sourcePosition.z);
            Gizmos.color = willBeStamped ? debugInteractingColor : debugOutsideColor;
            DrawCircle(drawPosition, source.Radius, 48);

            if (!willBeStamped)
            {
                DrawCircle(drawPosition, effectiveRadius, 48);
                continue;
            }

            Vector3 planarVelocity = new Vector3(source.Velocity.x, 0f, source.Velocity.z);
            if (planarVelocity.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Vector3 direction = planarVelocity.normalized;
            float wakeLength = Mathf.Max(0f, effectiveRadius - source.Radius);
            Vector3 wakeEnd = drawPosition - direction * wakeLength;

            Gizmos.color = debugWakeColor;
            Gizmos.DrawLine(drawPosition, wakeEnd);
            DrawCircle(wakeEnd, Mathf.Max(source.Radius * 0.5f, 0.01f), 32);
            DrawCircle(drawPosition, effectiveRadius, 48);
        }
    }

    private static void DrawCircle(Vector3 center, float circleRadius, int segments)
    {
        if (circleRadius <= 0f || segments < 3)
        {
            return;
        }

        Vector3 previous = center + new Vector3(circleRadius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private void UpdateShaderGlobals(bool enabled)
    {
        RenderTexture currentState = stateAIsCurrent ? stateA : stateB;
        RenderTexture currentFlatten = stateAIsCurrent ? flattenA : flattenB;
        Shader.SetGlobalTexture(StateMapId, currentState != null ? currentState : Texture2D.blackTexture);
        Shader.SetGlobalTexture(FlattenMapId, currentFlatten != null ? currentFlatten : Texture2D.blackTexture);
        Shader.SetGlobalVector(FieldParamsId, new Vector4(fieldCenter.x, fieldCenter.z, coverage, enabled ? 1f : 0f));
        Shader.SetGlobalVector(RenderParamsId, new Vector4(shaderBendScale, shaderFlattenScale, 0f, 0f));
        Shader.SetGlobalColor(DebugColorId, interactedGrassTint);
        Shader.SetGlobalVector(
            DebugParamsId,
            new Vector4(enabled && tintInteractedGrass ? 1f : 0f, interactedGrassTintStrength, debugBendSensitivity, debugFlattenSensitivity));
    }

    private static void ClearShaderGlobals()
    {
        Shader.SetGlobalTexture(StateMapId, Texture2D.blackTexture);
        Shader.SetGlobalTexture(FlattenMapId, Texture2D.blackTexture);
        Shader.SetGlobalVector(FieldParamsId, Vector4.zero);
        Shader.SetGlobalVector(RenderParamsId, Vector4.zero);
        Shader.SetGlobalColor(DebugColorId, Color.clear);
        Shader.SetGlobalVector(DebugParamsId, Vector4.zero);
    }

    private float GetFrameDeltaTime()
    {
        if (Application.isPlaying)
        {
            return Mathf.Max(Time.deltaTime, 0.0001f);
        }

        return 1f / Mathf.Max(simulationFrequency, 1f);
    }

    private static GraphicsFormat SelectFormat(GraphicsFormat preferred, GraphicsFormat fallback)
    {
        if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render) &&
            SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.LoadStore))
        {
            return preferred;
        }

        return fallback;
    }

    private static void ClearRenderTexture(RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }

    private void ReleaseResources()
    {
        sourceBuffer?.Release();
        sourceBuffer = null;

        ReleaseTexture(ref forceMap);
        ReleaseTexture(ref stateA);
        ReleaseTexture(ref stateB);
        ReleaseTexture(ref flattenA);
        ReleaseTexture(ref flattenB);
        simulateKernel = -1;
        recenterKernel = -1;
        stampKernel = -1;
        fieldCenterInitialized = false;
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        DestroyImmediateSafe(texture);
        texture = null;
    }

    private static void DestroyImmediateSafe(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

#if UNITY_EDITOR
    private static ComputeShader FindDefaultComputeShader(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:ComputeShader");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), assetName, StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            }
        }

        return null;
    }
#endif
}
