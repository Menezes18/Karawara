using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CircleWal : MonoBehaviour
    {
        // Lista de waypoints que o jogador deve seguir
    public List<Transform> waypoints;
    
    // Velocidade do movimento entre waypoints
    public float velocidade = 5f;

    // Índice do waypoint atual
    private int waypointAtual = 0;

    void Update()
    {
        // Se não há waypoints definidos, interrompe o movimento
        if (waypoints.Count == 0) return;

        // Posição atual do waypoint alvo
        Transform alvo = waypoints[waypointAtual];

        // Move o jogador em direção ao waypoint atual
        transform.position = Vector3.MoveTowards(transform.position, alvo.position, velocidade * Time.deltaTime);

        // Verifica se o jogador chegou ao waypoint alvo
        if (Vector3.Distance(transform.position, alvo.position) < 0.1f)
        {
            // Incrementa o índice do waypoint atual para o próximo waypoint
            waypointAtual++;

            // Volta ao primeiro waypoint se o último foi atingido
            if (waypointAtual >= waypoints.Count)
            {
                waypointAtual = 0;
            }
        }
    }
    }
}
