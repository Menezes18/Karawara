using System;
using UnityEngine;
using UnityEngine.UI; // Necessário para trabalhar com o Slider
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    public class HealthManager : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float currentHealth;

        [Header("UI")]
        public Slider healthSlider; // Referência ao slider de vida
        public GameObject healthBar;
        // Tempo entre hits consecutivos para o mesmo objeto
        private float hitCooldown = 0.5f;
        private float lastHitTime = -1f;

        private void Start(){
            healthBar.SetActive(false);
        }

        private void Awake()
        {
            currentHealth = maxHealth;
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }
        }

        public void TakeDamage(float damage)
        {
            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Evita valores negativos

            Debug.Log($"{gameObject.name} recebeu {damage} de dano. Vida atual: {currentHealth}");
            UpdateHealthSlider();

            if (currentHealth <= 20)
            {
                Die();
            }
        }

        public void AtivarHealthBar(){
            healthBar.SetActive(true);
        }
        private void OnTriggerEnter(Collider other)
        {
            // Verifica se o objeto é uma lança
            if (other.CompareTag("Lanca"))
            {
                var damageCollider = other.GetComponent<MeleeWeaponDamageCollider>();
                if (damageCollider != null)
                {
                    // Permite dano se o cooldown tiver passado
                    if (Time.time >= lastHitTime + hitCooldown)
                    {
                        lastHitTime = Time.time; // Atualiza o tempo do último hit
                        float damage = damageCollider.physicalDamage;
                        TakeDamage(damage);
                    }
                }
            }
        }

        public void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            Debug.Log($"{gameObject.name} foi curado em {amount}. Vida atual: {currentHealth}");
            UpdateHealthSlider();
        }

        private void UpdateHealthSlider()
        {
            if (healthSlider != null)
            {
                healthSlider.value = currentHealth;
            }
        }

        private void Die()
        {
            SceneManager.LoadScene("Creditos");
            
        }
    }
}
