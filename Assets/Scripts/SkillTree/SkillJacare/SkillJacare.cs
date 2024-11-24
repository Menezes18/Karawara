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

        [SerializeField]
        private List<AICharacterManager> affectedEnemies = new List<AICharacterManager>();
        private Transform _playerTransform;

        private float skillStartTime;
        private bool shouldDeactivateAll = false;

        private void Awake()
        {
            _playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
            transform.SetParent(_playerTransform);
            transform.localPosition = Vector3.zero;
        }

        private void Start()
        {
            ActivateSkill();
        }

        public void ActivateSkill()
        {
            skillStartTime = Time.time;
            ApplySkillEffect();
        }

        private void Update()
        {
            float elapsedTime = Time.time - skillStartTime;

            if (elapsedTime >= skillDuration - 1.5f && !shouldDeactivateAll)
            {
                DeactivateAllEnemies();
                
            }
            else{
                ApplySkillEffect();
            }

            if (elapsedTime >= skillDuration)
            {
                DeactivateSkill();
            }

           
        }

        private void ApplySkillEffect()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, skillRadius, enemyLayer);
            foreach (Collider collider in hitColliders)
            {
                AICharacterManager enemy = collider.GetComponent<AICharacterManager>();
                if (enemy != null && !affectedEnemies.Contains(enemy))
                {
                    enemy.isDisabled = true;
                    affectedEnemies.Add(enemy);
                }
            }
        }

        private void DeactivateAllEnemies()
        {
            foreach (AICharacterManager enemy in affectedEnemies)
            {
                if (enemy != null)
                {
                    enemy.isDisabled = false;
                }
            }
            affectedEnemies.Clear();
        }

        private void DeactivateSkill()
        {
            affectedEnemies.Clear(); // Limpa a lista para evitar reativação de objetos destruídos
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, skillRadius);
        }
    }
}
