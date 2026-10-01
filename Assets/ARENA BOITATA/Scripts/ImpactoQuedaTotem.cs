using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactoQuedaTotem : MonoBehaviour
{
    public GameObject hitbox; // Referência ao GameObject filho
    private BoxCollider boxCollider; // Referência ao BoxCollider
    private MeshRenderer meshRenderer; // Referência ao MeshRenderer
    private GameObject Modelo;

    void Start()
    {
        // Inicializa as referências
        boxCollider = GetComponent<BoxCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        Modelo = this.transform.GetChild(1).gameObject;
    }

    private void Caiu()
    {
        // Ativa o objeto filho
        hitbox.SetActive(true);
        Modelo.SetActive(false);

        // Desativa o BoxCollider e o MeshRenderer
        boxCollider.enabled = false;
        meshRenderer.enabled = false;

        // Inicia a coroutine para contar 0.5 segundos e depois desativar o objeto
        StartCoroutine(DesativarAposTempo(0.3f));
    }

    private IEnumerator DesativarAposTempo(float tempo)
    {
        // Espera pelo tempo especificado
        yield return new WaitForSeconds(tempo);

        // Desativa o GameObject que possui o script
        hitbox.SetActive(false);
        gameObject.SetActive(false);
    }
}
