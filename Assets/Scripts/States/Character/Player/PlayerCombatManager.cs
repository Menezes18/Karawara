using System.Collections;
using UnityEngine;
using Unity.Netcode;
using JetBrains.Annotations;

namespace RPGKarawara
{
    public class PlayerCombatManager : CharacterCombatManeger
    {
        PlayerManager player;

        public WeaponItem currentWeaponBeingUsed;

        [Header("Flags")]
        public bool canComboWithMainHandWeapon = false;
        private bool isWeaponActive = false;
        private Coroutine weaponDeactivateCoroutine;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerManager>();
        }

        public void PerformWeaponBasedAction([CanBeNull] WeaponItemAction weaponAction, [CanBeNull] WeaponItem weaponPerformingAction)
        {
            // Se a arma não estiver ativa, ativa ela
            if (!isWeaponActive)
            {
                player.playerEquipmentManager.SwitchRightWeapon();
                isWeaponActive = true;
            }

            // Cancela a desativação anterior, se existir
            if (weaponDeactivateCoroutine != null)
            {
                StopCoroutine(weaponDeactivateCoroutine);
            }

            if (player.IsOwner){
                
                
               weaponAction?.AttemptToPerformAction(player, weaponPerformingAction);
               // player?.playerNetworkManager.NotifyTheServerOfWeaponActionServerRpc(NetworkManager.Singleton.LocalClientId, weaponAction.actionID, weaponPerformingAction.itemID);
            }

            // Inicia a rotina para desativar a arma após x tempo segundos se nenhum ataque for realizado
            weaponDeactivateCoroutine = StartCoroutine(DeactivateWeaponAfterDelay());
        }

        private IEnumerator DeactivateWeaponAfterDelay()
        {
            yield return new WaitForSeconds(7f);

            // Desativa a arma chamando SwitchRightWeapon novamente
            player.playerEquipmentManager.SwitchRightWeapon();
            isWeaponActive = false;
        }

        public override void SetTarget(CharacterManager newTarget)
        {
            base.SetTarget(newTarget);

            if (player.IsOwner)
            {
                PlayerCamera.instance.SetLockCameraHeight();
            }
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
            if (!player.IsOwner)
                return;

            if (currentWeaponBeingUsed == null)
                return;

            float staminaDeducted = 0;

            switch (currentAttackType)
            {
                case AttackType.LightAttack01:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.lightAttackStaminaCostMultiplier;
                    break;
                case AttackType.LightAttack02:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.lightAttackStaminaCostMultiplier;
                    break;
                case AttackType.HeavyAttack01:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.heavyAttackStaminaCostMultiplier;
                    break;
                case AttackType.HeavyAttack02:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.heavyAttackStaminaCostMultiplier;
                    break;
                case AttackType.ChargedAttack01:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.chargedAttackStaminaCostMultiplier;
                    break;
                case AttackType.ChargedAttack02:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.chargedAttackStaminaCostMultiplier;
                    break;
                case AttackType.RunningAttack01:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.runningAttackStaminaCostMultiplier;
                    break;
                case AttackType.RollingAttack01:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.rollingAttackStaminaCostMultiplier;
                    break;
                case AttackType.BackstepAttack01:
                    staminaDeducted = currentWeaponBeingUsed.baseStaminaCost * currentWeaponBeingUsed.backstepAttackStaminaCostMultiplier;
                    break;
                default:
                    break;
            }

            //player.playerNetworkManager.currentStamina.Value -= Mathf.RoundToInt(staminaDeducted);
        }
        public override void DisableCanDoCombo()
        {
            player.playerCombatManager.canComboWithMainHandWeapon = false;
            //player.playerCombatManager.canComboWithOffHandWeapon = false;
        }
    }
    
}
