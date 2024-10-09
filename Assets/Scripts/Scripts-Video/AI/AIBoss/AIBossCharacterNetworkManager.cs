using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AIBossCharacterNetworkManager : AICharacterNetworkManager
    {
        AIBossCharacterManager aiBossCharacter;
        private bool hasShifted = false;
        public bool fogoBoss = false;
        protected override void Awake()
        {
            base.Awake();

            aiBossCharacter = GetComponent<AIBossCharacterManager>();
            currentHealth.Value = 9999;
            maxHealth.Value = 9999;
            // if (fogoBoss){
            //     currentHealth.Value = 1500;
            //     maxHealth.Value = 1500;
            // }
            // else{
            //     currentHealth.Value = 3000;
            //     maxHealth.Value = 3000;
            //     
            // }
        }

        public override void CheckHP(int oldValue, int newValue)
        {
            base.CheckHP(oldValue, newValue);

            if (aiBossCharacter.IsOwner)
            {
                if (currentHealth.Value <= 0)
                    return;

                float healthNeededForShift = maxHealth.Value * (aiBossCharacter.minimumHealthPercentageToShift / 100);

                if (currentHealth.Value <= healthNeededForShift && !hasShifted)
                {
                    aiBossCharacter.PhaseShift();
                    hasShifted = true;
                }
            }
        }
    }
}
