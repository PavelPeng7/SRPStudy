using UnityEngine;
using UnityEngine.Rendering;

// 每一帧unity都会在RP实例上调用Render
// 这个方法传递着一个提供一个上下文结构体这个结构体提供与本地引擎的链接。能被用来渲染
// 这个方法也会传递一个相机数组，这个数组包含了所有需要渲染的相机
// RP需要为每一个相机按照提供的顺序渲染这些相机

// CameraRender负责单个相机的渲染
// 我们的相机渲染器大致相当于URP的Scriptable Renderers，这种方法使得支持不同的相机渲染方式成为可能在未来
// 例如：第一人称中的3d地图，forward vs. deferred渲染

public partial class CameraRenderer
{
    private static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    // 通过配置，加入命令到context直接执行渲染命令，如：绘制Skybox
    private ScriptableRenderContext context;
    
    // 通过Command buffer收集GPU指令，发送给context执行
    // 给buffer命名方便在Profiler中查看
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer {
        // 直接在 new 表达式后追加代码块 的方式，一次性初始化对象的字段（fields）或属性（properties），
        // 无需在构造函数参数中传递所有值，也无需在实例化后单独为每个属性赋值。
        name = bufferName
    };
    
    Camera camera;
    CullingResults cullingResults;

    private bool useHDR;
    
    // 允许渲染的RenderPass
    static ShaderTagId 
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    // lighting实例，用于处理光照
    Lighting lighting = new Lighting();
    private PostFXStack postFXStack = new PostFXStack();

    private static CameraSettings defaultCameraSettings = new CameraSettings();

    // 依赖RP提供合批策略配置
    public void Render(ScriptableRenderContext context, Camera camera, bool allowHDR,bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution) {
        this.context = context;
        this.camera = camera;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

        if (cameraSettings.overridePostFX) {
            postFXSettings = cameraSettings.PostFXSettings;
        }
        
        PrepareBuffer();
        // 为了在Scene视图中绘制UI，我们需要调用PrepareForSceneWindow
        PrepareForSceneWindow();
        // 如果相机不可见，不渲染
        if (!Cull(shadowSettings.maxDistance, camera)) {
            return;
        }
        useHDR = allowHDR && camera.allowHDR;
        // 插入开始和结束的profiler samples，参数使用当前相机的名字
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        // 在绘制可见几何体之前，设置光照，阴影
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject, cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
        postFXStack.Setup(context, camera, postFXSettings, useHDR, colorLUTResolution, cameraSettings.finalBlendMode);
        buffer.EndSample(SampleName);
        Setup();
        // 绘制相机能看到的所有几何体
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraSettings.renderingLayerMask);
        DrawUnsupportedShaders();
        // 最后绘制Gizmos
        
        DrawGizmosBeforeFX();
        if (postFXStack.IsActive) {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();
        Cleanup();
        // 提交context中缓冲的渲染命令
        Submit();
    }

    void Setup() {
        // 设置相机属性,相机的位置和旋转
        context.SetupCameraProperties(camera);
        // 控制两个相机合并的方式
        CameraClearFlags flags = camera.clearFlags;

        if (postFXStack.IsActive) {
            if (flags > CameraClearFlags.Color) {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
            buffer.SetRenderTarget(
                frameBufferId, RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store
                );
        }
        // 在绘制新渲染之前清除旧的渲染屏幕
        // 清屏在设置好相机之后效率更高
        // flags枚举顺序：Skybox, Color, Depth, Nothing
        buffer.ClearRenderTarget(
            // 仅清Depth中除深度缓冲
            flags <= CameraClearFlags.Depth, 
            // 仅清除Skybox，Color中颜色缓冲  
            flags <= CameraClearFlags.Color,
            // 清除颜色缓冲时，使用相机的背景颜色，其他使用透明色
                     flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Submit() {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }
    
    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    // 输入合批策略配置
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, int renderingLayerMask) {
        PerObjectData lightPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        
        // 决定使用正交或基于距离的排序
        // 正交排序有啥用？
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        
        // 设置不透明绘制设置
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) {
            // 是否启用GPU Instance和Dynamic Batching
            // 动态合批会影响单位法线和绘制顺序
            // SRP Batch是优先级最高的合批
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            // 设置绘制对象的光照贴图属性，以便unity发送light map uv到shader中
            perObjectData = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.ShadowMask |PerObjectData.LightProbe | PerObjectData.OcclusionProbe |PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume | lightPerObjectFlags
        };
        // LitPass加入到需要被渲染的Passes中
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        
        // 绘制天空盒
        context.DrawSkybox(camera);
        
        // 设置透明绘制设置
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
    

    bool Cull(float maxShadowDistance, Camera camera) {
        // 获取相机的culling参数
        // out强制输出参数，不需要初始化
        // 如果成功，设置阴影距离为相机的farClipPlane和maxShadowDistance的最小值
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            // 获取CullingResults
            // ref引用参数，需要初始化。大结构体传参优化
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup() {
        lighting.Cleanup();
        if (postFXStack.IsActive) {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }
    
}
