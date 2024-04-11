using System.Collections;
using System.Collections.Generic;
using GenshinImpactMovementSystem;
using UnityEngine;
using Cinemachine;
using Unity.Mathematics;

namespace RPGKarawara
{   
    [RequireComponent(typeof(PlayerInput))]
    
    public class Player : MonoBehaviour
    {

        public static Player instancia;

        //public static Player player;
        [field: Header("References")]
        [field: SerializeField] public PlayerSO Data { get; private set; }

        [field: Header("Collisions")]
        [field: SerializeField] public PlayerCapsuleColliderUtility ColliderUtility { get; private set; }

        [field: SerializeField] public PlayerLayerData LayerData { get; private set; }

        [field: Header("Cameras")]
        [field: SerializeField] public PlayerCameraUtility CameraUtility { get; private set; }

        [field: Header("Animations")]
        [field: SerializeField] public PlayerAnimationData AnimationData { get; private set; }
        public Animator Animator;
        public Rigidbody Rigidbody { get; private set; }

        public PlayerInput Input { get; private set; }

        public Transform MainCameraTransform { get; private set; }

        private PlayerMovementStateMachine movementStateMachine;


        private CombatController _combatController;
        public Quaternion targetRotation;
        public CinemachineVirtualCamera virtualCamera;
        public Vector3 movedir;
        public float moveAmount;
        [SerializeField] float rotationSpeed = 500f;
        private void Awake() 
        {
            instancia = this;
            //player = this;

            Rigidbody = GetComponent<Rigidbody>();
            Animator = GetComponentInChildren<Animator>();
            _combatController = GetComponent<CombatController>();
            Input = GetComponent<PlayerInput>();

            ColliderUtility.Initialize(gameObject);
            ColliderUtility.CalculateCapsuleColliderDimensions();
            CameraUtility.Initialize();
            AnimationData.Initialize();

            MainCameraTransform = Camera.main.transform;

            movementStateMachine = new PlayerMovementStateMachine(this);
        }

        private void OnValidate() 
        {
            ColliderUtility.Initialize(gameObject);
            ColliderUtility.CalculateCapsuleColliderDimensions();
            
        }

        private void Start() 
        {
            movementStateMachine.ChangeState(movementStateMachine.IdlingState);
        }
        private void OnTriggerEnter(Collider collider) 
        {
            movementStateMachine.OnTriggerEnter(collider);
        }

        private void OnTriggerExit(Collider collider) 
        {
            movementStateMachine.OnTriggerExit(collider);
        }
            Vector3 CalculateMoveDirection(Vector2 input)
            {
                // Obter a rotação planar da câmera
                Quaternion planarRotation = Quaternion.Euler(0f, virtualCamera.transform.rotation.eulerAngles.y, 0f);

                // Calcular a direção do movimento baseada na rotação planar da câmera
                Vector3 moveDir = planarRotation * new Vector3(input.x, 0f, input.y);

                return moveDir;
            }

        private void Update() 
        {         
            if (_combatController.CombatMode)
            {
                
                // Obter a direção do movimento do jogador
                Vector2 movementInput = Input.PlayerActions.Movement.ReadValue<Vector2>();
                Vector3 moveDir = CalculateMoveDirection(movementInput);

                // Obter a posição do inimigo alvo
                Vector3 targetEnemyPosition = _combatController.TargetEnemy.transform.position;

                // Calcular a rotação para mirar no inimigo
                Vector3 targetVec = targetEnemyPosition - transform.position;
                targetVec.y = 0;

                // Verificar se o jogador está se movendo
                if (moveDir.magnitude > 0)
                {
                    targetRotation = Quaternion.LookRotation(targetVec);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                // Calcular as velocidades de movimento do jogador
                float forwardSpeed = Vector3.Dot(transform.forward, Rigidbody.velocity);
                float strafeSpeed = Vector3.Dot(transform.right, Rigidbody.velocity);

                // Atualizar os parâmetros do Animator com as velocidades calculadas
                Animator.SetFloat("forwardSpeed", forwardSpeed / 10, 0.1f, Time.deltaTime);
                Animator.SetFloat("strafeSpeed", strafeSpeed / 10, 0.1f, Time.deltaTime);
            }

            if (virtualCamera != null)
            {
                // Obtenha a rotação planar da câmera
                Quaternion planarRotation = Quaternion.Euler(0f, virtualCamera.transform.rotation.eulerAngles.y, 0f);

                // Use a rotação planar da câmera para calcular a direção de movimento
                Vector3 moveDir = planarRotation * new Vector3(Input.PlayerActions.Movement.ReadValue<Vector2>().x, 0f, Input.PlayerActions.Movement.ReadValue<Vector2>().y);

                // Salve a direção de movimento para uso posterior, se necessário
                movedir = moveDir;
            }
            movementStateMachine.HandleInput();

           movementStateMachine.Update(); 
        }
        private void FixedUpdate() 
        {
            movementStateMachine.PhysicsUpdate();
        }

        public void OnMovementStateAnimationEnterEvent()
        {
            movementStateMachine.OnAnimationEnterEvent();
        }

        public void OnMovementStateAnimationExitEvent()
        {
            movementStateMachine.OnAnimationExitEvent();
        }

        public void OnMovementStateAnimationTransitionEvent()
        {
            movementStateMachine.OnAnimationTransitionEvent();
        }

    }
}
