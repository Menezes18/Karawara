using UnityEngine;
using UnityEngine.UIElements;

public class MoveBetweenWaypoints : MonoBehaviour
{
    public Camera camera; // A câmera que será usada
    public Transform[] waypoints; // Array de waypoints
    public float speed = 2f; // Velocidade de movimento
    public float offset = 2f; // Offset em relação à frente da câmera

    private int currentWaypointIndex = 0; // Índice do waypoint atual

    public Image painel;

    void Update()
    {
        // Verifica se há waypoints definidos
        if (waypoints.Length == 0)
            return;

        // Define a posição do objeto na frente da câmera
        Vector3 targetPosition = waypoints[currentWaypointIndex].position + camera.transform.forward * offset;

        // Move o objeto em direção ao waypoint
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // Verifica se o objeto chegou ao waypoint
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            // Atualiza o índice para o próximo waypoint
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }
}