using System.Collections;
using UnityEngine;

namespace RPGKarawara
{
    public class SaciWhirlwindController : MonoBehaviour
    {
        [SerializeField] AISaciCharacterManager saciCharacterManager;  // Gerencia o boss Saci
        [SerializeField] GameObject tornadoPrefab;  // Prefab do tornado a ser instanciado
        [SerializeField] int numberOfTornadoes = 3; // Quantidade de tornados a serem instanciados
        [SerializeField] float tornadoSpeed = 5.0f; // Velocidade de movimento dos tornados
        [SerializeField] float spawnRadius = 2.0f;  // Raio ao redor do Saci onde os tornados serão instanciados
        [SerializeField] float tornadoLifetime = 5.0f; // Tempo de vida dos tornados, antes de serem destruídos

        // Método para ser chamado quando o ataque de tornado for ativado
        public void WhirlwindAttack(Vector3 playerPosition)
        {
            // Instancia múltiplos tornados ao redor do Saci
            for (int i = 0; i < numberOfTornadoes; i++)
            {
                // Gera uma posição aleatória ao redor do Saci para instanciar o tornado
                Vector3 spawnPosition = saciCharacterManager.transform.position + (Random.insideUnitSphere * spawnRadius);
                spawnPosition.y = saciCharacterManager.transform.position.y; // Mantém a altura dos tornados igual à do Saci

                // Instancia o tornado na posição gerada
                GameObject tornado = Instantiate(tornadoPrefab, spawnPosition, Quaternion.identity);

                // Começa a mover o tornado em direção ao jogador
                StartCoroutine(MoveTornadoTowardsPlayer(tornado, playerPosition));

                // Destrói o tornado após um tempo determinado (vida útil)
                Destroy(tornado, tornadoLifetime);
            }
        }

        // Corrotina para mover o tornado em direção ao jogador
        private IEnumerator MoveTornadoTowardsPlayer(GameObject tornado, Vector3 playerPosition)
        {
            while (tornado != null)
            {
                // Calcula a direção do tornado em relação ao jogador
                Vector3 direction = (playerPosition - tornado.transform.position).normalized;

                // Move o tornado na direção do jogador com base na velocidade especificada
                tornado.transform.position += direction * tornadoSpeed * Time.deltaTime;

                // Espera até o próximo frame
                yield return null;
            }
        }
    }
}
