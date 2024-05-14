using RPGKarawara;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AICharacterManager : CharacterManager
    {
        public AICharacterCombatManager aiCharacterCombatManager;

        [Header("Current State")]
        [SerializeField] AIState currentState;

        protected override void Awake()
        {
            base.Awake();

            aiCharacterCombatManager = GetComponent<AICharacterCombatManager>();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            ProcessStateMachine();
        }

        //  OPTION 01
        private void ProcessStateMachine()
        {
            AIState nextState = currentState?.Tick(this);

            if (nextState != null)
            {
                currentState = nextState;
            }
        }
    }
}
