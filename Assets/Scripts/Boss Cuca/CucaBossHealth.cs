using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CucaBossHealth : MonoBehaviour
    {
        public static CucaBossHealth instancia;

        public float maxHealth = 100f;
        private float currentHealth;

        private void Awake()
        {
            instancia = this;
        }

        private void Start()
        {
            currentHealth = maxHealth;
        }

        // Método para aplicar dano ao boss
        public void TakeDamage(float damageAmount)
        {
            currentHealth -= damageAmount;
            Debug.Log("Boss tomou " + damageAmount + " de dano. Vida restante: " + currentHealth);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        // Método para lidar com a morte do boss
        private void Die()
        {
            Debug.Log("Boss derrotado!");
            // Implementar lógica de morte, animação, desativar, etc.
            Destroy(gameObject);
        }

        // Método de detecção de colisão
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Player"){
                Debug.Log("AAAAAAAAAAAAAAAAAAAAAAAAAAAA");
            }
        }
    }
}