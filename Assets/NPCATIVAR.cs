using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class NPCATIVAR : MonoBehaviour
    {
        public float interactionRange = 3f; // Distância de interação
        public Transform player; // Referência ao player
        public GameObject dialogBox; // Objeto de UI do diálogo
        public GameObject dialogBox2; // Objeto de UI do diálogo
        public TMPro.TextMeshProUGUI dialogText; // Componente de texto do diálogo
        public string[] dialogLines; // Vetor de falas do NPC
        private int currentLineIndex = 0; // Índice da fala atual
        private bool isInteracting = false; // Controle de interação
        public GameObject obj;

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            //dialogBox.SetActive(false); // Esconde a caixa de diálogo no início
        }

        private void Update()
        {
            // Verifica a distância entre o player e o NPC
            if (Vector3.Distance(transform.position, player.position) <= interactionRange)
            {
                // Verifica se a tecla E foi pressionada
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    if (!isInteracting)
                    {
                        StartDialogue();
                    }
                    else
                    {
                        NextLine();
                    }
                }
            }
        }

        // Inicia o diálogo
        private void StartDialogue()
        {
            isInteracting = true;
            dialogBox2.SetActive(true);
            dialogBox.SetActive(true);
            currentLineIndex = 0;
            dialogText.text = dialogLines[currentLineIndex];
        }

        // Avança para a próxima linha do diálogo
        private void NextLine()
        {
            currentLineIndex++;

            if (currentLineIndex < dialogLines.Length)
            {
                dialogText.text = dialogLines[currentLineIndex];
            }
            else
            {
                EndDialogue();
            }
        }

        // Termina o diálogo
        private void EndDialogue()
        {
            obj.SetActive(false);
            isInteracting = false;
            dialogBox.SetActive(false);
        }
    }
}
