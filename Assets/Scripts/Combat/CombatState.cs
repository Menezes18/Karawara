using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class CombatState : PlayerGroundedState
    {
        

        public CombatState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
            
        }

        public override void Enter()
        {
            ResetVericalVelocity();
            stateMachine.ReusableData.MovementSpeedModifier = movementData.RunData.SpeedModifier;

            base.Enter();
            StartAnimation(stateMachine.Player.AnimationData.RunParameterHash);

            stateMachine.ReusableData.CurrentJumpForce = airborneData.JumpData.MediumForce;

        }

        public override void Exit()
        {
            base.Exit();
            StopAnimation(stateMachine.Player.AnimationData.RunParameterHash);
        }

        public override void Update()
        {
            Debug.Log("AA");
            base.Update();
            stateMachine.ReusableData.MovementSpeedModifier = 1f;
            // Obter a velocidade do jogador a partir do Rigidbody
            Vector3 velocity = Player.instancia.Rigidbody.velocity;
            // Obter a velocidade do jogador no eixo "forward" (frente)
            float forwardSpeed = Vector3.Dot(velocity, Player.instancia.transform.forward);
            // Definir a variável de animação "forwardSpeed"
            Player.instancia.Animator.SetFloat("forwardSpeed", forwardSpeed / velocity.magnitude, 0.2f, Time.deltaTime);

            // Calcular o ângulo entre a frente do jogador e sua velocidade
            float angle = Vector3.SignedAngle(Player.instancia.transform.forward, velocity, Vector3.up);
            // Calcular a velocidade de movimentação lateral (strafe)
            float strafeSpeed = Mathf.Sin(angle * Mathf.Deg2Rad);
            // Definir a variável de animação "strafeSpeed"
            Player.instancia.Animator.SetFloat("strafeSpeed", strafeSpeed, 0.2f, Time.deltaTime);
            
            
            if (!stateMachine.ReusableData.ShouldWalk)
            {
                return;
            }
            
            
            StopRunning();
        }
        private void StopRunning()
        {
            if (stateMachine.ReusableData.MovementInput == Vector2.zero)
            {

                stateMachine.ChangeState(stateMachine.IdlingState);
                
            
                return;
            }
            
            stateMachine.ChangeState(stateMachine.WalkingState);

        }
        public override void PhysicsUpdate()
        {
            base.PhysicsUpdate();
        }
        #region Input Methods

        protected override void OnMovementCanceled(InputAction.CallbackContext context)
        {
            stateMachine.ChangeState(stateMachine.MediumStoppingState);

            base.OnMovementCanceled(context);

        }

        protected override void OnWalkToggleStarted(InputAction.CallbackContext context)
        {
            base.OnWalkToggleStarted(context);

            stateMachine.ChangeState(stateMachine.WalkingState);
        }

        #endregion
    }
}
