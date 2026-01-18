// Assets/Editor/SerializedPropertyCopyUtil.cs
// Provides a Unity-version-safe replacement for CopyFromSerializedProperty.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GalacticFishing.EditorTools
{
    public static class SerializedPropertyCopyUtil
    {
        /// <summary>
        /// Copies the value of <paramref name="src"/> into <paramref name="dst"/> for common SerializedProperty types.
        /// This matches the (not always available) API some samples call "CopyFromSerializedProperty".
        /// </summary>
        public static void CopyFromSerializedProperty(this SerializedProperty dst, SerializedProperty src)
        {
            if (src == null || dst == null) return;
            if (src.propertyType != dst.propertyType)
            {
                Debug.LogWarning($"CopyFromSerializedProperty: type mismatch {src.propertyType} -> {dst.propertyType}");
                return;
            }

            switch (src.propertyType)
            {
                case SerializedPropertyType.Integer:         dst.intValue = src.intValue; break;
                case SerializedPropertyType.Boolean:         dst.boolValue = src.boolValue; break;
                case SerializedPropertyType.Float:           dst.floatValue = src.floatValue; break;
                case SerializedPropertyType.String:          dst.stringValue = src.stringValue; break;
                case SerializedPropertyType.Color:           dst.colorValue = src.colorValue; break;
                case SerializedPropertyType.ObjectReference: dst.objectReferenceValue = src.objectReferenceValue; break;
                case SerializedPropertyType.LayerMask:       dst.intValue = src.intValue; break;
                case SerializedPropertyType.Enum:            dst.enumValueIndex = src.enumValueIndex; break;
                case SerializedPropertyType.Vector2:         dst.vector2Value = src.vector2Value; break;
                case SerializedPropertyType.Vector3:         dst.vector3Value = src.vector3Value; break;
                case SerializedPropertyType.Vector4:         dst.vector4Value = src.vector4Value; break;
                case SerializedPropertyType.Rect:            dst.rectValue = src.rectValue; break;
                case SerializedPropertyType.Character:       dst.intValue = src.intValue; break;
                case SerializedPropertyType.AnimationCurve:  dst.animationCurveValue = src.animationCurveValue; break;
                case SerializedPropertyType.Bounds:          dst.boundsValue = src.boundsValue; break;
                case SerializedPropertyType.Quaternion:      dst.quaternionValue = src.quaternionValue; break;
#if UNITY_2019_1_OR_NEWER
                case SerializedPropertyType.Vector2Int:      dst.vector2IntValue = src.vector2IntValue; break;
                case SerializedPropertyType.Vector3Int:      dst.vector3IntValue = src.vector3IntValue; break;
                case SerializedPropertyType.RectInt:         dst.rectIntValue = src.rectIntValue; break;
                case SerializedPropertyType.BoundsInt:       dst.boundsIntValue = src.boundsIntValue; break;
#endif
                default:
                    Debug.LogWarning($"CopyFromSerializedProperty: unsupported type {src.propertyType}");
                    break;
            }
        }
    }
}
#endif
