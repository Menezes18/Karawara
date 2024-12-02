using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class PlayerLocomotionManager : CharacterLocomotionManager
    {
        PlayerManager player;

        [HideInInspector] public float verticalMovement;
        [HideInInspector] public float horizontalMovement;
        [HideInInspector] public float moveAmount;

        [Header("Movement Settings")]
        private Vector3 moveDirection;
        private Vector3 targetRotationDirection;
        [SerializeField] float walkingSpeed = 2;
        [SerializeField] float runningSpeed = 5;
        [SerializeField] float sprintingSpeed = 6.5f;
        [SerializeField] float rotationSpeed = 15;
        [SerializeField] int sprintingStaminaCost = 2;
        
        [Header("Jump")]
        [SerializeField] float jumpStaminaCost = 25;
        [SerializeField] float jumpHeight = 4;
        [SerializeField] float jumpForwardSpeed = 5;
        [SerializeField] float freeFallSpeed = 2;
        private Vector3 jumpDirection;

        [Header("Dodge")]
        private Vector3 rollDirection;
        public bool dodging = false;
        [SerializeField] private float dodgeDistance = 8f;   // Distância do dodge
        [SerializeField] private float dodgeDuration = 11f; // Duração do dodge
        private bool isDodging = false;
        public bool canDodge = true;
        public bool arco = false;
        public bool isMoving = false;
        
        [Header("Interact")]
        public bool canInteract = false;
        public Transform cubeTransform;
        [SerializeField] private float maxDistanceFromCube = 2f; 
        [SerializeField] float walkingSpeedCube = 2;
        [SerializeField] float runningSpeedCube = 4;
        private float auxWalking, auxRunning;
        public float rotationInput;
        protected override void Awake()
        {
            base.Awake();
            auxWalking = walkingSpeed;
            auxRunning = runningSpeed;
            player = GetComponent<PlayerManager>();
        }
        
        protected override void Update()
        {
            
            base.Update();
            LimitPlayerPositionNearCube();
            if (player.IsOwner)
            {
                player.characterNetworkManager.verticalMovement.Value = verticalMovement;
                player.characterNetworkManager.horizontalMovement.Value = horizontalMovement;
                player.characterNetworkManager.moveAmount.Value = moveAmount;
            }
            else
            {
                verticalMovement = player.characterNetworkManager.verticalMovement.Value;
                horizontalMovement = player.characterNetworkManager.horizontalMovement.Value;
                moveAmount = player.characterNetworkManager.moveAmount.Value;

                if (!player.playerNetworkManager.isLockedOn.Value || player.playerNetworkManager.isSprinting.Value)
                {
                    player.playerAnimatorManager.UpdateAnimatorMovementParameters(0, moveAmount, player.playerNetworkManager.isSprinting.Value);
                }
                else
                {
                    player.playerAnimatorManager.UpdateAnimatorMovementParameters(horizontalMovement, verticalMovement, player.playerNetworkManager.isSprinting.Value);
                }
            }

            if (moveAmount > 0)
            {
                
                character.animator.SetBool("isMoving", true);
                isMoving = true;
                player.playerAnimatorManager.UpdateAnimatorMovementParameters(PlayerInputManager.instance.horizontal_Input, PlayerInputManager.instance.vertical_Input, player.playerNetworkManager.isSprinting.Value);
            }
            else{
                isMoving = false;
                character.animator.SetBool("isMoving", false);
                player.playerAnimatorManager.UpdateAnimatorMovementParameters(PlayerInputManager.instance.horizontal_Input, PlayerInputManager.instance.vertical_Input, player.playerNetworkManager.isSprinting.Value);
            }
        }

        public void HandleAllMovement()
        {
            if (!isDodging)
            {
                HandleGroundedMovement();
                HandleRotation();
                HandleJumpingMovement();
                HandleFreeFallMovement();
            }
        }

        private void GetMovementValues()
        {
            verticalMovement = PlayerInputManager.instance.vertical_Input;
            horizontalMovement = PlayerInputManager.instance.horizontal_Input;
            moveAmount = PlayerInputManager.instance.moveAmount;
        }

        private void HandleGroundedMovement()
        {
            if (player.characterLocomotionManager.canMove || player.playerLocomotionManager.canRotate)
            {
                GetMovementValues();
            }

            if (!player.characterLocomotionManager.canMove)
                return;

            //  OUR MOVE DIRECTION IS BASED ON OUR CAMERAS FACING PERSPECTIVE & OUR MOVEMENT INPUTS
            moveDirection = PlayerCamera.instance.transform.forward * verticalMovement;
            moveDirection = moveDirection + PlayerCamera.instance.transform.right * horizontalMovement;
            moveDirection.Normalize();
            moveDirection.y = 0;

            if (player.playerNetworkManager.isSprinting.Value)
            {
                
                player.characterController.Move(moveDirection * sprintingSpeed * Time.deltaTime);
            }
            else
            {
                // if(!MudarAvatar.instancia.change && !MudarAvatar.instancia.eroding)MudarAvatar.instancia.TrocarPlayer();
                if (PlayerInputManager.instance.moveAmount > 0.5f)
                {
                    player.characterController.Move(moveDirection * runningSpeed * Time.deltaTime);
                }
                else if (PlayerInputManager.instance.moveAmount <= 0.5f)
                {
                    player.characterController.Move(moveDirection * walkingSpeed * Time.deltaTime);
                }
            }
        }

        private void HandleJumpingMovement()
        {
            if (player.playerNetworkManager.isJumping.Value)
            {
                player.characterController.Move(jumpDirection * jumpForwardSpeed * Time.deltaTime);
            }
        }

        private void HandleFreeFallMovement()
        {
            if (!player.characterLocomotionManager.isGrounded)
            {
                Vector3 freeFallDirection;

                freeFallDirection = PlayerCamera.instance.transform.forward * PlayerInputManager.instance.vertical_Input;
                freeFallDirection = freeFallDirection + PlayerCamera.instance.transform.right * PlayerInputManager.instance.horizontal_Input;
                freeFallDirection.y = 0;

                player.characterController.Move(freeFallDirection * freeFallSpeed * Time.deltaTime);
            }
        }

        private void HandleRotation()
        {
            if (player.isDead.Value || !player.characterLocomotionManager.canRotate)
                return;

            Quaternion targetRotation;

            // Checar se canInteract está ativo e o cubo é o alvo
            if (canInteract && cubeTransform != null)
            {
                walkingSpeed = walkingSpeedCube;
                runningSpeed = runningSpeedCube;

                // Pega o input de rotação do player
                rotationInput = PlayerInputManager.instance.horizontal_Input;

                if (rotationInput != 0)
                {
                    // Calcula a direção do player em relação ao cubo
                    Vector3 directionToCube = (cubeTransform.position - transform.position).normalized;
                    directionToCube.y = 0; // Mantém no plano horizontal

                    // Define a rotação-alvo com base na direção do player
                    targetRotation = Quaternion.LookRotation(directionToCube);

                    // Altera a rotação do player suavemente em direção ao cubo
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                    // Aplica a rotação ao cubo com base no input horizontal
                    

                    return; // Para evitar outras rotações
                }
            }

            else{
                walkingSpeed = auxWalking;
                runningSpeed = auxRunning;
            }

            // Código padrão de rotação para outras situações (lock-on, arco)
            if (player.playerNetworkManager.isLockedOn.Value)
            {
                if (player.playerNetworkManager.isSprinting.Value || player.playerLocomotionManager.isRolling)
                {
                    targetRotation = CalculateFreeRotation();
                }
                else
                {
                    if (player.playerCombatManager.currentTarget == null) return;
                    targetRotation = CalculateLockedOnRotation();
                }
            }
            else if (arco)
            {
                targetRotation = CalculateArcoRotation();
            }
            else
            {
                targetRotation = CalculateFreeRotation();
            }

            // Aplica a rotação suavemente
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        private void LimitPlayerPositionNearCube()
        {
            if (!canInteract || cubeTransform == null) return;

            Vector3 offset = transform.position - cubeTransform.position;
            float distance = offset.magnitude;

            if (distance > maxDistanceFromCube)
            {
                // Limita a posição do player à distância máxima permitida
                Vector3 clampedPosition = cubeTransform.position + offset.normalized * maxDistanceFromCube;
                transform.position = clampedPosition;
            }
        }


        // Funções auxiliares para cálculos de rotação, permanecem as mesmas
        private Quaternion CalculateFreeRotation() {
            Vector3 direction = PlayerCamera.instance.cameraObject.transform.forward * verticalMovement +
                                PlayerCamera.instance.cameraObject.transform.right * horizontalMovement;
            direction.y = 0;
            return Quaternion.LookRotation(direction == Vector3.zero ? transform.forward : direction.normalized);
        }

        private Quaternion CalculateLockedOnRotation() {
            Vector3 direction = player.playerCombatManager.currentTarget.transform.position - transform.position;
            direction.y = 0;
            return Quaternion.LookRotation(direction.normalized);
        }

        private Quaternion CalculateArcoRotation() {
            Vector3 direction = PlayerCamera.instance.cameraObject.transform.forward;
            direction.y = 0;
            return Quaternion.LookRotation(direction == Vector3.zero ? transform.forward : direction.normalized);
        }



        public void HandleSprinting()
        {
            if (player.isPerformingAction)
            {
                player.playerNetworkManager.isSprinting.Value = false;
            }
            //  IF WE ARE MOVING, SPRINTING IS TRUE
            if (moveAmount >= 0.5)
            {
                player.playerNetworkManager.isSprinting.Value = true;
            }
            //  IF WE ARE STATIONARY/MOVING SLOWLY SPRINTING IS FALSE
            else
            {
                player.playerNetworkManager.isSprinting.Value = false;
            }
            
        }

        // public void AttemptToPerformDodge()
        // {
        //     if (player.isPerformingAction || isDodging)
        //         return;
        //
        //     if (PlayerInputManager.instance.moveAmount > 0)
        //     {
        //         dodgeDirection = PlayerCamera.instance.cameraObject.transform.forward * PlayerInputManager.instance.vertical_Input;
        //         dodgeDirection += PlayerCamera.instance.cameraObject.transform.right * PlayerInputManager.instance.horizontal_Input;
        //         dodgeDirection.y = 0;
        //         dodgeDirection.Normalize();
        //         player.playerAnimatorManager.PlayTargetActionAnimation("Roll_Forward_01", true, false);
        //         StartCoroutine(PerformDodge(dodgeDirection));
        //     }
        //     else
        //     {
        //         StartCoroutine(PerformDodge(-transform.forward));
        //     }
        //
        //     // player.playerNetworkManager.currentStamina.Value -= dodgeStaminaCost;
        // }
        public void AttemptToPerformDodge()
        {
            if (player.isPerformingAction)
                return;

            // if (player.playerNetworkManager.currentStamina.Value <= 0)
            //     return;

            //  IF WE ARE MOVING WHEN WE ATTEMPT TO DODGE, WE PERFORM A ROLL
            if (PlayerInputManager.instance.moveAmount > 0)
            {
                rollDirection = PlayerCamera.instance.cameraObject.transform.forward * PlayerInputManager.instance.vertical_Input;
                rollDirection += PlayerCamera.instance.cameraObject.transform.right * PlayerInputManager.instance.horizontal_Input;
                rollDirection.y = 0;
                rollDirection.Normalize();

                // Multiplicando a direção pelo fator de distância desejado
                float dodgeDistance = 118f; // Defina a distância desejada do dodge
                rollDirection *= dodgeDistance;

                Quaternion playerRotation = Quaternion.LookRotation(rollDirection);
                player.transform.rotation = playerRotation;
                
                player.playerAnimatorManager.PlayTargetActionAnimation("Roll_Forward_01", true, true);
                player.playerLocomotionManager.isRolling = true;
                
                //Invoke("TrocarPlayer", 1.2f);
            }


            //  IF WE ARE STATIONARY, WE PERFORM A BACKSTEP
            else
            {
                player.playerAnimatorManager.PlayTargetActionAnimation("Back_Step_01", true, true);
                
            }

            player.playerLocomotionManager.canDodge = true;
        }

        public void StopPlayer()
        {
            Debug.Log("AAA");
            // Zerando as variáveis de movimento
            verticalMovement = 0;
            horizontalMovement = 0;
            moveAmount = 0;

            // Forçando a velocidade a zero no PlayerLocomotionManager
            if (character.characterController != null)
            {
                character.characterController.Move(Vector3.zero);
            }

            // Atualiza o Animator para garantir que o jogador pare completamente
            character.animator.SetBool("isMoving", false);

            // Força a animação de idle imediatamente
            character.animator.Play("Idle", 0);

            // Parar qualquer animação atual
            character.animator.speed = 0; // Isso pausa todas as animações

            // Opcional: Reinicie a velocidade da animação após um pequeno atraso
            // Você pode usar um coroutine para reverter a velocidade de volta ao normal
            StartCoroutine(ResetAnimatorSpeed());
        }

        private IEnumerator ResetAnimatorSpeed()
        {
            // Espera um pequeno tempo antes de reiniciar a velocidade da animação
            yield return new WaitForSeconds(0.1f);
            character.animator.speed = 1; // Reinicia a velocidade da animação
        }


        public void AttemptToPerformJump()
        {
            if (player.isPerformingAction)
                return;

            if (player.playerNetworkManager.isJumping.Value)
                return;

            if (!player.characterLocomotionManager.isGrounded)
                return;

            player.playerAnimatorManager.PlayTargetActionAnimation("Main_Jump_01", false);

            player.playerNetworkManager.isJumping.Value = true;

            // player.playerNetworkManager.currentStamina.Value -= jumpStaminaCost;

            jumpDirection = PlayerCamera.instance.cameraObject.transform.forward * PlayerInputManager.instance.vertical_Input;
            jumpDirection += PlayerCamera.instance.cameraObject.transform.right * PlayerInputManager.instance.horizontal_Input;
            jumpDirection.y = 0;

            if (jumpDirection != Vector3.zero)
            {
                if (player.playerNetworkManager.isSprinting.Value)
                {
                    jumpDirection *= 1.5f;
                }
                else if (PlayerInputManager.instance.moveAmount > 0.5)
                {
                    jumpDirection *= 1f;
                }
                else if (PlayerInputManager.instance.moveAmount <= 0.5)
                {
                    jumpDirection *= 1f;
                }
            }
        }

        public void ApplyJumpingVelocity()
        {
            yVelocity.y = Mathf.Sqrt(jumpHeight * -2 * gravityForce);
        }

        public void IncreaseSprintingSpeed(float amount)
        {
            sprintingSpeed += amount;
        }
    }
}
