using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
namespace RPGKarawara
{
    public class DropdownCheck : MonoBehaviour
    {

        public TMP_Dropdown  dropdown;
        

        void Start()
        {
            // Adiciona o listener para o evento de mudança do dropdown
            dropdown.onValueChanged.AddListener(delegate {
                DropdownValueChanged(dropdown);
            });
        }

        void DropdownValueChanged(TMP_Dropdown change)
        {
            // Obtém o índice selecionado
            int selectedIndex = change.value;

            // Obtém o nome da opção selecionada
            string selectedOption = change.options[selectedIndex].text;

            // Exibe o nome da opção selecionada no console
            //Debug.Log(selectedOption);
            WorldSaveGameManager.instance.OnSlotOverwriteSelected(selectedOption);
        }
    } 
}

