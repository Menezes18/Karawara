using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueSystem;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.ComponentModel.Design;
using RPGKarawara;
using UnityEngine.InputSystem.Controls;

public class SampleTriggerDialogue : MonoBehaviour
{
    public DialogueTheme alternativeTheme;
    public DialogueGraph graph;
    public SampleUIHandler UiHandler;
    public float distanciaPlayer = 10f; // Distância para detectar o player
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
    public GameObject hint;
    private Keyboard keyboard;
    private Mouse mouse;

    private void Awake()
    {
        PlayerUIManager.instance.playerUIHudManager.activateMarcador(gameObject.transform);
        keyboard = Keyboard.current;
        mouse = Mouse.current;

        // Esconder o cursor ao iniciar
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;  // Trava o cursor no centro da tela
    }

    private void Start()
    {

        dialogueManager = DialogueSystem.Dialogue.instance;
        gameStateVariables.Add(new DialogueGameState(false, "Clicou"));
        gameStateVariables.Add(new DialogueGameState(false, "Completo"));
        dialogueManager.SetDialogGameState(gameStateVariables);
        flags = GetComponent<SampleFlags>();

        dialogueManager.flags = flags.gameEventFlags;
        dialogueManager.dialogueCallbackActions.OnNodeLeave += OnNodeLeave;
        dialogueManager.dialogueCallbackActions.OnNodeEnter += OnNodeEnter;

        dialogueManager.OnChoiceDraw += OnChoiceDraw;

        dialogueManager.dictionary.AddEntry(test1, GetDictionaryValue);
        dialogueManager.dictionary.AddEntry(test2, "static value");

        dialogueManager.RegisterEventHandler(TestEventHandler);
    }

    private void Update()
    {
        CheckPlayer();
        if (isPlayerNearby && Keyboard.current.enterKey.wasPressedThisFrame)  // Verifica se o jogador pressionou Enter
        {

            if (!dialogueManager.IsRunning)
            {
                dialogueManager.SetDialogGameState(gameStateVariables);
                dialogueManager.StartDialogue();  // Inicia o diálogo
                dialogueActive = true;
                gameStateVariables[0].ChangeValue(true);

                // Mostrar o cursor e destravar
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                // Desativar o controle da câmera
                if (cameraController != null)
                {
                    cameraController.enabled = false;  // Desativa a movimentação da câmera
                }
                AtivarHint(DialogueHint.instance.currentLineIndex);
            }
        }

        if (!isPlayerNearby)
        {
            if (dialogueManager.IsRunning)
            {
                DesativarHint();
                dialogueManager.StopDialogue();
                dialogueManager._handler.EndDialogue();
            }
            return;
        }

        // Avança o diálogo se ele já estiver ativo
        if (dialogueActive && Keyboard.current.enterKey.wasPressedThisFrame && !inputCheck && isPlayerNearby)
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

    public void CheckPlayer()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, distanciaPlayer);
        bool playerFound = false; // Variável para verificar se o player foi encontrado

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                playerFound = true; // Define que o jogador foi encontrado
                isPlayerNearby = true;

                // Verifica se o diálogo atual não é o mesmo e troca
                if (dialogueManager.dialogueGraph != graph)
                {
                    DialogueSystem.Dialogue.instance._handler.EndDialogue();
                    dialogueManager.dialogueGraph = graph;
                    DialogueSystem.Dialogue.instance.Create();
                }
                break; // Sai do loop, pois o jogador foi encontrado
            }
        }

        // Se o jogador não foi encontrado em nenhum collider, define isPlayerNearby como false
        if (!playerFound)
        {
            isPlayerNearby = false;
        }
    }


    private void OnTriggerExit(Collider other)
    {

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

                case EventTypeDialogue.EnableUI:
                    AtivarHint(myEvent.intParameter);
                    break;
                case EventTypeDialogue.DisableUI:
                    DesativarHint();
                    break;
                case EventTypeDialogue.TutorialCompleto:
                    PassarObjetivo();
                    break;
                case EventTypeDialogue.CheckMouseInput:
                    StartCoroutine(CheckMouseInputCoroutine(myEvent.inputKey));
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

    private void AtivarHint(int fala)
    {
        hint.SetActive(true);
        DialogueHint.instance.currentLineIndex = fala;
        DialogueHint.instance.ShowNextDialogueLine(fala);
    }
    private void DesativarHint()
    {
        hint.SetActive(false);
    }

    void PassarObjetivo()
    {
        gameStateVariables[1].ChangeValue(true);
        PontosManager.instance.condicaoPontos[PontosManager.instance.index].podeAvançar = true;
    }

    IEnumerator CheckInputCoroutine(string key)
    {
        Debug.Log($"Esperando o jogador apertar a tecla {key.ToUpper()}...");


        if (!Enum.TryParse(key, out Key keyEnum))
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
        dialogueManager.AdvanceDialogue();
    }

    IEnumerator CheckMouseInputCoroutine(string button)
    {
        Debug.Log($"Esperando o jogador apertar o botão do mouse {button}...");

        ButtonControl mouseButton;

        switch (button.ToLower())
        {
            case "left":
            case "leftbutton":
            case "esquerdo":
                mouseButton = mouse.leftButton;
                break;
            case "right":
            case "rightbutton":
            case "direito":
                mouseButton = mouse.rightButton;
                break;
            case "middle":
            case "middlebutton":
            case "meio":
                mouseButton = mouse.middleButton;
                break;
            default:
                Debug.LogError($"Botão do mouse '{button}' inválido.");
                inputCheck = false;
                yield break;
        }

        while (!mouseButton.isPressed)
        {
            yield return null;
        }

        Debug.Log($"Parabéns! Você apertou o botão do mouse {button}.");
        inputCheck = false;
        dialogueManager.AdvanceDialogue();
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaPlayer);
    }

}

