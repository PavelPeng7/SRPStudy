using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 在管线中负责将光照数据传递给GPU
public class Lighting
{
    const string bufferName = "Lighting";
    
    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    private static int
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
    
    // 为什么不是用结构化缓冲区传递光照数据？
    // 结构化缓冲要么在shader中不支持，要么仅在片元着色器，要么性能差
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    private static int
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");

    private static Vector4[]
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount];

    private CommandBuffer buffer = new CommandBuffer() {
        name = bufferName
    };

    private CullingResults cullingResults;
    
    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings) {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        // 在设置光照前设置阴影，在设置光照时将阴影数据传递给GPU
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights();
        // 在设置好光照后渲染阴影
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights() {
        // 获取可见光源
        // NativeArray是什么？
        // NativeArray是Unity的一种数据结构，它是一种高效的数组，可以在C#代码中直接访问Unity的内存，而不需要通过GC分配内存
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++) {
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType) {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount) {
                        SetupDirectionalLight(dirLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount) {
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount) {
                        SetupSpotLight(otherLightCount++, ref visibleLight);
                    }
                    break;
            }
        }
        
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0) {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        }
        
        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0) {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
        }
        

        // 设置阴影数据
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }
    
    void SetupDirectionalLight(int index, ref VisibleLight visibleLight) {
        dirLightColors[index] = visibleLight.finalColor;
        // 取z轴负方向作为光源方向
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight) {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
    }
    
    
    void SetupSpotLight(int index, ref VisibleLight visibleLight) {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
    }
    
    public void Cleanup() {
        // 将调用转发给shadows
        shadows.Cleanup();
    }
}
