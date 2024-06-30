using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Combate1 : MonoBehaviour
{
    public GameObject recepitaculo; // Referência ao GameObject Recepitaculo
    public GameObject barreiraFrontal; // Referência ao GameObject Barreira Frontal

    private List<GameObject> inimigos; // Lista para armazenar inimigos

    void Start()
    {
        // Inicializa a lista de inimigos
        inimigos = new List<GameObject>();

        // Encontra todos os objetos com a tag "Inimigo" na cena
        GameObject[] inimigosEncontrados = GameObject.FindGameObjectsWithTag("Inimigo");

        // Adiciona cada inimigo encontrado à lista
        foreach (GameObject inimigo in inimigosEncontrados)
        {
            inimigos.Add(inimigo);
        }

        // Desativa o Recepitaculo e ativa a Barreira Frontal
        recepitaculo.SetActive(false);
        barreiraFrontal.SetActive(true);
    }

    void Update()
    {
        // Remove quaisquer inimigos que foram destruídos da lista
        inimigos.RemoveAll(inimigo => inimigo == null);

        // Verifica se a lista de inimigos está vazia
        if (inimigos.Count == 0)
        {
            // Ativa o Recepitaculo e desativa a Barreira Frontal
            recepitaculo.SetActive(true);
            barreiraFrontal.SetActive(false);
            Destroy(gameObject);
        }
    }
}
