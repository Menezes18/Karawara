using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    // Nome da cena para a qual você deseja mudar
    public string sceneName;

    // Método chamado quando outro collider entra no trigger deste GameObject
    private void OnTriggerEnter(Collider other)
    {
        // Verifica se o objeto que entrou no trigger tem a tag "Player"
        if (other.CompareTag("Player"))
        {
            // Carrega a cena especificada
            SceneManager.LoadScene(sceneName);
        }
    }
}