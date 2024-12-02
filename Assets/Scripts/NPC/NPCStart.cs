using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    public class NPCStart : MonoBehaviour
    {
         public List<Transform> waypoints; // Lista de waypoints
        public float rotationSpeed = 5f;  // Velocidade de rotação
        private int currentWaypointIndex = 0; // Índice do waypoint atual
        private NavMeshAgent agent; // Agente do NavMesh

    private void Start()
    {
        // Inicializa o NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        if (waypoints.Count > 0)
        {
            MoveToNextWaypoint();
        }
    }

    // Método para começar a corrida e movimentação para os waypoints
    public void StartTrigger()
    {
        StartCoroutine(MoveThroughWaypoints());
    }

    // Coroutine para mover e girar entre os waypoints
    private IEnumerator MoveThroughWaypoints()
    {
        while (currentWaypointIndex < waypoints.Count)
        {
            // Obtém o waypoint atual
            Transform targetWaypoint = waypoints[currentWaypointIndex];

            // Move o agente para o waypoint
            agent.SetDestination(targetWaypoint.position);
            
            // Enquanto o agente não alcançar o waypoint, gira e anda até ele
            while (Vector3.Distance(transform.position, targetWaypoint.position) > 0.1f)
            {
                // Gira o personagem em direção ao waypoint
                Vector3 direction = (targetWaypoint.position - transform.position).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                yield return null;
            }

            // O personagem chegou ao waypoint, então para
            agent.isStopped = true;

            // Aguarda um momento antes de continuar
            yield return new WaitForSeconds(1f);

            // Proxima waypoint
            currentWaypointIndex++;

            // Retorna o agente ao movimento
            agent.isStopped = false;
        }
    }

    // Método para mover diretamente até um waypoint
    private void MoveToNextWaypoint()
    {
        if (waypoints.Count > 0)
        {
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }
}
    
}
