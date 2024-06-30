using UnityEngine;
using UnityEngine.UI;

public class Inimigo : MonoBehaviour
{
    public Transform player; // Referência ao Transform do Player
    public float speed = 5f; // Velocidade de movimento do inimigo
    public int maxHp = 100; // Vida máxima do inimigo
    public int Hp = 100; // Vida atual do inimigo
    public int danoPlayer = 10; // Dano causado pelo jogador
    public int danoGeral = 5; // Dano causado por outras fontes
    public Slider healthSlider; // Referência ao Slider de saúde

    private Rigidbody rb;

    void Start()
    {
        // Obter o componente Rigidbody do inimigo
        rb = GetComponent<Rigidbody>();

        // Configurar o slider de saúde
        if (healthSlider != null)
        {
            // Definir o valor máximo do slider como o máximo de HP
            healthSlider.maxValue = maxHp;
            // Definir o valor atual do slider como o HP atual
            healthSlider.value = Hp;
        }
        else
        {
            Debug.LogWarning("Slider de saúde não atribuído ao inimigo.");
        }
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
        }

        // Verifica se colidiu com o trigger "HitBoxGeral"
        if (other.CompareTag("HitBoxGeral"))
        {
            RecebeDano(danoGeral);
        }
    }

    // Método para receber dano
    private void RecebeDano(int dano)
    {
        Hp -= dano;
        Debug.Log("Recebeu dano: " + dano + ", HP restante: " + Hp);

        // Atualizar o slider de saúde
        AtualizarSlider();

        // Verificar se a vida é menor ou igual a zero e acionar o método Morrer
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
        Debug.Log("Inimigo morreu!");
        Destroy(gameObject);
    }
}