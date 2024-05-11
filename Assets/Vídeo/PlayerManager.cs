using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerManager : CharacterManager
    {
        [HideInInspector] public PlayerAnimatorManager playerAnimatorManager;
        [HideInInspector] public PlayerLocomotionManager playerLocomotionManager;

        protected override void Awake()
        {
            base.Awake();

            //  DO MORE STUFF, ONLY FOR THE PLAYER

            playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            playerAnimatorManager = GetComponent<PlayerAnimatorManager>();
        }

        protected override void Update()
        {
            base.Update();

            //  IF WE DO NOT OWN THIS GAMEOBJECT, WE DO NOT CONTROL OR EDIT IT
            if (!IsOwner)
                return;

            //  HANDLE MOVEMENT
            playerLocomotionManager.HandleAllMovement();
        }

        protected override void LateUpdate()
        {
            if (!IsOwner)
                return;

            base.LateUpdate();

            PlayerCamera.instance.HandleAllCameraActions();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            //  IF THIS IS THE PLAYER OBJECT OWNED BY THIS CLIENT
            if (IsOwner)
            {
                PlayerCamera.instance.player = this;
                PlayerInputManager.instance.player = this;
            }
        }
    }
}
