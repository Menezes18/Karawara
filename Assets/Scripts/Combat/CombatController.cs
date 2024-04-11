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
        public static CombatController instacia;
        public EnemyController TargetEnemy;
        private Animator _animator;
        public MeeleFighter _meeleFighter;
        public CinemachineVirtualCamera _cinemachineCamera;
        private void Awake()
        {
            instacia = this;
            _meeleFighter = GetComponent<MeeleFighter>();
            _animator = GetComponentInChildren<Animator>();
            _cinemachineCamera = FindObjectOfType<CinemachineVirtualCamera>(); // Encontramos a câmera Cinemachine na cena
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
        public Vector3 GetTargetingDir()
        {
            if (_cinemachineCamera != null)
            {
                // A direção de mira será a direção para onde a câmera Cinemachine está olhando
                Vector3 camForward = _cinemachineCamera.transform.forward;
                camForward.y = 0f; // Mantemos a direção apenas no plano horizontal
                return camForward.normalized;
            }
            else
            {
                Debug.LogError("Cinemachine FreeLook Camera não encontrada na cena.");
                return Vector3.zero;
            }
        }
    }
}
