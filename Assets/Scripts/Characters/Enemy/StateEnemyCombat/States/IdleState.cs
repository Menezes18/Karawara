using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    public class IdleState : State<EnemyController>
    {
        EnemyController enemy;
        public override void Enter(EnemyController owner)
        {
            enemy = owner;
            Debug.Log("Enter Idle State");
        }

        public override void Execute()
        {
            Debug.Log("Execute Idle State");
            if(Keyboard.current.tKey.wasPressedThisFrame)
            {
                enemy.ChangeState(EnemyStates.Chase);
            }
        }

        public override void Exit()
        {
            Debug.Log("Exit Idle State");
        }
    }

}