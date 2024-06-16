using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }
        public float maxHealth;
        public float defense;
        public float speed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
