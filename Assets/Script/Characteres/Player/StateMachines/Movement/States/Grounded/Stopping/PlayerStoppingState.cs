using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerStoppingState : PlayerGroundedState
    {
        public PlayerStoppingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine){

        }

        public override void Enter()
        {
            base.Enter();

            stateMachine.ReusableData.MovementSpeedModifier = 0f;
        }

        public override void PhysicsUpdate()
        {
            base.PhysicsUpdate();

            if(!IsMovingHorizontally()){
                return;
            }

            DecelerateHorizontally();
        }
    }
}
