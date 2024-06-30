using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Boss : MonoBehaviour
{
    public Transform player; // Referência ao Transform do Player
    public float speed = 5f; // Velocidade de movimento do inimigo
    public int maxHp = 100; // Vida máxima do inimigo
    public int Hp = 100; // Vida atual do inimigo
    public int danoPlayer = 10; // Dano causado pelo jogador
    public int danoGeral = 5; // Dano causado por outras fontes
    public float stun = 0f; // Valor atual de stun
    public float maxStun = 100f; // Valor máximo de stun
    public float stunPlayer = 25f; // Valor de stun causado pelo jogador
    public List<GameObject> barreiras; // Lista de barreiras
    public List<Material> materiais; // Lista de materiais
    public Slider healthSlider; // Slider de vida do boss

    private float initialSpeed; // Velocidade inicial do inimigo
    private Rigidbody rb;
    private Renderer rend; // Referência ao componente Renderer

    void Start()
    {
        // Obter o componente Rigidbody do inimigo
        rb = GetComponent<Rigidbody>();
        initialSpeed = speed; // Armazena a velocidade inicial

        // Obter o componente Renderer do inimigo
        rend = GetComponent<Renderer>();

        // Configurar o slider de saúde
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHp;
            healthSlider.value = Hp;
        }
        else
        {
            Debug.LogWarning("Slider de saúde não atribuído ao Boss.");
        }

        // Iniciar a rotina de redução de stun
        StartCoroutine(ReduzirStun());
    }

    void FixedUpdate()
    {
        // Calcular a direção do inimigo para o jogador
        Vector3 direction = (player.position - transform.position).normalized;

        // Ignorar a componente Y para não mover verticalmente
        direction.y = 0;

        // Calcular o novo vetor de velocidade
        Vector3 velocity = direction * speed;

        // Aplicar a nova velocidade ao Rigidbody do inimigo
        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Verifica se colidiu com o trigger "HitPlayer"
        if (other.CompareTag("HitPlayer"))
        {
            RecebeDano(danoPlayer);
            stun += stunPlayer; // Adiciona o valor de stun causado pelo jogador

            // Verifica se o valor de stun é maior ou igual ao máximo
            if (stun >= maxStun)
            {
                stun = maxStun - 0.1f; // Ajusta o valor de stun para não ultrapassar o máximo
                Stunado();
            }
        }

        // Verifica se colidiu com o trigger "HitBoxGeral"
        if (other.CompareTag("HitBoxGeral"))
        {
            RecebeDano(danoGeral);
            stun = 0; // Reseta o valor de stun
            FimStun();
        }
    }

    // Método para receber dano
    private void RecebeDano(int dano)
    {
        Hp -= dano;
        Debug.Log("Recebeu dano: " + dano + ", HP restante: " + Hp);

        // Atualizar o slider de saúde
        AtualizarSlider();

        // Verifica se a vida é menor ou igual a zero e aciona o método Morrer
        if (Hp <= 0)
        {
            Morrer();
        }
    }

    // Método para atualizar o slider de saúde
    private void AtualizarSlider()
    {
        if (healthSlider != null)
        {
            // Atualizar o valor do slider com o HP atual
            healthSlider.value = Hp;
        }
    }

    // Método para destruir o objeto quando a vida for menor ou igual a zero
    private void Morrer()
    {
        Debug.Log("Boss morreu!");
        Destroy(gameObject);
    }

    // Método para reduzir o valor de stun ao longo do tempo
    private IEnumerator ReduzirStun()
    {
        while (true)
        {
            if (stun > 0)
            {
                stun -= 1f;
                if (stun < 0)
                {
                    stun = 0;
                }
            }
            if (stun <= 0)
            {
                FimStun();
            }
            yield return new WaitForSeconds(1f);
        }
    }

    // Método para aplicar o stun no inimigo
    private void Stunado()
    {
        //speed /= 2; // Reduz a velocidade pela metade
        rend.material = materiais[1]; // Altera o material para o segundo da lista

        // Desativa todos os componentes da lista de barreiras
        foreach (GameObject barreira in barreiras)
        {
            barreira.SetActive(false);
        }
    }

    // Método para finalizar o stun no inimigo
    private void FimStun()
    {
        speed = initialSpeed; // Retorna a velocidade ao valor inicial
        rend.material = materiais[0]; // Altera o material para o primeiro da lista

        // Reativa todos os componentes da lista de barreiras
        foreach (GameObject barreira in barreiras)
        {
            barreira.SetActive(true);
        }
    }
}
