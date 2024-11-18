using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


    public class WaypointIcon : MonoBehaviour
    {
        public GameObject waypointIcon;
        public string waypointName;
        
        public void Start(){
            waypointIcon =  GameObject.Find(waypointName);
        }

        private void FixedUpdate(){
            if (waypointName == null){
                waypointIcon =  GameObject.Find(waypointName);
            }
        }

        public void AtivarIcon(){
            
            
            waypointIcon.SetActive(true);
        }
        public void DesativarIcon(){
            
            
                waypointIcon.SetActive(false);
                
            
        }
        
    }

