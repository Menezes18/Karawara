using UnityEngine;

public class AraraMovement : MonoBehaviour
{
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;
    public float flightSpeed = 5.0f;
    public float rotationSpeed = 2.0f;
    public float stoppingDistance = 0.5f;

    private Vector3 direction;

    void Start()
    {
        if (waypoints.Length > 0)
        {
            direction = (waypoints[currentWaypointIndex].position - transform.position).normalized;
        }
    }

    void Update()
    {
        if (waypoints.Length == 0) return;

        Vector3 targetDirection = (waypoints[currentWaypointIndex].position - transform.position).normalized;

        if (targetDirection != Vector3.zero)
        {
            direction = Vector3.Lerp(direction, targetDirection, Time.deltaTime * rotationSpeed);
            transform.rotation = Quaternion.LookRotation(direction);
        }

        transform.position += direction * flightSpeed * Time.deltaTime;

        float distance = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        if (distance < stoppingDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }
}