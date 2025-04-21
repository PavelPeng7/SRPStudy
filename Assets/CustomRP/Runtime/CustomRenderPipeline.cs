using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// asset中需要返回的RP instance，所以继承自RenderPipeline
public partial class CustomRenderPipeline : RenderPipeline
{
    private bool allowHDR;
    private CameraRenderer renderer = new CameraRenderer();
    // 通过字段跟踪合批策略配置
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    ShadowSettings shadowSettings;
    
    // Render方法是抽象的，所以需要实现
    // Render方法是为自定义SRPs定义的入口点
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) 
    {
        
    }

    // Unity2022后使用List
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras) {
        for (int i = 0; i < cameras.Count; i++) {
            renderer.Render(context, cameras[i], allowHDR,useDynamicBatching, useGPUInstancing, useLightsPerObject, shadowSettings, postFXSettings, colorLUTResolution);
        }
    }

    PostFXSettings postFXSettings;
    private int colorLUTResolution;
    
    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution) {
        this.colorLUTResolution = colorLUTResolution;
        // 通过构造函数传递配置
        this.allowHDR = allowHDR;
        this.postFXSettings = postFXSettings;
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        // unity默认的光照强度是gamma space的，我们需要将其转换为linear space
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }
    
}
