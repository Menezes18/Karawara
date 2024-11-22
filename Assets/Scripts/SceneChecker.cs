using System;
using RPGKarawara;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChecker : MonoBehaviour{

    public GameObject statusBar;
    public GameObject spawn;
    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Marca este objeto para não ser destruído ao carregar uma nova cena
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // Registra o callback para ser chamado quando uma nova cena for carregada
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // Remove o callback quando o objeto for desativado ou destruído
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    Scene sceneStart;
    private void Start()
    {
        // Inicializa a variável com a cena atual
        // Scene sceneStart = SceneManager.GetActiveScene();
        //
        // if (sceneStart.name == "SceneMenu")
        // {
        //     Debug.Log("SCENE");
        //     Invoke("Tirar", 0.1f);
        // }
    }

    public void Tirar(){
        
            statusBar.SetActive(false);
    }
    public void teleportSpawn(Vector3 spawnPosition){
         //var player = GameObject.FindGameObjectWithTag("Player");
        // spawn = GameObject.FindGameObjectWithTag("Spawn");
        // player.transform.position = spawnPosition;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Verifica se a cena carregada é "SceneMenu"
        if (scene.name == "SceneMenu"){
            var player = GameObject.FindGameObjectWithTag("Player");
          //  player.transform.position = new Vector3(0,30,0);
            // Ativa o cursor do mouse
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            //statusBar.SetActive(false);
            // Exibe uma mensagem no console
           
        }
        else
        {
            PlayerCamera.instance.canFollow = true;
            var saveData = WorldSaveGameManager.instance.currentCharacterData;
            //Vector3 spawn = new Vector3(saveData.xPosition, saveData.yPosition, saveData.zPosition);
           // teleportSpawn(spawn);
            // Desativa o cursor do mouse e trava no centro da tela
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            WorldSaveGameManager.instance.loadscene.desativado();
            //statusBar.SetActive(true);
            // Exibe uma mensagem no console
           
        }
    }
}