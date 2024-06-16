using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SystemXP : MonoBehaviour{
        public static SystemXP Instance;
        public int currentXP = 0;
        public int currentLevel = 1;
        public int xpForNextLevel = 100;
        [SerializeField] private Slider xpBar; // Referência para o slider da barra de XP
        public GameObject xpBarUI; // Referência para o GameObject da barra de XP UI
        private Coroutine hideXPBarCoroutine;
        public GameObject panel;
        
        [Header("UI Menu")]
        [SerializeField] private TextMeshProUGUI xpText;
        [SerializeField] private TextMeshProUGUI currentlevelText;
        [SerializeField] private TextMeshProUGUI nextLevel;
        [SerializeField] private Slider xpUI;
        private void Awake(){
            Instance = this;
        }

        void Start(){
            xpBar.maxValue = xpForNextLevel;
            xpBar.value = currentXP;
            
            xpBarUI.SetActive(false);
            UpdateUI();
        }

        private void Update(){
            if (Keyboard.current.tabKey.wasPressedThisFrame){
                UpdateUI();
                // Alterna o estado ativo do GameObject
                bool isActive = !panel.activeSelf;
                panel.SetActive(isActive);

                // Ativa/desativa e trava/destrava o cursor baseado no estado do GameObject
                if (isActive)
                {
                    Time.timeScale = 0f;
                    Cursor.lockState = CursorLockMode.None; // Destrava o cursor
                    Cursor.visible = true; // Torna o cursor visível
                }
                else
                {
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.Locked; // Trava o cursor
                    Cursor.visible = false; // Torna o cursor invisível
                }
            }
        }

        public void UpdateUI(){
            currentlevelText.text = "Level: " + currentLevel;
            xpText.text = "XP: " + currentXP;
            xpUI.value = xpBar.value;
            nextLevel.text = "Next Level: " + currentLevel;
        }
        public void GainXP(int amount)
        {
            currentXP += amount;
            UpdateXPBar();
            UpdateUI();
            if (currentXP >= xpForNextLevel)
            {
                LevelUp();
            }

            if (hideXPBarCoroutine != null)
            {
                StopCoroutine(hideXPBarCoroutine);
            }
            hideXPBarCoroutine = StartCoroutine(HideXPBarAfterDelay(2.0f));
        }

        void UpdateXPBar()
        {
            xpBarUI.SetActive(true);
            xpBar.value = currentXP;
        }

        IEnumerator HideXPBarAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            xpBarUI.SetActive(false);
        }

        void LevelUp()
        {
            currentXP -= xpForNextLevel;
            currentLevel++;
            xpForNextLevel += 50;
            xpBar.maxValue = xpForNextLevel;
            xpBar.value = currentXP;
            Debug.Log("Parabéns! Você subiu para o nível " + currentLevel);
        }
    }
    
}
