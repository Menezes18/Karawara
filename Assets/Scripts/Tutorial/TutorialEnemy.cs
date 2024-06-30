using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class TutorialEnemy : MonoBehaviour
    {
        public float checkRadius = 10f; // Radius of the area to check for enemies
        public int maxEnemies = 4;
        public List<AICharacterManager> enemiesInArea = new List<AICharacterManager>();

        public bool clearArea = false;

        void Update(){
            if (clearArea) return;
            CheckEnemiesInArea();
            RemoveExitedOrDeadEnemies();
        }

        void CheckEnemiesInArea(){
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, checkRadius);

            foreach (var hitCollider in hitColliders)
            {
                AICharacterManager enemy = hitCollider.GetComponent<AICharacterManager>();
                if (enemy != null && !enemiesInArea.Contains(enemy) && !enemy.isDead.Value)
                {
                    enemiesInArea.Add(enemy);
                }
            }

            if (enemiesInArea.Count >= maxEnemies)
            {
                Debug.Log("There are 5 or more enemies in the area.");
            }
            else if (enemiesInArea.Count == 0){
                clearArea = true;
                Debug.Log("Area cleared.");
            }
            else
            {
                Debug.Log("Total enemies in the area: " + enemiesInArea.Count);
            }
        }

        void RemoveExitedOrDeadEnemies()
        {
            enemiesInArea.RemoveAll(enemy =>
            {
                if (enemy == null || enemy.isDead.Value)
                {
                    Debug.Log($"Removing enemy {enemy.name} from the area.");
                    return true;
                }
                return false;
            });
        }

        void OnDrawGizmosSelected()
        {
            // Draw a red sphere at the transform's position to show the check radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, checkRadius);
        }
    }
}
