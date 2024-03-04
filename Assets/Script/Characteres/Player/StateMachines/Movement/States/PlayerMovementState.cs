using UnityEngine;

namespace RPGKarawara
{
    public class PlayerMovementState : IState
    {
        protected PlayerMovementStateMachine stateMachine;
        public PlayerMovementState(PlayerMovementStateMachine playerMovementStateMachine) 
        { 
            stateMachine = playerMovementStateMachine;
        }
        public virtual void Enter()
        {      
            Debug.Log("State : " + GetType().Name);
        }      
               
        public virtual void Exit()
        {
            
        }

        public virtual void HandleInput()
        {
            
        }

        public virtual void PhysicsUpdate()
        {
            
        }

        public virtual void Update()
        {
            
        }
    }
}
