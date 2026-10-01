using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuebraTotem : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Verifica se o objeto colidido possui a tag "Inimigo" ou "Boss"
        if (collision.gameObject.CompareTag("Inimigo") || collision.gameObject.CompareTag("Boss"))
        {
            // Desativa o próprio objeto
            this.gameObject.SetActive(false);
        }
    }
}
