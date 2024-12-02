using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


    public class WaypointIcon : MonoBehaviour
    {
        public GameObject waypointIcon;
        public string waypointName;
        public bool showWaypointIcon;
        
        public bool isActive;
        public void Start(){
            waypointIcon =  GameObject.Find(waypointName);
        }
        private void FixedUpdate(){
            if (waypointName == null){
                waypointIcon =  GameObject.Find(waypointName);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !isActive)
            {
                isActive = true;
                TutorialManager.instance.ChamarCurrentTutorial();
            }
        }

        public void AtivarIcon(){
            waypointIcon.SetActive(true);
        }
        public void DesativarIcon(){
            waypointIcon.SetActive(false);
        }
        
    }

