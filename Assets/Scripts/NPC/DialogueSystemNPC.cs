using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGKarawara{
    public class DialogueSystemNPC : MonoBehaviour{
        
        public static DialogueSystemNPC instance;
        public TextMeshProUGUI dialogueText;
        public GameObject dialogueBox;

        private void Awake(){
            instance = this;
            dialogueBox.SetActive(false);
        }

        public void ShowMessage(string message){
            dialogueBox.SetActive(true);
            dialogueText.text = message;
            CancelInvoke(nameof(HideMessage));
            Invoke(nameof(HideMessage), 3f);
        }

        void HideMessage(){
            dialogueBox.SetActive(false);
        }
    }
}