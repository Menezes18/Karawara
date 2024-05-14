using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class WeaponItem : Item
    {
        //  ANIMATOR CONTROLLER OVERRIDE (Change attack animations based on weapon you are currently using)

        [Header("Weapon Model")]
        public GameObject weaponModel;

        [Header("Weapon Requirements")]
        public int strengthREQ = 0;
        public int dexREQ = 0;
        public int intREQ = 0;
        public int faithREQ = 0;

        [Header("Weapon Base Damage")]
        public int physicalDamage = 0;
        public int magicDamage = 0;
        public int fireDamage = 0;
        public int holyDamage = 0;
        public int lightningDamage = 0;

        //  WEAPON GUARD ABSORPTIONS (BLOCKING POWER)

        [Header("Weapon Poise")]
        public float poiseDamage = 10;
        //  OFFENSIVE  POISE BONUS WHEN ATTACKING

        [Header("Attack Modifiers")]
        public float light_Attack_01_Modifier = 1.1f;
        //  HEAVY ATTACK MODIFIER
        //  CRITICAL DAMAGE MODIFIER ECT

        [Header("Stamina Cost Modifiers")]
        public int baseStaminaCost = 20;
        public float lightAttackStaminaCostMultiplier = 0.9f;
        //  RUNNING ATTACK STAMINA COST MODIFIER
        //  HEAVY ATTACK STAMINA COST MODIFIER ECT


        [Header("Actions")]
        public WeaponItemAction oh_RB_Action;   // ONE HAND RIGHT BUMPER ACTION
        
        //  ASH OF WAR

        //  BLOCKING SOUNDS
    }
}
