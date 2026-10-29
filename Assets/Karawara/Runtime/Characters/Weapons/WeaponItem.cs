using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Karawara
{
    [CreateAssetMenu(menuName = "Items/Weapons/item")]
    public class WeaponItem : ScriptableObject
    {
        [Header("Animations")]
        public AnimatorOverrideController weaponAnimator;
        
       

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

        [Header("Weapon Poise")]
        public float poiseDamage = 10;
        

        // [Header("Actions")]
        // public WeaponItemAction oh_RB_Action;   // ONE HAND RIGHT BUMPER ACTION
        // public WeaponItemAction oh_RT_Action;   // ONE HAND RIGHT TRIGGER ACTION
        // public WeaponItemAction oh_LB_Action;   // ONE HAND LEFT BUMPER ACTION

        [Header("SFX")]
        public AudioClip[] whooshes;
        public AudioClip[] blocking;
    }
}
