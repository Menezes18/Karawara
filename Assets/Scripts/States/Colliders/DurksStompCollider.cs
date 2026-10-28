using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DurksStompCollider : DamageCollider
    {
        [SerializeField] AIDurkCharacterManager durkCharacterManager;

        protected override void Awake()
        {
            base.Awake();

            durkCharacterManager = GetComponentInParent<AIDurkCharacterManager>();
        }

        public void StompAttack()
        {
            charactersDamaged.Clear();
            sourceCharacter = durkCharacterManager;
            physicalDamage = durkCharacterManager.durkCombatManager.stompDamage;
            GameObject stompVFX = Instantiate(durkCharacterManager.durkCombatManager.durkImpactVFX, transform);

            Collider[] colliders = Physics.OverlapSphere(transform.position, durkCharacterManager.durkCombatManager.stompAttackAOERadius, WorldUtilityManager.Instance.GetCharacterLayers());



            foreach (var collider in colliders)
            {
                CharacterManager character = collider.GetComponentInParent<CharacterManager>();

                if (character != null)
                {
                    if (this.charactersDamaged.Contains(character))
                        continue;

                    //  WE DONT WANT DURK TO HURT HIMSELF WHEN HE STOMPS
                    if (character == durkCharacterManager)
                        continue;

                    DamagePublic(character);
                }
            }
        }
    }
}
