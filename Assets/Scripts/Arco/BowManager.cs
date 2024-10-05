using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara{
    public class BowManager : MonoBehaviour{
        [Header("Bow Settings")] public bool isAiming = false; // Determina se o jogador está mirando com o arco
        [SerializeField] GameObject arrowPrefab; // Prefab da flecha
        [SerializeField] Transform arrowSpawnPoint; // Ponto de spawn da flecha
        [SerializeField] float minArrowForce = 10f; // Força mínima da flecha
        [SerializeField] float maxArrowForce = 50f; // Força máxima da flecha
        [SerializeField] float chargeTime = 2f; // Tempo máximo para carregar a flecha
        private float currentArrowForce; // Força atual da flecha
        private bool isDrawingBow = false; // Se o arco está sendo "puxado"
        private float chargeStartTime; // Tempo em que o arco começou a ser puxado

        private Animator animator;
        public PlayerLocomotionManager playerLocomotionManager;

        private void Awake(){
            animator = GetComponent<Animator>();
            playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            currentArrowForce = minArrowForce; // Inicializa a força da flecha
        }

        private void Update(){
            // Verifica se o botão direito do mouse está pressionado para ativar a mira
            if (Mouse.current.rightButton.isPressed){
                playerLocomotionManager.arco = true;
                isAiming = true;
                PlayerCamera.instance.ToggleAimMode(true); // Ativa a câmera de mira

                if (!isDrawingBow){
                    isDrawingBow = true; // O arco está sendo preparado para disparo
                    chargeStartTime = Time.time; // Registra o tempo de início do carregamento
                }

                // Aumenta a força da flecha enquanto o botão está pressionado
                currentArrowForce =
                    Mathf.Lerp(minArrowForce, maxArrowForce, (Time.time - chargeStartTime) / chargeTime);
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame && isDrawingBow){
                // Quando o botão direito é solto, dispare a flecha
                FireArrow();
                isAiming = false;
                isDrawingBow = false;
                currentArrowForce = minArrowForce; // Reseta a força da flecha
                PlayerCamera.instance.ToggleAimMode(false); // Desativa a câmera de mira
                playerLocomotionManager.arco = false;
            }
        }

        // Método que dispara a flecha
        private void FireArrow(){
            if (arrowPrefab != null && arrowSpawnPoint != null){
                // Instanciar a flecha no ponto de spawn
                GameObject arrow = Instantiate(arrowPrefab, arrowSpawnPoint.position, arrowSpawnPoint.rotation);
                Rigidbody arrowRigidbody = arrow.GetComponent<Rigidbody>();

                // Obter a direção em que a câmera está olhando
                Vector3 shootDirection = Camera.main.transform.forward;

                // Aplicar força para lançar a flecha na direção da câmera
                if (arrowRigidbody != null){
                    arrowRigidbody.AddForce(shootDirection * currentArrowForce, ForceMode.Impulse);
                }

                // Tocar a animação de disparo da flecha
                // animator.Play("Fire_Bow_Animation", 0, 0);
            }
        }
    }
}