using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    
    public class CombatController : MonoBehaviour
    {
        public static CombatController instacia;
        
        
        public MeeleFighter _meeleFighter;

        private void Awake()
        {
            instacia = this;
            _meeleFighter = GetComponent<MeeleFighter>();
        }

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _meeleFighter.TryToAttack();
            }
        }
    }
}
