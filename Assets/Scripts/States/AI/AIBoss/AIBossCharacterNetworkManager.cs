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
            currentHealth.Value = 3000;
            maxHealth.Value = 3000;
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
            // A health callback can arrive while the scene's boss is being despawned.
            if (aiBossCharacter == null || !aiBossCharacter.IsSpawned) return;
            base.CheckHP(oldValue, newValue);

            if (aiBossCharacter.IsOwner)
            {
                if (currentHealth.Value <= 0)
                {
                    if (AudioController.AudioInstance != null)
                        AudioController.AudioInstance.PlayBackgroundMusic();
                    return;
                }

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
