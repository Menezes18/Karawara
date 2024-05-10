using System.Collections;
using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;
using System;


namespace RPGKarawara
{
    public class Player : MonoBehaviour
    {

        public static Player instancia;

        [field: Header("Animations")]
        public Animator Animator;
        public Rigidbody Rigidbody { get; private set; }

        public int Level;

        public Transform MainCameraTransform { get; private set; }
        public CinemachineVirtualCamera virtualCamera;

        public float stamina = 5f;
        public float speed = 5f;

        private void Awake()
        {
            instancia = this;

            Rigidbody = GetComponent<Rigidbody>();
            Animator = GetComponentInChildren<Animator>();

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
