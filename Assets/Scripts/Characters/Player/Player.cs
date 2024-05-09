using System.Collections;
using System.Collections.Generic;
using GenshinImpactMovementSystem;
using UnityEngine;
using Cinemachine;
using Unity.Mathematics;
using UnityEngine.InputSystem;


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

        public PlayerInput Input;
        public int Level;

        public Transform MainCameraTransform { get; private set; }

        private PlayerMovementStateMachine movementStateMachine;


        public CombatController _combatController;
        public Quaternion targetRotation;
        public CinemachineVirtualCamera virtualCamera;
        public float rotationY;
        public MeeleFighter meeleFighter;
        public Quaternion PlanarRotation => Quaternion.Euler(0, rotationY, 0);
        public float moveAmount;
        public Vector3 InputDir;
        public Vector3? attackDirPlayer;

        public float stamina = 5f;

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

        private void Update()
        {

            Vector2 movementInput = Input.GetMovementInput();

            // Convertendo o movementInput para um Vector3, mantendo a componente y como 0
            Vector3 moveInput = new Vector3(movementInput.x, 0f, movementInput.y);

            // Normalizando o vetor de movimento
            moveInput = moveInput.normalized;

            // Obtendo a rotação planar da câmera
            Quaternion cameraRotation = Quaternion.Euler(0f, MainCameraTransform.eulerAngles.y, 0f);

            // Rotacionando o vetor de movimento de acordo com a rotação da câmera
            Vector3 moveDirection = cameraRotation * moveInput;

            // Definindo o InputDir como o vetor de movimento resultante
            InputDir = moveDirection;

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

        public void StopStaminaCoroutine(){
            StopCoroutine("IncreaseStaminaOverTime");
        }
        
        IEnumerator IncreaseStaminaOverTime()
        {
            while (stamina <= 5f)
            {
                stamina += Time.deltaTime;
                yield return null;
            }

    
        }
    }
}
