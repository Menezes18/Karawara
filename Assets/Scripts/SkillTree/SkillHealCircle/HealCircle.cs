using System.Collections;
using UnityEngine;

namespace RPGKarawara.SkillTree {
    public class HealCircle : MonoBehaviour {
        
        private float _healingRate;
        private int _healingAmount;
        
        private bool _playerInRange = false;
        private float _nextHealTime = 0f;
        private PlayerManager _playerManager;

        private void Start() {
            _playerManager = FindObjectOfType<PlayerManager>();
        }
        
        private void Update() {
           
            if (_playerInRange) {
                HealPlayerOverTime();
            }
        }

        private void OnTriggerEnter(Collider other) {
            if (other.CompareTag("Player")) {
                _playerInRange = true;
            }
        }

        public void Initialize(float healingRate, int healingAmount){
            _healingAmount = healingAmount;
            _healingRate = healingRate;
        }

        private void OnTriggerExit(Collider other) {
            if (other.CompareTag("Player")) {
                _playerInRange = false;
            }
        }

        // Método para curar o jogador ao longo do tempo
        private void HealPlayerOverTime() {
            // Verifica se o tempo de espera para a próxima cura já passou
            if (Time.time >= _nextHealTime) {
                _playerManager.playerNetworkManager.currentHealth.Value += _healingAmount;
                _nextHealTime = Time.time + _healingRate; // Define o tempo da próxima cura
            }
        }
    }
}
