using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerIdlingState : PlayerGroundedState
    {
        public PlayerIdlingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {

        }
        #region IState Methods
        public override void Enter()
        {
            base.Enter();
            stateMachine.ReusableData.MovementOnSlopesSpeedModifier = 0;

            ResetVelocity();
        }

        public override void Update()
        {
            base.Update();
            if (stateMachine.ReusableData.MovementInput == Vector2.zero)
            {
                return;
            }
            OnMove();
        }


        #endregion
    }
}
