using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class FadeImage : MonoBehaviour
    {
        public Image image; // Referência para o componente Image que você quer fazer o fade
        public float fadeDuration = 1.0f; // Duração do fade em segundos

        private Coroutine currentFadeCoroutine; // Referência para a coroutine atual de fade

        void Start()
        {
            // Inicializa o alfa da imagem para 1 (totalmente visível)
        }

        // Função para iniciar o fade
        public void StartFade()
        {
            image.color = new Color(image.color.r, image.color.g, image.color.b, 1.0f);
            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
            }
            currentFadeCoroutine = StartCoroutine(FadeImageCoroutine(true));
        }

        public void mudar(){
            image.color = new Color(image.color.r, image.color.g, image.color.b, 1.0f);
        }
        // Função para parar o fade
        public void StopFade()
        {
            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
            }
           // currentFadeCoroutine = StartCoroutine(FadeImageCoroutine(false));
        }

        // Coroutine para controlar o fade da imagem
        private IEnumerator FadeImageCoroutine(bool fadeIn)
        {
            
            float elapsedTime = 0.0f;
            Color startColor = image.color;
            Color endColor = fadeIn ? new Color(startColor.r, startColor.g, startColor.b, 0.0f) : new Color(startColor.r, startColor.g, startColor.b, 1.0f);

            while (elapsedTime < fadeDuration)
            {
                image.color = Color.Lerp(startColor, endColor, elapsedTime / fadeDuration);
                elapsedTime += Time.unscaledDeltaTime; // Use Time.unscaledDeltaTime para não considerar o timeScale
                yield return null;
            }
            image.color = new Color(endColor.r, endColor.g, endColor.b, 0.0f);
            //image.color = endColor; // Garante que a cor final seja exatamente o alvo
        }
    }
}