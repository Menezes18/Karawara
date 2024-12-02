using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class FadeController : MonoBehaviour
    {
       public Animator animator;

       public void Awake(){
           FadeOut();
       }

       public void FadeOut(){
           animator.SetTrigger("FadeOut");
       }
    }
}
