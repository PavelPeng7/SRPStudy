using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    private PostFXSettings postFXSettings = default;

    [SerializeField]
    private Shader shader = default;

    [System.NonSerialized]
    private Material material;

    public Material Material
    {
        get
        {
            if (material == null && shader != null) {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }
    
    [System.Serializable]
    public class BloomSettings
    {
        [Range(0f, 16f)]
        public int maxIterations;

        [Min(1f)]
        public int downscaleLimit;

        public bool bicubicUpsampling;
        
        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)]
        public float intensity;

        public bool fadeFireflies;
        
        public enum Mode
        {
            Additive, Scattering
        }

        public Mode mode;

        [Range(0.05f, 0.95f)]
        public float scatter;
    }

    [SerializeField]
    public BloomSettings Bloom = new BloomSettings {
        scatter = 0.7f
    };
    
    [Serializable]
    public struct  ColorAdjustMentsSettiings
    {
        public float postExposure;

        [Range(-100f, 100f)]
        public float contrast;

        [ColorUsage(false, true)]
        public Color colorFilter;

        [Range(-180f, 180f)]
        public float hueShift;

        [Range(-100f, 100f)]
        public float saturation;
    }

    [SerializeField]
    private ColorAdjustMentsSettiings colorAdjustments = new ColorAdjustMentsSettiings {
        colorFilter = Color.white
    };
    
    public ColorAdjustMentsSettiings ColorAdjustments => colorAdjustments;
    
    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)]
        public float temprature, tint;
    }
    
    [SerializeField]
    private WhiteBalanceSettings whiteBalance = default;
    
    public WhiteBalanceSettings WhiteBalance => whiteBalance;
    
    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)]
        public Color shadows, hightlights;

        [Range(-100f, 100f)]
        public float balance;
    }

    [SerializeField]
    private SplitToningSettings splitToning = new SplitToningSettings {
        shadows = Color.gray,
        hightlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;
    
    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red, green, blue;
    }

    [SerializeField]
    private ChannelMixerSettings channelMixer = new ChannelMixerSettings {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;
    
    [Serializable]
    public struct ShadowsMidtoneshighlightsSettings
    {
        [ColorUsage(false, true)]
        public Color shadows, midtones, highlights;

        [Range(0f, 2f)]
        public float shadowsStart, shadowsEnd, highLightsStart, highLightsEnd;
    }

    [SerializeField]
    private ShadowsMidtoneshighlightsSettings shadowsMidtoneshighlights = new ShadowsMidtoneshighlightsSettings {
        shadows = Color.white,
        midtones = Color.white,
        highlights = Color.white,
        shadowsEnd = 0.3f,
        highLightsStart = 0.55f,
        highLightsEnd = 1f
    };

    public ShadowsMidtoneshighlightsSettings ShadowsMidtoneshighlights => shadowsMidtoneshighlights;
    

    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode{None, ACES, Neutral , Reinhard}

        public Mode mode;
    }

    [SerializeField]
    private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToonMapping => toneMapping;
}
