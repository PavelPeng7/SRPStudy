using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 将项目属性标记为asset，这样可以加入Asset/Create菜单，后面就是菜单路径了
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline Assets")]
// RP asset的目标是给Unity指向其需要控制的负责渲染的管线对象实例
public class CustomRenderPipelineAssets : RenderPipelineAsset
{
    // 覆写CreatePipeline方法，并且属性是protected保证只有RenderPipelineAsset及其子类可以调用它
    protected override RenderPipeline CreatePipeline() {
        // 返回一个合法可用的pipeline
        return new CustomRenderPipeline();
    }
}
