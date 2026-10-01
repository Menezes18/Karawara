using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevador : MonoBehaviour
{
     private float minY = -6.4f; // Valor mínimo no eixo Y
    private float maxY = -2.5f; // Valor máximo no eixo Y
    public float speed = 1.0f; // Velocidade do movimento

    private int direction = 1; // 1 para cima, -1 para baixo

    void Update()
    {
        // Move o objeto para cima até atingir o maxY
        if (direction == 1 && transform.position.y < maxY)
        {
            Vector3 newPos = transform.position + Vector3.up * speed * Time.deltaTime;
            transform.position = newPos;
        }
        // Move o objeto para baixo até atingir o minY
        else if (direction == -1 && transform.position.y > minY)
        {
            Vector3 newPos = transform.position + Vector3.down * speed * Time.deltaTime;
            transform.position = newPos;
        }
        // Inverte a direção quando alcançar um dos limites
        else
        {
            direction *= -1;
        }
    }
}
