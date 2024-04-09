using System.Collections;
using System.Collections.Generic;
using GenshinImpactMovementSystem;
using UnityEngine;
using Cinemachine;
namespace RPGKarawara
{   
    [RequireComponent(typeof(PlayerInput))]
    
    public class Player : MonoBehaviour
    {
        public static Player instancia;
        [field: Header("References")]
        [field: SerializeField] public PlayerSO Data { get; private set; }

        [field: Header("Collisions")]
        [field: SerializeField] public PlayerCapsuleColliderUtility ColliderUtility { get; private set; }

        [field: SerializeField] public PlayerLayerData LayerData { get; private set; }

        [field: Header("Cameras")]
        [field: SerializeField] public PlayerCameraUtility CameraUtility { get; private set; }

        [field: Header("Animations")]
        [field: SerializeField] public PlayerAnimationData AnimationData { get; private set; }
        public Animator Animator { get; private set; }
        public Rigidbody Rigidbody { get; private set; }

        public PlayerInput Input { get; private set; }

        public Transform MainCameraTransform { get; private set; }

        private PlayerMovementStateMachine movementStateMachine;

        private CombatController _combatController;
        private Quaternion targetRotation;
        public CinemachineVirtualCamera virtualCamera;
        public Vector3 movedir;
        public float moveAmount;
        private void Awake() 
        {
            instancia = this;
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

        private void Update() 
        {         
            
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
