using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class ComecarJogo : MonoBehaviour
    {
        public GameObject Menu;
        public Button newGame;
        
        void Update(){
            if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                if (TitleScreenManager.Instance == null || !TitleScreenManager.Instance.TryStartLocalRuntime()) return;
                if (Menu != null) Menu.SetActive(true);
                if (newGame != null) newGame.Select();
                gameObject.SetActive(false);
            }
        }
    }
}
