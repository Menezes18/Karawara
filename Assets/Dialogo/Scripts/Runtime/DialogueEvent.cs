using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    public enum EventTypeDialogue
    {
        MoveNPCToNextWaypoint,
        CheckInput,
        MoveNPCToPlayer,
        CheckPlayerPosition,
        EnableUI,
        DisableUI,
        TutorialCompleto,
        CheckMouseInput
    }


namespace DialogueSystem
{
    [Serializable]
    public class DialogueEvent
    {
        public EventTypeDialogue eventName;
        public int intParameter;
        public float floatParameter;
        public string stringParameter;
        public bool boolParameter;
        public string inputKey;
       // public GameObject checkPlayerPosition;


    }
}
