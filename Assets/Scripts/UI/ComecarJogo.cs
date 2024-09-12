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
            if (Keyboard.current.anyKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
            {
                TitleScreenManager.Instance.StartNetworkAsHost();
                Menu.SetActive(true);
                newGame.Select();
                gameObject.SetActive(false);
            }
        }
    }
}
