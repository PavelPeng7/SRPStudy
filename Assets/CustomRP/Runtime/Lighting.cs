using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

// 在管线中负责将光照数据传递给GPU
public class Lighting
{
    const string bufferName = "Lighting";
    
    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    private static int
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
    
    // 为什么不是用结构化缓冲区传递光照数据？
    // 结构化缓冲要么在shader中不支持，要么仅在片元着色器，要么性能差
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirectionsAndMasks = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    private static int
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirectionsAndMasks"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    private static Vector4[]
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];

    private static string lightPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    private CommandBuffer buffer = new CommandBuffer() {
        name = bufferName
    };

    private CullingResults cullingResults;
    
    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask) {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        // 在设置光照前设置阴影，在设置光照时将阴影数据传递给GPU
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights(useLightsPerObject, renderingLayerMask);
        // 在设置好光照后渲染阴影
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights(bool useLightsPerObject, int renderingLayerMask) {
        // 获取可见光源
        // NativeArray是什么？
        // NativeArray是Unity的一种数据结构，它是一种高效的数组，可以在C#代码中直接访问Unity的内存，而不需要通过GC分配内存
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++) {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;
            if ((light.renderingLayerMask & renderingLayerMask) != 0) {
                switch (visibleLight.lightType) {
                    case LightType.Directional:
                        if (dirLightCount < maxDirLightCount) {
                            SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
                        }
                        break;
                    case LightType.Point:
                        if (otherLightCount < maxOtherLightCount) {
                            newIndex = otherLightCount;
                            SetupPointLight(otherLightCount++, i, ref visibleLight, light);
                        }
                        break;
                    case LightType.Spot:
                        if (otherLightCount < maxOtherLightCount) {
                            newIndex = otherLightCount;
                            SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
                        }
                        break;
                }
            }
            if (useLightsPerObject) {
                indexMap[i] = newIndex;
            }
        }

        if (useLightsPerObject) {
            for (; i < indexMap.Length; i++) {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightPerObjectKeyword);
        }
        else {
            Shader.DisableKeyword(lightPerObjectKeyword);
        }
        
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0) {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirectionsAndMasks);
        }
        
        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0) {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirectionsAndMasks);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }
        
        // 设置阴影数据
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }
    
    void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light) {
        dirLightColors[index] = visibleLight.finalColor;
        // 取z轴负方向作为光源方向
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        dirLightDirectionsAndMasks[index] = dirAndMask;
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(light, visibleIndex);
    }

    void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light) {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f);
  
        Vector4 dirAndMask = Vector4.zero;
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirectionsAndMasks[index] = dirAndMask;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }
    
    
    void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light) {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirectionsAndMasks[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirectionsAndMasks[index] = dirAndMask;
        
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }
    
    public void Cleanup() {
        // 将调用转发给shadows
        shadows.Cleanup();
    }
}
