using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AICharacterCombatManager : CharacterCombatManeger
    {
        [Header("Detection")]
        [SerializeField] float detectionRadius = 15;
        [SerializeField] float minimumDetectionAngle = -35;
        [SerializeField] float maximumDetectionAngle = 35;

        public void FindATargetViaLineOfSight(AICharacterManager aiCharacter)
        {
            if (currentTarget != null)
                return;

            Collider[] colliders = Physics.OverlapSphere(aiCharacter.transform.position, detectionRadius, WorldUtilityManager.Instance.GetCharacterLayers());

            for (int i = 0; i < colliders.Length; i++)
            {
                CharacterManager targetCharacter = colliders[i].transform.GetComponent<CharacterManager>();

                if (targetCharacter == null)
                    continue;

                if (targetCharacter == aiCharacter)
                    continue;

                if (targetCharacter.isDead.Value)
                    continue;

                //  CAN I ATTACK THIS CHARACTER, IF SO, MAKE THEM MY TARGET
                if (WorldUtilityManager.Instance.CanIDamageThisTarget(aiCharacter.characterGroup, targetCharacter.characterGroup))
                {
                    //  IF A POTENTIAL TARGET IS FOUND, IT HAS TO BE INFRONT OF US
                    Vector3 targetsDirection = targetCharacter.transform.position - aiCharacter.transform.position;
                    float viewableAngle = Vector3.Angle(targetsDirection, aiCharacter.transform.forward);

                    if (viewableAngle > minimumDetectionAngle && viewableAngle < maximumDetectionAngle)
                    {
                        //  LASTLY, WE CHECK FOR ENVIRO BLOCKS
                        if (Physics.Linecast(
                            aiCharacter.characterCombatManager.lockOnTransform.position, 
                            targetCharacter.characterCombatManager.lockOnTransform.position, 
                            WorldUtilityManager.Instance.GetEnviroLayers()))
                        {
                            Debug.DrawLine(aiCharacter.characterCombatManager.lockOnTransform.position, targetCharacter.characterCombatManager.lockOnTransform.position);
                            Debug.Log("BLOCKED");
                        }
                        else
                        {
                            aiCharacter.characterCombatManager.SetTarget(targetCharacter);
                        }
                    }
                }
            }
        }
    }
}
