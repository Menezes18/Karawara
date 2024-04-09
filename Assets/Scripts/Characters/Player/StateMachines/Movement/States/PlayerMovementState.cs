using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
namespace RPGKarawara
{
    public class PlayerMovementState : IState
    {   
        protected PlayerMovementStateMachine stateMachine;
        
        protected PlayerGroundedData movementData;

        protected PlayerAirborneData airborneData;
        Quaternion targetRotation;
        public PlayerMovementState(PlayerMovementStateMachine playerMovementStateMachine)
        {
            stateMachine = playerMovementStateMachine;

            movementData = stateMachine.Player.Data.GroundedData;

            airborneData = stateMachine.Player.Data.AirborneData;

            SetBaseCameraRecenteringData();

            InitializeData();
        }

        public void t()
        {
            CinemachineVirtualCamera virtualCamera = Player.instancia.virtualCamera;

            // Verifique se a câmera virtual foi encontrada
            if (virtualCamera != null)
            {
                // Obtenha a rotação planar da câmera
                Quaternion planarRotation = Quaternion.Euler(0f, virtualCamera.transform.rotation.eulerAngles.y, 0f);

                // Use a rotação planar da câmera para calcular a direção de movimento
                Vector3 moveDir = planarRotation * new Vector3(stateMachine.ReusableData.MovementInput.x, 0f, stateMachine.ReusableData.MovementInput.y);

                // Salve a direção de movimento para uso posterior, se necessário
                Player.instancia.movedir = moveDir;
            }
            HandleInput();

            Update(); 
        }
        public Quaternion GetTargetRotation()
        {
            return targetRotation;
        }
        private void InitializeData()
        {
            SetBaseRotationData();
        }

        #region IState Methods

        public virtual void Enter()
        {
            //Debug.Log("State: " + GetType().Name);

            AddInputActionsCallbacks();
        }

        public virtual void Exit()
        {
            RemoveInputActionsCallbacks();
        }

        public virtual void HandleInput()
        {
            ReadMovementInput();
            Player.instancia.moveAmount = Mathf.Clamp01(Mathf.Abs(stateMachine.ReusableData.MovementInput.x) + Mathf.Abs(stateMachine.ReusableData.MovementInput.y));
        }

        public virtual void Update()
        {
        }
        public virtual void PhysicsUpdate()
        {
           // if (Player.instancia.meeleFighter.InAction)
           // {
             //   resetvelocitycombat();
                //stateMachine.ReusableData.MovementSpeedModifier = 0f;
           //     return;
          //  }
            Move();
        }

        public virtual void OnAnimationEnterEvent()
        {
        }

        public virtual void OnAnimationExitEvent()
        {
        }

        public virtual void OnAnimationTransitionEvent()
        {
        }

        public virtual void OnTriggerEnter(Collider collider)
        {
            if (stateMachine.Player.LayerData.IsGroundLayer(collider.gameObject.layer))
            {
                OnContactWithGround(collider);

                return;
            }
        }

        public void OnTriggerExit(Collider collider)
        {
            if (stateMachine.Player.LayerData.IsGroundLayer(collider.gameObject.layer))
            {
                OnContactWithGroundExited(collider);

                return;
            }
        }

        #endregion

        #region Main Methods

        private void ReadMovementInput()
        {
            stateMachine.ReusableData.MovementInput = stateMachine.Player.Input.PlayerActions.Movement.ReadValue<Vector2>();
        }

       private void Move()
        {
            if (stateMachine.ReusableData.MovementInput == Vector2.zero || stateMachine.ReusableData.MovementSpeedModifier == 0f)
            {
                return;
            }

            // if(Player.instancia.meeleFighter.InAction)
            // {
            //     //stateMachine.ReusableData.MovementSpeedModifier = 0f;
            //     resetvelocitycombat();
            // }
            Vector3 movementDirection = GetMovementInputDirection();

            float targetRotationYAngle = Rotate(movementDirection);

            Vector3 targetRotationDirection = GetTargetRotationDirection(targetRotationYAngle);

            float movementSpeed = GetMovementSpeed();

            Vector3 currentPlayerHorizontalVelocity = GetPlayerHorizontalVelocity();

            stateMachine.Player.Rigidbody.AddForce(targetRotationDirection * movementSpeed - currentPlayerHorizontalVelocity, ForceMode.VelocityChange);
        }

         private float Rotate(Vector3 direction)
        {
            float directionAngle = UpdateTargetRotation(direction);

            RotateTowardsTargetRotation();

            return directionAngle;
        }

       private float GetDirectionAngle(Vector3 direction)
        {
            float directionAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            if (directionAngle < 0f)
            {
                directionAngle += 360f;
            }

            return directionAngle;
        }

        private float AddCameraRotationToAngle(float angle)
        {
            angle += stateMachine.Player.MainCameraTransform.eulerAngles.y;

            if (angle > 360f)
            {
                angle -= 360f;
            }

            return angle;
        }

        private void UpdateTargetRotationData(float targetAngle)
        {
            stateMachine.ReusableData.CurrentTargetRotation.y = targetAngle;

            stateMachine.ReusableData.DampedTargetRotationPassedTime.y = 0f;
        }

        #endregion

        #region Reusable Methods
        protected void StartAnimation(int animationHash)
        {
            stateMachine.Player.Animator.SetBool(animationHash, true);
        }
        protected void StopAnimation(int animationHash)
        {
            stateMachine.Player.Animator.SetBool(animationHash, false);
        }
        protected void SetBaseCameraRecenteringData()
        {
            stateMachine.ReusableData.BackwardsCameraRecenteringData = movementData.BackwardsCameraRecenteringData;
            stateMachine.ReusableData.SidewaysCameraRecenteringData = movementData.SidewaysCameraRecenteringData;

        }

        protected void SetBaseRotationData()
        {
            stateMachine.ReusableData.RotationData = movementData.BaseRotationData;

            stateMachine.ReusableData.TimeToReachTargetRotation = stateMachine.ReusableData.RotationData.TargetRotationReachTime;
        }

        protected virtual void AddInputActionsCallbacks()
        {
            stateMachine.Player.Input.PlayerActions.WalkToggle.started += OnWalkToggleStarted;
            
            stateMachine.Player.Input.PlayerActions.Look.started += OnMouseMovementStarted;

            stateMachine.Player.Input.PlayerActions.Movement.performed += OnMovementPerformed;

            stateMachine.Player.Input.PlayerActions.Movement.canceled +=  OnMovementCanceled;
        }

        protected virtual void RemoveInputActionsCallbacks()
        {
            stateMachine.Player.Input.PlayerActions.WalkToggle.started -= OnWalkToggleStarted;
            
            stateMachine.Player.Input.PlayerActions.Look.started -= OnMouseMovementStarted;

            stateMachine.Player.Input.PlayerActions.Movement.performed -= OnMovementPerformed;

            stateMachine.Player.Input.PlayerActions.Movement.canceled -= OnMovementCanceled;

        }

        protected Vector3 GetMovementInputDirection()
        {
            return new Vector3(stateMachine.ReusableData.MovementInput.x, 0f, stateMachine.ReusableData.MovementInput.y);
        }

        protected float GetMovementSpeed(bool shouldConsiderSlopes = true)
        {
            float movementSpeed = movementData.BaseSpeed * stateMachine.ReusableData.MovementSpeedModifier;

            if(CombatController.instacia._meeleFighter.InAction)
            {
                stateMachine.ReusableData.MovementOnSlopeSpeedModifier = 0;
                movementSpeed = 0;
            }
            if(shouldConsiderSlopes)
            {
                movementSpeed *= stateMachine.ReusableData.MovementOnSlopeSpeedModifier;
            }
            return movementSpeed;
        }

        protected Vector3 GetPlayerHorizontalVelocity()
        {
            Vector3 playerHorizontalVelocity = stateMachine.Player.Rigidbody.velocity;

            playerHorizontalVelocity.y = 0f;

            return playerHorizontalVelocity;
        }

        protected Vector3 GetPlayerVerticalVelocity()
        {
            return new Vector3(0f, stateMachine.Player.Rigidbody.velocity.y, 0f);
        }

        protected void RotateTowardsTargetRotation()
        {
           float currentYAngle = stateMachine.Player.Rigidbody.rotation.eulerAngles.y;

           if (currentYAngle == stateMachine.ReusableData.CurrentTargetRotation.y)
           {
                return;
           }

           float smoothedYAngle = Mathf.SmoothDampAngle(currentYAngle, stateMachine.ReusableData.CurrentTargetRotation.y, ref stateMachine.ReusableData.DampedTargetRotationCurrentVelocity.y, stateMachine.ReusableData.TimeToReachTargetRotation.y - stateMachine.ReusableData.DampedTargetRotationPassedTime.y);
        
            stateMachine.ReusableData.DampedTargetRotationPassedTime.y += Time.deltaTime;

            Quaternion targetRotation = Quaternion.Euler(0f, smoothedYAngle, 0f);

            stateMachine.Player.Rigidbody.MoveRotation(targetRotation); 
        }

        protected float UpdateTargetRotation(Vector3 direction, bool shouldConsiderCameraRotation = true)
        {
            float directionAngle = GetDirectionAngle(direction);

            if (shouldConsiderCameraRotation)
            {
                directionAngle = AddCameraRotationToAngle(directionAngle);
            }

            if (directionAngle != stateMachine.ReusableData.CurrentTargetRotation.y)
            {
                UpdateTargetRotationData(directionAngle);
            }

            return directionAngle;
        }

        protected Vector3 GetTargetRotationDirection(float targetAngle)
        {
            return Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        protected void ResetVelocity()
        {
            stateMachine.Player.Rigidbody.velocity = Vector3.zero;
        }

        protected void ResetVericalVelocity()
        {
            Vector3 playerHorizontalVelocity = GetPlayerHorizontalVelocity();

            stateMachine.Player.Rigidbody.velocity = playerHorizontalVelocity;
        }

        protected void DecelerateHorizontally()
        {
            Vector3 playerHorizontalVelocity = GetPlayerHorizontalVelocity();

            stateMachine.Player.Rigidbody.AddForce(-playerHorizontalVelocity * stateMachine.ReusableData.MovementDecelerationForce, ForceMode.Acceleration);
        }
       
        protected void DecelerateVertically()
        {
            Vector3 playerVerticalVelocity = GetPlayerVerticalVelocity();

            stateMachine.Player.Rigidbody.AddForce(-playerVerticalVelocity * stateMachine.ReusableData.MovementDecelerationForce, ForceMode.Acceleration);
        }

        protected bool IsMovingHorizontally(float minimumMagnitude = 0.1f)
        {
            Vector3 playerHorizontalVelocity = GetPlayerHorizontalVelocity();

            Vector2 playerHorizontalMovement = new Vector2(playerHorizontalVelocity.x, playerHorizontalVelocity.z);

            return playerHorizontalMovement.magnitude > minimumMagnitude;
        }

        protected bool IsMovingUp(float minimumVelocity = 0.1f)
        {
            return GetPlayerVerticalVelocity().y > minimumVelocity;
        }
        

        protected bool IsMovingDown(float minimumVelocity = 0.1f)
        {
            return GetPlayerVerticalVelocity().y < -minimumVelocity;
        }

        protected virtual void OnContactWithGround(Collider collider)
        {
        }

        protected virtual void OnContactWithGroundExited(Collider collider)
        {
        }

        protected void UpdateCameraRecenteringState(Vector2 movementInput)
        {
            if (movementInput == Vector2.zero)
            {
                return;
            }   

            if (movementInput == Vector2.up)
            {
                DisableCameraRecentering();

                return;
            }

            float cameraVerticalAngle = stateMachine.Player.MainCameraTransform.eulerAngles.x;

            if (cameraVerticalAngle >= 270f)
            {
                cameraVerticalAngle -= 360f;
            }

            cameraVerticalAngle = Mathf.Abs(cameraVerticalAngle);

            if (movementInput == Vector2.down)
            {
                SetCameraRecenteringState(cameraVerticalAngle, stateMachine.ReusableData.BackwardsCameraRecenteringData);

                return;
            }

            SetCameraRecenteringState(cameraVerticalAngle, stateMachine.ReusableData.SidewaysCameraRecenteringData);
        }

        protected void EnableCameraRecentering(float waitTime = -1f, float recenteringTime = -1f)
        {
            float movementSpeed = GetMovementSpeed();
            
            if (movementSpeed == 0f)
            {
                movementSpeed = movementData.BaseSpeed;
            }

            stateMachine.Player.CameraUtility.EnableRecentering(waitTime, recenteringTime, movementData.BaseSpeed, movementSpeed);
        }

        protected void DisableCameraRecentering()
        {
            stateMachine.Player.CameraUtility.DisableRecentering();
        }

        protected void resetvelocitycombat()
        {
            Vector3 playervelocity = stateMachine.Player.Rigidbody.velocity;

            playervelocity.x = 0f;
            playervelocity.z = 0f;
            stateMachine.Player.Rigidbody.velocity = playervelocity;
        }
        protected void SetCameraRecenteringState(float cameraVerticalAngle, List<PlayerCameraRecenteringData> cameraRecenteringData)
        {
            foreach (PlayerCameraRecenteringData recenteringData in cameraRecenteringData)
            {
                if (!recenteringData.IsWithinRange(cameraVerticalAngle))
                {
                    continue;
                }

                EnableCameraRecentering(recenteringData.WaitTime, recenteringData.RecenteringTime);

                return;
            }

            DisableCameraRecentering();
        }

        #endregion

        #region Input Methods
        

        protected virtual void OnWalkToggleStarted(InputAction.CallbackContext context)
        {
            stateMachine.ReusableData.ShouldWalk = !stateMachine.ReusableData.ShouldWalk;
        }

        private void OnMouseMovementStarted(InputAction.CallbackContext context)
        {
            UpdateCameraRecenteringState(stateMachine.ReusableData.MovementInput);
        }

        private void OnMovementPerformed(InputAction.CallbackContext context)
        {
            UpdateCameraRecenteringState(context.ReadValue<Vector2>());
        }

        protected virtual void OnMovementCanceled(InputAction.CallbackContext context)
        {
            DisableCameraRecentering();
        }
        #endregion
    }
}
