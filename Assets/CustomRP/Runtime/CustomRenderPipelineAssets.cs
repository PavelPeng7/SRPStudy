using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 将项目属性标记为asset，这样可以加入Asset/Create菜单，后面就是菜单路径了
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline Assets")]
// RP asset的目标是给Unity提供其需要控制的负责渲染的管线对象实例的方法，其本身存储一些句柄和管线设置
public partial class CustomRenderPipelineAssets : RenderPipelineAsset
{
    // SRP batch和GPU Instance需要shader中的支持，这里可以选择是否启用
    [SerializeField]
    private bool allowHDR = true;
    
    [SerializeField]
    bool useDynamicBatching = true,
        useGPUInstancing = true,
        useSRPBatcher = true,
        useLightPerObject = true;

    [SerializeField]
    ShadowSettings shadows = default;

    [SerializeField]
    PostFXSettings postFXSettings = default;
    
    public enum ColorLutResolution{ _16 = 16, _32 = 32, _64 = 64}

    [SerializeField]
    private ColorLutResolution colorLUTResolution = ColorLutResolution._32;

    
    // 覆写CreatePipeline方法，并且属性是protected保证只有RenderPipelineAsset及其子类可以调用它
    protected override RenderPipeline CreatePipeline() {
        // 返回一个合法可用的pipeline
        return new CustomRenderPipeline(allowHDR, useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightPerObject, shadows, postFXSettings, (int)colorLUTResolution);
    }
}
