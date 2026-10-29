using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{

    public class Plataforma : MonoBehaviour
    {

        public bool isPlayerOnPlatform = false;
        public float timer = 0f;
        public float timeToDestruct = 3f;
        public bool isPlatformActive = true;

        public void Start(){
          isPlatformActive = true;
        }

        private void Update()
        {
            if (isPlayerOnPlatform)
            {
                timer += Time.deltaTime;

                if (timer >= timeToDestruct && isPlatformActive)
                {
                    // Desativa a plataforma
                    gameObject.SetActive(false);
                    isPlatformActive = false;
                    timer = 0f;

                    
                   
                }
            }
            else
            {
                // Reseta o timer se o player sair da plataforma
                timer = 0f;
            }
        }

        public virtual void OnTriggerEnter(Collider other){
            Debug.Log(other);
            PlayerManager player = other.GetComponent<PlayerManager>();

            if(player != null)
                isPlayerOnPlatform = true;
                        
                    
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerOnPlatform = false;
            }
        }

       

        
    }

    }

