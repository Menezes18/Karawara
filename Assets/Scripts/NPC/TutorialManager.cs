using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class TutorialStep
{
    public GameObject waypoint;    
    public string dica;            
    public string popupDica;       
}

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager instance;
    public TutorialStep[] tutorialSteps; 
    public TextMeshProUGUI dicaText;      
    public GameObject tutorialUI;        
    public GameObject popupUI;            
    public TextMeshProUGUI popupText;    
    public float popupDuration = 3f;     
    public float distanceWaypoint;        
    public bool _iniciarTutorial = false; 

    private int currentStep = 0;          
    private bool isTutorialActive = false; 
    private Transform player;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (_iniciarTutorial)
        {
            tutorialUI.SetActive(false);  
            popupUI.SetActive(false);    
            player = GameObject.FindGameObjectWithTag("Player").transform;  
            //ToggleTutorial();
        }
    }

    void Update()
    {
        
    }

    public void ChamarCurrentTutorial()
    {
        if (isTutorialActive && currentStep < tutorialSteps.Length)
        {
                ShowDica();                     
                ShowPopup(tutorialSteps[currentStep].popupDica);
                DeactivateWaypoint(currentStep); 
                currentStep++;                 

                if (currentStep < tutorialSteps.Length)
                {
                    ActivateWaypoint(currentStep);  
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
            ToggleTutorial();  
            _iniciarTutorial = true;
        }
    }

    void ShowDica()
    {
        if (currentStep < tutorialSteps.Length)
        {
            dicaText.text = tutorialSteps[currentStep].dica;  
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
        isTutorialActive = !isTutorialActive;  
        tutorialUI.SetActive(isTutorialActive);  

        if (isTutorialActive)
        {
            currentStep = 0;  
            ShowDica();  
            ActivateWaypoint(currentStep); 
        }
        else
        {
            dicaText.text = "";  
        }
    }

    void ActivateWaypoint(int stepIndex)
    {
        if (stepIndex < tutorialSteps.Length)
        {
            tutorialSteps[stepIndex].waypoint.SetActive(true);  
            DesativarAllIcon(stepIndex); 
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
            popupText.text = message;           
            popupUI.SetActive(true);            
            CancelInvoke(nameof(HidePopup));    
            Invoke(nameof(HidePopup), popupDuration); 
        }
    }

    void HidePopup()
    {
        if (popupUI != null)
        {
            popupUI.SetActive(false);        
        }
    }
}
