using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DialogueSystem;

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(DialogueEvent))]
public class DialogueEventPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        if (property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true))
        {
            EditorGUI.BeginProperty(position, label, property);

            position.x += 30;

            float contentHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 5;
            float labelSpacing = 140f;
            float contentWidth = 200f;

            SerializedProperty nameProp = property.FindPropertyRelative("eventName");
            Rect nameRect = new Rect(position.x + labelSpacing, position.y + 20, contentWidth, contentHeight);
            Rect nameLabelRect = new Rect(position.x, position.y + 20, contentWidth, contentHeight);
            EditorGUI.LabelField(nameLabelRect, new GUIContent("Event Name"));
            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);

            SerializedProperty intProp = property.FindPropertyRelative("intParameter");
            Rect intRect = new Rect(position.x + labelSpacing, nameRect.y + nameRect.height + spacing, contentWidth, contentHeight);
            Rect intLabelRect = new Rect(position.x, intRect.y, intRect.width, intRect.height);
            EditorGUI.LabelField(intLabelRect, new GUIContent("Int Parameter"));
            EditorGUI.PropertyField(intRect, intProp, GUIContent.none);

            SerializedProperty floatProp = property.FindPropertyRelative("floatParameter");
            Rect floatRect = new Rect(position.x + labelSpacing, intRect.y + intRect.height + spacing, contentWidth, contentHeight);
            Rect floatLabelRect = new Rect(position.x, floatRect.y, floatRect.width, floatRect.height);
            EditorGUI.LabelField(floatLabelRect, new GUIContent("Float Parameter"));
            EditorGUI.PropertyField(floatRect, floatProp, GUIContent.none);
            
            SerializedProperty stringProp = property.FindPropertyRelative("stringParameter");
            Rect stringRect = new Rect(position.x + labelSpacing, floatRect.y + floatRect.height + spacing, contentWidth, contentHeight);
            Rect stringLabelRect = new Rect(position.x, stringRect.y, stringRect.width, stringRect.height);
            EditorGUI.LabelField(stringLabelRect, new GUIContent("String Parameter"));
            EditorGUI.PropertyField(stringRect, stringProp, GUIContent.none);

            SerializedProperty boolProp = property.FindPropertyRelative("boolParameter");
            Rect boolRect = new Rect(position.x + labelSpacing, stringRect.y + stringRect.height + spacing, contentWidth, contentHeight);
            Rect boolLabelRect = new Rect(position.x, boolRect.y, boolRect.width, boolRect.height);
            EditorGUI.LabelField(boolLabelRect, new GUIContent("Bool Parameter"));
            EditorGUI.PropertyField(boolRect, boolProp, GUIContent.none);
            
            SerializedProperty inputKey = property.FindPropertyRelative("inputKey");
            Rect inputKeyRect = new Rect(position.x + labelSpacing, boolRect.y + boolRect.height + spacing, contentWidth, contentHeight);
            Rect inputKeyLabelRect = new Rect(position.x, inputKeyRect.y, inputKeyRect.width, inputKeyRect.height);
            EditorGUI.LabelField(inputKeyLabelRect, new GUIContent("InputKey"));
            EditorGUI.PropertyField(inputKeyRect, inputKey, GUIContent.none);
            
            EditorGUI.EndProperty();
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return property.isExpanded ? (6 * (EditorGUIUtility.singleLineHeight + 12)) : EditorGUIUtility.singleLineHeight;
    }
}
#endif
