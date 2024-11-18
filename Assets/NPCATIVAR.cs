using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    public class NPCATIVAR : MonoBehaviour
    {
        public float interactionRange = 3f; 
        public Transform player;
        public GameObject obj;

        private void Start(){
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }

        private void Update()
        {
            
            if (Vector3.Distance(transform.position, player.position) <= interactionRange)
            {
               
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    Interact();
                }
            }
        }

      
        private void Interact()
        {
           obj.SetActive(false);
        }
    }
}
