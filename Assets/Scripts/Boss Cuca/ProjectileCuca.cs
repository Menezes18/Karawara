using System;
using UnityEngine;

namespace RPGKarawara{
    public class ProjectileCuca : MonoBehaviour{
        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                Destroy(gameObject);
            }
        }
    }
}