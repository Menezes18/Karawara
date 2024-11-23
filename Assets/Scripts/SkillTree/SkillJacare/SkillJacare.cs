using System;
using System.Collections;
using System.Collections.Generic;
using RPGKarawara.SkillTree;
using UnityEngine;

namespace RPGKarawara
{
    public class SkillJacare : MonoBehaviour
    {
        public float skillRadius = 10f;
        public float skillDuration = 5f;
        public LayerMask enemyLayer;

        private List<AICharacterManager> affectedEnemies = new List<AICharacterManager>();
        private Transform _playerTransform;


        private void Awake(){
            _playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
            transform.SetParent(_playerTransform);
        }

        private void Start(){
            
            ActivateSkill();
        }

        public void ActivateSkill()
        {
            StartCoroutine(SkillEffect());
        }

        private IEnumerator SkillEffect()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, skillRadius, enemyLayer);

            foreach (Collider collider in hitColliders)
            {
                AICharacterManager enemy = collider.GetComponent<AICharacterManager>();
                if (enemy != null)
                {
                    enemy.isDisabled = true;
                    affectedEnemies.Add(enemy);
                }
            }

            yield return new WaitForSeconds(skillDuration);

            foreach (AICharacterManager enemy in affectedEnemies)
            {
                if (enemy != null)
                {
                    enemy.isDisabled = false;
                }
            }

            affectedEnemies.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, skillRadius);
        }
    }
}
