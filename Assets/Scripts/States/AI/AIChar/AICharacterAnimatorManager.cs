using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AICharacterAnimatorManager : CharacterAnimatorManager
    {
        AICharacterManager aiCharacter;

        protected override void Awake()
        {
            base.Awake();

            aiCharacter = GetComponent<AICharacterManager>();
        }

        private void OnAnimatorMove()
        {
            if (!aiCharacter.IsInitialized || !aiCharacter.characterLocomotionManager.isGrounded) return;
            aiCharacter.characterController.Move(aiCharacter.animator.deltaPosition);
            aiCharacter.transform.rotation *= aiCharacter.animator.deltaRotation;
        }
    }
}
