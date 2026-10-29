using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Karawara.Core.Characters;

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


        private GameSessionController session;
        public void BindSession(GameSessionController owner) => session = owner;
        private HudPresenter presenter;
        private PauseMenuPresenter pausePresenter;
        private CharacterRuntimeState boundState;
        private WorldItemDatabase catalog;
        public void BindState(CharacterRuntimeState state, PlayerInputManager input, WorldItemDatabase items)
        {
            boundState = state; catalog = items;
            if (presenter == null) presenter = new HudPresenter(this);
            if (isActiveAndEnabled) presenter.Bind(state);
            if (pausePresenter == null) pausePresenter = new PauseMenuPresenter(pauseMenu, pauseSkillMenu);
            if (input != null) pausePresenter.Bind(input.Pause, input.RefreshCursorPolicy, SceneManager.GetActiveScene().name == "SceneMenu");
        }
        public void RefreshState() => presenter?.Refresh();
        public void UnbindState(CharacterRuntimeState state)
        {
            if (!ReferenceEquals(boundState, state)) return;
            presenter?.Unbind(); boundState = null; catalog = null;
        }
        public void ReleaseSession() { presenter?.Unbind(); boundState = null; catalog = null; pausePresenter?.Dispose(); pausePresenter = null; }
        private void OnEnable() { if (presenter != null && boundState != null) presenter.Bind(boundState); }
        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConfigureUIBasedOnScene();
        }

        private void ConfigureUIBasedOnScene()
        {
            // Verifica se a cena ativa NÃƒO Ã© "SceneMenu"
            if (SceneManager.GetActiveScene().name == "SceneMenu")
            {
                // Ativa os elementos da UI que nÃ£o devem estar ativos na cena "SceneMenu"
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
                // Desativa elementos especÃ­ficos apenas se estiver na "SceneMenu"
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
            // Chama o mÃ©todo para configurar a UI baseada na nova cena carregada
            pausePresenter?.Close();
            ConfigureUIBasedOnScene();
        }

        private void OnDisable() { presenter?.Unbind(); pausePresenter?.Close(); }
        void OnDestroy()
        {
            // Cancelar o registro do evento para evitar referÃªncias Ã³rfÃ£s
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseSession(); session = null;
        }
        public void SetRightWeaponQuickSlotIcon(int weaponID)
        {
            if (rightWeaponQuickSlotIcon == null || catalog == null) return;
            WeaponItem weapon = catalog.GetWeaponByID(weaponID);

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
            if (leftWeaponQuickSlotIcon == null || catalog == null) return;
            WeaponItem weapon = catalog.GetWeaponByID(weaponID);

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

        public void ActivatePause(int index) => pausePresenter?.Toggle(index);

        public void SetCursorPause(bool value){
            cursorPause = value;
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
            if (session == null) return;
            var result = await session.ReturnToMenuAsync();
            if (result != Karawara.Core.Session.SessionResult.Success) Debug.LogWarning(session.LastError);
        }
    }
}
