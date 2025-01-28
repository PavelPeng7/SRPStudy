using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// asset中需要返回的RP instance
public class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching, useGPUInstancing;
    ShadowSettings shadowSettings;
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) 
    {
        
    }

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
