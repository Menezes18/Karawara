using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SystemFps : MonoBehaviour
    {
        public TextMeshProUGUI fpsText; // Referência ao TextMeshPro Text
        public Toggle fpsToggle; // Referência ao Toggle

        private float deltaTime = 0.0f;

        void Start()
        {
            if (fpsToggle != null)
            {
                fpsToggle.onValueChanged.AddListener(delegate { ToggleFPSDisplay(fpsToggle.isOn); });
            }

            // Inicialmente desativa o texto dos FPS
            if (fpsText != null)
            {
                fpsText.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (fpsText != null && fpsText.gameObject.activeSelf)
            {
                deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
                float fps = 1.0f / deltaTime;
                fpsText.text = Mathf.Ceil(fps).ToString() + " FPS";
            }
        }

        public void ToggleFPSDisplay(bool show)
        {
            if (fpsText != null)
            {
                fpsText.gameObject.SetActive(show);
            }
        }
    }
}
