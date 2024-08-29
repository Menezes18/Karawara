using UnityEngine;

namespace RPGKarawara{
    [CreateAssetMenu(menuName = "Character Effects/Instant Effects/Take Blocked Damage")]
    public class TakeBlockedDamageEffect : InstantCharacterEffect
    {
        [Header("Character Causing Damage")]
        public CharacterManager characterCausingDamage; // IF A CHARACTER IS CAUSING THIS DAMAGE, THAT CHARACTER IS STORED HERE

        [Header("Damage")]
        public float physicalDamage = 0;            // (TO DO, SPLIT INTO "Standard", "Strike", "Slash" and "Pierce")
        public float magicDamage = 0;
        public float fireDamage = 0;
        public float lightningDamage = 0;
        public float holyDamage = 0;

        [Header("Final Damage")]
        private int finalDamageDealt = 0;         // FINAL DAMAGE TAKEN AFTER ALL CALCULATIONS HAVE BEEN MADE

        [Header("Poise")]
        public float poiseDamage = 0;
        public bool poiseIsBroken = false;          //  IF POISE IS BROKEN CHARACTER IS "STUNNED" AND A DAMAGE ANIMATION IS PLAYED

        //  (TO DO) BUILD UPS
        //  build up effect amounts
        [Header("Sound FX")]
        public bool willPlayDamageSFX = true;
        public AudioClip elementalDamageSoundFX;    //  USED ON TOP OF REGULAR SFX IF THERE IS ELEMENTAL DAMAGE PRESENT (Magic/Fire/Lightning/Holy)

        public override void ProcessEffect(CharacterManager character)
        {
            if (character.characterNetworkManager.isInvulnerable.Value)
                return;

            base.ProcessEffect(character);

            Debug.Log("HIT WAS BLOCKED!");

            //  IF THE CHARACTER IS DEAD, NO ADDITIONAL DAMAGE EFFECTS SHOULD BE PROCESSED
            if (character.isDead.Value)
                return;

            CalculateDamage(character);
            PlayDirectionalBasedBlockingAnimation(character);
            //  CHECK FOR BUILD UPS (POISON, BLEED ECT)
            PlayDamageSFX(character);
            PlayDamageVFX(character);

            //  IF CHARACTER IS A.I, CHECK FOR NEW TARGET IF CHARACTER CAUSING DAMAGE IS PRESENT
        }

        private void CalculateDamage(CharacterManager character)
        {
            if (!character.IsOwner)
                return;

            if (characterCausingDamage != null)
            {
                //  CHECK FOR DAMAGE MODIFIERS AND MODIFY BASE DAMAGE (PHYSICAL/ELEMENTAL DAMAGE BUFF)
            }

            //  CHECK CHARACTER FOR FLAT DEFENSES AND SUBTRACT THEM FROM THE DAMAGE

            //  CHECK CHARACTER FOR ARMOR ABSORPTIONS, AND SUBTRACT THE PERCENTAGE FROM THE DAMAGE

            //  ADD ALL DAMAGE TYPES TOGETHER, AND APPLY FINAL DAMAGE


            physicalDamage -= physicalDamage; // blockingPhysicalAbsorption = 100
            magicDamage = magicDamage;
            fireDamage -= fireDamage; // blockingFireAbsorption = 100
            lightningDamage -= lightningDamage; // blockingLightningAbsorption = 100
            


            finalDamageDealt = Mathf.RoundToInt(physicalDamage + magicDamage + fireDamage + lightningDamage + holyDamage);

            if (finalDamageDealt <= 0)
            {
                finalDamageDealt = 1;
            }

            Debug.Log("FINAL PHYSICAL DAMAGE: " + physicalDamage);
            Debug.Log("ORIGINAL PHYSICAL DAMAGE: " + finalDamageDealt);

            character.characterNetworkManager.currentHealth.Value -= finalDamageDealt;

            //  CALCULATE POISE DAMAGE TO DETERMINE IF THE CHARACTER WILL BE STUNNED
        }

        private void PlayDamageVFX(CharacterManager character)
        {
            //  IF WE HAVE FIRE DAMAGE, PLAY FIRE PARTICLES
            //  LIGHTNING DAMAGE, LIGHTNING PARTICLES ECT

            // 1. GET VFX BASED ON BLOCKING WEAPON
        }

        private void PlayDamageSFX(CharacterManager character)
        {
            //  IF FIRE DAMAGE IS GREATER THAN 0, PLAY BURN SFX
            //  IF LIGHTNING DAMAGE IS GREATER THAN 0, PLAY ZAP SFX

            // 1. GET SFX BASED ON BLOCKING WEAPON
        }

        private void PlayDirectionalBasedBlockingAnimation(CharacterManager character)
        {
            if (!character.IsOwner)
                return;

            if (character.isDead.Value)
                return;

            DamageIntensity damageIntensity = WorldUtilityManager.Instance.GetDamageIntensityBasedOnPoiseDamage(poiseDamage);
            // 2. PLAY A PROPER ANIMATION TO MATCH THE "INTENSITY" OF THE BLOW

            //  TODO: CHECK FOR TWO HAND STATUS, IF TWO HANDING USE TWO HAND VERSION OF BLOCK ANIM INSTEAD
            switch (damageIntensity)
            {
                case DamageIntensity.Block:
                    //VFX OU ANIMATION
                    break;
                case DamageIntensity.Colossal:
                    
                    break;
                default:
                    break;
            }
        }
    }
}

