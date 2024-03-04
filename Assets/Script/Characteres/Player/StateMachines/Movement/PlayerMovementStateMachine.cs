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
        public PlayerRunningState RunningState { get;}
        public PlayerSprintingState SprintingState { get; }
        public PlayerWalkingState WalkingState { get; }

        public PlayerMovementStateMachine(Player player)
        {
            Player = player;
            ReusableData = new PlayerStateReusableData();
            IdlingState = new PlayerIdlingState(this);

            WalkingState = new PlayerWalkingState(this);
            RunningState = new PlayerRunningState(this);
            SprintingState = new PlayerSprintingState(this);

        }
    }
}
