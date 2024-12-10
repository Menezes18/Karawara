using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField]
        private float delay = 10f; // Tempo de espera em segundos

        [SerializeField]
        private string sceneName = "NextScene"; // Nome da cena para onde será redirecionado

        private void Start()
        {
            // Inicia a contagem regressiva
            Invoke("LoadNextScene", delay);
        }

        private void LoadNextScene()
        {
            // Carrega a próxima cena
            SceneManager.LoadScene(sceneName);
        }
    }
}
