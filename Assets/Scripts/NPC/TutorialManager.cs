using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class TutorialStep
{
    public GameObject waypoint;    // O waypoint
    public string dica;            // Dica associada a este waypoint
    public string popupDica;       // Dica adicional para exibir no popup
}

public class TutorialManager : MonoBehaviour
{
    public TutorialStep[] tutorialSteps;  // Lista de etapas do tutorial
    public TextMeshProUGUI dicaText;      // Texto que exibirá as dicas
    public GameObject tutorialUI;         // Interface do tutorial
    public GameObject popupUI;            // Interface do popup
    public TextMeshProUGUI popupText;     // Texto do popup
    public float popupDuration = 3f;      // Duração do popup na tela
    public float distanceWaypoint;        // Distância de ativação do waypoint
    public bool _iniciarTutorial = false; // Controle para iniciar o tutorial uma vez

    private int currentStep = 0;          // Etapa atual do tutorial
    private bool isTutorialActive = false; // Indica se o tutorial está ativo
    private Transform player;             // Referência ao jogador

    void Start()
    {
        if (_iniciarTutorial)
        {
            tutorialUI.SetActive(false);  // Desativa a UI do tutorial inicialmente
            popupUI.SetActive(false);     // Desativa o popup inicialmente
            player = GameObject.FindGameObjectWithTag("Player").transform;  // Encontra o jogador
            //ToggleTutorial();
        }
    }

    void Update()
    {
        if (isTutorialActive && currentStep < tutorialSteps.Length)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            float distance = Vector3.Distance(player.position, tutorialSteps[currentStep].waypoint.transform.position);

            if (distance < distanceWaypoint)  // Se o jogador está perto o suficiente do waypoint
            {
                ShowDica();                     // Exibe a dica do waypoint
                ShowPopup(tutorialSteps[currentStep].popupDica); // Exibe o popup se houver dica adicional
                DeactivateWaypoint(currentStep);  // Desativa o ícone do waypoint atual
                currentStep++;                  // Avança para o próximo waypoint

                if (currentStep < tutorialSteps.Length)
                {
                    ActivateWaypoint(currentStep);  // Ativa o ícone do próximo waypoint
                }
                else{
                    // dicaText.text = "Vá até a vila, descubra onde o Mapinguari se esconde e derrote-o!";
                    // ShowPopup("Vá até a vila, descubra onde o Mapinguari se esconde e derrote-o!");
                    // isTutorialActive = false;  // Desativa o tutorial
                    // Invoke("Acabou", 4f);
                    
                }
            }
        }
    }

    public void Acabou(){
        dicaText.text = "";
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_iniciarTutorial)
        {
            ToggleTutorial();  // Inicia o tutorial quando o jogador entra na área
            _iniciarTutorial = true;
        }
    }

    void ShowDica()
    {
        if (currentStep < tutorialSteps.Length)
        {
            dicaText.text = tutorialSteps[currentStep].dica;  // Exibe a dica da etapa atual
        }
    }

    public void DesativarAllIcon(int active)
    {
        for (int i = 0; i < tutorialSteps.Length; i++)
        {
            if (i == active)
            {
                tutorialSteps[i].waypoint.GetComponent<WaypointIcon>().AtivarIcon();
                tutorialSteps[i].waypoint.SetActive(true);
            }
            else
            {
                tutorialSteps[i].waypoint.GetComponent<WaypointIcon>().DesativarIcon();
                tutorialSteps[i].waypoint.SetActive(false);
            }
        }
    }

    void ToggleTutorial()
    {
        isTutorialActive = !isTutorialActive;  // Alterna o estado do tutorial
        tutorialUI.SetActive(isTutorialActive);  // Ativa ou desativa a UI do tutorial

        if (isTutorialActive)
        {
            currentStep = 0;  // Começa do primeiro waypoint
            ShowDica();  // Exibe a primeira dica
            ActivateWaypoint(currentStep);  // Ativa o primeiro waypoint
        }
        else
        {
            dicaText.text = "";  // Limpa a dica quando o tutorial é desativado
        }
    }

    void ActivateWaypoint(int stepIndex)
    {
        if (stepIndex < tutorialSteps.Length)
        {
            tutorialSteps[stepIndex].waypoint.SetActive(true);  // Ativa o waypoint atual
            DesativarAllIcon(stepIndex); // Ativa o ícone do waypoint
        }
    }

    void DeactivateWaypoint(int stepIndex)
    {
        if (stepIndex < tutorialSteps.Length)
        {
            tutorialSteps[stepIndex].waypoint.GetComponent<WaypointIcon>().DesativarIcon();
            tutorialSteps[stepIndex].waypoint.SetActive(false);
        }
    }

    void ShowPopup(string message)
    {
        if (!string.IsNullOrEmpty(message) && popupUI != null)
        {
            popupText.text = message;           // Define a mensagem no popup
            popupUI.SetActive(true);            // Ativa o popup
            CancelInvoke(nameof(HidePopup));    // Cancela qualquer ocultação agendada anterior
            Invoke(nameof(HidePopup), popupDuration); // Oculta o popup após o tempo definido
        }
    }

    void HidePopup()
    {
        if (popupUI != null)
        {
            popupUI.SetActive(false);           // Desativa o popup
        }
    }
}
