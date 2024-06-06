using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
using System.Linq;
using UnityEngine.UI;
namespace RPGKarawara
{
    public class MenuSettings : MonoBehaviour
    {
        public TMP_Dropdown resolutionDropdown;
        public TMP_Dropdown refreshRateDropdown;
        public Toggle fullscreenToggle; // Adicione este campo para o toggle de fullscreen
        public TMP_Text fullscreenStatusText; // Opcional: Adicione este campo para o texto de status

        private Resolution[] resolutions;
        private Dictionary<string, List<int>> resolutionToRefreshRates;

        void Start()
        {
            // Inicializa o toggle de fullscreen com o estado atual
            fullscreenToggle.isOn = Screen.fullScreen;
            UpdateFullscreenText(); // Atualiza o texto do toggle de fullscreen

            // Obtém as resoluções disponíveis
            resolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            refreshRateDropdown.ClearOptions();

            // Usa um HashSet para garantir que cada resolução seja única
            HashSet<string> uniqueResolutions = new HashSet<string>();
            resolutionToRefreshRates = new Dictionary<string, List<int>>();
            List<string> resolutionOptions = new List<string>();

            int currentResolutionIndex = 0;
            for (int i = 0; i < resolutions.Length; i++)
            {
                string option = resolutions[i].width + " x " + resolutions[i].height;
                if (uniqueResolutions.Add(option))
                {
                    resolutionOptions.Add(option);
                    resolutionToRefreshRates[option] = new List<int>();
                }

                resolutionToRefreshRates[option].Add(resolutions[i].refreshRate);

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = resolutionOptions.IndexOf(option);
                }
            }

            resolutionDropdown.AddOptions(resolutionOptions);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();

            // Preenche o dropdown de taxas de atualização com base na resolução atual
            UpdateRefreshRateDropdown(resolutionOptions[currentResolutionIndex]);

            // Adiciona o listener para o evento de mudança do dropdown de resolução
            resolutionDropdown.onValueChanged.AddListener(delegate {
                ResolutionDropdownValueChanged(resolutionDropdown);
            });

            // Adiciona o listener para o evento de mudança do dropdown de taxa de atualização
            refreshRateDropdown.onValueChanged.AddListener(delegate {
                RefreshRateDropdownValueChanged(refreshRateDropdown);
            });

            // Adiciona o listener para o evento de mudança do toggle de fullscreen
            fullscreenToggle.onValueChanged.AddListener(delegate {
                OnFullscreenToggleChanged(fullscreenToggle.isOn);
            });
        }

        void ResolutionDropdownValueChanged(TMP_Dropdown change)
        {
            // Obtém a resolução selecionada
            string selectedResolution = change.options[change.value].text;

            // Atualiza o dropdown de taxas de atualização com base na resolução selecionada
            UpdateRefreshRateDropdown(selectedResolution);

            // Define a resolução com a taxa de atualização padrão (primeira da lista)
            SetResolution(change.value, 0);
        }

        void RefreshRateDropdownValueChanged(TMP_Dropdown change)
        {
            // Define a resolução com base na resolução e taxa de atualização selecionadas
            SetResolution(resolutionDropdown.value, change.value);
        }

        void UpdateRefreshRateDropdown(string resolution)
        {
            refreshRateDropdown.ClearOptions();

            List<int> refreshRates = resolutionToRefreshRates[resolution];
            List<string> refreshRateOptions = refreshRates.Select(rate => rate + " Hz").Distinct().ToList();
            refreshRateDropdown.AddOptions(refreshRateOptions);
            refreshRateDropdown.value = 0;
            refreshRateDropdown.RefreshShownValue();
        }

        void SetResolution(int resolutionIndex, int refreshRateIndex)
        {
            string[] dimensions = resolutionDropdown.options[resolutionIndex].text.Split('x');
            int width = int.Parse(dimensions[0].Trim());
            int height = int.Parse(dimensions[1].Trim());
            int refreshRate = resolutionToRefreshRates[resolutionDropdown.options[resolutionIndex].text][refreshRateIndex];

            Screen.SetResolution(width, height, Screen.fullScreen, refreshRate);
        }

        void OnFullscreenToggleChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            UpdateFullscreenText(); // Atualiza o texto do toggle de fullscreen
        }

        void UpdateFullscreenText()
        {
            fullscreenStatusText.text = "Fullscreen: " + (Screen.fullScreen ? "On" : "Off");
        }
    }
}
