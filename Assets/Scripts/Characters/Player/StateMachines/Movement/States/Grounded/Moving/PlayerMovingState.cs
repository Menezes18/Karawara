using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerMovingState : PlayerGroundedState
    {
        public PlayerMovingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
    }
}
