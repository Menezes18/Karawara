using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerMovementStateMachine : StateMachine
    {
        public Player Player { get;}
        public PlayerIdlingState IdlingState { get;}
        public PlayerRunningState RunningState { get;}
        public PlayerSprintingState SprintingState { get; }
        public PlayerWalkingState WalkingState { get; }

        public PlayerMovementStateMachine(Player player)
        {
            Player = player;
            IdlingState = new PlayerIdlingState();

            WalkingState = new PlayerWalkingState();
            RunningState = new PlayerRunningState();
            SprintingState = new PlayerSprintingState();

        }
    }
}
