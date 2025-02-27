using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// asset中需要返回的RP instance，所以继承自RenderPipeline
public class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching, useGPUInstancing;
    ShadowSettings shadowSettings;
    
    // Render方法是抽象的，所以需要实现
    // Render方法是为自定义SRPs定义的入口点
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) 
    {
        
    }
    // 2022版本中的新方法
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras) {
        for (int i = 0; i < cameras.Count; i++) {
            renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing, shadowSettings);
        }
    }

    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings) {
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        
        GraphicsSettings.useScriptableRenderPipelineBatching = false;
        GraphicsSettings.lightsUseLinearIntensity = true;
    }
    
}
