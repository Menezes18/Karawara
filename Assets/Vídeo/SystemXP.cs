using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SystemXP : MonoBehaviour{
        public static SystemXP instance;
        public int currentXP = 0;
        public int currentLevel = 1;
        public int xpForNextLevel = 100;
        public Slider xpBar; // Referência para o slider da barra de XP
        public GameObject xpBarUI; // Referência para o GameObject da barra de XP UI
        private Coroutine hideXPBarCoroutine;

        private void Awake(){
            instance = this;
        }

        void Start()
        {
            xpBar.maxValue = xpForNextLevel;
            xpBar.value = currentXP;
            
            xpBarUI.SetActive(false); 
        }

        public void GainXP(int amount)
        {
            currentXP += amount;
            UpdateXPBar();

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
