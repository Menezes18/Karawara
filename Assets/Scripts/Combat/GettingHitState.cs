using System.Collections;
using System.Collections.Generic;
using RPGKarawara;
using UnityEngine;

namespace RPGKarawara{
    public class GettingHitState : StateEnemy<EnemyController>{
        [SerializeField] float stunnTime = 0.5f;

        EnemyController enemy;

        public override void Enter(EnemyController owner){
            StopAllCoroutines();

            enemy = owner;
            //enemy.Fighter.OnHitComplete += () => StartCoroutine(GoToCombatMovement());
        }

        IEnumerator GoToCombatMovement(){
            yield return new WaitForSeconds(stunnTime);
            enemy.ChangeState(EnemyStates.CombatMovement);
        }
    }
}