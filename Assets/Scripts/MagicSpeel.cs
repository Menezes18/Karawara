using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class MagicSpeel : MonoBehaviour
    {
        public static MagicSpeel instance;
        public GameObject tiro;
        public Transform inicio;
        public Transform player; // Refer�ncia para o jogador
        public float speed = 10f; // Velocidade do tiro
        public float lifetime = 5f; // Tempo de vida do tiro
        
        [field: SerializeField]public float pursueTime = 0.5f;

        public void Start()
        {
            instance = this;
        }

        public void Tiro(Transform Player)
        {
            player = Player;
            // Instancia o objeto 'tiro' na posi��o e rota��o definidas por 'inicio'
            GameObject tiroInstance = Instantiate(tiro, inicio.position, inicio.rotation);

            // Inicia a coroutine para mover o tiro em dire��o ao jogador
            StartCoroutine(MoveTowardsPlayer(tiroInstance));
        }

        IEnumerator MoveTowardsPlayer(GameObject tiroInstance)
        {
            float elapsedTime = 0;
            Vector3 direction = Vector3.zero; // Armazena a dire��o do tiro

            while (elapsedTime < pursueTime)
            {
                // Move o tiro em dire��o ao jogador
                direction = (new Vector3(player.position.x, player.position.y + 1.4f, player.position.z) - tiroInstance.transform.position).normalized;
                tiroInstance.transform.position += direction * speed * Time.deltaTime;

                elapsedTime += Time.deltaTime;
                
                yield return null;
            }

            while (elapsedTime < lifetime)
            {
                // Move o tiro na �ltima dire��o que estava seguindo
                tiroInstance.transform.position += direction * speed * Time.deltaTime;

                elapsedTime += Time.deltaTime;

                yield return null;
            }

            // Destroi o tiro ap�s 5 segundos
            Destroy(tiroInstance);
        }

    }
}
