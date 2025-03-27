#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varings ShadowCasterPassVertex(Attributes input)
{
    Varings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    
    // 消除unity近裁剪面前移（Shadow Pancaking）错误裁剪
    // 顶点位置夹紧(Vertex Clamping)
    #if UNITY_REVERSED_Z
    // positionCS还在裁剪空间未进行透视除法，将其除以w分量得到NDC坐标，将右边项w除以w得到的是1也就是近裁剪面
    // 取最大就是将z夹紧或投影到近裁剪面（DX12中）
    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

// 渲染目标是ShadowMap RT所以片元着色器可以没有输出
// 深度由顶点着色器输出自动写入深度缓冲区
// 片元着色器只需处理是否需要丢弃片元即可
void ShadowCasterPassFragment(Varings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    float4 base = GetBase(input.baseUV);

    // 半透明透明阴影的混合模式，裁剪，抖动
    #if defined(_SHADOWS_CLIP)
        clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, GetCutoff(input.baseUV)));
    #elif defined(_SHADOWS_DITHER)
        float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
        clip(base.a - dither);
    #endif
}


#endif
