using System.Collections;
using UnityEngine;

namespace RPGKarawara
{
    public class SaciWhirlwindController : MonoBehaviour
    {
        [SerializeField] GameObject tornadoPrefab;  // Prefab do tornado a ser instanciado
        [SerializeField] int numberOfTornadoes = 1; // Quantidade de tornados a serem instanciados
        [SerializeField] float tornadoSpeed = 5.0f; // Velocidade de movimento dos tornados
        [SerializeField] float spawnRadius = 2.0f;  // Raio ao redor do objeto onde o script está anexado para spawn
        [SerializeField] float minDistanceFromCenter = 1.0f; // Distância mínima entre o centro e o tornado
        [SerializeField] float tornadoLifetime = 5.0f; // Tempo de vida dos tornados, antes de serem destruídos

        private Vector3[] spawnPositions; // Para armazenar as posições de spawn dos tornados

        // Método para ser chamado quando o ataque de tornado for ativado
        public void WhirlwindAttack(Vector3 playerPosition)
        {
            spawnPositions = new Vector3[numberOfTornadoes]; // Armazena as posições de spawn

            for (int i = 0; i < numberOfTornadoes; i++)
            {
                Vector3 spawnPosition;

                // Gera uma posição aleatória ao redor do objeto onde o script está anexado até que a distância mínima seja respeitada
                do
                {
                    spawnPosition = transform.position + (Random.insideUnitSphere * spawnRadius);
                    spawnPosition.y = transform.position.y; // Mantém a altura dos tornados igual à do objeto
                }
                while (Vector3.Distance(spawnPosition, transform.position) < minDistanceFromCenter);

                spawnPositions[i] = spawnPosition; // Salva a posição de spawn

                // Instancia o tornado na posição gerada
                GameObject tornado = Instantiate(tornadoPrefab, this.transform.position, Quaternion.identity);

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

        // Função para desenhar os Gizmos no editor
        private void OnDrawGizmos()
        {
            // Desenha o raio de spawn dos tornados
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            // Desenha os locais de spawn dos tornados (se WhirlwindAttack tiver sido chamado)
            if (spawnPositions != null)
            {
                Gizmos.color = Color.red;
                foreach (var position in spawnPositions)
                {
                    Gizmos.DrawSphere(position, 0.5f); // Desenha uma esfera pequena no local de spawn
                }
            }
        }
    }
}
