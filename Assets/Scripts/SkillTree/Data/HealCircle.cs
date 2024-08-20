using System.Collections;
using UnityEngine;

namespace RPGKarawara.SkillTree {
    public class HealCircle : MonoBehaviour {
        public float duration = 5f;          // Duração do Heal Circle
        public float healingRate = 1f;       // Taxa de cura por segundo
        public int healingAmount = 10;       // Quantidade total de cura
        
        private bool _playerInRange = false; // Verifica se o jogador está dentro do trigger
        private PlayerManager _playerManager;

        private void Start() {
            _playerManager = FindObjectOfType<PlayerManager>();
            Invoke(nameof(DestroyCircle), duration);
        }

        private void OnTriggerEnter(Collider other) {
            if (other.CompareTag("Player")) {
                _playerInRange = true;
                StartCoroutine(HealOverTime());
            }
        }

        public void Initialize(){
            
        }
        private void OnTriggerExit(Collider other) {
            if (other.CompareTag("Player")) {
                _playerInRange = false;
                StopCoroutine(HealOverTime());
            }
        }

        private IEnumerator HealOverTime() {
            while (_playerInRange) {
                _playerManager.playerNetworkManager.currentHealth.Value += healingAmount;
                yield return new WaitForSeconds(healingRate);
            }
        }

        private void DestroyCircle() {
            Destroy(gameObject);
        }
    }
}