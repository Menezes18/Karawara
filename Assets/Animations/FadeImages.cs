using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeImages : MonoBehaviour
{
    public CanvasGroup galhoFadeCanvas;    // CanvasGroup do galho
    public CanvasGroup leafFadeGalho;      // CanvasGroup da folha
    public float fadeDuration = 2f;        // Duração do fade em segundos
    public float startGalhoFadeAt = 0.5f;  // Ponto no fade da folha para começar o fade do galho (0.5 = metade)
    private float leafFadeDelay = 0.1f;       // Atraso antes de começar o fade das folhas

    public CanvasGroup painel;
    private float timer = 0f;
    private bool leafFadeStarted = false;  // Verifica se o fade das folhas já começou
    public Image fillImage; // A imagem que você deseja preencher
    public Image fillIcon; // A imagem que você deseja preencher
    public float fillDuration = 0.5f; // Duração em segundos para preencher
    public float darkenDelay = 5f; // Tempo antes de escurecer tudo
    public float darkenDuration = 2f; // Duração do escurecimento
    public GameObject fundo;

    void Start()
    {
        Inicializar();
    }

    private void Inicializar()
    {
        galhoFadeCanvas.alpha = 0f;
        leafFadeGalho.alpha = 0f; // Começa com as imagens invisíveis
        painel.alpha = 0f;
        fillImage.fillAmount = 0f;
        fillIcon.fillAmount = 0f;

        timer = 0f;
        leafFadeStarted = false; // Reseta o estado do fade das folhas
    }

    private IEnumerator FillImage()
    {
        float elapsedTime = 0f; // Tempo decorrido
        float startAmount = 0f; // Valor inicial do fillAmount
        float endAmount = 1f; // Valor final do fillAmount

        while (elapsedTime < fillDuration)
        {
            elapsedTime += Time.deltaTime; // Incrementa o tempo decorrido
            float currentFillAmount = Mathf.Lerp(startAmount, endAmount, elapsedTime / fillDuration); // Calcula o fillAmount atual
            fillImage.fillAmount = currentFillAmount; // Atualiza o fillAmount da imagem
            fillIcon.fillAmount = currentFillAmount;
            yield return null; // Espera até o próximo frame
        }

        fillImage.fillAmount = endAmount; // Garante que o fillAmount final seja 1
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Inicia o fade das folhas após o atraso (leafFadeDelay)
        if (timer >= leafFadeDelay && !leafFadeStarted)
        {
            leafFadeStarted = true; // Marca que o fade das folhas começou
        }

        // Fade in das folhas
        if (leafFadeStarted)
        {
            leafFadeGalho.alpha += Time.deltaTime / fadeDuration;

            // Verifica se o fade das folhas atingiu o ponto desejado para começar o fade do galho
            if (leafFadeGalho.alpha >= startGalhoFadeAt && galhoFadeCanvas.alpha < 1f)
            {
                // Fade in do galho
                painel.alpha += Time.deltaTime / (fadeDuration / 1.5f);
                galhoFadeCanvas.alpha += Time.deltaTime / fadeDuration;
            }

            // Clampeia os valores de alpha entre 0 e 1
            leafFadeGalho.alpha = Mathf.Clamp01(leafFadeGalho.alpha);
            galhoFadeCanvas.alpha = Mathf.Clamp01(galhoFadeCanvas.alpha);

            // Quando o fade das folhas estiver completo, travar o valor em 1
            if (leafFadeGalho.alpha >= 1f)
            {
                StartCoroutine(FillImage());
                leafFadeGalho.alpha = 1f; // As folhas ficam totalmente visíveis
            }

            // Quando o fade do galho estiver completo
            if (galhoFadeCanvas.alpha >= 1f)
            {
                galhoFadeCanvas.alpha = 1f; // O galho fica totalmente visível
                enabled = false; // Desabilita o script para parar a execução
            }
        }
    }

   

    public void Reiniciar()
    {
        Inicializar();
        enabled = true; // Reabilita o script para reiniciar a execução
    }
}
