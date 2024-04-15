using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
namespace RPGKarawara
{
    
    public class CombatController : MonoBehaviour
    {
        bool combatMode;
        public bool CombatMode
        {
            get => combatMode;
            set {
                combatMode = value;

                if (TargetEnemy == null)
                    combatMode = false;
                _animator.SetBool("combatMode", combatMode);
            }
        }
        public static CombatController instacia;
        
        EnemyController targetEnemy; 
        public EnemyController TargetEnemy
        {
            get => targetEnemy;
            set
            {
                targetEnemy = value;

                if (targetEnemy == null)
                    CombatMode = false;
            }
        }
        private Animator _animator;
        public MeeleFighter _meeleFighter;
        public CinemachineVirtualCamera _cinemachineCamera;
        private void Awake()
        {
            instacia = this;
            _meeleFighter = GetComponent<MeeleFighter>();
            _animator = GetComponentInChildren<Animator>();
            _cinemachineCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                var enemy = EnemyManager.i.GetAttackingEnemy();
                // if (enemy != null && enemy.Fighter.IsCounterable && !_meeleFighter.InAction)
                // {
                //     StartCoroutine(_meeleFighter.PerformCounterAttack(enemy));
                // }
                // else{

                    var enemyToAttack = EnemyManager.i.GetClosestEnemyToDirection(Player.instancia.InputDir);

                    _meeleFighter.TryToAttack(enemyToAttack.Fighter);
                    CombatMode = true;

               // }
            }

            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                CombatMode = !CombatMode;
            }
        }
        private void OnAnimatorMove()
        {
            if (!_meeleFighter.InCounter)
                transform.position += _animator.deltaPosition;

            transform.rotation *= _animator.deltaRotation;
        }
        public Vector3 GetTargetingDir()
        {
            if (_cinemachineCamera != null)
            {
                if (!CombatMode)
                {
                    Vector3 camForward = _cinemachineCamera.transform.forward;
                    camForward.y = 0f; // Mantemos a direção apenas no plano horizontal
                    return camForward.normalized;
                    
                }
                else
                {
                    return transform.forward;
                }
                // A direção de mira será a direção para onde a câmera Cinemachine está olhando
            }
            else
            {
                Debug.LogError("Cinemachine FreeLook Camera não encontrada na cena.");
                return Vector3.zero;
            }
        }
    }
}
