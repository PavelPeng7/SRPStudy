using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int cutoffId = Shader.PropertyToID("_Cutoff");
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    
    [SerializeField]
    Color baseColor = Color.white;
    
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f;
    static MaterialPropertyBlock block;
     
    void Awake() {
        OnValidate();
    }
    
    private void OnValidate() {
        if (block == null) {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorId, baseColor);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }
}
