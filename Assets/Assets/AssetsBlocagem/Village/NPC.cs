using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class NPC : MonoBehaviour
    {
        public GameObject TelaDialogo;

        
        public TextMeshProUGUI dialogueText; // Arraste e solte o componente de texto da UI aqui no Inspector
        public string[] dialogueLines; 
        private int currentLineIndex = 0;


        // Update is called once per frame
        void OnTriggerEnter(Collider col)
        {
            if(col.tag == "Player"){
                ActivateCursor();
                
                TelaDialogo.SetActive(true);  

                
            }
        }
        
        void Update()
        {
            if (Keyboard.current.enterKey.wasReleasedThisFrame)
            {
                ShowNextDialogueLine();
            }
        }
        void OnTriggerExit(Collider col)
        {
            if(col.tag == "Player"){
                DeactivateCursor();
                TelaDialogo.SetActive(false);   
            }
        }
        void ShowNextDialogueLine()
        {
            if (dialogueLines.Length > 0)
            {
                
                currentLineIndex++;
                if (currentLineIndex >= dialogueLines.Length)
                {
                    TelaDialogo.SetActive(false);
                }
                dialogueText.text = dialogueLines[currentLineIndex];
            }
        }
        public void ActivateCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Método para desativar o cursor
        public void DeactivateCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
       }
    }
}
