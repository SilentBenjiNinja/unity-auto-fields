using bnj.auto_fields.Runtime;
using UnityEditor;
using UnityEngine;

namespace bnj.auto_fields.Editor
{
    // Credit to Tutorial by Freedom Coding:
    // https://youtu.be/cVxjKphoi5o
    [CustomPropertyDrawer(typeof(AutoAttribute))]
    public class AutoAssignPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();

            var showWarning = property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null;

            var warningRect = !showWarning ? Rect.zero : new(
                position.x,
                position.y,
                position.width,
                position.height * 1.5f
            );
            var margin = (warningRect.height - position.height) / 2;
            var fieldRect = !showWarning ? position : new(
                position.x + warningRect.height,
                position.y + margin,
                position.width - warningRect.height - margin,
                position.height
            );

            if (showWarning)
            {
                EditorGUI.HelpBox(warningRect, "", MessageType.Error);
                GUILayout.Space(warningRect.height - position.height);
            }

            GUI.enabled = showWarning;
            EditorGUI.PropertyField(fieldRect, property, label, true);
            GUI.enabled = true;
        }
    }
}
