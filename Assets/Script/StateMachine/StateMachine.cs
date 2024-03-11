

using UnityEngine;

namespace RPGKarawara
{
    public abstract class StateMachine
    {

        protected IState _currentState;


        public void ChangeState(IState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
        }

        public void HandleInput()
        {
            _currentState?.HandleInput();
        }
        public void Update()
        {
            _currentState?.Update();
        }
        public void PhysicsUpdate()
        {
            _currentState?.PhysicsUpdate();
        }

        public void OnAnimationEnterEvent(){
            _currentState?.OnAnimationEnterEvent();
        }
        
        public void OnAnimationExitEvent(){
            _currentState?.OnAnimationExitEvent();
        }
        
        public void OnAnimationTransitionEvent(){
            _currentState?.OnAnimationTransitionEvent();
        }
        public void OnTriggerEnter(Collider collider)
        {
            _currentState.OnTriggerEnter(collider);
        }
    }
}
