using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGKarawara
{
    public class VSyncControl : MonoBehaviour
    {
             public Toggle vSyncToggle;
            public TMP_Text vSyncStatusText;
        
            void Start()
            {
                // Inicializa o toggle com o estado atual do V-Sync
                vSyncToggle.isOn = QualitySettings.vSyncCount > 0;
        
                // Atualiza o texto do toggle
                UpdateVSyncText();
        
                // Adiciona listener para o toggle
                vSyncToggle.onValueChanged.AddListener(delegate { OnVSyncToggleChanged(); });
            }
        
            void OnVSyncToggleChanged()
            {
                // Ativa ou desativa o V-Sync com base no estado do toggle
                if (vSyncToggle.isOn)
                {
                    QualitySettings.vSyncCount = 1; // Ativa o V-Sync
                }
                else
                {
                    QualitySettings.vSyncCount = 0; // Desativa o V-Sync
                }
        
                // Atualiza o texto do toggle
                UpdateVSyncText();
            }
        
            void UpdateVSyncText()
            {
                vSyncStatusText.text = "V-Sync: " + (vSyncToggle.isOn ? "On" : "Off");
            }
    }
}
