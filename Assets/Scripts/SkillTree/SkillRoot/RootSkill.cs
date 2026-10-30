using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class RootSkill : MonoBehaviour
    {
        public GameObject prefabToSpawn; 
        public float detectionRange = 5f; 
        public float destroyAllDelay = 10f; 
        public LayerMask enemyLayer; // Define a camada dos inimigos para detectar apenas os desejados

        public bool active = true;
        private List<GameObject> spawnedPrefabs = new List<GameObject>(); 
        [SerializeField]
        private List<AICharacterManager> affectedEnemies = new List<AICharacterManager>();
        private bool isDestroying = false; 

        void Start()
        {
            active = true;
            //StartCoroutine(DestroyAllPrefabsAfterDelay());
        }

        void Update()
        {
            if (!active) return;

            ApplyEffectToEnemies();
        }

        private void ApplyEffectToEnemies()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRange, enemyLayer);
            foreach (Collider collider in hitColliders)
            {
                AICharacterManager enemy = collider.GetComponent<AICharacterManager>();

                if (enemy != null && !affectedEnemies.Contains(enemy))
                {
                    enemy.isDisabled = true;
                    affectedEnemies.Add(enemy);

                    if (!HasPrefabForEnemy(enemy.gameObject))
                    {
                        Vector3 spawnPosition = enemy.transform.position;
                        GameObject spawnedPrefab = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, enemy.transform);
                        spawnedPrefabs.Add(spawnedPrefab);
                    }
                }
            }
        }

        private bool HasPrefabForEnemy(GameObject enemy)
        {
            foreach (GameObject prefab in spawnedPrefabs)
            {
                if (prefab != null && prefab.transform.parent == enemy.transform)
                {
                    return true;
                }
            }
            return false;
        }

        public IEnumerator DestroyAllPrefabsAfterDelay()
        {
            yield return new WaitForSeconds(destroyAllDelay);

            foreach (GameObject prefab in spawnedPrefabs)
            {
                if (prefab != null)
                {
                    Destroy(prefab);
                }
            }

            foreach (AICharacterManager enemy in affectedEnemies)
            {
                if (enemy != null)
                {
                    enemy.isDisabled = false; // Reativa os inimigos afetados
                }
            }

            spawnedPrefabs.Clear();
            affectedEnemies.Clear();
            active = false;
            Debug.Log("Todos os prefabs foram destruídos");
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
