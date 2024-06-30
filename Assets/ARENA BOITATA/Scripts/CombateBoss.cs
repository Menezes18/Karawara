using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CombateBoss : MonoBehaviour
{
    public GameObject boss; // Referência ao GameObject do boss
    private bool foiCombate = false; // Variável booleana para verificar se o combate foi iniciado
    public List<GameObject> barreira; // Lista de GameObjects das barreiras
    public List<GameObject> fogoArena; // Lista de GameObjects do fogo na arena

    public Slider healthSlider;

    void Start()
    {
        // Desativa o objeto boss no início
        boss.SetActive(false);

        // Marca foiCombate como false no início
        foiCombate = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // Verifica se colidiu com um objeto que tem a tag "Player" e se o combate ainda não foi iniciado
        if (!foiCombate && other.CompareTag("Player"))
        {
            // Ativa o objeto boss e marca foiCombate como true
            boss.SetActive(true);
            healthSlider.gameObject.SetActive(true);
            foiCombate = true;
            foreach (GameObject obj in barreira)
            {
                obj.SetActive(true);
            }
        }
    }

    void Update()
    {
        // Se o combate foi iniciado e o objeto boss não está mais ativo na cena
        if (foiCombate && boss == null)
        {
            healthSlider.gameObject.SetActive(false);
            // Desativa todos os objetos da lista barreira
            foreach (GameObject obj in barreira)
            {
                obj.SetActive(false);
            }

            // Desativa todos os objetos da lista fogoArena
            foreach (GameObject obj in fogoArena)
            {
                obj.SetActive(false);
            }
        }
    }
}
