Shader "Hidden/Custom RP/ Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostFXStackPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Bloom Horizontal"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Vertical"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Add"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomAddPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter Fireflies"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomPrefilterFirefliesPassFragment
            ENDHLSL
        }


        Pass
        {
            Name "Bloom Scatter"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomScatterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Scatter Final"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ColorGrading None Pass"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment ColorGradingNonePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ToneMapping ACES"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment ToneMappingACESPassFragment
            ENDHLSL
        }

       Pass
        {
            Name "ToneMapping Neutral"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment ToneMappingNeutralPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ToneMapping Reinhard"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment ToneMappingReinhardPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Final"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex  DefaultPassVertex
                #pragma fragment FinalPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Copy"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyPassFragment
            ENDHLSL
        }
    }
}
