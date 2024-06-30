using System.Collections;
using UnityEngine;

namespace RPGKarawara
{
    public class StoneMovement : MonoBehaviour{
        public Animator animator;
        void Start()
        {
            // Configura o Animator para usar tempo não escalado
        }

        public void stonemovement(){
            animator = GetComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime; 
        }
    }
}