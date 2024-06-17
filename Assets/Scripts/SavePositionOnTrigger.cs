using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class SavePositionOnTrigger : MonoBehaviour{
        public Transform savedPostion;

        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                savedPostion = this.transform;
            }
        }

        
        public void Reviver()
        {
           
                var player = FindObjectOfType<PlayerManager>();
                player.transform.position = savedPostion.position;
                player.ReviveCharacter();
            
        }
        // Update is called once per frame
        
    }
}
