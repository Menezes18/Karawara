using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace RPGKarawara
{
    public class CharacterCombatManeger : MonoBehaviour
    {
        protected CharacterManager character;

        [Header("Last Attack Animation Performed")]
        public string lastAttackAnimationPerformed;

        public CharacterManager currentTarget
        {
            get => character != null ? CharacterManager.Resolve(character.State.Target) : null;
            set { if (character != null) character.State.SetTarget(value != null ? value.State : null); }
        }

        [Header("Attack Type")]
        public AttackType currentAttackType;

        [Header("Lock On Transform")]
        public Transform lockOnTransform;

        [Header("Attack Flags")]
        public bool canPerformRollingAttack = false;
        public bool canPerformBackstepAttack = false;

        protected virtual void Awake()
        {
            character = GetComponent<CharacterManager>();
        }

        public virtual void SetTarget(CharacterManager newTarget)
        {
            currentTarget = newTarget;
        }

        public void EnableIsInvulnerable()
        {
                character.characterNetworkManager.isInvulnerable.Value = true;
        }

        public void DisableIsInvulnerable()
        {
                character.characterNetworkManager.isInvulnerable.Value = false;
        }

        public void EnableCanDoRollingAttack()
        {
            canPerformRollingAttack = true;
        }

        public void DisableCanDoRollingAttack()
        {
            canPerformRollingAttack = false;
        }

        public void EnableCanDoBackstepAttack()
        {
            canPerformBackstepAttack = true;
        }

        public void DisableCanDoBackstepAttack()
        {
            canPerformBackstepAttack = false;
        }

        public virtual void EnableCanDoCombo()
        {

        }

        public virtual void DisableCanDoCombo()
        {

        }
                
        public void DestroyAllCurrentActionFX()
        {
            character.characterNetworkManager.DestroyAllCurrentActionFX();
        }
    }
}
