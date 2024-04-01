using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class CombatController : MonoBehaviour
    {
        MeeleFighter meeleFighter;
        

        private void Awake()
        {
            meeleFighter = GetComponent<MeeleFighter>();
        }
        private void Update()
        {
            if(Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log("Atack");
                
                meeleFighter.TryToAttack();
            }
        }
    }
}
