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


}
