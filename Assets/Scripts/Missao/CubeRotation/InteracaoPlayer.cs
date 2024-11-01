using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class InteracaoPlayer : MonoBehaviour
    {
        LayerMask layerMask;
        public Transform cameraTransform; 
        public Transform raycastOrigin; 
        private RaycastHit hitInfo; 
        public string targetLayerName = "Rotation"; 
        public float rayDistance = 2; 
        private bool checkDistance = false;
        public bool interacting = false;
        public GameObject currentTarget;
        public float rotationSpeed = 10;
        void Start()
        {
            layerMask = ~LayerMask.GetMask("Character");
            cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                // Alterna o estado de interação
                if (interacting)
                {
                    Debug.Log("Saiu da interação");
                    interacting = false;
                    InteractWithTarget(false); // Finaliza a interação
                }
                else if (checkDistance)
                {
                    Debug.Log("Entrou na interação");
                    currentTarget = hitInfo.transform.gameObject; 
                    interacting = true;
                    InteractWithTarget(true); // Inicia a interação
                }
            }

            // Se estiver interagindo, realiza a rotação contínua
            if (interacting && currentTarget != null)
            {
                RotateTarget();
            }
        }

        void FixedUpdate()
        {
            if (cameraTransform != null && raycastOrigin != null)
            {
                var rayOrigin = new Ray(raycastOrigin.position, cameraTransform.forward);
                var didHit = Physics.Raycast(rayOrigin, out hitInfo, rayDistance, layerMask);
                
                if (didHit && hitInfo.transform.gameObject.layer == LayerMask.NameToLayer(targetLayerName))
                {
                    checkDistance = true;
                }
                else
                {
                    checkDistance = false;
                }
            }
        }

        private void InteractWithTarget(bool isInteracting)
        {
            PlayerLocomotionManager playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            PlayerCamera playerCamera = FindObjectOfType<PlayerCamera>();

            if (isInteracting)
            {
                // Habilita a interação
                playerCamera.targetToLookAt = currentTarget.transform;
                playerLocomotionManager.canInteract = true;
                playerCamera.lockOnTarget = true;
                playerLocomotionManager.cubeTransform = currentTarget.transform;
            }
            else
            {
                // Finaliza a interação
                playerCamera.targetToLookAt = null;
                playerCamera.lockOnTarget = false;
                playerLocomotionManager.cubeTransform = null;
                playerLocomotionManager.canInteract = false;
            }
        }

        private void RotateTarget()
        {
            PlayerLocomotionManager playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            
            currentTarget.transform.Rotate(Vector3.up * (playerLocomotionManager.rotationInput * rotationSpeed * Time.deltaTime));
        }

        void OnDrawGizmos()
        {
            if (cameraTransform != null && raycastOrigin != null)
            {
                Vector3 rayOrigin = raycastOrigin.position;
                Vector3 rayDirection = cameraTransform.forward * rayDistance;
                Vector3 rayEndPosition = rayOrigin + rayDirection;

                Gizmos.color = Color.green; 
                Gizmos.DrawLine(rayOrigin, rayEndPosition); 

                if (hitInfo.transform != null)
                {
                    Gizmos.color = Color.red; 
                    Gizmos.DrawSphere(hitInfo.point, 0.1f); 
                }
            }
        }
    }
}
