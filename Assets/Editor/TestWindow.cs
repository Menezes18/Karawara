using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

public class SceneSelectorWindow : EditorWindow
{
    // Lista de cenas disponíveis para selecionar
    private List<string> scenePaths = new List<string>
    {
        "Assets/Scenes/SceneMenu.unity",    // Caminho da primeira cena
        "Assets/Scenes/Video1.unity",       // Caminho da segunda cena
        "Assets/Scenes/t.unity"        // Caminho da terceira cena
    };

    private void OnGUI()
    {
        GUILayout.Label("Selecione uma cena e use os botões:", EditorStyles.boldLabel);

        // Itera sobre as cenas disponíveis
        foreach (var scenePath in scenePaths)
        {
            // Nome da cena (sem o caminho completo)
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            GUILayout.BeginVertical("box"); // Cria uma caixa para cada cena
            GUILayout.Label(sceneName, EditorStyles.boldLabel); // Exibe o nome da cena

            // Botões para Trocar e Play
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Trocar", GUILayout.Width(100)))
            {
                ChangeScene(scenePath); // Troca para a cena selecionada
            }

            if (GUILayout.Button("Play", GUILayout.Width(100)))
            {
                SetPlayModeStartScene(scenePath); // Define a cena para o Play mode
                EditorApplication.isPlaying = true; // Inicia o modo Play
            }
            GUILayout.EndHorizontal(); // Finaliza a linha de botões

            GUILayout.EndVertical(); // Finaliza a caixa da cena
            GUILayout.Space(10); // Espaço pequeno entre as cenas
        }
    }

    // Método para mudar a cena
    void ChangeScene(string scenePath)
    {
        // Carrega a cena
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            Debug.Log("Cena trocada para: " + scenePath);
        }
        else
        {
            Debug.Log("Não foi possível trocar a cena.");
        }
    }

    // Método para definir a cena de início do modo Play
    void SetPlayModeStartScene(string scenePath)
    {
        SceneAsset myWantedStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (myWantedStartScene != null)
        {
            // Define a cena para ser carregada no Play mode
            EditorSceneManager.playModeStartScene = myWantedStartScene;
            Debug.Log("Cena de início configurada: " + scenePath);
        }
        else
        {
            Debug.LogError("Não foi possível encontrar a cena " + scenePath);
        }
    }

    [MenuItem("Test/Scene Selector")]
    static void Open()
    {
        GetWindow<SceneSelectorWindow>("Scene Selector");
    }
}
