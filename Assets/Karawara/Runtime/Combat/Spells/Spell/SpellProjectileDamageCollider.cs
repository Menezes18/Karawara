using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class SpellProjectileDamageCollider : DamageCollider
    {
        public CharacterManager spellCaster;
        protected override CharacterManager SourceCharacter => spellCaster;
    }
}
