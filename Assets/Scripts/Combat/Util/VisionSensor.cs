using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class VisionSensor : MonoBehaviour
    {
        [SerializeField] private EnemyController enemy;
        private void OnTriggerEnter(Collider other)
        {
           var fighter = other.GetComponent<MeeleFighter>();

           if (fighter != null)
           {
               enemy.TargetInRange.Add(fighter);
               EnemyManager.i.AddEnemyInRange(enemy);
           }
        }

        private void OnTriggerExit(Collider other)
        {
            var fighter = other.GetComponent<MeeleFighter>();

            if (fighter != null)
            {
                enemy.TargetInRange.Remove(fighter);
                EnemyManager.i.RemoveEnemyInRange(enemy);
            }
        }
    }
}
