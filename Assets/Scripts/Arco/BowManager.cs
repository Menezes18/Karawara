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
        public GameObject arrow;


        private float timerArrowOff = 2.5f;
        [SerializeField]
        private float currentTimerArrowOff = 0;
        private void Awake(){
            arrow.SetActive(false);
            animator = GetComponent<Animator>();
            playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            currentArrowForce = minArrowForce; // Inicializa a força da flecha
        }

        // private void Update(){
        //     // Verifica se o botão direito do mouse está pressionado para ativar a mira
        //     
        //     if (Mouse.current.rightButton.isPressed){
        //         ActiveBow(true);
        //         playerLocomotionManager.arco = true;
        //         isAiming = true;
        //         PlayerCamera.instance.ToggleAimMode(true); // Ativa a câmera de mira
        //
        //         if (!isDrawingBow){
        //             isDrawingBow = true; // O arco está sendo preparado para disparo
        //             chargeStartTime = Time.time; // Registra o tempo de início do carregamento
        //         }
        //         
        //         
        //         currentArrowForce =
        //             Mathf.Lerp(minArrowForce, maxArrowForce, (Time.time - chargeStartTime) / chargeTime);
        //         string targetAnimation = "ArcoPress";  // A animação que você quer que o player execute
        //         //animator.SetBool("Press", true);
        //         
        //         CharacterAnimatorManager animatorManager = GetComponent<CharacterAnimatorManager>();
        //         bool isPerformingAction = false;           // Indica que o player está realizando uma ação 
        //         bool applyRootMotion = false;              // O movimento deve ser influenciado pela animação
        //         bool canRotate = true;                   // O player  pode girar durante 
        //         bool canMove = true;                     // O player pode se mover durante 
        //         Chama a animação com os parâmetros estilo Rennala Queen
        //         animatorManager.PlayTargetActionAnimation(targetAnimation, isPerformingAction, applyRootMotion, canRotate, canMove);*/
        //
        //         animator.CrossFade(targetAnimation, 0);
        //         currentTimerArrowOff = 0;
        //     }
        //     else if (Mouse.current.rightButton.wasReleasedThisFrame && isDrawingBow){
        //         animator.SetBool("Press", false);
        //          Quando o botão direito é solto, dispare a flecha
        //         FireArrow();
        //         isAiming = false;
        //         isDrawingBow = false;
        //         currentArrowForce = minArrowForce; // Reseta a força da flecha
        //         PlayerCamera.instance.ToggleAimMode(false); // Desativa a câmera de mira
        //         playerLocomotionManager.arco = false;
        //
        //     }
        //
        //     if (currentTimerArrowOff < timerArrowOff && !isAiming){
        //         currentTimerArrowOff += Time.deltaTime;
        //         if (currentTimerArrowOff >= timerArrowOff){
        //             ActiveBow(false);
        //         }
        //
        //     }*/
        //     
        // }

        public void ActiveBow(bool active){
            arrow.SetActive(active);
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