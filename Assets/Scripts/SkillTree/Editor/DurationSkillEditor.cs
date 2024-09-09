using UnityEngine;
using UnityEditor;
using RPGKarawara.SkillTree;

[CustomEditor(typeof(DurationSkill))]
public class DurationSkillEditor : Editor
{
    private SerializedProperty showFields;
    private SerializedProperty erodeRate;
    private SerializedProperty erodeRefreshRate;
    private SerializedProperty erodeDelay;
    private SerializedProperty erodeObject;

    private void OnEnable()
    {
        // Pegue as propriedades serializadas
        showFields = serializedObject.FindProperty("showFields");
        erodeRate = serializedObject.FindProperty("erodeRate");
        erodeRefreshRate = serializedObject.FindProperty("erodeRefreshRate");
        erodeDelay = serializedObject.FindProperty("erodeDelay");
        erodeObject = serializedObject.FindProperty("erodeObject");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Adiciona o toggle para ativar ou desativar a visualização dos campos com base no valor de showFields
        showFields.boolValue = EditorGUILayout.Toggle("Ativar erode Object!", showFields.boolValue);

        if (showFields.boolValue)
        {
            EditorGUILayout.PropertyField(erodeRate);
            EditorGUILayout.PropertyField(erodeRefreshRate);
            EditorGUILayout.PropertyField(erodeDelay);
            EditorGUILayout.PropertyField(erodeObject);
        }

        serializedObject.ApplyModifiedProperties();
    }
}