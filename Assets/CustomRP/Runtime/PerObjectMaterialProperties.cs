using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int 
        cutoffId = Shader.PropertyToID("_Cutoff"),
        baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness"),
        emissionColorId = Shader.PropertyToID("_EmissionColor");
    
    [SerializeField]
    Color baseColor = Color.white;
    
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;
    static MaterialPropertyBlock block;
    
    [SerializeField, ColorUsage(false, true)]
    Color emissionColor = Color.black;
     
    // 解决Runtime下无法调用OnValidate()的问题
    void Awake() {
        OnValidate();
    }
    
    private void OnValidate() {
        if (block == null) {
            block = new MaterialPropertyBlock();
        }
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
        block.SetFloat(cutoffId, cutoff);
        block.SetColor(baseColorId, baseColor);
        block.SetColor(emissionColorId, emissionColor);
        // SRP Batcher依赖于材质参数的统一内存布局和缓存，而MaterialPropertyBlock动态修改每个对象的属性
        // 导致这些参数无法被合并到同一内存区域，从而无法批处理
        GetComponent<Renderer>().SetPropertyBlock(block);
    }
}
