using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueSystem;

public class NPCDialogue : MonoBehaviour{
    public DialogueGraph npc1Graph;
    public DialogueGraph npc2Graph;

    private DialogueHandler dialogueHandler;

    private void Start(){
    }

    void Update(){
    }

    private void OnTriggerEnter(Collider other){
        if (other.CompareTag("Player")){

            DialogueSystem.Dialogue.instance._handler.EndDialogue();
        }
    }
}
