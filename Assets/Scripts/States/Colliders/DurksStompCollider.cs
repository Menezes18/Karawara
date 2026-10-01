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
            GameObject stompVFX = Instantiate(durkCharacterManager.durkCombatManager.durkImpactVFX, transform);

            Collider[] colliders = Physics.OverlapSphere(transform.position, durkCharacterManager.durkCombatManager.stompAttackAOERadius, WorldUtilityManager.Instance.GetCharacterLayers());

            List<CharacterManager> charactersDamaged = new List<CharacterManager>();

            foreach (var collider in colliders)
            {
                CharacterManager character = collider.GetComponentInParent<CharacterManager>();

                if (character != null)
                {
                    if (charactersDamaged.Contains(character))
                        continue;

                    //  WE DONT WANT DURK TO HURT HIMSELF WHEN HE STOMPS
                    if (character == durkCharacterManager)
                        continue;

                    charactersDamaged.Add(character);

                    //  WE ONLY PROCESS DAMAGE IF THE CHARACTER "ISOWNER" SO THAT THEY ONLY GET DAMAGED IF THE COLLIDER CONNECTS ON THEIR CLIENT
                    //  MEANING IF YOU ARE HIT ON THE HOSTS SCREEN BUT NOT ON YOUR OWN, YOU WILL NOT BE HIT
                    if (character.IsOwner)
                    {
                        //  CHECK FOR BLOCK

                        TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
                        damageEffect.physicalDamage = durkCharacterManager.durkCombatManager.stompDamage;
                        damageEffect.poiseDamage = durkCharacterManager.durkCombatManager.stompDamage;

                        character.characterEffectsManager.ProcessInstantEffect(damageEffect);
                    }
                }
            }
        }
    }
}
