using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class StateMachineEnemy<T>
    {
        private T _owner;
        public StateEnemy<T> CurrentState { get; private set; }

        public StateMachineEnemy(T owner)
        {
            _owner = owner;
        }
        
        public void ChangeState(StateEnemy<T> newState)
        {
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter(_owner);
        }

        public void Execute()
        {
            CurrentState?.Execute();
        }
    }
}


