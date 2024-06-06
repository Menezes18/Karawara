using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPGKarawara
{
    public class URPPostProcessingControl : MonoBehaviour
    {
        public Toggle ambientOcclusionToggle;
        public Toggle bloomToggle;
        public Toggle motionBlurToggle;
        public Toggle vignetteToggle;
        public Toggle depthOfFieldToggle;

        private Volume postProcessVolume;
        
        private Bloom bloom;
        private MotionBlur motionBlur;
        private Vignette vignette;
        private DepthOfField depthOfField;

        void Start()
        {
            // Obtém o volume de pós-processamento
            postProcessVolume = FindObjectOfType<Volume>();

            // Obtém os efeitos do perfil de volume
            
            postProcessVolume.profile.TryGet(out bloom);
            postProcessVolume.profile.TryGet(out motionBlur);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out depthOfField);

            // Inicializa os toggles com o estado atual dos efeitos
            
            bloomToggle.isOn = bloom.active;
            motionBlurToggle.isOn = motionBlur.active;
            vignetteToggle.isOn = vignette.active;
            depthOfFieldToggle.isOn = depthOfField.active;

            // Adiciona listeners para os toggles
            
            bloomToggle.onValueChanged.AddListener(delegate { ToggleEffect(bloom, bloomToggle.isOn); });
            motionBlurToggle.onValueChanged.AddListener(delegate { ToggleEffect(motionBlur, motionBlurToggle.isOn); });
            vignetteToggle.onValueChanged.AddListener(delegate { ToggleEffect(vignette, vignetteToggle.isOn); });
            depthOfFieldToggle.onValueChanged.AddListener(delegate { ToggleEffect(depthOfField, depthOfFieldToggle.isOn); });
        }

        void ToggleEffect(VolumeComponent effect, bool isOn)
        {
            effect.active = isOn;
        }
    }
}
