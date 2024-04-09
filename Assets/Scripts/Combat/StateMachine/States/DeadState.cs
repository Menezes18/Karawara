using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DeadState : StateEnemy<EnemyController>
    {
        public override void Enter(EnemyController owner)
        {
            owner.VisionSensor.gameObject.SetActive(false);
            EnemyManager.i.RemoveEnemyInRange(owner);

            owner.NavAgent.enabled = false;
            owner.CharacterController.enabled = false;
        }
    }
}
