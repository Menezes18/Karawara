using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    

    public class ParticleActivator : MonoBehaviour
    {
        public GameObject[] Particle;
        void Start()
        {

        }

        // Update is called once per frame
        private void OnTriggerEnter(Collider collision)
        {
            if (collision.CompareTag("Player"))
            {
                foreach(GameObject particula in Particle)
                {
                    particula.SetActive(true);
                    Destroy(particula,3f);
                }
            }
        }
    }
}
