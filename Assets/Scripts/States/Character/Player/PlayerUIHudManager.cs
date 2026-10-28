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
        public bool IsHealthBarReady => healthBar != null && healthBar.IsInitialized;

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
            if (healthBar == null) return;
            healthBar.gameObject.SetActive(false);
            healthBar.gameObject.SetActive(true);
        }

        public void SetNewHealthValue(int oldValue, int newValue)
        {
            if (healthBar != null) healthBar.SetStat(newValue);
        }

        public void SetMaxHealthValue(int maxhealth)
        {
            if (healthBar != null) healthBar.SetMaxStat(maxhealth);
        }


        private void Awake()
        {
            if (SkillTreeUiManager.instance != null)
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
                if (healthBar != null) healthBar.gameObject.SetActive(false);
                SetQuickSlotVisibility(rightWeaponQuickSlotIcon, false);
                SetQuickSlotVisibility(leftWeaponQuickSlotIcon, false);
                if (bossHealthBarParent != null) bossHealthBarParent.gameObject.SetActive(false);
                if (bossHealthBarObject != null && bossHealthBarObject.scene.IsValid()) bossHealthBarObject.SetActive(false);
                if (pauseMenu != null) pauseMenu.SetActive(false);
                if (pauseSkillMenu != null) pauseSkillMenu.SetActive(false);
                if (SkillSlot != null) SkillSlot.SetActive(false);
            }
            else
            {
                // Desativa elementos específicos apenas se estiver na "SceneMenu"
                if (healthBar != null) healthBar.gameObject.SetActive(true);
                SetQuickSlotVisibility(rightWeaponQuickSlotIcon, true);
                SetQuickSlotVisibility(leftWeaponQuickSlotIcon, true);
                if (bossHealthBarParent != null) bossHealthBarParent.gameObject.SetActive(true);
                if (bossHealthBarObject != null && bossHealthBarObject.scene.IsValid()) bossHealthBarObject.SetActive(true);
                if (SkillSlot != null) SkillSlot.SetActive(true);
            }
        }

        private void SetQuickSlotVisibility(Image icon, bool visible)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(visible);
            if (!visible) return;
            // The prefab's QuickSlots container is inactive; SceneMenu previously supplied an override.
            for (Transform parent = icon.transform.parent; parent != null && parent != transform && parent.IsChildOf(transform); parent = parent.parent)
            {
                if (parent.gameObject == pauseMenu || parent.gameObject == pauseSkillMenu) return;
                parent.gameObject.SetActive(true);
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
            if (rightWeaponQuickSlotIcon == null || WorldItemDatabase.Instance == null) return;
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

            rightWeaponQuickSlotIcon.sprite = weapon.itemIcon;
            rightWeaponQuickSlotIcon.enabled = true;
        }

        public void SetLeftWeaponQuickSlotIcon(int weaponID)
        {
            if (leftWeaponQuickSlotIcon == null || WorldItemDatabase.Instance == null) return;
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
                if (pauseMenu == null) return;
                pauseMenu.SetActive(!pauseMenu.activeSelf);
        
                if (pauseSkillMenu != null && pauseSkillMenu.activeSelf)
                {
                    pauseSkillMenu.SetActive(false);
                    
                }
            }
            else if (index == 2)
            {
                if (pauseSkillMenu == null) return;
                pauseSkillMenu.SetActive(!pauseSkillMenu.activeSelf);

                if (pauseMenu != null && pauseMenu.activeSelf)
                {
                    pauseMenu.SetActive(false);
                }
            }

            if ((pauseMenu != null && pauseMenu.activeSelf) || (pauseSkillMenu != null && pauseSkillMenu.activeSelf))
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
            if (PlayerInputManager.instance != null && PlayerInputManager.instance.playerControls != null)
            {
                PlayerInputManager.instance.DisableInput(); // Você precisará implementar este método no seu PlayerController
            }
        }

        private void EnablePlayerInputs()
        {
            if (PlayerInputManager.instance != null && PlayerInputManager.instance.playerControls != null)
            {
                PlayerInputManager.instance.EnableInput(); // Você precisará implementar este método no seu PlayerController
            }
        }

        public void activateMarcador(Transform pos){
            if (marcardor == null) return;
            IndicadorObjetivoScript indicador = marcardor.GetComponent<IndicadorObjetivoScript>();
            if (indicador == null) return;
            indicador.objetivo = pos;
            marcardor.SetActive(true);
        }

        public async void ChangeScene()
        {
            var session = GameBootstrap.EnsureInstance().Session;
            var result = await session.ReturnToMenuAsync();
            if (result != Karawara.Core.Session.SessionResult.Success) Debug.LogWarning(session.LastError);
        }
    }
}
