using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "A.I/States/Idle")]
    public class IdleState : AIState
    {
        public override AIState Tick(AICharacterManager aiCharacter)
        {
            if (aiCharacter.characterCombatManager.currentTarget != null)
            {
                //  RETURN THE PURSUE TARGET STATE (CHANGE THE STATE TO THE PURSUE TARGET STATE)
                Debug.Log("WE HAVE A TARGET");

                return this;
            }
            else
            {
                //  RETURN THIS STATE, TO CONTINUALLY SEARCH FOR A TARGET (KEEP THE STATE HERE, UNTIL A TARGET IS FOUND)
                aiCharacter.aiCharacterCombatManager.FindATargetViaLineOfSight(aiCharacter);
                Debug.Log("SEARCHING FOR A TARGET");
                return this;
            }
        }
    }
}
