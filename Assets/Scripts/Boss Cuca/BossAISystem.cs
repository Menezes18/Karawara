using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{

    public class BossAISystem : MonoBehaviour{
        
        public AudioClip Risada;
        
        
        
        
        
        
        private GameObject player;
        private AudioSource audio;
        Animator animator;
        float detectionRadiusPlayer;
        void Start()
        {
            animator = GetComponent<Animator>();
            audio = GetComponent<AudioSource>();
            
            
            player = GameObject.FindGameObjectWithTag("Player");
        }

        // Update is called once per frame
        void Update()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadiusPlayer);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    
                    audio.PlayOneShot(Risada);
                    
                    
                }
            }
        }
    }
}
