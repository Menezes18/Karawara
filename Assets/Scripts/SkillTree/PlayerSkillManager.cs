using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Unity.VisualStudio.Editor;

namespace RPGKarawara.SkillTree
{
    public class PlayerSkillManager : MonoBehaviour
    {
        public static PlayerSkillManager instance;
        public Skill attackSkill;
        public Skill defenseSkill;
        public Skill supportSkill;
        public float skilldelay = 5f;
        public Dictionary<string, bool> cooldowns = new Dictionary<string, bool>();

        [HideInInspector] public float damageSpirit;
        public Image skillIcon;
        private void Awake()
        {
            instance = this;
            // Inicializa os cooldowns para todas as habilidades
            cooldowns["attack"] = false;
            cooldowns["defense"] = false;
            cooldowns["support"] = false;
            InitializeCooldown(attackSkill);
            InitializeCooldown(defenseSkill);
            InitializeCooldown(supportSkill);
        }

        private void Update()
        {
            //ARRUAR ESSE LOGICA 
            // Verifica se a tecla 1 foi liberada
            if (Keyboard.current.digit1Key.wasReleasedThisFrame)
            {
                if (IsCooldownActive("attack") && !attackSkill.IsOnCooldown)
                {
                    Debug.Log("Attack Skill cooldown");
                    
                    StartCoroutine(PrintMessageAfterDelay("attack"));
                }
                if (attackSkill != null && !attackSkill.IsOnCooldown && !IsCooldownActive("attack"))
                {
                    cooldowns["attack"] = true;
                    attackSkill.Activate(gameObject);
                }
                else
                {
                    Debug.Log("Cooldown is active attack");
                }
            }

            // Verifica se a tecla 2 foi liberada
            if (Keyboard.current.digit2Key.wasReleasedThisFrame)
            {
                if (IsCooldownActive("defense") && !defenseSkill.IsOnCooldown)
                {
                    StartCoroutine(PrintMessageAfterDelay("defense"));
                }
                else if (defenseSkill != null && !defenseSkill.IsOnCooldown && !IsCooldownActive("defense"))
                {
                    defenseSkill.Activate(gameObject);
                    cooldowns["defense"] = true;
                }
                else
                {
                    Debug.Log("Cooldown is active defense");
                }
            }

            // Verifica se a tecla 3 foi liberada
            if (Keyboard.current.digit3Key.wasReleasedThisFrame)
            {
                if (IsCooldownActive("support") && !supportSkill.IsOnCooldown)
                {
                    StartCoroutine(PrintMessageAfterDelay("support"));
                }
                else if (supportSkill != null && !supportSkill.IsOnCooldown && !IsCooldownActive("support"))
                {
                    supportSkill.Activate(gameObject);
                    cooldowns["support"] = true;
                    Debug.Log("Support skill activated");
                }
                else
                {
                    Debug.Log("Support skill is on cooldown or is null");
                }
            }
        }


        IEnumerator PrintMessageAfterDelay(string skill)
        {
            yield return new WaitForSeconds(skilldelay);
            cooldowns[skill] = false;
        }

        public bool IsCooldownActive(string type)
        {
            if (cooldowns.ContainsKey(type))
            {
                return cooldowns[type];
            }
            else
            {
                Debug.LogWarning($"Tipo de cooldown desconhecido: {type}");
                return false;
            }
        }

        private void InitializeCooldown(Skill skill)
        {
            if (skill != null)
            {
                // Garantir que o cooldown comece como false
                skill.ResetCooldown();
            }
        }
    }
}
