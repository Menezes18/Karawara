using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CharacterStatsManager : MonoBehaviour
    {
        CharacterManager character;
        

        [Header("Blocking Absorptions")]
        public float blockingPhysicalAbsorption = 100;
        public float blockingFireAbsorption = 100;
        public float blockingMagicAbsorption = 100;
        public float blockingLightningAbsorption = 100;
        public float blockingHolyAbsorption = 100;

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

        public int CalculateStaminaBasedOnEnduranceLevel(int endurance)
        {
            float stamina = 0;

            //  CREATE AN EQUATION FOR HOW YOU WANT YOUR STAMINA TO BE CALCULATED

            stamina = endurance * 10;

            return Mathf.RoundToInt(stamina);
        }
        
    }
}
