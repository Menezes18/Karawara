using System;
using RPGKarawara.SkillTree;
using UnityEngine;

namespace RPGKarawara
{
    public enum SkillType
    {
        Attack,
        Defense,
        Support
    }

    public abstract class Skill : ScriptableObject
    {
        public SkillType skillType; // Tipo de habilidade: Ataque, Defesa ou Suporte
        public float cooldownDuration;
        public Sprite sprite;
        public string name;
        [TextArea(5,10)]
        public string description;
        public GameObject prefab;
        public bool isUnlocked; // Indica se a habilidade está desbloqueada ou não
        public bool active;
        private float lastActivationTime;

        public bool IsOnCooldown
        {
            get
            {
                return Time.time < lastActivationTime + cooldownDuration;
            }
        }

        private void OnEnable()
        {
            ResetCooldown();
        }

        public void ResetCooldown()
        {
            lastActivationTime = -cooldownDuration;
        }

        public void Activate(GameObject user)
        {
            if (isUnlocked && !IsOnCooldown) // Verifica se a habilidade está desbloqueada e fora do cooldown
            {
                lastActivationTime = Time.time;
                Execute(user);
            }
            else
            {
                Debug.Log("Skill is locked or on cooldown.");
            }
        }

        public void AddSkill(int index)
        {
            switch (index)
            {
                 case 1:
                     PlayerSkillManager.instance.slot[0].skillSlot = this;
                     break;
                 case 2:
                     PlayerSkillManager.instance.slot[1].skillSlot  = this;
                     break;
                 case 3:
                     PlayerSkillManager.instance.slot[2].skillSlot  = this;
                     break;
                 default:
                    Debug.LogWarning("Tipo de habilidade desconhecido: " + skillType);
                     break;
            }
        }
        public void RemoveSkill(int slot)
        {
            switch (slot)
            {
                case 1:
                    PlayerSkillManager.instance.slot[0].skillSlot = null;
                    break;
                case 2:
                    PlayerSkillManager.instance.slot[1].skillSlot  = null;
                    break;
                case 3:
                    PlayerSkillManager.instance.slot[2].skillSlot  = null;
                    break;
                default:
                    Debug.LogWarning("Tipo de habilidade desconhecido: " + skillType);
                    break;
            }
        }
        public float GetCooldownProgress()
        {
            // Retorna a porcentagem de cooldown concluída (0 = completo, 1 = ainda em cooldown)
            if (IsOnCooldown)
            {
                return 1f - (Time.time - lastActivationTime) / cooldownDuration;
            }
            return 0f;
        }
        protected abstract void Execute(GameObject user);
    }
}