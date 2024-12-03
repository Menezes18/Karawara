using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPGKarawara.SkillTree;
namespace RPGKarawara
{
    public class MeleeWeaponDamageCollider : DamageCollider
    {
        [Header("Attacking Character")]
        public CharacterManager characterCausingDamage; // (When calculating damage this is used to check for attackers damage modifiers, effects etc)

        [Header("Weapon Attack Modifiers")]
        public float light_Attack_01_Modifier;
        public float light_Attack_02_Modifier;
        public float heavy_Attack_01_Modifier;
        public float heavy_Attack_02_Modifier;
        public float charge_Attack_01_Modifier;
        public float charge_Attack_02_Modifier;
        public float running_Attack_01_Modifier;
        public float rolling_Attack_01_Modifier;
        public float backstep_Attack_01_Modifier;

        private ElementManager elementManager;

        protected override void Awake()
        {
            base.Awake();

            if (damageCollider == null)
            {
                damageCollider = GetComponent<Collider>();
            }

            damageCollider.enabled = false; // MELEE WEAPON COLLIDERS SHOULD BE DISABLED AT START, ONLY ENABLED WHEN ANIMATIONS ALLOW

            // Encontrar o ElementManager na cena
            elementManager = FindObjectOfType<ElementManager>();
            if (elementManager == null)
            {
                Debug.LogError("ElementManager not found in the scene!");
            }
        }
        
        protected override void OnTriggerEnter(Collider other)
        {
            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null)
            {
                // WE DO NOT WANT TO DAMAGE OURSELVES
                if (damageTarget == characterCausingDamage)
                    return;

                contactPoint = other.gameObject.GetComponent<Collider>().ClosestPointOnBounds(transform.position);

                // CHECK IF WE CAN DAMAGE THIS TARGET BASED ON FRIENDLY FIRE

                // CHECK IF TARGET IS BLOCKING

                DamageTarget(damageTarget);
            }
            
        }
        
        protected override void CheckForBlock(CharacterManager damageTarget){
            
            if (damageTarget.characterNetworkManager.isBlocking.Value){
                charactersDamaged.Add(damageTarget);
                TakeBlockedDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.TakeBlockedDamageEffect);
                damageEffect.physicalDamage = physicalDamage;
                damageEffect.magicDamage = magicDamage;
                damageEffect.fireDamage = fireDamage;
                damageEffect.holyDamage = holyDamage;

                damageTarget.characterEffectsManager.ProcessInstantEffect(damageEffect);
                
            }
        }

        protected override void DamageTarget(CharacterManager damageTarget)
        {
            // WE DON'T WANT TO DAMAGE THE SAME TARGET MORE THAN ONCE IN A SINGLE ATTACK
            // SO WE ADD THEM TO A LIST THAT CHECKS BEFORE APPLYING DAMAGE
            if (charactersDamaged.Contains(damageTarget))
                return;

            charactersDamaged.Add(damageTarget);

            TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
            damageEffect.physicalDamage = physicalDamage;
            damageEffect.magicDamage = magicDamage;
            damageEffect.fireDamage = fireDamage;
            damageEffect.holyDamage = holyDamage;
            damageEffect.contactPoint = contactPoint;
            damageEffect.angleHitFrom = Vector3.SignedAngle(characterCausingDamage.transform.forward, damageTarget.transform.forward, Vector3.up);

            switch (characterCausingDamage.characterCombatManager.currentAttackType)
            {
                case AttackType.LightAttack01:
                    ApplyAttackDamageModifiers(light_Attack_01_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.LightAttack02:
                    ApplyAttackDamageModifiers(light_Attack_02_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.HeavyAttack01:
                    ApplyAttackDamageModifiers(heavy_Attack_01_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.HeavyAttack02:
                    ApplyAttackDamageModifiers(heavy_Attack_02_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.ChargedAttack01:
                    ApplyAttackDamageModifiers(charge_Attack_01_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.ChargedAttack02:
                    ApplyAttackDamageModifiers(charge_Attack_02_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.RunningAttack01:
                    ApplyAttackDamageModifiers(running_Attack_01_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.RollingAttack01:
                    ApplyAttackDamageModifiers(rolling_Attack_01_Modifier, damageEffect, damageTarget);
                    break;
                case AttackType.BackstepAttack01:
                    ApplyAttackDamageModifiers(backstep_Attack_01_Modifier, damageEffect, damageTarget);
                    break;
                default:
                    break;
            }

            if (characterCausingDamage.IsOwner)
            {
                damageTarget.characterNetworkManager.NotifyTheServerOfCharacterDamageServerRpc(
                    damageTarget.NetworkObjectId,
                    characterCausingDamage.NetworkObjectId,
                    damageEffect.physicalDamage,
                    damageEffect.magicDamage,
                    damageEffect.fireDamage,
                    damageEffect.holyDamage,
                    damageEffect.poiseDamage,
                    damageEffect.angleHitFrom,
                    damageEffect.contactPoint.x,
                    damageEffect.contactPoint.y,
                    damageEffect.contactPoint.z);
            }
        }

        private PlayerNetworkManager _playerNetworkManager;
        private void ApplyAttackDamageModifiers(float modifier, TakeDamageEffect damage, CharacterManager damageTarget)
        {
            damage.physicalDamage *= modifier;
            damage.magicDamage *= modifier;
            damage.fireDamage *= modifier;
            damage.holyDamage *= modifier;
            damage.poiseDamage *= modifier;
            _playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
            
            if (_playerNetworkManager.isPowerPet.Value){
                
                damage.physicalDamage *=  PlayerSkillManager.instance.damageSpirit;
                damage.magicDamage *=  PlayerSkillManager.instance.damageSpirit;
                damage.fireDamage *= PlayerSkillManager.instance.damageSpirit;;
                damage.holyDamage *= PlayerSkillManager.instance.damageSpirit;;
                damage.poiseDamage *= PlayerSkillManager.instance.damageSpirit;;
                return;
            }
            
            // Aplicar modificadores de dano baseados no elemento atual
            if (elementManager != null)
            {
                AICharacterManager aiDamageTarget = damageTarget as AICharacterManager;
                if (aiDamageTarget != null)
                {
                    
                    switch (elementManager.currentElement)
                    {
                        case Element.Fire:
                            if (aiDamageTarget.characterElement == Element.Fire)
                            {
                                damage.physicalDamage *= 1.5f; // Aumenta o dano se ambos forem fogo
                            }
                            else if (aiDamageTarget.characterElement == Element.Water)
                            {
                                damage.physicalDamage *= 0.5f; // Reduz o dano se o alvo for água
                            }
                            break;
                        case Element.Water:
                            if (aiDamageTarget.characterElement == Element.Water)
                            {
                                damage.physicalDamage *= 1.5f;
                            }
                            else if (aiDamageTarget.characterElement == Element.Fire)
                            {
                                damage.physicalDamage *= 0.5f; 
                            }
                            break;
                        default:
                            break;
                    }
                }
                else{
                    
                }
            }
        }
    }
}
