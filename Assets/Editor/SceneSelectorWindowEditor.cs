using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

public class SceneSelectorWindowEditor : EditorWindow
{

    private List<string> scenePaths = new List<string>
    {
        "Assets/Scenes/SceneMenu.unity",    
        "Assets/Scenes/Video1.unity",       
        "Assets/Scenes/t.unity",        
        "Assets/Scenes/Arte.unity",
        "Assets/Scenes/CucaLoad.unity",
        

    };

    private void OnGUI()
    {
        GUILayout.Label("Selecione uma cena e use os botões:", EditorStyles.boldLabel);
        foreach (var scenePath in scenePaths)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            GUILayout.BeginVertical("box"); // Cria uma caixa para cada cena
            GUILayout.Label(sceneName, EditorStyles.boldLabel); // Exibe o nome da cena
            
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


    void SetPlayModeStartScene(string scenePath)
    {
        SceneAsset myWantedStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (myWantedStartScene != null)
        {
            // Define a cena para ser carregada no Play mode
            EditorSceneManager.playModeStartScene = myWantedStartScene;
            //Debug.Log("Cena de início configurada: " + scenePath);
        }
        else
        {
            Debug.LogError("Não foi possível encontrar a cena " + scenePath);
        }
    }

    [MenuItem("Scene Selector/Selecione uma cena")]
    static void Open()
    {
        GetWindow<SceneSelectorWindowEditor>("Scene Selector");
    }
}
