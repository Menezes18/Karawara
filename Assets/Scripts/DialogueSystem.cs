using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine;

namespace RPGKarawara
{
    public class DialogueSystem : MonoBehaviour
    {
        public TextMeshPro dialogueText; // Arraste e solte o componente de texto da UI aqui no Inspector
        public string[] dialogueLines; 
        private int currentLineIndex = 0;

        void Update()
        {
            if (Keyboard.current.enterKey.wasReleasedThisFrame)
            {
                ShowNextDialogueLine();
            }
        }

        void ShowNextDialogueLine()
        {
            if (dialogueLines.Length > 0)
            {
                
                currentLineIndex++;
                if (currentLineIndex >= dialogueLines.Length)
                {
                    currentLineIndex = 0; // Volta ao início se passar do final
                }
                dialogueText.text = dialogueLines[currentLineIndex];
            }
        }
    }
}
