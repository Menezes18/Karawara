using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    // Restored from fbe5d86e^ with the original script GUID for existing scenes.
    public class SystemFps : MonoBehaviour
    {
        public TextMeshProUGUI fpsText;
        public Toggle fpsToggle;

        private float deltaTime;

        private void Awake()
        {
            if (fpsText == null)
                fpsText = GetComponent<TextMeshProUGUI>();
            if (fpsToggle != null)
                fpsToggle.onValueChanged.AddListener(ToggleFPSDisplay);
        }

        private void OnDestroy()
        {
            if (fpsToggle != null)
                fpsToggle.onValueChanged.RemoveListener(ToggleFPSDisplay);
        }

        private void Start()
        {
            // Preserve the old initially hidden display when a settings toggle is assigned.
            if (fpsToggle != null)
                ToggleFPSDisplay(false);
        }

        private void Update()
        {
            if (fpsText == null || !fpsText.gameObject.activeSelf)
                return;

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            if (deltaTime > 0f)
                fpsText.text = Mathf.Ceil(1f / deltaTime).ToString() + " FPS";
        }

        public void ToggleFPSDisplay(bool show)
        {
            if (fpsText != null)
                fpsText.gameObject.SetActive(show);
        }
    }
}
