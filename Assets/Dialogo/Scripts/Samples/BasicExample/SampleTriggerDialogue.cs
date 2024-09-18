using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueSystem;
using UnityEngine.InputSystem;

public class SampleTriggerDialogue : MonoBehaviour
{
    public DialogueTheme alternativeTheme;
    public DialogueGraph graph;
    public SampleUIHandler UiHandler;

    private string testDict = "First initial value";
    private DialogueSystem.Dialogue dialogueManager;
    private SampleFlags flags;
    private List<DialogueGameState> gameStateVariables = new List<DialogueGameState>();
    private bool isPlayerNearby = false;  // Verifica se o jogador está próximo
    private bool dialogueActive = false;  // Verifica se o diálogo está ativo

    // Variável para o script de controle de câmera
    public MonoBehaviour cameraController;

    // Variável para o script de movimentação do NPC
    public NPCMovement npcMovement;
    private bool inputCheck = false;

    public Transform targetPosition;
    public float playerPositionThreshold = 1.5f;
    public string test1, test2;

    private void Awake()
    {
        keyboard = Keyboard.current; 

        // Esconder o cursor ao iniciar
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;  // Trava o cursor no centro da tela
    }

    private void Start()
    {
        dialogueManager = DialogueSystem.Dialogue.instance;
        flags = GetComponent<SampleFlags>();

        dialogueManager.flags = flags.gameEventFlags;
        dialogueManager.dialogueCallbackActions.OnNodeLeave += OnNodeLeave;
        dialogueManager.dialogueCallbackActions.OnNodeEnter += OnNodeEnter;

        gameStateVariables.Add(new DialogueGameState(10f, "floatExample"));
        dialogueManager.SetDialogGameState(gameStateVariables);
        dialogueManager.OnChoiceDraw += OnChoiceDraw;

        dialogueManager.dictionary.AddEntry(test1, GetDictionaryValue);
        dialogueManager.dictionary.AddEntry(test2, "static value");

        dialogueManager.RegisterEventHandler(TestEventHandler);
    }
    
    private void Update()
    {
        if (isPlayerNearby && Keyboard.current.enterKey.wasPressedThisFrame)  // Verifica se o jogador pressionou Enter
        {
            
            if (!dialogueManager.IsRunning)
            {
                
                dialogueManager.StartDialogue();  // Inicia o diálogo
                dialogueActive = true;

                // Mostrar o cursor e destravar
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                // Desativar o controle da câmera
                if (cameraController != null)
                {
                    cameraController.enabled = false;  // Desativa a movimentação da câmera
                }
            }
        }

        // Avança o diálogo se ele já estiver ativo
        if (dialogueActive && Keyboard.current.mKey.wasPressedThisFrame && !inputCheck && isPlayerNearby)
        {
            if (dialogueManager.IsRunning)
            {
                if (dialogueManager.isAnimating)
                {
                    dialogueManager.EndLine();
                }
                else
                {
                    if (dialogueManager.CurrentState != DialogueState.AwaitingEventResponse)
                        dialogueManager.AdvanceDialogue();
                    
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;

           // Verifica se o diálogo atual não é o mesmo e troca
            if (dialogueManager.dialogueGraph != graph)
            {
                
                DialogueSystem.Dialogue.instance._handler.EndDialogue();
                dialogueManager.dialogueGraph = graph;
                DialogueSystem.Dialogue.instance.Create();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;  // Define que o jogador saiu da área
        }
    }

    public void OnNodeLeave(BaseNode node) { /* Custom logic for when a node is left */ }

    public void OnNodeEnter(BaseNode node) { /* Custom logic for when a node is entered */ }

    public bool OnChoiceDraw(DialogueChoices node)
    {
        UiHandler.RenderDialogueChoices(node, dialogueManager);
        return true;
    }

    public string GetDictionaryValue()
    {
        return testDict;
    }

    public void TestEventHandler(DialogueEvent myEvent)
    {
        if (myEvent != null)
        {
            switch (myEvent.eventName)
            {

                case EventTypeDialogue.MoveNPCToNextWaypoint:
                    MoveNPCToNextWaypoint();
                    break;

                case EventTypeDialogue.CheckInput:
                    inputCheck = true;
                    StartCoroutine(CheckInputCoroutine(myEvent.inputKey));
                    break;

                case EventTypeDialogue.MoveNPCToPlayer:
                    npcMovement.MoveToPlayer();
                    break;

                case EventTypeDialogue.CheckPlayerPosition:
                    CheckPlayerPosition();
                    break;
            }
        }
    }

    private void CheckPlayerPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("Player not found");
            return;
        }

        float distanceToTarget = Vector3.Distance(player.transform.position, targetPosition.position);
        if (distanceToTarget <= playerPositionThreshold)
        {
            Debug.LogError("O jogador está na posição correta!");
            dialogueManager.AdvanceDialogue();
        }
        else
        {
            Debug.LogError("O jogador não está na posição correta!");
        }
    }

    private void MoveNPCToNextWaypoint()
    {
        if (npcMovement != null)
        {
            npcMovement.MoveToNextWaypoint();  // Chama o método de movimentação do NPC
        }
        else
        {
            Debug.LogWarning("NPCMovement script is not assigned!");
        }
    }
    
    private Keyboard keyboard;

    IEnumerator CheckInputCoroutine(string key)
    {
        Debug.Log($"Esperando o jogador apertar a tecla {key.ToUpper()}...");

        
        if (!Enum.TryParse(key.ToUpper(), out Key keyEnum))
        {
            Debug.LogError($"Tecla {key} inválida.");
            inputCheck = false;
            yield break;
        }

        
        while (!keyboard[keyEnum].isPressed)
        {
            yield return null;
        }

        Debug.Log($"Parabens! Você apertou a tecla {key.ToUpper()}.");
        inputCheck = false;
    }

}

