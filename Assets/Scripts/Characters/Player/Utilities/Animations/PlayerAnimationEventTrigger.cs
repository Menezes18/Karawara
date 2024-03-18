using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerAnimationEventTrigger : MonoBehaviour
    {
        private Player player;

        private void Awake()
        {
            player = transform.parent.GetComponent<Player>();
        }

        public void TriggerOnMovementStateAnimationEnterEvent()
        {
            if (IsAnimationTransition())
            {
                return; 
            }
            player.OnMovementStateAnimationEnterEvent();
        }
        public void TriggerOnMovementStateAnimationExitEvent()
        {
            if (IsAnimationTransition())
            {
                return;
            }
            player.OnMovementStateAnimationExitEvent();
        }
        public void TriggerOnMovementStateAnimationTransitionEvent()
        {
            if (IsAnimationTransition())
            {
                return;
            }
            player.OnMovementStateAnimationTransitionEvent();
        }

        private bool IsAnimationTransition(int layerIndex = 0)
        {
            return player.Animator.IsInTransition(layerIndex);

        }
    }
}
