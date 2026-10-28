using System;
using System.Collections;
using UnityEngine;

using JetBrains.Annotations;

namespace RPGKarawara
{
    public class PlayerCombatManager : CharacterCombatManeger
    {
        [NonSerialized]  public PlayerManager player;

        public WeaponItem currentWeaponBeingUsed;

        [Header("Flags")]
        public bool canComboWithMainHandWeapon = false;
        public bool isWeaponActive = false;
        private Coroutine weaponDeactivateCoroutine;

        public void EnsureRightWeaponActive()
        {
            if (!isWeaponActive)
            {
                player.playerEquipmentManager.SwitchRightWeapon();
                isWeaponActive = true;
            }
        }

        public void DeactivateActiveWeapon()
        {
            if (weaponDeactivateCoroutine != null)
            {
                StopCoroutine(weaponDeactivateCoroutine);
                weaponDeactivateCoroutine = null;
            }
            if (isWeaponActive) player.playerEquipmentManager.SwitchRightWeapon();
            isWeaponActive = false;
        }

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerManager>();
        }

        public void PerformWeaponBasedAction([CanBeNull] WeaponItemAction weaponAction, [CanBeNull] WeaponItem weaponPerformingAction)
        {
            if (!player.IsInitialized || weaponAction == null || weaponPerformingAction == null) return;
            // Se a arma não estiver ativa, ativa ela
            EnsureRightWeaponActive();

            // Cancela a desativação anterior, se existir
            if (weaponDeactivateCoroutine != null)
            {
                StopCoroutine(weaponDeactivateCoroutine);
            }

            {
                
                
               weaponAction?.AttemptToPerformAction(player, weaponPerformingAction);
            }

            // Inicia a rotina para desativar a arma após x tempo segundos se nenhum ataque for realizado
            weaponDeactivateCoroutine = StartCoroutine(DeactivateWeaponAfterDelay());
        }

        private IEnumerator DeactivateWeaponAfterDelay()
        {
            yield return new WaitForSeconds(7f);
            Debug.Log("Saiu");
            // Desativa a arma chamando SwitchRightWeapon novamente
            weaponDeactivateCoroutine = null;
            if (isWeaponActive) player.playerEquipmentManager.SwitchRightWeapon();
            isWeaponActive = false;
        }

        public override void SetTarget(CharacterManager newTarget)
        {
            base.SetTarget(newTarget);

            {
                if (PlayerCamera.instance != null) PlayerCamera.instance.SetLockCameraHeight();
            }
        }

        public void ShutdownActions()
        {
            StopAllCoroutines();
            weaponDeactivateCoroutine = null;
            canComboWithMainHandWeapon = false;
        }

        // ANIMATION EVENT CALLS
        public override void EnableCanDoCombo()
        {
            if (player.playerNetworkManager.isUsingRightHand.Value)
            {
                player.playerCombatManager.canComboWithMainHandWeapon = true;
            }
            else
            {
                // ENABLE OFF HAND COMBO
            }
        }
        public virtual void DrainStaminaBasedOnAttack()
        {

            if (currentWeaponBeingUsed == null)
                return;

            float staminaDeducted = 0;

            switch (currentAttackType)
            {
                case AttackType.LightAttack01:
                   
                    break;
                case AttackType.LightAttack02:
                   
                    break;
                case AttackType.HeavyAttack01:
                    
                    break;
                case AttackType.HeavyAttack02:
                    
                    break;
                case AttackType.ChargedAttack01:
                    
                    break;
                case AttackType.ChargedAttack02:
                   
                    break;
                case AttackType.RunningAttack01:
                    
                    break;
                case AttackType.RollingAttack01:
                    
                    break;
                case AttackType.BackstepAttack01:
                    
                    break;
                default:
                    break;
            }

            //player.playerNetworkManager.currentStamina.Value -= Mathf.RoundToInt(staminaDeducted);
        }
        public void InstantiateSpellWarmUpFX()
        {
            if (player.playerInventoryManager.currentSpell == null)
                return;

            player.playerInventoryManager.currentSpell.InstantiateWarmUpSpellFX(player);
        }

        public void SuccessfullyCastSpell()
        {
            if (player.playerInventoryManager.currentSpell == null)
                return;

            player.playerInventoryManager.currentSpell.SuccessfullyCastSpell(player);
        }
        public void SuccessfullyChargeSpell()
        {
            if (player.playerInventoryManager.currentSpell == null)
                return;

            player.playerInventoryManager.currentSpell.SuccessfullyChargeSpell(player);
        }

        public void SuccessfullyCastSpellFullCharge()
        {
            if (player.playerInventoryManager.currentSpell == null)
                return;

            player.playerInventoryManager.currentSpell.SuccessfullyCastSpellFullCharge(player);
        }
        public override void DisableCanDoCombo()
        {
            player.playerCombatManager.canComboWithMainHandWeapon = false;
            //player.playerCombatManager.canComboWithOffHandWeapon = false;
        }
    }
    
}
