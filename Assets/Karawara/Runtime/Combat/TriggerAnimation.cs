using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class TriggerAnimation : MonoBehaviour{
        public Animator anim;
        private void OnTriggerEnter(Collider other){
            anim.SetBool("cair", true);
        }
    }
}
