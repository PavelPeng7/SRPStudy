using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering;

partial class CameraRenderer 
{
    partial void DrawUnsupportedShaders();
    partial void DrawGizmos();
    partial void PrepareForSceneWindow();
    partial void PrepareBuffer();
    
    // 再编辑器环境下自动添加的代码
#if UNITY_EDITOR
    static Material errorMaterial;
    string SampleName { get; set; }

    // 用于绘制不支持的shader
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    
    // 绘制Gizmos
    partial void DrawGizmos () {
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
    }
    
    // 绘制不支持的shader
    partial void DrawUnsupportedShaders() {
        // 如果errorMaterial为空，就创建一个
        if (errorMaterial == null) {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        
        // 创建一个DrawingSettings对象，用于绘制不支持的shader
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera)) {
            overrideMaterial = errorMaterial
        };
        
        // 为每个shader tag id设置shader pass name
        for (int i = 0; i < legacyShaderTagIds.Length; i++) {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }

        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
    
    // 在Scene视图中绘制UI
    partial void PrepareForSceneWindow() {
        if (camera.cameraType == CameraType.SceneView) {
            // Emits UI geometry into the Scene view for rendering.
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }
    
    // buffer的名字等于相机的名字防止profile中混合
    partial void PrepareBuffer() {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }
#else
        // 在非编辑器环境下，SampleName就是bufferName，防止多次分配字符串实例
        const string SampleName => bufferName;
#endif
}