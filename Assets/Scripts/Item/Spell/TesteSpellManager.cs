using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class TesteSpellManager : MonoBehaviour
    {
        public bool teste = false;
        public WeaponItem espada;
        public WeaponItem spell;
        public PlayerInventoryManager inventory;
        private PlayerCombatManager combatManager;

        void Start()
        {
            combatManager = GetComponent<PlayerCombatManager>();
        }

        void Update()
        {
            if (teste)
            {
                inventory.weaponsInRightHandSlots[2] = espada;
            }
            else
            {
                inventory.weaponsInRightHandSlots[2] = spell;
                
               
                if (combatManager != null && combatManager.isWeaponActive)
                {
                    combatManager.player.playerEquipmentManager.SwitchRightWeapon();
                    combatManager.isWeaponActive = false;
                }
            }
        }
    }
}