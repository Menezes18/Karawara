using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class SpellItem : Item
    {
        [Header("Spell Class")]
        public SpellClass SpellClass;
        

        [Header("Spell Modifiers")]
        //public float fullChargeEffectMultiplier = 2;
        public int spellSlotsUsed = 1;

        [Header("Spell FX")]
        [SerializeField] protected GameObject spellCastWarmUpFX;
        [SerializeField] protected GameObject spellCastReleaseFX;
        //  FULL CHARGE VERSION OF FX (TO DO)

        [Header("Animations")]
        [SerializeField] protected string mainHandSpellAnimation;
        [SerializeField] protected string offHandSpellAnimation;

        [Header("Sound FX")]
        public AudioClip warmUpSoundFX;
        public AudioClip releaseSoundFX;

        //  THIS IS WHERE YOU PLAY THE "WARM UP" ANIMATION
        public virtual void AttemptToCastSpell(PlayerManager player)
        {

        }

        //  THIS IS WHERE YOU PLAY THE "THROW" OR "CAST" ANIMATION
        public virtual void SuccessfullyCastSpell(PlayerManager player)
        {

        }

        //  SPELL FX THAT ARE INSTANTIATED WHEN ATTEMPTING TO CAST THE SPELL
        public virtual void InstantiateWarmUpSpellFX(PlayerManager player)
        {

        }

        //  SPELL FX THAT ARE INSTANTIATED WHEN SPELL HAS BEEN SUCCESSFULLY CAST
        public virtual void InstantiateReleaseFX(PlayerManager player)
        {

        }

        //  HELPER FUNCTION TO CHECK WEATHER OR NOT WE ARE ABLE TO USE THE SPELL WHEN ATTEMPTING TO CAST
        public virtual bool CanICastThisSpell(PlayerManager player)
        {
            return true;
        }
    }
}
