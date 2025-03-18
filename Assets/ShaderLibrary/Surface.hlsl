#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

// 这里多引入一个结构体会不会造成性能压力？
// 不会shader编译器会高度优化程序
struct Surface
{
    float3 position;
    float3 normal;
    float3 viewDirection;
    float3 color;
    float depth;
    float alpha;
    float metallic;
    float smoothness;
    float dither;
};

#endif
