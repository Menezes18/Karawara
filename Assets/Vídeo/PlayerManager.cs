using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerManager : CharacterManager
    {
        PlayerLocomotionManager locomotionManager;
        public PlayerAnimatorManager playerAnimatorManager;

        protected override void Awake()
        {
            base.Awake();

            locomotionManager = GetComponent<PlayerLocomotionManager>();
            playerAnimatorManager = GetComponent<PlayerAnimatorManager>();
        }

        protected override void Update()
        {
            base.Update();

            locomotionManager.HandleAllMovemmment();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            PlayerCamera.instance.HandleAllCameraActions();
        }


    }
}
