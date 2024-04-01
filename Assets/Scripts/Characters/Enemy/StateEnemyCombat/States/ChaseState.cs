using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class ChaseState : State<EnemyController>
    {
        public override void Enter(EnemyController owner)
        {
            Debug.Log(" Enter Chase State");
        }

        public override void Execute()
        {
            Debug.Log("Execute Chase State");
        }

        public override void Exit()
        {
            Debug.Log("Exit Chase State");
        }
    }
}
