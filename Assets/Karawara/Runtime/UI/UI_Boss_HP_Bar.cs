using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

namespace RPGKarawara
{
    public class UI_Boss_HP_Bar : UI_StatBar
    {
        [SerializeField] AIBossCharacterManager bossCharacter;
        bool subscribed;

        public void EnableBossHPBar(AIBossCharacterManager boss)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Unsubscribe();
            bossCharacter = boss;
            if (bossCharacter == null || bossCharacter.aiCharacterNetworkManager == null) return;
            bossCharacter.aiCharacterNetworkManager.currentHealth.OnValueChanged += OnBossHPChanged;
            subscribed = true;
            SetMaxStat(bossCharacter.characterNetworkManager.maxHealth.Value);
            SetStat(bossCharacter.aiCharacterNetworkManager.currentHealth.Value);
            TextMeshProUGUI nameText = GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null) nameText.text = bossCharacter.characterName;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Unsubscribe();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RemoveHPBar(0);
        }

        private void Unsubscribe()
        {
            if (subscribed && bossCharacter != null && bossCharacter.aiCharacterNetworkManager != null)
                bossCharacter.aiCharacterNetworkManager.currentHealth.OnValueChanged -= OnBossHPChanged;
            subscribed = false;
        }

        private void OnBossHPChanged(int oldValue, int newValue)
        {
            SetStat(newValue);

            if (newValue <= 0)
            {
                RemoveHPBar(2.5f);
            }
        }

        public void RemoveHPBar(float time)
        {
            Unsubscribe();
            Destroy(gameObject, time);
        }
    }
}
