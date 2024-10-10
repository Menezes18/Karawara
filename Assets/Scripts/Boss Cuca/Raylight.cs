using System;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class Raylight : MonoBehaviour
{
    public Transform player;                // Referência para o player
    public Transform bossTransform;         // Referência para o boss
    public LineRenderer lineRenderer;       // Linha do raio
    public float raySpeed = 10f;            // Velocidade do raio
    public float maxRayDistance = 30f;      // Distância máxima do raio
    public float damage = 50f;              // Dano do raio
    public float rayDuration = 3f;          // Duração do raio
    public float delayBeforeRay = 1f;       // Delay antes de o raio começar a seguir o player

    private bool isFiringRay = false;       // Se o raio está ativo ou não
    private Vector3 targetPosition;         // Posição do player no momento do disparo
    private float currentRayTime = 0f;      // Tempo de duração do raio

    private void Awake()
    {
        // GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        // player = playerObj.GetComponent<Transform>();
        // bossTransform = this.transform;
    }

    private void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        // Configurações iniciais do LineRenderer
        lineRenderer.startWidth = 0.2f;         // Largura inicial
        lineRenderer.endWidth = 0.2f;           // Largura final
        lineRenderer.positionCount = 2;         // Dois pontos: origem e destino
        lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Material do LineRenderer
        lineRenderer.startColor = Color.yellow;    // Cor do início do raio
        lineRenderer.endColor = Color.red;    // Cor do fim do raio
        lineRenderer.enabled = false;           // Desativa inicialmente
    }

    void Update()
    {
        // Verifica se a tecla L foi pressionada
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            StartFiringRay();
        }

        // Se o raio estiver ativo, chama o método para fazer o raio se mover
        if (isFiringRay)
        {
            FireRay();
        }
    }

    public void StartFiringRay()
    {
        isFiringRay = true;
        targetPosition = player.position; // Define a posição do player
        lineRenderer.enabled = true;       // Ativa o LineRenderer
        StartCoroutine(RayDelayTimer());   // Inicia a Coroutine para o delay antes de começar a seguir o player
    }

    private IEnumerator RayDelayTimer()
    {
        // Adiciona um atraso antes de começar a seguir o player
        yield return new WaitForSeconds(delayBeforeRay);

        // Depois do delay, começa a seguir o player
        StartCoroutine(FollowPlayerRay());
    }

    private void FireRay()
    {
        // O LineRenderer está sempre apontando para o player
        lineRenderer.SetPosition(0, bossTransform.position); // Posição de origem (boss)
        lineRenderer.SetPosition(1, Vector3.MoveTowards(lineRenderer.GetPosition(1), targetPosition, raySpeed * Time.deltaTime));
    }

    private IEnumerator FollowPlayerRay()
    {
        float rayTime = 0f;
        
        // Enquanto o tempo não acabar, o raio vai seguir o player
        while (rayTime < rayDuration)
        {
            // Movimenta o ponto final do raio em direção ao player
            FireRay();

            // Verifica se o raio atingiu o player
            float distanceToPlayer = Vector3.Distance(lineRenderer.GetPosition(1), player.position);
            if (distanceToPlayer < 1f)
            {
                // Colidiu com o player, aplicar dano
                ApplyDamageToPlayer();
                break; // Sai do loop se atingir o player
            }

            rayTime += Time.deltaTime; // Atualiza o tempo do raio
            yield return null;         // Espera até o próximo frame
        }

        // Finaliza o raio após o tempo ou se atingiu o player
        isFiringRay = false;
        lineRenderer.enabled = false; // Desativa o LineRenderer
    }

    private void ApplyDamageToPlayer()
    {
        // Aqui você pode aplicar dano ao player
        // Exemplo: player.GetComponent<PlayerHealth>().TakeDamage(damage);
        Debug.Log("Player atingido pelo raio, causando " + damage + " de dano!");
    }
}
