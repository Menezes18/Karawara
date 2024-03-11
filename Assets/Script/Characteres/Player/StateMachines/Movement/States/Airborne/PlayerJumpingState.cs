using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerJumpingState : PlayerAirborneState
    {
        public PlayerJumpingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {

        }
        #region IState Methods
        public override void Enter()
        {
            base.Enter();
            stateMachine.ReusableData.MovementSpeedModifier = 0;

            Jump();
        }
        #endregion
        #region Main Methods
        private void Jump()
        {
            Vector3 jumpFoce = stateMachine.ReusableData.CurrentJumpForce;
            Vector3 playerForward = stateMachine.Player.transform.forward;

            jumpFoce.x *= playerForward.x;
            jumpFoce.z *= playerForward.z;

            ResetVelocity();

            stateMachine.Player.Rigidbody.AddForce(jumpFoce, ForceMode.VelocityChange);

        }


        #endregion

        #region Input Methods

        #endregion
    }
}
