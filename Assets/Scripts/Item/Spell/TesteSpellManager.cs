using System.Collections;
using System.Collections.Generic;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    public class TesteSpellManager : MonoBehaviour
    {
        public bool troca = false;
        public WeaponItem espada;
        public WeaponItem spell;
        public PlayerInventoryManager inventory;
        private PlayerCombatManager combatManager;
        public Image ImageTroca;
        public Sprite spriteEspada;
        public Sprite spriteSpell;
        // Cooldown
        private float cooldownDuration = 1f;
        private float lastSwitchTime = -1f;
        public SkillCooldownUI skillCooldownUI;
        
        private float cooldownTime = 0.5f; // Tempo de cooldown em segundos
        private float lastPressTime = -Mathf.Infinity;
        void Start()
        {
            
            combatManager = GetComponent<PlayerCombatManager>();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && Time.timeScale > 0 &&
                inventory != null && combatManager != null && combatManager.player.IsOwner &&
                !combatManager.player.isPerformingAction && Time.time >= lastPressTime + cooldownTime){
                
                
                    
                    lastPressTime = Time.time;

                    skillCooldownUI = FindObjectOfType<SkillCooldownUI>();
                    if (combatManager != null) combatManager.DeactivateActiveWeapon();
                    troca = !troca;
                
            }

            if (troca)
            {
                inventory.weaponsInRightHandSlots[2] = espada;
            }
            else
            {
                inventory.weaponsInRightHandSlots[2] = spell;
                
               
            }

            if(skillCooldownUI != null) ShowUI();
        }

        private void ShowUI()
        {
            // Exibe a UI correspondente ao item selecionado
            if (troca)
            {
                
                skillCooldownUI.slotSpellOrLanca.sprite = spriteEspada;
            }
            else
            {
                skillCooldownUI.slotSpellOrLanca.sprite = spriteSpell;
            }
        }
    }
}
