using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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

        private void OnDestroy() { if (instance == this) instance = null; }
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }


    }
}
