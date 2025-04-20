using System;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public partial class PostFXStack : MonoBehaviour
{
    private bool useHDR;
    
    private const string bufferName = "Post FX";
    
    private CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    
    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    public bool IsActive => settings != null;

    private int
        bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        bloomResultId = Shader.PropertyToID("_BloomResult"),
        colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
        colorFilterId = Shader.PropertyToID("_ColorFilter"),
        whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
        splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
        splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
        channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
        channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
        channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
        smhShadowsId = Shader.PropertyToID("_SMHShadows"),
        smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
        smhHighlightsId = Shader.PropertyToID("_SMHHightlights"),
        smhRangeId = Shader.PropertyToID("_SMHRange");

    private const int maxBloomPyramidLevels = 16;

    private int bloomPyramidId;

    enum  Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomPrefilter,
        BloomPrefilterFireflies,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        ToneMappingNone,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
        Copy
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings, bool useHDR) {
        this.useHDR = useHDR;
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    public void Render(int sourceId) {
        if (DoBloom(sourceId)) {
            DoColorGradingAndToneMapping(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else {
            DoColorGradingAndToneMapping(sourceId);
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    public PostFXStack() {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++) {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    bool DoBloom(int sourceId) {
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;

        if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit) {
            return false;
        }
        
        buffer.BeginSample("Bloom");
        
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0 , FilterMode.Bilinear, format);
        Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        int i;
        for (i = 0; i < bloom.maxIterations; i++) {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit) {
                break;
            }
            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }

        buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
            finalIntensity = bloom.intensity;
        }
        else {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        if (i > 1) {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;

            for (i -= 1; i > 0; i--) {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        buffer.GetTemporaryRT(bloomResultId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, format);
        Draw(fromId, bloomResultId, finalPass);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        return true;
    }

    void ConfigureColorAdjustments() {
        ColorAdjustMentsSettiings colorAdjustMents = settings.ColorAdjustments;
        buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
            Mathf.Pow(2f, colorAdjustMents.postExposure),
            colorAdjustMents.contrast * 0.01f + 1f,
            colorAdjustMents.hueShift * (1f / 360f),
            colorAdjustMents.saturation * 0.01f + 1f
            ));
        buffer.SetGlobalVector(colorFilterId, colorAdjustMents.colorFilter.linear);
    }

    void ConfigureWhiteWhiteBalance() {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temprature, whiteBalance.tint));
    }

    void ConfigureSplitToning() {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.hightlights);
    }

    void ConfigureChannelMixer() {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHighlights() {
        ShadowsMidtoneshighlightsSettings smh = settings.ShadowsMidtoneshighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        buffer.SetGlobalVector(smhRangeId, new Vector4(smh.shadowsStart, smh.shadowsEnd, smh.highLightsStart, smh.highLightsEnd));
    }
    
    void DoColorGradingAndToneMapping(int sourceId) {
        ConfigureColorAdjustments();
        ConfigureWhiteWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();
        
        ToneMappingSettings.Mode mode = settings.ToonMapping.mode;
        Pass pass = Pass.ToneMappingNone + (int)mode;
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
    }
}
