using System.Collections;
using UnityEngine;

namespace RPGKarawara
{
    public class ParticleCollisionDetection : MonoBehaviour
    {
        public GameObject Particulas; // Prefab ou objeto de partículas
        public GameObject Pedras; // Objeto que será ativado ao colidir

        // Use "Collision" como tipo de parâmetro para o método OnCollisionEnter
        private void OnTriggerEnter(Collider collision)
        {
            // Verifica se o objeto que colidiu tem a tag "Player"
            if (collision.CompareTag("Player"))
            {
                Debug.Log("Entrou em colisão com o Player.");
                
                // Inicia a rotina para lidar com as partículas
                StartCoroutine(TimeParticles());

                // Ativa o objeto "Pedras"
                
            }
        }

        // Coroutine para controlar a ativação e destruição das partículas
        private IEnumerator TimeParticles()
        {
            if (Particulas != null)
            {
                Particulas.SetActive(true); // Ativa o objeto de partículas
                yield return new WaitForSeconds(0.1f); // Aguarda 0.1 segundos
                
                // Desativa em vez de destruir, caso você precise reutilizar
                yield return new WaitForSeconds(4f); // Aguarda mais 2 segundos
                if (Pedras != null)
                {
                    Pedras.SetActive(true);
                }
                Particulas.SetActive(false);
            }
        }
    }
}
