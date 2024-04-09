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

        private Animator _animator;
        public MeeleFighter _meeleFighter;

        private void Awake()
        {
            instacia = this;
            _meeleFighter = GetComponent<MeeleFighter>();
            _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                var enemy = EnemyManager.i.GetAttackingEnemy();
                if (enemy != null && enemy.Fighter.IsCounterable && !_meeleFighter.InAction)
                {
                    StartCoroutine(_meeleFighter.PerformCounterAttack(enemy));
                }
                else
                {
                    _meeleFighter.TryToAttack();
                    
                }
            }
        }
        private void OnAnimatorMove()
        {
            if (!_meeleFighter.InCounter)
                transform.position += _animator.deltaPosition;

            transform.rotation *= _animator.deltaRotation;
        }
    }
}
