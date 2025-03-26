using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";

    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;

    // 最大支持一个带阴影的方向光（初始配置）
    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    int ShadowedDirectionalLightCount;

    private static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        // 用于PCF采样shadowmap
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    
    static string[] cascadeBlendKeywords = {
        "_CASCADE_SHADOWS_SOFT",
        "_CASCADE_SHADOWS_DITHER"
    };
    
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    
    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_DISTANCE"
    };
    
    // 跟踪是否使用阴影遮罩
    bool useShadowMask;
    
    static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];
    
    // 定义转换到Atlas空间的阴影矩阵数组，数量是最大阴影光源数量 * 最大阴影级联数量
    private static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings) {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        // 每次设置重置灯光计数
        ShadowedDirectionalLightCount = 0;
        useShadowMask = false;
    }
    
    // 检测
    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
        // 如果阴影光源数量未达到最大值，且光源有阴影，且阴影强度大于0，且光源有阴影投射体则返回
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            // 检测可见光没有对任何物体投射阴影
            // 例如：光照只影响到了超出最大阴影距离的ShadowCaster，此时没有投射阴影
            cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) {
                // 检测当前光源是否符合ShadowMask条件
                LightBakingOutput lightBaking = light.bakingOutput;
                if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask) 
                {
                        useShadowMask = true;
                }
                shadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight {
                    visibleLightIndex = visibleLightIndex, 
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
                // 返回light的阴影强度，阴影索引，阴影偏差
                return new Vector3(
                    light.shadowStrength,
                    settings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                    light.shadowNormalBias);
        }
        return Vector3.zero;
    }

    public void Render() {
        // 如果有阴影光源，渲染阴影
        if (ShadowedDirectionalLightCount > 0) {
            RenderDirectionalShadows();
        }
        else {
            // 在WebGL2.0中texture与sampler绑定在一起，无texture会导至报错
            // 所以即使没有阴影光源，也需要创建一个空的RT
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        // 是否启用ShadowMask
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ? 0 : -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows() {
        int atlasSize = (int)settings.directional.atlasSize;
        // 创建阴影图集：
        // depthBuffer尽可能比特数越高，URP采用16位
        // 默认双线性滤波
        // shadowmap格式，提供一个合适的shadowmap格式，具体取决于目标平台
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        // 命令GPU渲染目标到texture而不是相机，明确该纹理的数据如何加载和存储
        // 我们不关心它的初始状态，因为需要立即清除它
        // 目的时存储阴影数据，所以得存储
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        // 清除深度缓冲区，但不清除颜色缓冲区
        buffer.ClearRenderTarget(true, false, Color.clear);
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
        buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        
        
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        // 为什么限制到2的次幂？
        // 防止3x3分区无法整除，浪费纹理空间
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        // 渲染每个光源的阴影
        for (int i = 0; i < ShadowedDirectionalLightCount; i++) {
            RenderDirectionalShadows(i, split, tileSize);
        }
        
        // 在渲染级联阴影后将级联数据传递给GPU
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        // 传递阴影矩阵到Shader
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        // 截断阴影
        // buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);
        
        // Distance Fade
        // buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade));
        
        // Cascade fade
        float f = 1f - settings.directional.cascadeFade;
        
        // Distance fade 
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));
        
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void SetKeywords(string[] keywords, int enabledIndex) {
        for (int i = 0; i < keywords.Length; i++) {
            if(i == enabledIndex) {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    void RenderDirectionalShadows(int index, int split, int tileSize) {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        
        // 渲染每个级联
        for (int i = 0; i < cascadeCount; i++) {
            // 计算光源的观察矩阵和投影矩阵
            // light.nearPlaneOffset: 近平面后移防止剔除大物体阴影
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
            );
            // 设置级联阴影的剔除因子，减少级联之间的重叠
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            // split data包含阴影投射对象如何被切分的信息
            shadowSettings.splitData = splitData;
            // 仅传递一次剔除球体数据
            if (index == 0) {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            int tileIndex = tileOffset + i;
            // 从世界空间转换到光源空间通过乘以光源的阴影投影矩阵和观察矩阵
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
            // 应用光源的投影矩阵
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            // 设置全局DepthBias
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            // 重置全局DepthBias，隔离阴影贴图渲染的临时状态，确保全局渲染状态的纯净性
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    
    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize) {
        float texelSize = 2f * cullingSphere.w / tileSize;
        // 加上filterSize使normalbias匹配采样器尺寸
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        // 比免采样到级联裁剪球外的shadowatlas通过球半径减去采样器尺寸
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    // 将世界坐标转换到 Shadow Atlas 贴图的 Tile 空间的矩阵
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split) {
        // 在某些图形 API (Vulkan, DirectX) 中，Z 缓冲是反向的，即 1.0 代表最接近相机，0.0 代表最远。
        // 与OpenGL相比是为了利用浮点数精度的特性，在远处用更高精度的浮点数，避免z-fight。
        if (SystemInfo.usesReversedZBuffer) {
            // Z轴取反，以确保 阴影采样 (Shadow Sampling) 兼容所有平台。
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        // 计算tile的大小
        float scale = 1f / split;

        // 将Clip Space(-1, 1)转换到Shadow Atlas UV(0,1), 并加上偏移量
        // [ m00  m01  m02  m03 ]   // 第一列: X 轴变换
        // [ m10  m11  m12  m13 ]   // 第二列: Y 轴变换
        // [ m20  m21  m22  m23 ]   // 第三列: Z 轴变换
        // [ m30  m31  m32  m33 ]   // 第四列: 透视投影 (w 分量）在下面用来抵消透视除法
        // 1. 0.5f * (m.m00 + m.m30) 转换到[0,1]
        // 2. offset.x * m.m30 为了将Tile的偏移量加到UV坐标上
        // 3. scale 为了将Clip Space转换到Shadow Atlas UV
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }
    
    // 通过调整渲染视口渲染单个Tile
    Vector2 SetTileViewport(int index, int split, int tileSize) {
        // 计算Tile的偏移量，范围是0～1
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    // 直到相机完成渲染后释放RT
    public void Cleanup() {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
}