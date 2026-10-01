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
    private bool initialized;
    private DialogueHandler choiceHandler;

    private void Awake()
    {
        if (PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHudManager != null)
            PlayerUIManager.instance.playerUIHudManager.activateMarcador(gameObject.transform);
        keyboard = Keyboard.current;
        mouse = Mouse.current;

        // Esconder o cursor ao iniciar
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;  // Trava o cursor no centro da tela
    }

    private void Start()
    {
        gameStateVariables.Add(new DialogueGameState(false, "Clicou"));
        gameStateVariables.Add(new DialogueGameState(false, "Completo"));
        flags = GetComponent<SampleFlags>();
        TryInitialize();
    }

    private bool TryInitialize()
    {
        if (initialized && dialogueManager != null) return true;
        dialogueManager = DialogueSystem.Dialogue.instance;
        if (dialogueManager == null || dialogueManager._handler == null || dialogueManager.dialogueCallbackActions == null) return false;
        dialogueManager.dialogueCallbackActions.OnNodeLeave += OnNodeLeave;
        dialogueManager.dialogueCallbackActions.OnNodeEnter += OnNodeEnter;
        dialogueManager.dialogueCallbackActions.OnDialogueGraphEnd += OnDialogueEnded;
        if (dialogueManager.dictionary != null)
        {
            if (!string.IsNullOrEmpty(test1)) dialogueManager.dictionary.AddEntry(test1, GetDictionaryValue);
            if (!string.IsNullOrEmpty(test2)) dialogueManager.dictionary.AddEntry(test2, "static value");
        }
        dialogueManager.RegisterEventHandler(TestEventHandler);
        initialized = true;
        return true;
    }

    private void Update()
    {
        if (!TryInitialize()) return;
        CheckPlayer();
        if (isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {

            if (!dialogueManager.IsRunning)
            {
                if (graph == null) return;
                if (dialogueManager.dialogueGraph != graph)
                {
                    dialogueManager.dialogueGraph = graph;
                    dialogueManager.Create();
                }
                if (choiceHandler != null) choiceHandler.OnChoiceDraw -= OnChoiceDraw;
                choiceHandler = dialogueManager._handler;
                choiceHandler.OnChoiceDraw += OnChoiceDraw;
                if (flags != null) dialogueManager.flags = flags.gameEventFlags;
                dialogueManager.SetDialogGameState(gameStateVariables);
                dialogueActive = true;
                dialogueManager.StartDialogue();
                gameStateVariables[0].ChangeValue(true);

                // Mostrar o cursor e destravar
                // Cursor.visible = true;
                // Cursor.lockState = CursorLockMode.None;

                // Desativar o controle da câmera
                if (cameraController != null)
                {
                    cameraController.enabled = false;  // Desativa a movimentação da câmera
                }
                if (DialogueHint.instance != null) AtivarHint(DialogueHint.instance.currentLineIndex);
                return; // The start key must not also advance the first line.
            }
        }

        if (!isPlayerNearby)
        {
            if (dialogueActive)
            {
                DesativarHint();
                dialogueManager.StopDialogue();
                dialogueActive = false;
                inputCheck = false;
                StopAllCoroutines();
                if (cameraController != null) cameraController.enabled = true;
            }
            return;
        }

        // Avança o diálogo se ele já estiver ativo
        if (dialogueActive && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !inputCheck && isPlayerNearby)
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

    private void OnDialogueEnded(DialogueGraph endedGraph)
    {
        if (!dialogueActive) return;
        dialogueActive = false;
        inputCheck = false;
        StopAllCoroutines();
        if (choiceHandler != null) choiceHandler.OnChoiceDraw -= OnChoiceDraw;
        choiceHandler = null;
        if (cameraController != null) cameraController.enabled = true;
    }

    public bool OnChoiceDraw(DialogueChoices node)
    {
        if (!dialogueActive || UiHandler == null) return false;
        UiHandler.RenderDialogueChoices(node, dialogueManager);
        return true;
    }

    public string GetDictionaryValue()
    {
        return testDict;
    }

    public void TestEventHandler(DialogueEvent myEvent)
    {
        if (!dialogueActive) return;
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
                    if (npcMovement != null) npcMovement.MoveToPlayer();
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
        if (targetPosition == null) return;
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
        // hint.SetActive(true);
        // DialogueHint.instance.currentLineIndex = fala;
        // DialogueHint.instance.ShowNextDialogueLine(fala);
    }
    private void DesativarHint()
    {
        //hint.SetActive(false);
    }

    void PassarObjetivo()
    {
        gameStateVariables[1].ChangeValue(true);
        PontosManager.instance.condicaoPontos[PontosManager.instance.index].podeAvançar = true;
    }

    IEnumerator CheckInputCoroutine(string key)
    {
        keyboard = Keyboard.current;
        if (keyboard == null || string.IsNullOrEmpty(key)) { inputCheck = false; yield break; }
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
        mouse = Mouse.current;
        if (mouse == null || string.IsNullOrEmpty(button)) { inputCheck = false; yield break; }
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

    private void OnDestroy()
    {
        if (choiceHandler != null) choiceHandler.OnChoiceDraw -= OnChoiceDraw;
        if (dialogueManager == null || !initialized) return;
        dialogueManager.dialogueCallbackActions.OnNodeLeave -= OnNodeLeave;
        dialogueManager.dialogueCallbackActions.OnNodeEnter -= OnNodeEnter;
        dialogueManager.dialogueCallbackActions.OnDialogueGraphEnd -= OnDialogueEnded;
        dialogueManager.DeregisterEventHandler(TestEventHandler);
        if (dialogueActive) dialogueManager.StopDialogue();
    }

}

