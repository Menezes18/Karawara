using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public class TitleScreenLoadMenuInputManager : MonoBehaviour
    {
        PlayerControls playerControls;

        [Header("Title Screen Inputs")]
        [SerializeField] bool deleteCharacterSlot = false;
        [SerializeField] bool back = false;

        private void Update()
        {
            if (deleteCharacterSlot)
            {
                deleteCharacterSlot = false;
                TitleScreenManager.Instance.AttemptToDeleteCharacterSlot();
            }
            
            if(back)
            {
                back = false;
                TitleScreenManager.Instance.CloseLoadGameMenu();
            }
        }

        private void OnEnable()
        {
            if (playerControls == null)
            {
                playerControls = new PlayerControls();
                playerControls.UI.X.performed += i => deleteCharacterSlot = true;
                playerControls.UI.PauseBack.performed += i => back = true;
            }

            playerControls.Enable();
        }

        public void OnButtonClick()
        {
            deleteCharacterSlot = true;
        }

        private void OnDisable()
        {
            playerControls.Disable();
        }
    }
}
