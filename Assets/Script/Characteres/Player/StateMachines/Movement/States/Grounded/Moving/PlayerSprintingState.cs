using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerSprintingState : PlayerGroundedState
    {
        public PlayerSprintingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
    }
}
