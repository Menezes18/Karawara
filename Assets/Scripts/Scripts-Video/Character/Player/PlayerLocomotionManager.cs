using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        private Vector3 dodgeDirection;
        public bool dodging = false;
        public bool humanAction = false;
        [SerializeField] float dodgeDistance = 8f;   // Distância do dodge
        [SerializeField] float dodgeDuration = 11f; // Duração do dodge
        private bool isDodging = false;
        public bool canDodge = true;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerManager>();
        }

        protected override void Update()
        {
            base.Update();

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
                player.playerAnimatorManager.UpdateAnimatorMovementParameters(PlayerInputManager.instance.horizontal_Input, PlayerInputManager.instance.vertical_Input, player.playerNetworkManager.isSprinting.Value);
            }
            else
            {
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
            if (player.isDead.Value)
                return;

            if (!player.characterLocomotionManager.canRotate)
                return;

            if (player.playerNetworkManager.isLockedOn.Value)
            {
                if (player.playerNetworkManager.isSprinting.Value || player.playerLocomotionManager.isRolling)
                {
                    Vector3 targetDirection = Vector3.zero;
                    targetDirection = PlayerCamera.instance.cameraObject.transform.forward * verticalMovement;
                    targetDirection += PlayerCamera.instance.cameraObject.transform.right * horizontalMovement;
                    targetDirection.Normalize();
                    targetDirection.y = 0;

                    if (targetDirection == Vector3.zero)
                        targetDirection = transform.forward;

                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                    Quaternion finalRotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    transform.rotation = finalRotation;
                }
                else
                {
                    if (player.playerCombatManager.currentTarget == null)
                        return;

                    Vector3 targetDirection;
                    targetDirection = player.playerCombatManager.currentTarget.transform.position - transform.position;
                    targetDirection.y = 0;
                    targetDirection.Normalize();

                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                    Quaternion finalRotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    transform.rotation = finalRotation;
                }
            }
            else
            {
                targetRotationDirection = Vector3.zero;
                targetRotationDirection = PlayerCamera.instance.cameraObject.transform.forward * verticalMovement;
                targetRotationDirection = targetRotationDirection + PlayerCamera.instance.cameraObject.transform.right * horizontalMovement;
                targetRotationDirection.Normalize();
                targetRotationDirection.y = 0;

                if (targetRotationDirection == Vector3.zero)
                {
                    targetRotationDirection = transform.forward;
                }

                Quaternion newRotation = Quaternion.LookRotation(targetRotationDirection);
                Quaternion targetRotation = Quaternion.Slerp(transform.rotation, newRotation, rotationSpeed * Time.deltaTime);
                transform.rotation = targetRotation;
            }
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

        public void AttemptToPerformDodge()
        {
            if (player.isPerformingAction || isDodging)
                return;

            if (PlayerInputManager.instance.moveAmount > 0)
            {
                dodgeDirection = PlayerCamera.instance.cameraObject.transform.forward * PlayerInputManager.instance.vertical_Input;
                dodgeDirection += PlayerCamera.instance.cameraObject.transform.right * PlayerInputManager.instance.horizontal_Input;
                dodgeDirection.y = 0;
                dodgeDirection.Normalize();
                player.playerAnimatorManager.PlayTargetActionAnimation("Roll_Forward_01", true, false);
                StartCoroutine(PerformDodge(dodgeDirection));
            }
            else
            {
                StartCoroutine(PerformDodge(-transform.forward));
            }

            // player.playerNetworkManager.currentStamina.Value -= dodgeStaminaCost;
        }

        private IEnumerator PerformDodge(Vector3 direction)
        {
            isDodging = true;

            float elapsedTime = 0f;
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + direction * dodgeDistance;

            while (elapsedTime < dodgeDuration)
            {
                // Calculate the target position for this frame
                Vector3 targetPosition = Vector3.Lerp(startPosition, endPosition, elapsedTime / dodgeDuration);

                // Perform the movement using CharacterController.Move
                
                player.characterController.Move(targetPosition - transform.position);

                // Check for collision flags to handle collisions
                if ((player.characterController.collisionFlags & CollisionFlags.Sides) != 0)
                {
                    // Collision detected, break the loop
                    break;
                }
               
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure the final position is set correctly
            player.characterController.Move(endPosition - transform.position);

            isDodging = false;
            
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
