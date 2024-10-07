using System;
using System.Collections;
using UnityEngine;

public class ProjectileMovement : MonoBehaviour
{
    public float projectileSpeed = 2f; // Velocidade do projétil
    public float destroyDelay = 5f; // Tempo antes do projétil ser destruído

    private Vector3 targetPosition;

    // Método para inicializar o movimento do projétil
    public void Initialize()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        targetPosition = player.transform.position;
        StartCoroutine(MoveProjectile());
    }

    private IEnumerator MoveProjectile()
    {
        Debug.Log(transform.position);
        // Obtém a direção para o alvo
        Vector3 direction = (targetPosition - transform.position).normalized;

        float elapsedTime = 0f;

        while (elapsedTime < destroyDelay)
        {
            // Move o projétil em direção ao player
            transform.position += direction * projectileSpeed * Time.deltaTime;

            // Atualiza o tempo decorrido
            elapsedTime += Time.deltaTime;

            // Aguarda o próximo frame
            yield return null;
        }

        // Destroi o projétil após o tempo especificado
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other){

    if(other.gameObject.CompareTag("Player")){
            Destroy(gameObject);
            Debug.Log("AAA");
        }
    }
}