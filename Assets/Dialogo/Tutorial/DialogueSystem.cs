using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine;
using Unity.VisualScripting;

namespace RPGKarawara
{
    public class DialogueSystem : MonoBehaviour
    {
        public TextMeshProUGUI dialogueText; // Arraste e solte o componente de texto da UI aqui no Inspector
        public string[] dialogueLines;
        public int currentLineIndex = -2;
        public int cenas = 0;
        bool wait = false;

        // void Update()
        // {
        //     if (cenas == 0)
        //     {
        //         if ((Keyboard.current.wKey.wasPressedThisFrame && currentLineIndex == -1) ||
        //         (Keyboard.current.aKey.wasPressedThisFrame && currentLineIndex == -1) ||
        //         (Keyboard.current.sKey.wasPressedThisFrame && currentLineIndex == -1) ||
        //         (Keyboard.current.dKey.wasPressedThisFrame && currentLineIndex == -1))
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if ((Keyboard.current.wKey.isPressed && Keyboard.current.shiftKey.wasPressedThisFrame && currentLineIndex == 0) ||
        //         (Keyboard.current.aKey.isPressed && Keyboard.current.shiftKey.wasPressedThisFrame && currentLineIndex == 0) ||
        //         (Keyboard.current.sKey.isPressed && Keyboard.current.shiftKey.wasPressedThisFrame && currentLineIndex == 0) ||
        //         (Keyboard.current.dKey.isPressed && Keyboard.current.shiftKey.wasPressedThisFrame && currentLineIndex == 0)
        //         )
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if ((Keyboard.current.wKey.isPressed && Keyboard.current.ctrlKey.wasPressedThisFrame && currentLineIndex == 1) ||
        //         (Keyboard.current.aKey.isPressed && Keyboard.current.ctrlKey.wasPressedThisFrame && currentLineIndex == 1) ||
        //         (Keyboard.current.sKey.isPressed && Keyboard.current.ctrlKey.wasPressedThisFrame && currentLineIndex == 1) ||
        //         (Keyboard.current.dKey.isPressed && Keyboard.current.ctrlKey.wasPressedThisFrame && currentLineIndex == 1))
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if (Keyboard.current.ctrlKey.wasPressedThisFrame && currentLineIndex == 2)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if (Keyboard.current.tabKey.wasPressedThisFrame && currentLineIndex == 3)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if (Mouse.current.leftButton.wasPressedThisFrame && currentLineIndex == 4)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if (Keyboard.current.tabKey.wasPressedThisFrame && currentLineIndex == 5)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if ((Keyboard.current.digit1Key.wasPressedThisFrame && currentLineIndex == 6) ||
        //         (Keyboard.current.digit1Key.wasPressedThisFrame && currentLineIndex == 6) ||
        //         (Keyboard.current.digit3Key.wasPressedThisFrame && currentLineIndex == 6))
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if (Keyboard.current.escapeKey.wasPressedThisFrame && currentLineIndex == 7)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }

        //         if (Keyboard.current.escapeKey.wasPressedThisFrame && currentLineIndex == 8)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }
        //         if (Mouse.current.leftButton.wasPressedThisFrame && currentLineIndex == 9)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }
        //         if (Mouse.current.leftButton.wasPressedThisFrame && currentLineIndex == 10)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }
        //         if (Keyboard.current.anyKey.wasPressedThisFrame && currentLineIndex == 11)
        //         {
        //             ShowNextDialogueLine();
        //             return;
        //         }
        //     } else {
        //         if (Mouse.current.leftButton.wasPressedThisFrame && currentLineIndex == -1 && wait == false)
        //         {
        //             wait = true;
        //             Invoke("ShowNextDialogueLine", 4f);
        //             return;
        //         }

        //         if (Mouse.current.middleButton.wasPressedThisFrame && currentLineIndex == 0 && wait == false)
        //         {
        //             wait = true;
        //             Invoke("ShowNextDialogueLine", 4f);
        //             return;
        //         }
        //     }
        // }

        void OnEnable(){
            ShowNextDialogueLine();
        }

        void ShowNextDialogueLine()
        {
            if (dialogueLines.Length > 0)
            {

                currentLineIndex++;
                if (currentLineIndex >= dialogueLines.Length)
                {
                    dialogueText.text = "";
                }
                else if(currentLineIndex > 0)
                {
                    dialogueText.text = dialogueLines[currentLineIndex];

                }
                else {
                    Debug.Log("-2");
                }
            }
            else
            {
                Debug.Log("AAA");
            }
            wait = false;
        }
    }
}
