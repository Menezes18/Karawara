using System.Collections;
using UnityEngine;

namespace RPGKarawara.SkillTree {
    public class HealCircle : MonoBehaviour {
        
        private float _healingRate;
        private int _healingAmount;
        
        private bool _playerInRange = false;
        private PlayerManager _playerManager;

        private void Start() {
            _playerManager = FindObjectOfType<PlayerManager>();
            
        }
        
        private void OnTriggerEnter(Collider other) {
            if (other.CompareTag("Player")) {
                _playerInRange = true;
                StartCoroutine(HealOverTime());
            }
        }

        public void Initialize(float healingRate, int healingAmount){
            _healingAmount = healingAmount;
            _healingRate = healingRate;
        }
        private void OnTriggerExit(Collider other) {
            if (other.CompareTag("Player")) {
                _playerInRange = false;
                StopCoroutine(HealOverTime());
            }
        }

        private IEnumerator HealOverTime() {
            while (_playerInRange) {
                _playerManager.playerNetworkManager.currentHealth.Value += _healingAmount;
                yield return new WaitForSeconds(_healingRate);
            }
        }

       
    }
}