using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CharacterFootStepSFXMaker : MonoBehaviour
    {
        [SerializeField] CharacterManager character;
        [SerializeField] PlayerLocomotionManager playerLocomotion;
        [SerializeField] AudioSource audioSource;
        GameObject steppedOnObject;

        [SerializeField] float distanceToGround = 0.1f; // Distância do cast
        [SerializeField] Vector3 boxSize = new Vector3(0.5f, 0.1f, 0.5f); // Tamanho da caixa do cast
        [SerializeField] float footstepDelay = 0.5f; // Delay entre os passos

        private float lastFootstepTime = 0f; // Tempo da última reprodução do som

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            character = GetComponentInParent<CharacterManager>();
            playerLocomotion = GetComponentInParent<PlayerLocomotionManager>();
            
        }

        private void FixedUpdate()
        {
            CheckForFootSteps();
        }

        private void CheckForFootSteps()
        {
            if (character == null)
            {
                Debug.LogError("Character Manager is null");
                return;
            }
            if(!playerLocomotion.isMoving)
                return;
            RaycastHit hits;

            // Realiza um Raycast em direção para baixo
            if (Physics.Raycast(transform.position, Vector3.down, out hits, distanceToGround))
            {

                // Verifica se o tempo desde o último passo é maior que o delay
                if (Time.time - lastFootstepTime >= footstepDelay)
                {
                    steppedOnObject = hits.transform.gameObject; // Pega o objeto atingido
                    PlayFootStepSoundFX(); // Toca o som
                    lastFootstepTime = Time.time; // Atualiza o tempo do último passo
                }
            }
        }

        private void PlayFootStepSoundFX()
        {
            character.characterSoundFXManager.PlayFootStepSoundFX();
        }

        // Gizmos method to visualize the Raycast in the editor
        private void OnDrawGizmos()
        {
            if (character == null)
                return;

            Gizmos.color = Color.red; // Cor do gizmo
            Vector3 boxCenter = transform.position - new Vector3(0, boxSize.y / 2, 0); // Centro da caixa

            // Desenhe a caixa do gizmo
            Gizmos.DrawWireCube(boxCenter, boxSize); // Desenha uma caixa wireframe
            Gizmos.DrawWireCube(boxCenter + Vector3.down * distanceToGround, boxSize); // Desenha a caixa na posição final do raycast
        }
    }
}
