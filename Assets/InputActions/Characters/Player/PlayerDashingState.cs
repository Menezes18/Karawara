using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerDashingState : PlayerGroundedState
    {
        private PlayerDashData dashData;

        public PlayerDashingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine){
            dashData = movementData.DashData;
        }

        #region IState Methods
        public override void Enter()
        {
            base.Enter();

            stateMachine.ReusableData.MovementSpeedModifier = movementData.DashData.SpeedModifier;

            AddForceOnTransitionFromStationaryState();
        }
        #endregion

        #region Main Methods
        private void AddForceOnTransitionFromStationaryState(){
            if(stateMachine.ReusableData.MovementInput != Vector2.zero){
                return;
            }

            Vector3 characterRotationDirection = stateMachine.Player.transform.forward;

            characterRotationDirection.y = 0f;

            stateMachine.Player.Rigidbody.velocity = characterRotationDirection * GetMovementSpeed();
        }
        #endregion
    }
}
