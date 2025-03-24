// 避免被重复编译
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

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
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

// SV_TARGET是渲染目标的语义
float4 UnlitPassFragment(Varings input):SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 base = GetBase(input.baseUV);
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(input.baseUV));
    #endif
    
    clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    return base;
}
#endif
