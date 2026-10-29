using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Karawara.Core.Characters;


namespace RPGKarawara
{
    public class PlayerUIManager : MonoBehaviour
    {
        public static PlayerUIManager instance;

        [HideInInspector] public PlayerUIHudManager playerUIHudManager;
        [HideInInspector] public PlayerUIPopUpManager playerUIPopUpManager;

        [Header("UI Flags")]
        public bool menuWindowIsOpen = false;       // INVENTORY SCREEN, EQUIPMENT MENU, BLACKSMITH MENU ECT
        public bool popUpWindowIsOpen = false;      // ITEM PICK UP, DIALOGUE POP UP ECT

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            playerUIHudManager = GetComponentInChildren<PlayerUIHudManager>(true);
            playerUIPopUpManager = GetComponentInChildren<PlayerUIPopUpManager>(true);
        }

        public void BindPlayer(CharacterRuntimeState state, PlayerInputManager input, WorldItemDatabase catalog = null)
        {
            playerUIHudManager?.BindState(state, input, catalog);
            playerUIPopUpManager?.BindOwner(this);
        }
        public void UnbindPlayer(CharacterRuntimeState state) => playerUIHudManager?.UnbindState(state);
        public void ReleaseSession() => playerUIHudManager?.ReleaseSession();
        private void OnDestroy() { ReleaseSession(); if (instance == this) instance = null; }
        private void Start() { DontDestroyOnLoad(gameObject); }
    }
}
