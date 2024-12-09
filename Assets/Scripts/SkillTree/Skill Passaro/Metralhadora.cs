using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPGKarawara;
public class Metralhadora : MonoBehaviour
{
    // Guarda a posicao do alvo atual
    [SerializeField] private Transform target;

    // Define a distancia maxima para a mira
    [SerializeField] private float range = 15f;

    [SerializeField] private string enemyTag;

    [SerializeField] private Transform engrenagem;

    [SerializeField] private float turnSpeed = 10f;

    [Header("Ataque")]
    // Define que vamos atirar um projetil por segundo
    [SerializeField] private float fireRate = 1f;
    // Determina a proximo momento de disparo (ja comeca atirando)
    [SerializeField] private float fireCountdown = 0f;

    [SerializeField] private GameObject projetilPrefab;
    [SerializeField] private Transform firePoint;
    private DamageCollider damageCollider;
    private void Start()
    {
        // Vamos chamar o metodo de encontrar alvo assim que entrarmos no Start e apos isso, a cada meio segundo --> dessa forma nao sobrecarregamos o Update
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    private void UpdateTarget()
    {
        // Encontra inimigos spawnados na cena
        GameObject[] inimigos = GameObject.FindGameObjectsWithTag(enemyTag);

        // Variaveis para armazenar informacoes do inimigo mais proximo
        GameObject inimigoMaisProximo = null;
        float distanciaInimigoMaisProximo = Mathf.Infinity;

        // Faz loop no vetor de inimigos e descobre o inimigo mais proximo
        foreach (GameObject inimigo in inimigos)
        {
            float distanciaAteInimigo = Vector3.Distance(transform.position, inimigo.transform.position);

            if (distanciaAteInimigo < distanciaInimigoMaisProximo)
            {
                distanciaInimigoMaisProximo = distanciaAteInimigo;
                inimigoMaisProximo = inimigo;
            }
        }

        // Verifica se o inimigo mais proximo esta dentro da area de cobertura
        if (inimigoMaisProximo != null && distanciaInimigoMaisProximo < range)
        {
            target = inimigoMaisProximo.transform;
        }
        else
        {
            target = null; // limpa o alvo caso nao encontre um alvo valido
        }

    }

    private void Update()
    {
        // Executa a logica somente se tivermos um alvo
        if (target != null)
        {
            Vector3 direcaoParaMirar = target.position - transform.position;

            // Pega a rotacao necessaria para virar para posicao do alvo
            Quaternion rotacaoNecessariaParaVirar = Quaternion.LookRotation(direcaoParaMirar);
            // Faz o calculo do vetor de rotacao 
            Vector3 rotacaoParaMirar = Quaternion.Lerp(engrenagem.rotation, rotacaoNecessariaParaVirar, Time.deltaTime * turnSpeed).eulerAngles;
            // Define a rotacao da engrenagem para este vetor de rotacao
            engrenagem.rotation = Quaternion.Euler(0f, rotacaoParaMirar.y, 0f);

            // Verifica se eh hora de atirar
            if (fireCountdown <= 0f)
            {
                Atirar();
                // Atualiza a variavel fireCountdown para respeitar o fireRate
                fireCountdown = 1f / fireRate;
            }
            // Atualiza a variavel fireCountdown
            fireCountdown -= Time.deltaTime;
        }
    }

    private void Atirar()
    {
        GameObject projetilGObject = (GameObject)Instantiate(projetilPrefab, firePoint.position, firePoint.rotation);
        Projetil projetil = projetilGObject.GetComponent<Projetil>();

        if (projetil != null)
        {
            projetil.BuscarAlvo(target.position);

        }
    }

    // Essa funcao nos permite visualizar a distancia maxima do tiro 
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
