using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class CursorManager : MonoBehaviour
    {
        // Este método é usado para travar o cursor na tela
        public static void TravarCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    
        // Este método é usado para destravar o cursor
        public static void DestravarCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    
        // Exemplo de uso:
        void Start()
        {
            // Para travar o cursor no início do jogo
            TravarCursor();
        }
    
        void Update()
        {
            // Exemplo de como alternar entre travar e destravar o cursor usando uma tecla (por exemplo, a tecla Escape)
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    DestravarCursor();
                }
                else
                {
                    TravarCursor();
                }
            }
        }
    }

}
