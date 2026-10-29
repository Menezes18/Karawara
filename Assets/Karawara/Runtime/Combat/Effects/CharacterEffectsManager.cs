using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class CharacterEffectsManager : MonoBehaviour
    {
        //  PROCESS INSTANT EFFECTS (TAKE DAMAGE, HEAL)

        //  PROCESS TIMED EFFECTS (POISON, BUILD UPS)

        //  PROCESS STATIC EFFECTS (ADDING/REMOVING BUFFS FROM TALISMANS ECT)

        CharacterManager character;
        public GameObject activeSpellWarmUpFX;
        [Header("VFX")]
        [SerializeField] GameObject bloodSplatterVFX;

        protected virtual void Awake()
        {
            character = GetComponent<CharacterManager>();
        }

        public virtual void ProcessInstantEffect(InstantCharacterEffect effect)
        {
            effect.ProcessEffect(character);
        }

        // Transfers ownership of a freshly instantiated, synchronous effect.
        // Shared authored assets continue to use ProcessInstantEffect.
        public void ProcessOwnedInstantEffect(InstantCharacterEffect effect)
        {
            try { ProcessInstantEffect(effect); }
            finally { if (effect != null) Destroy(effect); }
        }

        public void PlayBloodSplatterVFX(Vector3 contactPoint)
        {
            //  IF WE MANUALLY HAVE PLACED A BLOOD SPLATTER VFX ON THIS MODEL, PLAY ITS VERSION
            if (bloodSplatterVFX != null)
            {
                GameObject bloodSplatter = Instantiate(bloodSplatterVFX, contactPoint, Quaternion.identity);
            }
            //  ELSE, USE THE GENERIC (DEFAULT VERSION) WE HAVE ELSEWHERE
            else
            {
                GameObject bloodSplatter = Instantiate(WorldCharacterEffectsManager.instance.bloodSplatterVFX, contactPoint, Quaternion.identity);
            }
        }
    }
}
