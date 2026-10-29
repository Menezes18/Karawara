using UnityEngine;
using UnityEditor;
using RPGKarawara.SkillTree;

[CustomEditor(typeof(DurationSkill))]
public class DurationSkillEditor : Editor
{
    // Propriedades serializadas
    private SerializedProperty showFields;
    private SerializedProperty erodeRate;
    private SerializedProperty erodeRefreshRate;
    private SerializedProperty erodeDelay;
    private SerializedProperty erodeObject;
    private SerializedProperty skillRoot;

    private void OnEnable()
    {
        // Inicializa as propriedades serializadas
        skillRoot = serializedObject.FindProperty("skillRoot");
        showFields = serializedObject.FindProperty("showFields");
        erodeRate = serializedObject.FindProperty("erodeRate");
        erodeRefreshRate = serializedObject.FindProperty("erodeRefreshRate");
        erodeDelay = serializedObject.FindProperty("erodeDelay");
        erodeObject = serializedObject.FindProperty("erodeObject");
    }

    public override void OnInspectorGUI()
    {

        serializedObject.Update();


        EditorGUILayout.LabelField("Configurações de Duração da Habilidade", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        skillRoot.boolValue = EditorGUILayout.Toggle("Ativar Skill Root", skillRoot.boolValue);
        showFields.boolValue = EditorGUILayout.Toggle("Ativar Campos de Erosão", showFields.boolValue);
        EditorGUILayout.Space(5);

        if (showFields.boolValue)
        {
            if (skillRoot.boolValue)
            {
                EditorGUILayout.HelpBox("Skill Root está ativado. Campos adicionais desativados.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Configurações de Erosão", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(erodeRate, new GUIContent("Taxa de Erosão"));
                EditorGUILayout.PropertyField(erodeRefreshRate, new GUIContent("Taxa de Atualização"));
                EditorGUILayout.PropertyField(erodeDelay, new GUIContent("Atraso de Erosão"));
                EditorGUILayout.PropertyField(erodeObject, new GUIContent("Objeto de Erosão"));
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}