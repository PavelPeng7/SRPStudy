using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer : MonoBehaviour
{
    private ScriptableRenderContext context;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    Camera camera;
    CullingResults cullingResults;
    
    static ShaderTagId 
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();
    

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings) {
        this.context = context;
        this.camera = camera;
        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance, camera)) {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();
        Submit();
    }

    void Setup() {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color, flags == CameraClearFlags.Color ?camera.backgroundColor.linear : Color.clear);
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
    
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing) {
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        
        context.DrawSkybox(camera);
        
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        
        
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings
            );
    }
    

    bool Cull(float maxShadowDistance, Camera camera) {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
