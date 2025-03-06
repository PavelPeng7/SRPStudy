// 避免被重复编译
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
// OpenGL ES 2.0不支持Cbuffer所以我们得换种写法
// cbuffer UnityPerMaterial
// {
//     float4 _BaseColor;
// };
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// 相比于uniform用常量缓冲区将变量隔离
// 可以实现将材质属性缓存到GPU上不用每次drawcall时都传递
// 每一次drawcall包含一个正确的内存地址的偏移量
// SrpBatch条件：
// 不支持OpenGL ES 2.0
// 保证Shader中每个材质的内存布局一致，使用同一种变体

// CBUFFER_START(UnityPerMaterial)
    // float4x4 unity_ObjectToWorld;
    // float4x4 unity_WorldToObject;
    // float4 unity_LODFade;
    // real4 unity_WorldTransformParams;
// CBUFFER_END

// GPUInstance对batch的大小有限制取决于目标平台提供给每个GpuInstance的数据大小
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    // 下划线开头是材质属性的命名规范
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

// 为了易读性将输入的数据定义为结构体
struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    // 顶点信息中需提供实例ID
    UNITY_VERTEX_INPUT_INSTANCE_ID
    
};

// 顶点着色器的输出结构体，它包含在同一个三角形中不同(vary)的片元信息
// 以及传递到片元着色器中的实例ID的
struct Varings
{
    float4 positionCS : SV_POSITION;
    // VAR_BASE_UV没有指向任何特殊的数据在GPU中，只是一个自定义的语义
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varings UnlitPassVertex(Attributes input)
{
    Varings output;
    // 从input中提取实例ID并存储在一个全局静态变量中一共其他实例宏使用
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    // 为什么positionWS是float3而不是float4？
    // 因为float4的w分量是1.0，区分向量和点，而我们不需要这个分量，可以减少运算
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    output.baseUV = input.baseUV * baseST.xy + baseST.zw;
    return output;
}

// SV_TARGET是渲染目标的语义
float4 UnlitPassFragment(Varings input):SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 base = baseMap * baseColor;
    #if defined(_CLIPPING)
        clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif
    
    clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    return base;
}
#endif
