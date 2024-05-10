using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara
{
    public class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager instance;
        PlayerControls playerControls;

        [Header("Movement Input")]
        [SerializeField] Vector2 movementInput;
        public float horizontalInput;
        public float verticalInput;
        public float moveAmount;

        [Header("Camera Input")]
        [SerializeField] Vector2 cameraInput;
        public float cameraHorizontalInput;
        public float cameraVerticalInput;
        private void Awake()
        {
            if(instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            HandlePlayerMovementInput();
            HandleCameraMovementInput();
        }


        private void OnEnable()
        {
            if (playerControls == null) { 
                playerControls = new PlayerControls();

                playerControls.PlayerMovement.Movement.performed += i => movementInput = i.ReadValue<Vector2>();
                playerControls.PlayerCamera.CameraControls.performed += i => cameraInput = i.ReadValue<Vector2>();
            }

            playerControls.Enable();

        }

        private void HandlePlayerMovementInput()
        {
            verticalInput = movementInput.y;
            horizontalInput = movementInput.x;

            moveAmount = Mathf.Clamp01(Mathf.Abs(verticalInput) * Mathf.Abs(horizontalInput));

            if(moveAmount <= 0.5 && moveAmount > 0)
            {
                moveAmount = 0.5f;
            }
            else if(moveAmount > 0.5 && moveAmount <= 1)
            {
                moveAmount = 1;
            }
        }

        private void HandleCameraMovementInput()
        {
            cameraHorizontalInput = cameraInput.x;
            cameraVerticalInput = cameraInput.y;
        }
    }
}
