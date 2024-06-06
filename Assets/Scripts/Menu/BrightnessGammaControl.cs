using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BrightnessGammaControl : MonoBehaviour
{
    public Slider brightnessSlider;
    public Slider gammaSlider;
    public TMP_Text brightnessValueText;
    public TMP_Text gammaValueText;
    
    void Start()
    {
        // Inicializa os sliders com valores padrão
        //brightnessSlider.value = RenderSettings.ambientLight.r; // Assume que o brilho inicial está configurado no canal R
        //gammaSlider.value = 1.0f; // Gama padrão
        
        // Atualiza os textos dos sliders
        UpdateBrightnessText();
        UpdateGammaText();
        OnBrightnessChanged();
        // Adiciona listeners para os sliders
        //brightnessSlider.onValueChanged.AddListener(delegate { OnBrightnessChanged(); });
        //gammaSlider.onValueChanged.AddListener(delegate { OnGammaChanged(); });
    }

    void OnBrightnessChanged()
    {
        // Define a iluminação ambiente com base no valor do slider
        float brightness = brightnessSlider.value;
        //RenderSettings.ambientLight = new Color(brightness, brightness, brightness, 1);
        RenderSettings.ambientIntensity = 0.1f;
       
        // Atualiza o texto do slider
        UpdateBrightnessText();
    }

    void OnGammaChanged()
    {
        // Define a gama com base no valor do slider
        float gamma = gammaSlider.value;
        // Nota: A configuração direta de gama não é suportada nativamente pelo Unity, 
        // então você pode ajustar o valor da exposição da câmera ou aplicar uma correção de gama através de shaders.
        UpdateGammaText();
    }

    void UpdateBrightnessText()
    {
        brightnessValueText.text = brightnessSlider.value.ToString("F2");
    }

    void UpdateGammaText()
    {
        gammaValueText.text = gammaSlider.value.ToString("F2");
    }
}