using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class PlayerEffectsManager : CharacterEffectsManager
    {
        [Header("Debug Delete Later")]
        [SerializeField] InstantCharacterEffect effectToTest;
        [SerializeField] bool processEffect = false;

        private void Update()
        {
            if (processEffect)
            {
                processEffect = false;
                InstantCharacterEffect effect = Instantiate(effectToTest);
                ProcessInstantEffect(effect);
            }
        }
    }
}
