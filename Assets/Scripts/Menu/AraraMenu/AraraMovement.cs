using UnityEngine;

public class AraraMovement : MonoBehaviour
{
    public Transform[] waypoints;   // Lista de waypoints
    private int currentWaypointIndex = 0;  // Índice do waypoint atual
    public float flightSpeed = 5.0f;  // Velocidade de voo da arara
    public float rotationSpeed = 2.0f;  // Velocidade de rotação
    public float stoppingDistance = 0.5f; // Distância mínima para considerar que chegou ao waypoint

    private Vector3 direction;    // Direção atual da arara

    void Start()
    {
        if (waypoints.Length > 0)
        {
            // Definir o primeiro destino
            direction = (waypoints[currentWaypointIndex].position - transform.position).normalized;
        }
    }

    void Update()
    {
        if (waypoints.Length == 0) return; // Se não houver waypoints, não faz nada

        // Calcula a direção para o waypoint atual
        Vector3 targetDirection = (waypoints[currentWaypointIndex].position - transform.position).normalized;

        // Suavizar a rotação da arara usando Lerp para parecer mais natural
        direction = Vector3.Lerp(direction, targetDirection, Time.deltaTime * rotationSpeed);
        transform.rotation = Quaternion.LookRotation(direction);

        // Move a arara na direção atual
        transform.position += direction * flightSpeed * Time.deltaTime;

        // Verifica se a arara chegou perto o suficiente do waypoint, e então vai para o próximo sem parar
        float distance = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        if (distance < stoppingDistance)
        {
            // Avançar para o próximo waypoint sem parar
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }
}