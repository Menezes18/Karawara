using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class PlayerRunningState : PlayerMovingState
    {
        private PlayerSprintData spintData;
        private float starTime;

        public PlayerRunningState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine){
            spintData = movementData.SprintData;
        }

        #region IState Methods
        public override void Enter()
        {
            base.Enter();

            stateMachine.ReusableData.MovementOnSlopesSpeedModifier = movementData.RunData.SpeedModifier;

            starTime = Time.time;
        }

        public override void Update()
        {
            base.Update();

            if(!stateMachine.ReusableData.ShouldWalk){
                return;
            }

            if(Time.time < starTime + spintData.RunToWalkTime){
                return;
            }

            StopRunning();
        }
        #endregion

        #region Main Methods
        private void StopRunning(){
            if(stateMachine.ReusableData.MovementInput == Vector2.zero){
                stateMachine.ChangeState(stateMachine.IdlingState);

                return;
            }

            stateMachine.ChangeState(stateMachine.WalkingState);
        }
        #endregion

        #region Input Methods

        protected override void OnWalkToggleStarted(InputAction.CallbackContext context)
        {
            base.OnWalkToggleStarted(context);
            stateMachine.ChangeState(stateMachine.WalkingState);
        }
        #endregion

    }
}
