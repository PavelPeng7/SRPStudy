using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer {

    public static void Draw (
        Rect position, SerializedProperty property, GUIContent label
    ) {
        //SerializedProperty property = settings.renderingLayerMask;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        bool isUint = property.type == "uint";
        // SerialzedProperty采用Uint格式32位有符号整数，而layermask是32位无符号整数，使用位mask原理
        // 当取到SerialzedProperty取到32时也就是100...000表示的是uint的最大值，对应int的是-1也就是011...111启用前面所有选项
        if (isUint && mask == int.MaxValue) {
            mask = -1;
        }
        // 绘制有作用的UI部分
        mask = EditorGUI.MaskField(
            position, label, mask,
            GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
        );
        if (EditorGUI.EndChangeCheck()) {
            property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }
        EditorGUI.showMixedValue = false;
    }
    
    public override void OnGUI (
        Rect position, SerializedProperty property, GUIContent label
    ) {
        Draw(position, property, label);
    }
    
    public static void Draw (SerializedProperty property, GUIContent label) {
        Draw(EditorGUILayout.GetControlRect(), property, label);
    }
}