using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera instance;
        public Camera cameraObj;
        public PlayerManager player;
        [SerializeField] Transform cameraPivotTransform;

        [Header("Cammera Settings")]//Performance
        private float cameraSmoothSpeed = 1; //quanto maior, tempo da camera aumenta pra chegar na posicao durante o movimento
        [SerializeField] float leftAndRightRotationSpeed = 220;
        [SerializeField] float upAndDownRotationSpeed = 220;
        [SerializeField] float minimumPivot = -30; //o menor ponto q da pra olhar
        [SerializeField] float maximumPivot = 60; // o maior ponto q da pra olhar
        [SerializeField] float cameraCollisionRadius = 0.2f;
        [SerializeField] LayerMask collideWithLayers;

        [Header("Cammera Values")]
        private Vector3 cameraVelocity;
        private Vector3 cameraObjectPosition; //Usado para a colisao da camera mover o obj da camera para essa posicao quando colidir
        [SerializeField] float leftAndRighLookAngle;
        [SerializeField] float upAndDownLookAngle;
        private float cameraZPosition; //Colisao da camera
        private float targetCameraZPosition; //Colisao da camera

        private void Awake()
        {
            if(instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(instance);
            }
        }

        private void Start()
        {
            cameraZPosition = cameraObj.transform.localPosition.z;
        }

        public void HandleAllCameraActions()
        {
            if(player!=null)
            {
                HandleFollowTargert();
                HandleRotations();
                HandleCollisons();
            }
        }

        private void HandleFollowTargert()
        {
            Vector3 targetCammeraPosition = Vector3.SmoothDamp(transform.position, player.transform.position, ref cameraVelocity, cameraSmoothSpeed * Time.deltaTime);
            transform.position = targetCammeraPosition;
        }

        private void HandleRotations()
        {
            //Rotaciona pra esquerda e pra direita
            leftAndRighLookAngle += (PlayerInputManager.instance.cameraHorizontalInput * leftAndRightRotationSpeed * Time.deltaTime);
            //Rotaciona para cima e pra baixo
            upAndDownLookAngle -= (PlayerInputManager.instance.cameraVerticalInput * upAndDownRotationSpeed * Time.deltaTime);
            //Coloca o maximo pra rotacionar
            upAndDownLookAngle = Mathf.Clamp(upAndDownLookAngle, minimumPivot, maximumPivot);

            Vector3 cameraRotation = Vector3.zero;
            Quaternion targetRotation;
            //Rotaciona o objeto pra esquerda e direita

            cameraRotation.y = leftAndRighLookAngle;
            targetRotation = Quaternion.Euler(cameraRotation);
            transform.rotation = targetRotation;

            //Rotaciona esse objeto pra cima e pra baixo
            cameraRotation = Vector3.zero;
            cameraRotation.x = upAndDownLookAngle;
            targetRotation = Quaternion.Euler(cameraRotation);
            cameraPivotTransform.localRotation = targetRotation;
        }

        private void HandleCollisons()
        {
            targetCameraZPosition = cameraZPosition;
            RaycastHit hit;
            //Direcao para colisao
            Vector3 direction = cameraObj.transform.position - cameraPivotTransform.position;
            direction.Normalize();
            
            //Chegando se tem um obj na frente da camera na direcao para colisao
            if(Physics.SphereCast(cameraPivotTransform.position, cameraCollisionRadius, direction, out hit, Mathf.Abs(targetCameraZPosition), collideWithLayers)){
                //se tem, pegamos a distancia dele
                float distanceFromHitObject = Vector3.Distance(cameraPivotTransform.position, hit.point);
                // fazemos a equacao para acharmos o posicao alvo Z
                targetCameraZPosition = -(distanceFromHitObject - cameraCollisionRadius);
            }

            //se nossa posicao alvo eh menor que o raio da colisao, subtraimos a colisao do raio para dar um snap
            if(Mathf.Abs(targetCameraZPosition)< cameraCollisionRadius)
            {
                targetCameraZPosition = -cameraCollisionRadius;
            }

            // Ent nos aplicamos a posicao final usando lerp pelo tempo
            cameraObjectPosition.z = Mathf.Lerp(cameraObj.transform.localPosition.z, targetCameraZPosition, 0.2f);
            cameraObj.transform.localPosition = cameraObjectPosition;
        }
    }
}
