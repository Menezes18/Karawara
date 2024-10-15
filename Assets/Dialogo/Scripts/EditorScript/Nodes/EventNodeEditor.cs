#if UNITY_EDITOR
using DialogueSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using XNodeEditor;

[CustomNodeEditor(typeof(EventNode))]
public class EventNodeEditor : NodeEditor
{
    private ReorderableList list;
    private bool hasArrayData;
    private EventNode node;
    private SerializedProperty arrayData;

    public override int GetWidth()
    {
        return 500;
    }

    public override void OnCreate()
    {
        // Inicializa o nó e verifica se é nulo
        if (node == null) node = target as EventNode;
        base.OnCreate();

        // Busca a propriedade dos eventos
        arrayData = serializedObject.FindProperty("events");
        hasArrayData = arrayData != null && arrayData.isArray;

        // Cria a lista ordenável
        list = EditorUtilities.CreateReorderableList("events", node.events, arrayData, 
            typeof(int), serializedObject, null, (int)EditorGUIUtility.singleLineHeight * 8);
        list.list = node.events;
    }

    public override void OnBodyGUI()
    {
        // Atualiza o objeto serializado
        serializedObject.Update();

        // Renderiza os campos do nó
        RenderNodeFields();

        // Aplica as modificações nas propriedades
        serializedObject.ApplyModifiedProperties();
    }

    private void RenderNodeFields()
    {
        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("input"));
        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("nodeName"));

        // Renderiza a lista de eventos
        list.DoLayoutList();

        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("GoToNextNodeAutomatically"));
        NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("output"));
    }
}
#endif