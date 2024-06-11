using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChecker : MonoBehaviour{

    public GameObject statusBar;
    void Awake()
    {
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

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Verifica se a cena carregada é "SceneMenu"
        if (scene.name == "SceneMenu")
        {
            // Ativa o cursor do mouse
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            statusBar.SetActive(false);
            // Exibe uma mensagem no console
            Debug.Log("Estamos na SceneMenu. O cursor está ativado.");
        }
        else
        {
            // Desativa o cursor do mouse e trava no centro da tela
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            statusBar.SetActive(true);
            // Exibe uma mensagem no console
            Debug.Log("Não estamos na SceneMenu. O cursor está desativado e travado.");
        }
    }
}