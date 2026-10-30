using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    
    public class SaciWhirlwindDamage : DamageCollider
    {
        [SerializeField] AISaciCharacterManager saciCharacterManager;
        [SerializeField] GameObject tornadoPrefab;  // Prefab do tornado a ser instanciado
        [SerializeField] int numberOfTornadoes = 3; // Quantidade de tornados
        [SerializeField] float tornadoSpeed = 5.0f; // Velocidade de movimento dos tornados
        [SerializeField] float spawnRadius = 2.0f;  // Raio de onde os tornados serão instanciados ao redor do Saci
        [SerializeField] float tornadoLifetime = 5.0f; // Tempo de vida dos tornados

        protected override void Awake()
        {
            base.Awake();
            saciCharacterManager = GetComponentInParent<AISaciCharacterManager>();
        }

        public void WhirlwindAttack(Vector3 playerPosition)
        {
            // Instancia múltiplos tornados ao redor do Saci
            for (int i = 0; i < numberOfTornadoes; i++)
            {
                // Gera uma posição aleatória ao redor do Saci para instanciar o tornado
                Vector3 spawnPosition = saciCharacterManager.transform.position + (Random.insideUnitSphere * spawnRadius);
                spawnPosition.y = saciCharacterManager.transform.position.y; // Mantém a altura dos tornados na mesma do Saci

                // Instancia o tornado
                GameObject tornado = Instantiate(tornadoPrefab, spawnPosition, Quaternion.identity);

                // Move o tornado na direção do player
                StartCoroutine(MoveTornadoTowardsPlayer(tornado, playerPosition));

                // Destroi o tornado após um tempo
                Destroy(tornado, tornadoLifetime);
            }
        }

        private IEnumerator MoveTornadoTowardsPlayer(GameObject tornado, Vector3 playerPosition)
        {
            while (tornado != null)
            {
                // Move o tornado em direção ao player
                Vector3 direction = (playerPosition - tornado.transform.position).normalized;
                tornado.transform.position += direction * tornadoSpeed * Time.deltaTime;

                yield return null; // Espera até o próximo frame para continuar o movimento
            }
        }
    }
}
