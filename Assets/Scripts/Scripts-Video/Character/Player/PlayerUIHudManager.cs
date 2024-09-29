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
        bool active = false;
        [NonSerialized] public bool paneldesativar = false;
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

        public GameObject menu;

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
                menu.SetActive(false); // Mantém o menu desativado até ser chamado
                SkillSlot.SetActive(false); // Mantém o slot de habilidade desativado
            }
            else
            {
                Debug.Log("true");
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

        void Update()
        {
            if (Keyboard.current.tabKey.wasReleasedThisFrame)
            {
                ToggleMenu();
            }
        }

        void ToggleMenu()
        {
            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                bool isMenuActive = menu.activeSelf;

                // Alterna o estado ativo do GameObject
                menu.SetActive(!isMenuActive);

                if(pauseMenu.activeSelf == true){
                    pauseMenu.SetActive(false);
                }

                // Controla o estado do cursor
                if (!isMenuActive)
                {
                    Cursor.lockState = CursorLockMode.None; // Destrava o cursor
                    Cursor.visible = true; // Torna o cursor visível
                    Time.timeScale = 0f; // Pausa o jogo
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked; // Trava o cursor
                    Cursor.visible = false; // Torna o cursor invisível
                    Time.timeScale = 1f; // Retoma o jogo
                }
            }
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

        public void activatePause()
        {
            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                if (paneldesativar)
                {
                    paneldesativar = false;
                }
                else
                {
                    pauseMenu.SetActive(!pauseMenu.activeSelf);
                    if (menu.activeSelf == true)
                    {
                        menu.SetActive(false);
                    }
                    active = !active;
                    if (active)
                    {
                        Time.timeScale = 0f;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    else
                    {
                        Time.timeScale = 1f;
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
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
