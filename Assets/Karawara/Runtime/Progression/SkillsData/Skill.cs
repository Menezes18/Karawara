using System;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.Video;

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
        public new string name;
        [TextArea(5,10)]
        public string description;
        public CustomRenderTexture texture;
        public VideoClip vClip;
        public GameObject prefab;
        public bool isUnlocked; // Indica se a habilidade está desbloqueada ou não
        public bool active;
        // Legacy parameterless UI/QA adapters. Gameplay always supplies the actual user.
        public bool IsOnCooldown => GetCooldownProgress() > 0;

        public void ResetCooldown()
        {
            PlayerSkillManager.instance?.ResetSkillCooldown(this);
        }

        public void Activate(GameObject user)
        {
            if (user == null) return;
            var owner = user.GetComponent<PlayerSkillManager>();
            // Older direct-cast consumers also receive a local owner, with symmetric GameObject lifetime.
            if (owner == null) owner = user.AddComponent<PlayerSkillManager>();
            owner.TryActivateDefinition(this);
        }

        internal void ExecuteForUser(GameObject user) => Execute(user);

        public void AddSkill(int index)
        {
            PlayerSkillManager.instance?.EquipSkill(this, index - 1);
        }
        public void RemoveSkill(int slot)
        {
            PlayerSkillManager.instance?.EquipSkill(null, slot - 1);
        }
        public float GetCooldownProgress()
        {
            return PlayerSkillManager.instance != null ? PlayerSkillManager.instance.GetCooldownProgress(this) : 0;
        }
        protected abstract void Execute(GameObject user);
    }
}
