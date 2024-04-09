using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

namespace RPGKarawara
{
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager i { get; private set; }
        [SerializeField] Vector2 timeRangeBetweenAttacks = new Vector2(1, 4);
        public List<EnemyController> enemiesInRange = new List<EnemyController>();
        private float notAttackingTimer = 2;

        private void Awake()
        {
            i = this;
        }

        public void AddEnemyInRange(EnemyController enemy)
        {
            if(!enemiesInRange.Contains(enemy))
                enemiesInRange.Add(enemy);
        }
        
        public void RemoveEnemyInRange(EnemyController enemy)
        {
            
                enemiesInRange.Remove(enemy);
        }

        private void Update()
        {
            if(enemiesInRange.Count == 0)
                return;
            if (!enemiesInRange.Any(e => e.IsInState(EnemyStates.Attack)))
            {
                if(notAttackingTimer > 0)
                    notAttackingTimer -= Time.deltaTime;

                if (notAttackingTimer <= 0)
                {
                    var attackingEnemy = SelectEnemyForAttack();
                    attackingEnemy.ChangeState(EnemyStates.Attack);
                    notAttackingTimer = UnityEngine.Random.Range(timeRangeBetweenAttacks.x, timeRangeBetweenAttacks.y);
                }
                
            }
        }

        EnemyController SelectEnemyForAttack()
        {
            return enemiesInRange.OrderByDescending(e => e.CombatMovementTimer).FirstOrDefault();
        }
        public EnemyController GetAttackingEnemy()
        {
            return enemiesInRange.FirstOrDefault(e => e.IsInState(EnemyStates.Attack));
        }
    }
}
