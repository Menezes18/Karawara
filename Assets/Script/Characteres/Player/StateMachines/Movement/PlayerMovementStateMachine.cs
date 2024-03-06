using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerMovementStateMachine : StateMachine
    {
        public Player Player { get;}
        public PlayerStateReusableData ReusableData { get;}
        public PlayerIdlingState IdlingState { get;}
        public PlayerDashingState DashingState { get;}

        public PlayerRunningState RunningState { get;}
        public PlayerSprintingState SprintingState { get; }
        public PlayerWalkingState WalkingState { get; }

        public PlayerLightStoppingState LightStoppingState { get; }
        public PlayerMediumStoppingState MediumStoppingState { get; }
        public PlayerHardStoppingState HardStoppingState { get; }

        public PlayerMovementStateMachine(Player player)
        {
            Player = player;
            ReusableData = new PlayerStateReusableData();

            IdlingState = new PlayerIdlingState(this);
            DashingState = new PlayerDashingState(this);

            WalkingState = new PlayerWalkingState(this);
            RunningState = new PlayerRunningState(this);
            SprintingState = new PlayerSprintingState(this);

            LightStoppingState = new PlayerLightStoppingState(this);
            MediumStoppingState = new PlayerMediumStoppingState(this);
            HardStoppingState = new PlayerHardStoppingState(this);
        }
    }
}
