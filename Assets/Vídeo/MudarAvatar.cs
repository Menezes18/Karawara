using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

        public Avatar jabutiAvatar;
        public RuntimeAnimatorController jabutiAnimator;

        public GameObject persona;
        public GameObject animal;
        public GameObject jabutiGameobject;

        public bool change = false;

        private void Awake(){
            instancia = this;
        }

        
        public void TrocarBear(){
            animatorMudar.runtimeAnimatorController = animator2;
            animatorMudar.avatar = avatar2;
            persona.SetActive(false);
            animal.SetActive(true);
            jabutiGameobject.SetActive(false);
        }

        public void TrocarPlayer(){
            change = false;
            animatorMudar.runtimeAnimatorController = animator1;
          
            animatorMudar.avatar = avatar1;
            persona.SetActive(true);
            animal.SetActive(false);
            jabutiGameobject.SetActive(false);
            
        }

        public void TrocarJabuti(){
            change = true;
            animatorMudar.runtimeAnimatorController = jabutiAnimator;
          
            animatorMudar.avatar = jabutiAvatar;
            
            jabutiGameobject.SetActive(true);
            persona.SetActive(false);
            animal.SetActive(false);
        } 
        void Update()
        {
            if (Keyboard.current.pKey.wasPressedThisFrame){
                var player = FindObjectOfType<PlayerManager>();
                
                player.ReviveCharacter();
            }
            
        }
    }
}
