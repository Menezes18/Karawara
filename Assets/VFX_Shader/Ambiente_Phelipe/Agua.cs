using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class Agua : MonoBehaviour
    {
        public GameObject Player;
        public GameObject Particle;

        void Start()
        {
            Player = GameObject.FindWithTag("Player");
            if (Particle == null)
            {
                Particle.SetActive(false);
            }
        }
        void OnTriggerEnter(Collider col)
        {
            if(col.CompareTag("Player") && Particle != null)
            {
                Particle.SetActive(true);
            }
        }
        void OnTriggerExit(Collider col)
        {
            Particle.SetActive(false);
        }
    }
}
