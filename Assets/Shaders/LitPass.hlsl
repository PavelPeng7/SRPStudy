#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
// 为什么不在surface中包含Lighting.hlsl？
// 避免包含一个文件中包含另一个文件，这样会导致文件的依赖关系过于复杂
#include "../ShaderLibrary/Lighting.hlsl"
// OpenGL ES 2.0不支持Cbuffer所以我们得换种写法
// cbuffer UnityPerMaterial
// {
//     float4 _BaseColor;
// };

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseUV : TEXCOORD0;
    float4 tangentOS : TANGENT;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;
    float3 positionWS : VAR_POSITION;
    float2 detailUV : VAR_DETAIL_UV;
    #if defined(_NORMAL_MAP)
        float4 tangentWS : VAR_TANGENT;
    #endif
    
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varings LitPassVertex(Attributes input)
{
    Varings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    InputConfig config = GetInputConfig(input.baseUV);
    output.baseUV = TransformBaseUV(config);
    
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_NORMAL_MAP)
        output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif

    #if defined(_DETAIL_MAP)
        output.detailUV = TransformDetailUV(config);
    #endif
    
    TRANSFER_GI_DATA(input, output);
    return output;
}

float4 LitPassFragment(Varings input):SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    InputConfig config = GetInputConfig(input.baseUV);
    
    #if defined(_MASK_MAP)
        config.useMask = true;
    #endif
    #if defined(_DETAIL_MAP)
        config.detailUV = input.detailUV;
        config.useDetail = true;
    #endif
    
    float4 base = GetBase(config);
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif
    
    Surface surface;
    surface.position = input.positionWS;
    // 为什么要对法线进行归一化？
    // 在顶点法线插值成片元法线的过程中，会导致法线长度变化
    #if defined(_NORMAL_MAP)
        surface.normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
        surface.interpolatedNormal = input.normalWS;
    #else
        surface.normal = normalize(input.normalWS);
        surface.interpolatedNormal = surface.normal;
    #endif
    
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.occlusion = GetOcclusion(config);
    surface.smoothness = GetSmoothness(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    // 世界空间转换到观察空间取z值取反获得深度，左手坐标系z轴朝屏幕外
    surface.depth = -TransformWorldToView(input.positionCS.z);
    
    #if defined(_PREMULTIPLY_ALPHA)
        BRDF brdf = GetBRDF(surface, true);
    #else
        BRDF brdf = GetBRDF(surface);
    #endif
    // GI_FRAGMENT_DATA(input)返回的是lightMapUV
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(config);
    return float4(color, GetFinalAlpha(surface.alpha));
}
#endif
