using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara.SkillTree{
    public class PlayerSkillManager : MonoBehaviour{
        public static PlayerSkillManager instance;
        public Skill attackSkill;
        public Skill defenseSkill;
        public Skill supportSkill;

        public bool Escudo = true;
        private void Awake(){
            instance = this;
            InitializeCooldown(attackSkill);
            InitializeCooldown(defenseSkill);
            InitializeCooldown(supportSkill);
        }

        private void Update(){

            if (Keyboard.current.digit1Key.wasReleasedThisFrame){
                if (attackSkill != null && !attackSkill.IsOnCooldown){
                    attackSkill.Activate(gameObject);
                }
            }

            if (Keyboard.current.digit2Key.wasReleasedThisFrame){
                if (defenseSkill != null && !defenseSkill.IsOnCooldown){
                    defenseSkill.Activate(gameObject);
                }
            }

            if (Keyboard.current.digit3Key.wasReleasedThisFrame){
                if (supportSkill != null && !supportSkill.IsOnCooldown){
                    supportSkill.Activate(gameObject);
                    Debug.Log("Support skill activated");
                }
                else{
                    Debug.Log("Support skill is on cooldown or is null");
                }
            }
        }
        
        private void InitializeCooldown(Skill skill){
            if (skill != null){
                // Garantir que o cooldown comece como false
                skill.ResetCooldown();
            }
        }
    }
}