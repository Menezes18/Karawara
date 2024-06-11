using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class MudarAvatar : MonoBehaviour{
        public static MudarAvatar instancia;

        public Animator animatorMudar;
        
        public Avatar avatar1;
        public RuntimeAnimatorController animator1;
        
        public Avatar avatar2; 
        public RuntimeAnimatorController animator2;


        public GameObject persona;
        public GameObject animal;

        public bool change = false;

        private void Awake(){
            instancia = this;
        }

        public void TrocarBear(){
            animatorMudar.runtimeAnimatorController = animator2;
            animatorMudar.avatar = avatar2;
            persona.SetActive(false);
            animal.SetActive(true);
            
        }

        public void TrocarPlayer(){
            animatorMudar.runtimeAnimatorController = animator1;
          
            animatorMudar.avatar = avatar1;
            persona.SetActive(true);
            animal.SetActive(false);
        }
       
        void Update()
        {
            
            
        }
    }
}
