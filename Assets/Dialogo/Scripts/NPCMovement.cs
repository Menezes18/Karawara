using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCMovement : MonoBehaviour
{
    public List<Transform> waypoints;  // Lista de waypoints que o NPC deve seguir
    public float moveSpeed = 3.5f;     // Velocidade de movimento do NPC
    public float waypointThreshold = 1f; // Distância mínima para considerar que o NPC chegou ao waypoint

    private int currentWaypointIndex = -1;  // Índice do waypoint atual
    private bool isMoving = false;         // Verifica se o NPC está se movendo
    private NavMeshAgent navAgent;         // NavMeshAgent para controlar o movimento do NPC

    private void Start()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
        {
            Debug.LogError("NavMeshAgent não encontrado no NPC!");
            return;
        }

        navAgent.speed = moveSpeed;

        // NPC não se move até que MoveToNextWaypoint() seja chamado manualmente.
    }

    private void Update()
    {
        // if (Input.GetKeyDown(KeyCode.Space)){
        //     MoveToNextWaypoint();
        // }
        // Verifica se o NPC está se movendo e se já chegou ao waypoint
        if (isMoving && navAgent.remainingDistance <= waypointThreshold && !navAgent.pathPending)
        {
            isMoving = false;  // Para de se mover após alcançar o waypoint
            Debug.Log("NPC chegou ao waypoint.");
        }
    }
    GameObject player;
    float minDistanceFromPlayer = 2.0f;
    public void MoveToPlayer()
    {
        player = GameObject.FindWithTag("Player");

        // Calcula a posição do destino, garantindo que o NPC fique a uma distância mínima do jogador
        Vector3 directionToPlayer = player.transform.position - transform.position;
        Vector3 targetPosition = player.transform.position - directionToPlayer.normalized * minDistanceFromPlayer;

        // Define a nova posição de destino para o NPC
        navAgent.SetDestination(targetPosition);
    }

    public void MoveToNextWaypoint()
    {
        if (waypoints.Count == 0) return;

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;  // Pega o próximo waypoint na lista
        navAgent.SetDestination(waypoints[currentWaypointIndex].position);   // Define o destino do NPC para o próximo waypoint
        isMoving = true;                                                     // Seta que o NPC está se movendo
        Debug.Log("NPC está indo para o próximo waypoint.");
    }
}