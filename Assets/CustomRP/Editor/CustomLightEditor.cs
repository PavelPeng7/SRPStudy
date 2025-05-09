using UnityEngine;
using UnityEditor;
[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAssets))]
public class CustomeLightEditor : LightEditor
{
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        if (!settings.lightType.hasMultipleDifferentValues &&
            (LightType)settings.lightType.enumValueIndex == LightType.Spot) 
        {
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }
    }
    
}