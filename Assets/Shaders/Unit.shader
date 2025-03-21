Shader "Custom RP/Unlit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0
        [HDR]_BaseColor("Color", color) = (1.0, 1.0, 1.0, 1.0)
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "UnlitInput.hlsl"
        ENDHLSL

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            // pragma来自与希腊语含义是一个操作，在计算机语言上多用来表示发出一个特殊的编译指令
            #pragma shader_feature _CLIPPING
            // GPU instances需要通过数组来提供数据，但是Shader不支持数组
            // 所以需要使用multi_compile_instancing来支持
            // 这会使Shader生成两个变体，一个支持GPU instances，一个不支持
            #pragma multi_compile_instancing
            #pragma  vertex UnlitPassVertex
            #pragma  fragment UnlitPassFragment
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    Pass
    {
        Tags
        {
            "LightMode" = "ShadowCaster"
        }
        
        ColorMask 0
        
        HLSLPROGRAM
        #pragma target 3.5
        #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
        #pragma multi_compile_instancing
        #pragma vertex ShadowCasterPassVertex
        #pragma fragment ShadowCasterPassFragment
        #include "ShadowCasterPass.hlsl"
        ENDHLSL
    }
    }
    CustomEditor "CustomShaderGUI"
}
