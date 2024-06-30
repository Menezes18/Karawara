using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    public class TutorialWaypoint : MonoBehaviour
    {
        public float checkRadius = 10f; // Radius of the area to check for player proximity
        public Transform waypoint; // The waypoint to check proximity to
        public float waypointThreshold = 5f; // Threshold distance to consider player near waypoint
        private bool shiftPressed = false; // Flag to track if Shift key was released
        public bool waypointComplete = false;

        void Update()
        {
            if (Keyboard.current.leftShiftKey.wasReleasedThisFrame)
            {
                shiftPressed = true;
            }

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, checkRadius);

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    Vector3 playerPosition = hitCollider.transform.position;

                    if (shiftPressed && Vector3.Distance(playerPosition, waypoint.position) < waypointThreshold){
                        waypointComplete = true;
                        Debug.Log("Shift pressionado e jogador chegou próximo ao waypoint. Próxima ação: Correr.");
                    }
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            // Draw a red wire sphere at the transform's position to show the check radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, checkRadius);
        }
    }
}
