using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCMovement : MonoBehaviour
{
    public List<Transform> waypoints;
    public float moveSpeed = 3.5f;
    public float waypointThreshold = 1f;

    private int currentWaypointIndex = -1;
    private bool isMoving = false;
    private NavMeshAgent navAgent;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
        {
            Debug.LogError("NavMeshAgent não encontrado no NPC!");
            return;
        }

        navAgent.speed = moveSpeed;
        MoveToNextWaypoint();
    }

    private void Update()
    {
        if (isMoving && navAgent.remainingDistance <= waypointThreshold && !navAgent.pathPending)
        {
            isMoving = false;
            MoveToNextWaypoint();
        }
    }

    public void MoveToNextWaypoint()
    {
        if (waypoints.Count == 0) return;

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        navAgent.SetDestination(waypoints[currentWaypointIndex].position);
        isMoving = true;
    }
}