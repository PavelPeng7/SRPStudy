#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
     #define DIRECTIONAL_FILTER_SAMPLES 4
     #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
     #define DIRECTIONAL_FILTER_SAMPLES 9
     #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
     #define DIRECTIONAL_FILTER_SAMPLES 16
     #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
// 使用对比采样器
#define SHADOW_SAMPLER sampler_linear_clamp_compare
// 定义采样器状态
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    // float _ShadowDistance;
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
    // 级联阴影剔除球体
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END


float FadedShadowStrength(float distance, float scale, float fade)
{
    // fade = (1 - d/m)/f
    return saturate((1.0 - distance * scale) * fade);
}

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
};

// 定义ShadowMask是否符合使用距离，以及其阴影值
struct ShadowMask
{
    bool distance;
    float4 shadows;
};

// 级联阴影id逐像素变化而不是逐光源
struct ShadowData
{
    int cascadeIndex;
    float cascadeBlend;
    float strength;
    ShadowMask shadowMask;
};

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    // data.strength = surfaceWS.depth < _ShadowDistance ? 1.0 : 0.0;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    
    data.cascadeBlend = 1.0;
    // 阴影渐消
    data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        // 距离小于半径
        if (distanceSqr < sphere.w)
        {
            // _CascadeData[i].x : 1 / cullingSphere.w
            // _ShadowDistanceFade.z : 1 / (1 - f * f) 改变f值使其逼近线性衰减
            float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            if (i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
            else
            {
                // 赋值级联混合因子
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    
    // 超出级联范围,强度为0
    if (i == _CascadeCount)
    {
        data.strength = 0.0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
        // 通过dither减少阴影混合的锯齿
        // 阴影同一级联中从强到弱，如果混合系数小于dither值则取下一个级联
        else if (data.cascadeBlend < surfaceWS.dither)
        {
            i += 1;
        }
    #endif
    
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
        data.cascadeIndex = i;
    return data;
}


// 阴影采样函数
float SampleDirectionalShadowAtlas(float3 positionSTS) {
    return SAMPLE_TEXTURE2D_SHADOW(
            _DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
        );
}

float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        // _ShadowAtlasSize: x:atlasSize y:1/atlasSize
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0.0;
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
        {
            shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
        }
        return shadow;
    #else
        return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

// 计算阴影衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{ 
    // 不接收阴影
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    
    // 分支条件统一，不会发生线程分散
    if (directional.strength <= 0.0)
    {
        return 1.0;
    }
    // 通过对采样shadowmap的顶点沿法线偏移消除自阴影
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    // world space to shadow tile space
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);
    if (global.cascadeBlend < 1.0)
    {
        // 采样到下一个级联阴影的normalBias，投影矩阵
        normalBias = surfaceWS.normal * (directional.normalBias *  _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(
            _DirectionalShadowMatrices[directional.tileIndex + 1],
            float4(surfaceWS.position + normalBias, 1.0)).xyz;
        // 混合级联阴影
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    // 针对阴影强度进行插值，用处半透明阴影
    return lerp(1.0, shadow, directional.strength);
}

#endif
