using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class NPCDialogue : MonoBehaviour
    {
        public DataMissao missao1;
        public GameObject player;
        public GameObject dialogueUI;
        public TextMeshProUGUI dialogueText; 
        public Image npcSprite; 
        public float textSpeed = 0.05f; 

        [Header("Player Detection")]
        public float detectionRange = 5f; 
        public Color gizmoColor = Color.yellow;

        [Header("Escolha")] 
        public GameObject canvasEscolha;
        public GameObject escolha1;
        public GameObject escolha2;
        public Image npc1Image;
        public Image npc2Image;

        private bool playerProximo = false; 
        private bool missaoAceita = false; 
        private bool isTyping = false; 
        private int dialogueIndex = 0; 
        private int npcDialogueIndex = 0;
        private bool canSkipDialogue = false;
        private bool dialogueStarted;
        private Coroutine typingCoroutine;
        private static NPCDialogue activeDialogue;

        private void Start()
        {
            canvasEscolha.SetActive(false);
            player = GameObject.FindGameObjectWithTag("Player");
        }

        private void Update()
        {
            DetectarPlayer();

            if (playerProximo && !missaoAceita)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !dialogueStarted)
                {
                    IniciarDialogo();
                }

                // Se pressionar "Enter" e o diálogo estiver ativo
                if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame && dialogueStarted && dialogueUI.activeSelf)
                {
                    // Se o texto está sendo digitado, o jogador pode pular
                    if (isTyping)
                    {
                        // Completar o texto se o jogador apertar "Enter" durante a digitação
                        canSkipDialogue = true;
                    }
                    // Se o texto terminou ou foi pulado, avançar para o próximo
                    else if (!isTyping && !canSkipDialogue)
                    {
                        NextDialogue();
                    }
                }
            }
        }
        
        private void IniciarDialogo()
        {
            if (activeDialogue != null && activeDialogue != this) return;
            if (missao1 == null || missao1.dialogue == null || missao1.dialogue.Length == 0 || dialogueUI == null || dialogueText == null) return;
            activeDialogue = this;
            dialogueStarted = true;
            dialogueUI.SetActive(true); 
            npcDialogueIndex = 0; 
            dialogueIndex = 0; 
            UpdateNPCSprite(); 
            NextDialogue(); 
        }

        private void NextDialogue()
        {
            if (dialogueIndex < missao1.dialogue[npcDialogueIndex].textoDialogue.Length)
            {
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                typingCoroutine = StartCoroutine(TypeDialogue(missao1.dialogue[npcDialogueIndex].textoDialogue[dialogueIndex]));
                dialogueIndex++;
            }
            else
            {
                dialogueIndex = 0;
                npcDialogueIndex++;

                if (npcDialogueIndex < missao1.dialogue.Length)
                {
                    UpdateNPCSprite(); 
                    NextDialogue(); 
                }
                else
                {
                    FinalizarDialogo();
                }
            }
        }

        private IEnumerator TypeDialogue(string dialogue)
        {
            isTyping = true;
            dialogueText.text = "";

            foreach (char letter in dialogue.ToCharArray())
            {
                
                if (canSkipDialogue)
                {
                    dialogueText.text = dialogue;
                    canSkipDialogue = false; 
                    break;
                }
                dialogueText.text += letter;
                yield return new WaitForSeconds(textSpeed);
            }

            isTyping = false; 
            canSkipDialogue = false; 
            typingCoroutine = null;
        }
        
        private void UpdateNPCSprite()
        {
            npcSprite.sprite = missao1.dialogue[npcDialogueIndex].icon;
        }

        public void SelecionarMissao(int missao)
        {
            Debug.Log("selecionar missao");
            if (missao == 1)
            {
                escolha1.SetActive(true);
            }
            else if(missao == 2)
            {
                escolha2.SetActive(true);
            }
            canvasEscolha.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        private void FinalizarDialogo()
        {
            dialogueStarted = false;
            if (activeDialogue == this) activeDialogue = null;
            npc1Image.sprite = missao1.dialogue[0].icon;
            npc2Image.sprite = missao1.dialogue[1].icon;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            dialogueUI.SetActive(false); 
            canvasEscolha.SetActive(true);
        }

        private void DetectarPlayer()
        {
            if (player == null) player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) { playerProximo = false; return; }
            float distance = Vector3.Distance(transform.position, player.transform.position);
            playerProximo = distance <= detectionRange;
        }

        private void OnDisable()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
            dialogueStarted = false;
            if (activeDialogue == this) activeDialogue = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
