using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AtivarSaci : MonoBehaviour
    {
        public BossSpawnerInput Input;
        private bool _active;
        public void OnTriggerEnter(Collider other)
        {
            if(other.CompareTag("Player") && !_active)
            {
                Input.SpawnBoss();
                _active = true;
            }
            
        }
    }
}
