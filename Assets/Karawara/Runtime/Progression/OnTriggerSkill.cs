using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class OnTriggerSkill : MonoBehaviour
    {
        public Skill novaSkill;
        private bool skillAtivada = false; 

        public void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player") && !skillAtivada) {
                skillAtivada = true; 
                AtivarSkillPop.instance.AtivarSkill(novaSkill);
                other.GetComponentInParent<PlayerLocomotionManager>().StopPlayer();
                Destroy(gameObject);
            }
        }
    }
}