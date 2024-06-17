using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class SavePositionOnTrigger : MonoBehaviour{
        private Transform savedPostion;

        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                savedPostion.transform.position = transform.position;
            }
        }

        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            if (Keyboard.current.pKey.wasPressedThisFrame){
                var player = FindObjectOfType<PlayerManager>();
                player.transform.position = savedPostion.position;
                player.ReviveCharacter();
            }
        }
    }
}
