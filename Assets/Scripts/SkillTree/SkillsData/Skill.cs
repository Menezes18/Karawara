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

        public void AddSkill()
        {
            switch (skillType)
            {
                case SkillType.Attack:
                    PlayerSkillManager.instance.attackSkill = this;
                    break;
                case SkillType.Defense:
                    PlayerSkillManager.instance.defenseSkill = this;
                    break;
                case SkillType.Support:
                    PlayerSkillManager.instance.supportSkill = this;
                    break;
                default:
                    Debug.LogWarning("Tipo de habilidade desconhecido: " + skillType);
                    break;
            }
        }

        protected abstract void Execute(GameObject user);
    }
}