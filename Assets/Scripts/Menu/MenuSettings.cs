using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
using System.Linq;

namespace RPGKarawara
{
    public class MenuSettings : MonoBehaviour
    {
  public TMP_Dropdown dropdown;

        private Resolution[] resolutions;

        void Start()
        {
            // Obtém as resoluções disponíveis
            resolutions = Screen.resolutions;
            dropdown.ClearOptions();

            // Usa um HashSet para garantir que cada resolução seja única
            HashSet<string> uniqueResolutions = new HashSet<string>();
            List<string> options = new List<string>();

            int currentResolutionIndex = 0;
            for (int i = 0; i < resolutions.Length; i++)
            {
                string option = resolutions[i].width + " x " + resolutions[i].height;
                if (uniqueResolutions.Add(option))
                {
                    options.Add(option);
                }

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = uniqueResolutions.Count - 1;
                }
            }

            dropdown.AddOptions(options);
            dropdown.value = currentResolutionIndex;
            dropdown.RefreshShownValue();

            // Adiciona o listener para o evento de mudança do dropdown
            dropdown.onValueChanged.AddListener(delegate {
                DropdownValueChanged(dropdown);
            });
        }

        void DropdownValueChanged(TMP_Dropdown change)
        {
            // Obtém o índice selecionado
            int selectedIndex = change.value;

            // Define a resolução com base na seleção
            SetResolution(selectedIndex);

            // Obtém o nome da opção selecionada
            string selectedOption = change.options[selectedIndex].text;


        }

        void SetResolution(int resolutionIndex)
        {
            string[] dimensions = dropdown.options[resolutionIndex].text.Split('x');
            int width = int.Parse(dimensions[0].Trim());
            int height = int.Parse(dimensions[1].Trim());

            Screen.SetResolution(width, height, Screen.fullScreen);
        }
    }
}