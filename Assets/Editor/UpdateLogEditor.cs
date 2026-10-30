using UnityEditor;
using UnityEngine;

public class UpdateLogEditor : EditorWindow
{
    private UpdateLog updateLog;

    [MenuItem("Window/Update Log Editor")]
    public static void ShowWindow()
    {
        GetWindow<UpdateLogEditor>("Update Log Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("Update Log Editor", EditorStyles.boldLabel);
        
        updateLog = (UpdateLog)EditorGUILayout.ObjectField("Update Log", updateLog, typeof(UpdateLog), false);

        if (updateLog == null) return;

        // Edição da versão
        for (int i = 0; i < updateLog.updates.Length; i++)
        {
            GUILayout.Label($"Version {i + 1}", EditorStyles.boldLabel);
            updateLog.updates[i].version = EditorGUILayout.TextField("Version", updateLog.updates[i].version);
            updateLog.updates[i].text = EditorGUILayout.TextArea(updateLog.updates[i].text, GUILayout.Height(50));
        }

        // Edição das pessoas e atividades
        for (int i = 0; i < updateLog.people.Length; i++)
        {
            GUILayout.Label($"Person {i + 1}", EditorStyles.boldLabel);
            updateLog.people[i].name = EditorGUILayout.TextField("Name", updateLog.people[i].name);
            
            for (int j = 0; j < updateLog.people[i].activities.Length; j++)
            {
                updateLog.people[i].activities[j] = EditorGUILayout.TextField($"Activity {j + 1}", updateLog.people[i].activities[j]);
            }

            // Adiciona botão para adicionar nova atividade
            if (GUILayout.Button("Add Activity"))
            {
                System.Array.Resize(ref updateLog.people[i].activities, updateLog.people[i].activities.Length + 1);
                updateLog.people[i].activities[^1] = ""; // Adiciona uma nova string vazia
            }
        }

        // Adiciona botão para adicionar nova pessoa
        if (GUILayout.Button("Add Person"))
        {
            System.Array.Resize(ref updateLog.people, updateLog.people.Length + 1);
            updateLog.people[^1] = new UpdateLog.PersonInfo(); // Adiciona uma nova pessoa vazia
        }

        // Salva as alterações
        if (GUI.changed)
        {
            EditorUtility.SetDirty(updateLog);
        }
    }
}
