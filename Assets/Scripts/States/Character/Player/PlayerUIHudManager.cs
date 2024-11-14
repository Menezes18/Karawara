using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class PlayerUIHudManager : MonoBehaviour
    {
        [Header("Stat Bars")]
        [SerializeField] UI_StatBar healthBar;

        [Header("Quick Slots")]
        [SerializeField] Image rightWeaponQuickSlotIcon;
        [SerializeField] Image leftWeaponQuickSlotIcon;

        [Header("Boss Health Bar")]
        public Transform bossHealthBarParent;
        public GameObject bossHealthBarObject;

        [Header("Marcador")]
        public GameObject marcardor;

        [Header("PAUSE MENU")]
        [SerializeField] GameObject pauseMenu;
        [SerializeField] GameObject SkillSlot;
        public GameObject pauseSkillMenu;
        bool active = false;
        [NonSerialized] public bool cursorPause = false;
        public void RefreshHUD()
        {
            healthBar.gameObject.SetActive(false);
            healthBar.gameObject.SetActive(true);
        }

        public void SetNewHealthValue(int oldValue, int newValue)
        {
            healthBar.SetStat(newValue);
        }

        public void SetMaxHealthValue(int maxhealth)
        {
            healthBar.SetMaxStat(maxhealth);
        }


        private void Awake()
        {
            SkillTreeUiManager.instance.DeactivateAllSkillsAndResetCooldown();
            
        }

        void Start()
        {
            // Registrar o evento para quando uma nova cena for carregada
            SceneManager.sceneLoaded += OnSceneLoaded;

            ConfigureUIBasedOnScene();
        }

        private void ConfigureUIBasedOnScene()
        {
            // Verifica se a cena ativa NÃO é "SceneMenu"
            if (SceneManager.GetActiveScene().name == "SceneMenu")
            {
                // Ativa os elementos da UI que não devem estar ativos na cena "SceneMenu"
                healthBar.gameObject.SetActive(false);
                rightWeaponQuickSlotIcon.gameObject.SetActive(false);
                leftWeaponQuickSlotIcon.gameObject.SetActive(false);
                bossHealthBarParent.gameObject.SetActive(false);
                bossHealthBarObject.SetActive(false);
                pauseMenu.SetActive(false); // Mantém o menu de pausa desativado até ser chamado
                pauseSkillMenu.SetActive(false); // Mantém o menu desativado até ser chamado
                SkillSlot.SetActive(false); // Mantém o slot de habilidade desativado
            }
            else
            {
                // Desativa elementos específicos apenas se estiver na "SceneMenu"
                healthBar.gameObject.SetActive(true);
                rightWeaponQuickSlotIcon.gameObject.SetActive(true);
                leftWeaponQuickSlotIcon.gameObject.SetActive(true);
                bossHealthBarParent.gameObject.SetActive(true);
                bossHealthBarObject.SetActive(true);
                SkillSlot.SetActive(true); // Ativa o slot de habilidades no menu
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Chama o método para configurar a UI baseada na nova cena carregada
            ConfigureUIBasedOnScene();
        }

        void OnDestroy()
        {
            // Cancelar o registro do evento para evitar referências órfãs
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        public void SetRightWeaponQuickSlotIcon(int weaponID)
        {
            WeaponItem weapon = WorldItemDatabase.Instance.GetWeaponByID(weaponID);

            if (weapon == null)
            {
                Debug.Log("ITEM IS NULL");
                rightWeaponQuickSlotIcon.enabled = false;
                rightWeaponQuickSlotIcon.sprite = null;
                return;
            }

            if (weapon.itemIcon == null)
            {
                Debug.Log("ITEM HAS NO ICON");
                rightWeaponQuickSlotIcon.enabled = false;
                rightWeaponQuickSlotIcon.sprite = null;
                return;
            }

            rightWeaponQuickSlotIcon.enabled = true;
        }

        public void SetLeftWeaponQuickSlotIcon(int weaponID)
        {
            WeaponItem weapon = WorldItemDatabase.Instance.GetWeaponByID(weaponID);

            if (weapon == null)
            {
                Debug.Log("ITEM IS NULL");
                leftWeaponQuickSlotIcon.enabled = false;
                leftWeaponQuickSlotIcon.sprite = null;
                return;
            }

            if (weapon.itemIcon == null)
            {
                Debug.Log("ITEM HAS NO ICON");
                leftWeaponQuickSlotIcon.enabled = false;
                leftWeaponQuickSlotIcon.sprite = null;
                return;
            }

            leftWeaponQuickSlotIcon.sprite = weapon.itemIcon;
            leftWeaponQuickSlotIcon.enabled = true;
        }

        public void ActivatePause(int index)
        {
            if (SceneManager.GetActiveScene().buildIndex == 0) return;
    
            if (index == 1)
            {
                pauseMenu.SetActive(!pauseMenu.activeSelf);
        
                if (pauseSkillMenu.activeSelf)
                {
                    pauseSkillMenu.SetActive(false);
                    
                }
            }
            else if (index == 2)
            {
                pauseSkillMenu.SetActive(!pauseSkillMenu.activeSelf);

                if (pauseMenu.activeSelf)
                {
                    pauseMenu.SetActive(false);
                }
            }

            if (pauseMenu.activeSelf || pauseSkillMenu.activeSelf)
            {
                DisablePlayerInputs(); // Desativa os inputs do jogador
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                //Invoke("DeactivatePause", 0.5f);
                EnablePlayerInputs(); // Reativa os inputs do jogador
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
        }

        public void SetCursorPause(bool value){
            cursorPause = value;
        }
        private void DisablePlayerInputs()
        {
            if (PlayerInputManager.instance.playerControls != null)
            {
                PlayerInputManager.instance.DisableInput(); // Você precisará implementar este método no seu PlayerController
            }
        }

        private void EnablePlayerInputs()
        {
            if (PlayerInputManager.instance.playerControls != null)
            {
                PlayerInputManager.instance.EnableInput(); // Você precisará implementar este método no seu PlayerController
            }
        }

        public void activateMarcador(Transform pos){
            marcardor.GetComponent<IndicadorObjetivoScript>().objetivo = pos;
            marcardor.SetActive(true);
        }

        public void ChangeScene()
        {
            WorldAIManager.instance.DespawnAllCharacters();
            SceneManager.LoadScene("SceneMenu");
            ConfigureUIBasedOnScene();
            SceneManager.UnloadSceneAsync(SceneManager.sceneCount);
        }
    }
}
