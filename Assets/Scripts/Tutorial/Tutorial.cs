using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace RPGKarawara
{
    public class Tutorial : MonoBehaviour
    {
        public TutorialEnemy tutorialEnemy;
        public TutorialWaypoint tutorialWaypoint;
        public TutorialWaypoint totemWaypoint;
        public TutorialCanvas tutorialCanvas;
        
        private bool wPressed = false;
        private bool aPressed = false;
        private bool sPressed = false;
        private bool dPressed = false;
        private bool ePressed = false;

        private bool onedPressed = false;
        public enum ActionState
        {
            WASDComplete,
            Correr,
            MouseClicksComplete,
            Totem,
            EnemyDead,
            Complete,
        }

        public ActionState currentState = ActionState.WASDComplete;
        private bool wasdPressed = false;
        private int mouseClickCount = 0;
        private float checkRadius = 2f;
        void Update()
        {
            switch (currentState)
            {
                case ActionState.WASDComplete:
                    CheckWASD();
                    SwitchImage(1);
                    break;

                case ActionState.Correr:
                    CheckShiftAndMove();
                    SwitchImage(3);
                    
                    break;

                case ActionState.MouseClicksComplete:
                    CheckMouseClicks();
                    SwitchImage(2);
                    break;
                case ActionState.Totem:
                    TotemCheck();
                    SwitchImage(6);
                    break;
                case ActionState.EnemyDead:
                    Enemy();
                    SwitchImage(4);
                    break;

                case ActionState.Complete:
                    SwitchImage(5);
                    break;
            }
        }

        public void SwitchImage(int image){
            switch (image){
                case 1:
                    tutorialCanvas.wasd.SetActive(true);
                    tutorialCanvas.botao1.SetActive(false);
                    tutorialCanvas.shift.SetActive(false);
                    tutorialCanvas.emeny.SetActive(false);
                    tutorialCanvas.totem.SetActive(false);
                    break;
                case 2:
                    tutorialCanvas.wasd.SetActive(false);
                    tutorialCanvas.botao1.SetActive(true);
                    tutorialCanvas.shift.SetActive(false);
                    tutorialCanvas.emeny.SetActive(false);
                    tutorialCanvas.totem.SetActive(false);
                    break;
                case 3:
                    tutorialCanvas.wasd.SetActive(false);
                    tutorialCanvas.botao1.SetActive(false);
                    tutorialCanvas.shift.SetActive(true);
                    tutorialCanvas.emeny.SetActive(false);
                    tutorialCanvas.totem.SetActive(false);
                    break;
                case 4:
                    tutorialCanvas.wasd.SetActive(false);
                    tutorialCanvas.botao1.SetActive(false);
                    tutorialCanvas.shift.SetActive(false);
                    tutorialCanvas.emeny.SetActive(true);
                    tutorialCanvas.totem.SetActive(false);
                    break;
                case 6:
                    tutorialCanvas.wasd.SetActive(false);
                    tutorialCanvas.botao1.SetActive(false);
                    tutorialCanvas.shift.SetActive(false);
                    tutorialCanvas.emeny.SetActive(false);
                    tutorialCanvas.totem.SetActive(true);
                    break;
                case 5:
                    tutorialCanvas.totem.SetActive(false);
                    tutorialCanvas.wasd.SetActive(false);
                    tutorialCanvas.botao1.SetActive(false);
                    tutorialCanvas.shift.SetActive(false);
                    tutorialCanvas.emeny.SetActive(false);
                    break;
            }
        }
        public void Enemy(){
            
            if (tutorialEnemy.clearArea){
                currentState = ActionState.Complete;
            }
             
            
        }

        public void TotemCheck(){
            if (totemWaypoint.waypointComplete){
                    Debug.Log("Troque o elemento para agua e mate os inimigos");
                if (Keyboard.current.eKey.wasPressedThisFrame){
                    ePressed = true;
                    
                }

                if (ePressed){
                    currentState = ActionState.EnemyDead;
                }    
            }
            
        }
        
        void CheckWASD()
        {
            
            if (Keyboard.current.wKey.wasReleasedThisFrame)
            {
                wPressed = true;
                tutorialCanvas.w.color = Color.green;
            }
            if (Keyboard.current.aKey.wasReleasedThisFrame)
            {
                aPressed = true;
                tutorialCanvas.a.color = Color.green;
            }
            if (Keyboard.current.sKey.wasReleasedThisFrame)
            {
                sPressed = true;
                tutorialCanvas.s.color = Color.green;
            }
            if (Keyboard.current.dKey.wasReleasedThisFrame)
            {
                dPressed = true;
                tutorialCanvas.d.color = Color.green;
            }

           
            if (wPressed && aPressed && sPressed && dPressed)
            {
                wasdPressed = true;
                tutorialCanvas.wasd.SetActive(false);
                
                currentState = ActionState.Correr;
                
            }
        }

        void CheckShiftAndMove()
        {
            tutorialCanvas.shift.SetActive(true);
            if (tutorialWaypoint.waypointComplete){
                tutorialCanvas.digitShift.color = Color.green;
                currentState = ActionState.MouseClicksComplete;
            }
        }
        

        void CheckMouseClicks()
        {
            tutorialCanvas.botao1.SetActive(true);
            if (Keyboard.current[Key.Digit1].wasReleasedThisFrame){
                dPressed = true;
                tutorialCanvas.digit1.color = Color.green;
            }
            if (Mouse.current.leftButton.wasPressedThisFrame && dPressed)
            {
                mouseClickCount++;
            }

            if (mouseClickCount >= 3)
            {
                tutorialCanvas.clickmouse.color = Color.green;
                currentState = ActionState.Totem;
                Debug.Log("Botão esquerdo do mouse clicado 3 vezes.");
            }
        }
        
    }
}
