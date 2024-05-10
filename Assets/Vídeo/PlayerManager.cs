using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerManager : CharacterManager
    {
        PlayerLocomotionManager locomotionManager;

        protected override void Awake()
        {
            base.Awake();

            locomotionManager = GetComponent<PlayerLocomotionManager>();
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
