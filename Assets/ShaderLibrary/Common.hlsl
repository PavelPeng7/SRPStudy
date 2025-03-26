#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"
// 这里使用宏定义的作用？
// 使用宏可以在编译时将代码替换成宏定义的内容，这样可以减少代码量，提高代码的可读性
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

// 为了避免打断Instance加入关键字检测
#if defined(_SHADOW_MASK_DISTANCE)
    #define SHADOWS_SHADOWMASK
#endif
// UnityInstanceing.hlsl必须在定义了变换矩阵的宏之后，在SpaceTransforms.hlsl之前包含
// UnityInstanceing.hlsl重定义了宏，以访问实例化数据数组
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

// float3 TransformObjectToWorld(float3 positionOS)
// {
//     return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
// }
//
// float4 TransformWorldToHClip(float3 positionWS)
// {
//     return mul(unity_MatrixVP, float4(positionWS, 1.0));
// }

float DistanceSquared(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}

#endif
