using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CharacterManager : MonoBehaviour
    {
        [HideInInspector]public CharacterController characterController;
        [HideInInspector]public Animator animator;

        protected virtual void Awake()
        {
            DontDestroyOnLoad(this);

            characterController = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

        }

        protected virtual void Update()
        {

        }

        protected virtual void LateUpdate()
        {

        }
    }
}
