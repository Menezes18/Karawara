using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class OnTriggerSkill : MonoBehaviour
    {
        public Skill novaSkill;
        public void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                AtivarSkillPop.instance.AtivarSkill(novaSkill);
                other.GetComponentInParent<PlayerLocomotionManager>().StopPlayer();
                Destroy(gameObject);
            }
        }
    }
}
