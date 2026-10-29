using UnityEngine;
using UnityEngine.UIElements;

public class MoveBetweenWaypoints : MonoBehaviour
{
    public Camera mainCamera;
    public Transform[] waypoints;
    public float speed = 2f;
    public float offset = 2f;

    private int currentWaypointIndex = 0;

    public Image painel;

    void Update()
    {
        if (waypoints.Length == 0)
            return;

        Vector3 targetPosition = waypoints[currentWaypointIndex].position + mainCamera.transform.forward * offset;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }
}