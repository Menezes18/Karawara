using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class SavePositionOnTrigger : MonoBehaviour{
        
        public Transform[] savedPostion;
        public int numberSave;

        

        
        public void Reviver()
        {
           
                var player = FindObjectOfType<PlayerManager>();
                player.transform.position = savedPostion[numberSave].position;
                player.ReviveCharacter();
            
        }
        // Update is called once per frame
        
    }
}
