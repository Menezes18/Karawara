using System.Collections;
using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;


namespace RPGKarawara
{
    public class Player : MonoBehaviour
    {

        public static Player instancia;

        [field: Header("Animations")]
        public Animator Animator;
        public Rigidbody Rigidbody { get; private set; }

        public PlayerInput Input;
        public int Level;

        public Transform MainCameraTransform { get; private set; }

        public Quaternion targetRotation;
        public CinemachineVirtualCamera virtualCamera;
        public float rotationY;
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
            Input = GetComponent<PlayerInput>();

            MainCameraTransform = Camera.main.transform;
        }


        private void Update()
        {


        }


        private void FixedUpdate()
        {
            
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
