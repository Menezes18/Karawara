using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class BossAISystem : MonoBehaviour{
        private GameObject player;
        
        Animator animator;
        float detectionRadiusPlayer;
        void Start()
        {
            animator = GetComponent<Animator>();
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
                
                    
                }
            }
        }
    }
}
