using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CharacterStatsManager : MonoBehaviour
    {
        CharacterManager character;

       // [Header("Stamina Regeneration")]
        //private float staminaRegenerationTimer = 0;
        //private float staminaTickTimer = 0;

        protected virtual void Awake()
        {
            character = GetComponent<CharacterManager>();
        }

        protected virtual void Start()
        {

        }

        public int CalculateHealthBasedOnVitalityLevel(int vitality)
        {
            float health = 0;

            //  CREATE AN EQUATION FOR HOW YOU WANT YOUR STAMINA TO BE CALCULATED

            health = vitality * 15;

            return Mathf.RoundToInt(health);
        }

    }
}
