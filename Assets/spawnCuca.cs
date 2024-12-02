using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class SpawnCuca : MonoBehaviour
    {
        public Transform player;
        public Transform spawnPoint;

        private void Awake()
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }

        void Start()
        {
            if (player != null && spawnPoint != null)
            {
                player.position = spawnPoint.position; 
            }
            
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}