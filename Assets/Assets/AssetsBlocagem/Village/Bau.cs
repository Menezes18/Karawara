using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Bau : MonoBehaviour
    {
        private Animator anim;
        public GameObject fog;

        void Start()
        {
            anim = GetComponent<Animator>();
        }
        void OnTriggerEnter(Collider col)
        {
            if(col.tag == "Player" && fog != null){
                anim.SetTrigger("Abrir");
                fog.SetActive(false);
            }
        }
    }
}
