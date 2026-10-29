using System.Collections;
using UnityEngine;

namespace RPGKarawara.SkillTree {
    public class HealCircle : MonoBehaviour {
        
        private float _healingRate;
        private int _healingAmount;
        
        private bool _playerInRange = false;
        private float _nextHealTime = 0f;
        private PlayerManager _playerManager;

        public PlayerManager Owner => _playerManager;
        public void BindOwner(PlayerManager owner) { _playerManager = owner; _playerInRange = false; }
        private void OnDisable() { _playerInRange = false; _playerManager = null; }
        
        private void Update() {
           
            if (_playerInRange && _playerManager != null && _playerManager.IsInitialized) {
                HealPlayerOverTime();
            }
        }

        private void OnTriggerEnter(Collider other) {
            if (_playerManager != null && other.GetComponentInParent<PlayerManager>() == _playerManager) {
                _playerInRange = true;
            }
        }

        public void Initialize(float healingRate, int healingAmount){
            _healingAmount = healingAmount;
            _healingRate = healingRate;
        }

        private void OnTriggerExit(Collider other) {
            if (_playerManager != null && other.GetComponentInParent<PlayerManager>() == _playerManager) {
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
