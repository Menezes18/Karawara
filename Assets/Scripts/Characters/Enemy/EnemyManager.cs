using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager i { get; private set; }

        public List<EnemyController> _enemiesRange;

        private void Awake()
        {

            i = this;
        }

        public void AddEnemyInRange(EnemyController enemy)
        {
            if(!_enemiesRange.Contains(enemy))
                _enemiesRange.Add(enemy);
        }

        public void RemoveEnemyInRange(EnemyController enemy)
        {
           
                _enemiesRange.Remove(enemy);
        }
    }
}
