using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace RPGKarawara
{
    public class AICharacterNetworkManager : CharacterNetworkManager
    {
        public Transform playerTransform;
        public float visionRange = 10f;
        public float fieldOfViewAngle = 110f;

        private void Update()
        {
            if (IsServer)
            {
                if (CanSeePlayer())
                {
                    MoveTowardsPlayer();
                }
                else
                {
                    StopMoving();
                }
            }
        }

        private bool CanSeePlayer()
        {
            if (playerTransform == null)
            {
                return false;
            }

            Vector3 directionToPlayer = playerTransform.position - transform.position;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

            if (directionToPlayer.magnitude < visionRange && angleToPlayer < fieldOfViewAngle * 0.5f)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position + Vector3.up, directionToPlayer.normalized, out hit, visionRange))
                {
                    if (hit.collider.transform == playerTransform)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void MoveTowardsPlayer()
        {
            if (!isMoving.Value)
            {
                isMoving.Value = true;
                // Logic to move the enemy towards the player
                // For example:
                // character.MoveTo(playerTransform.position);
            }
        }

        private void StopMoving()
        {
            if (isMoving.Value)
            {
                isMoving.Value = false;
                // Logic to stop the enemy from moving
                // For example:
                // character.StopMoving();
            }
        }
    }
}
