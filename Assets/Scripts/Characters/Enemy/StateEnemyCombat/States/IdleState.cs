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
            foreach(var target in enemy.TargetsInRange)
            {
                var vecToTarget = target.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, vecToTarget);

                if(angle <= enemy.Fov / 2)
                {
                    Debug.Log("EMntou");
                    enemy.Target = target;
                    enemy.ChangeState(EnemyStates.CombatMovement);
                    break;
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("Exit Idle State");
        }
    }

}