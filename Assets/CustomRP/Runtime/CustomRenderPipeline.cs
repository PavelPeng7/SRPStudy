using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// asset返回的Type继承自RenderPipeline也就是RP实例
public class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();
    
    // 重载抽象类方法Render创建一个管线
    
    // 为了兼容Profiler Screenshots使用旧的相机数组
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) 
    {
        
    }

    // Unity2022后使用List
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras) {
        for (int i = 0; i < cameras.Count; i++) {
            renderer.Render(context, cameras[i]);
        }
    }
}
