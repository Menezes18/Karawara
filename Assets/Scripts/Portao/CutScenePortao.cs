using System.Collections;
using UnityEngine;

namespace RPGKarawara
{
    public class CutScenePortao : MonoBehaviour
    {
        public GameObject[] stone;
        public FadeImage fadeImage;
        public GameObject cameraObject;

            
        void Start(){
            

        }

        public void StartSequence(int numero, bool check)
        {
            // Pausa o tempo
            Time.timeScale = 0;

            // Ativa a câmera
            cameraObject.SetActive(true);
            fadeImage.StartFade();
            stone[numero].SetActive(check);
            // Ativa o fade

            // Ativa o movimento da pedra

            // Espera um tempo antes de retomar o tempo
            StartCoroutine(WaitAndResumeTime());
        }

        IEnumerator WaitAndResumeTime()
        {
            // Espera 2 segundos usando o tempo real
            yield return new WaitForSecondsRealtime(2.0f);

            // Desativa o fade
            fadeImage.StopFade();

            // Volta o tempo ao normal
            Time.timeScale = 1;

            // Desativa a câmera após a cutscene
            cameraObject.SetActive(false);
        }
    }
}