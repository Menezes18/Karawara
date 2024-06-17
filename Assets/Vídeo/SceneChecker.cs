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

    public void teleportSpawn(){
        var player = GameObject.FindGameObjectWithTag("Player");
        spawn = GameObject.FindGameObjectWithTag("Spawn");
        player.transform.position = spawn.transform.position;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Verifica se a cena carregada é "SceneMenu"
        if (scene.name == "SceneMenu"){
            var player = GameObject.FindGameObjectWithTag("Player");
            player.transform.position = new Vector3(0,30,0);
            // Ativa o cursor do mouse
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            statusBar.SetActive(false);
            // Exibe uma mensagem no console
            Debug.Log("Estamos na SceneMenu. O cursor está ativado.");
        }
        else
        {
            PlayerCamera.instance.canFollow = true;
            teleportSpawn();
            // Desativa o cursor do mouse e trava no centro da tela
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            statusBar.SetActive(true);
            // Exibe uma mensagem no console
            Debug.Log("Não estamos na SceneMenu. O cursor está desativado e travado.");
        }
    }
}