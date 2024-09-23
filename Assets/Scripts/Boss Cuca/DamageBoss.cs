using System;
using UnityEngine;

namespace RPGKarawara{
    public class DamageBoss : MonoBehaviour{
        
        public Collider damageCollider;
        public bool canDamage;
        public void Start(){
            
            damageCollider.isTrigger = true;
            damageCollider.enabled = false;
        }

        public void AtivarCollider(){
            damageCollider.enabled = true;
        }        
        public void DesativarCollider(){
            damageCollider.enabled = false;
        }
        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                canDamage = true;
            }
        }
    }
}