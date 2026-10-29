using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AtivarSaci : MonoBehaviour
    {
        public BossSpawnerInput Input;
        public void Start()
        {
            Input.SpawnBoss();
            
        }
    }
}
