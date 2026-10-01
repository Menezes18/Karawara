#if UNITY_EDITOR
using DialogueSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using XNode;
using XNodeEditor;

[CustomNodeEditor(typeof(ChoiceNode))]
public class ChoiceNodeEditor : NodeEditor
{
    private ChoiceNode node;
    private bool hasArrayData;

    public override int GetWidth()
    {
        return 400;
    }

    public override void OnBodyGUI()
    {
        if (node == null) node = target as ChoiceNode;

        // Atualiza a representação do objeto serializado
        serializedObject.Update();

        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("input"));
        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("nodeName"));
        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("textPrompt"));

        EditorGUILayout.Space(10);

        // Lista de opções
        NodeEditorGUILayout.DynamicPortList("choices", typeof(DialogueChoice), serializedObject, 
            NodePort.IO.Output, Node.ConnectionType.Override, Node.TypeConstraint.None, OnCreateReorderableList);

        // Aplica as modificações nas propriedades
        serializedObject.ApplyModifiedProperties();
    }

    private void OnCreateReorderableList(ReorderableList list)
    {
        SerializedProperty arrayData = serializedObject.FindProperty("choices");
        hasArrayData = arrayData != null && arrayData.isArray;

        list.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Choices");
        };

        list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            if (hasArrayData && arrayData.propertyType != SerializedPropertyType.String)
            {
                if (arrayData.arraySize <= index)
                {
                    EditorGUI.LabelField(rect, $"Array[{index}] data out of range");
                    return;
                }

                SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, itemData, new GUIContent($"Choice {index}"), true);
            }
            else
            {
                EditorGUI.LabelField(rect, "Invalid choice");
            }

            // Conecta os nós
            XNode.NodePort port = node.GetPort("choices" + " " + index);
            if (port != null)
            {
                Vector2 pos = rect.position + (port.IsOutput ? new Vector2(rect.width + 6, 0) : new Vector2(-36, 0));
                NodeEditorGUILayout.PortField(pos, port);
            }
        };
    }
}
#endif
