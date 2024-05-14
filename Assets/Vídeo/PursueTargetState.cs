using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "A.I/States/Pursue Target")]
    public class PursueTargetState : AIState
    {
        public override AIState Tick(AICharacterManager aiCharacter)
        {
            //  CHECK IF WE ARE PERFORMING AN ACTION (IF SO DO NOTHING UNTIL ACTION IS COMPLETE)
            if (aiCharacter.isPerformingAction)
                return this;

            //  CHECK IF OUR TARGET IS NULL, IF WE DO NOT HAVE A TARGET, RETURN TO IDLE STATE
            if (aiCharacter.aiCharacterCombatManager.currentTarget == null)
                return SwitchState(aiCharacter, aiCharacter.idle);

            //  MAKE SURE OUR NAVMESH AGENT IS ACTIVE, IF ITS NOT ENABLE IT
            if (!aiCharacter.navMeshAgent.enabled)
                aiCharacter.navMeshAgent.enabled = true;

            aiCharacter.aiCharacterLocomotionManager.RotateTowardsAgent(aiCharacter);

            //  IF WE ARE WITHIN COMBAT RANGE OF A TARGET, SWITCH STATE TO COMBAT STANCE STATE

            //  IF THE TARGET IS NOT REACHABLE, AND THEY ARE FAR AWAY, RETURN HOME



            //  PURSUE THE TARGET
            //OPTION 01
            //aiCharacter.navMeshAgent.SetDestination(aiCharacter.aiCharacterCombatManager.currentTarget.transform.position);

            //OPTION 02
            NavMeshPath path = new NavMeshPath();
            aiCharacter.navMeshAgent.CalculatePath(aiCharacter.aiCharacterCombatManager.currentTarget.transform.position, path);
            aiCharacter.navMeshAgent.SetPath(path);

            return this;
        }
    }
}
