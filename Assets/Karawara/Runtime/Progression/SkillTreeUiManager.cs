using System;
using System.Linq;
using RPGKarawara.SkillTree;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RPGKarawara
{
    public class SkillTreeUiManager : MonoBehaviour
    {
        public static SkillTreeUiManager instance;
        public SkillUISlot[] attackSkillSlots, defenseSkillSlots, supportSkillSlots;
        public Skill[] attackSkills, defenseSkills, supportSkills, allSkills;
        [Header("Dados Skill Direita")]
        public TextMeshProUGUI textCabecalho;
        public VideoPlayer videoPlayer;
        public RawImage rawImage;
        public TextMeshProUGUI descriptionCabecalho;
        public Skill novaSkillAdd;
        private PlayerSkillManager owner;
        private bool bindingAssigned;
        public PlayerSkillManager Owner => bindingAssigned ? owner : PlayerSkillManager.instance;

        private void Awake() { instance = this; }
        private void OnDestroy() { owner = null; if (instance == this) instance = null; }
        private void Start() => RefreshSlots();
        public void Bind(PlayerSkillManager player)
        {
            bindingAssigned = true; owner = player;
            RefreshSlots();
        }
        private void RefreshSlots()
        {
            SetupSkillSlots(attackSkills, attackSkillSlots);
            SetupSkillSlots(defenseSkills, defenseSkillSlots);
            SetupSkillSlots(supportSkills, supportSkillSlots);
        }
        public void DeactivateAllSkillsAndResetCooldown()
        {
            var player = Owner;
            if (player == null) return;
            player.ResetAllCooldowns();
            allSkills = (attackSkills ?? Array.Empty<Skill>()).Concat(defenseSkills ?? Array.Empty<Skill>()).Concat(supportSkills ?? Array.Empty<Skill>()).Where(s => s != null).Distinct().ToArray();
            foreach (var skill in allSkills) player.GetRuntimeState(skill).Progression.SetEquipped(false);
            RefreshSlots();
        }
        private void SetupSkillSlots(Skill[] skills, SkillUISlot[] slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].Bind(this);
                slots[i].SetSkill(skills != null && i < skills.Length ? skills[i] : null);
            }
        }
        public void UnlockSkill(Skill skill) { Owner?.UnlockSkill(skill); RefreshSlots(); }
        public void Cabecalho(Skill skill)
        {
            if (skill == null) return;
            if (textCabecalho != null) textCabecalho.text = skill.name;
            if (descriptionCabecalho != null) descriptionCabecalho.text = skill.description;
            if (rawImage != null) rawImage.texture = skill.texture;
            if (videoPlayer != null) { videoPlayer.clip = skill.vClip; videoPlayer.targetTexture = skill.texture; videoPlayer.Play(); }
        }
        public void ClearCabecalho()
        {
            if (textCabecalho != null) textCabecalho.text = "";
            if (descriptionCabecalho != null) descriptionCabecalho.text = "";
            if (videoPlayer != null) { videoPlayer.clip = null; videoPlayer.targetTexture = null; }
        }
        public void DisableSkillsExcept(SkillType skillType, Skill skillToIgnore, SkillUISlot selected)
        {
            var skills = skillType == SkillType.Attack ? attackSkills : skillType == SkillType.Defense ? defenseSkills : supportSkills;
            var slots = skillType == SkillType.Attack ? attackSkillSlots : skillType == SkillType.Defense ? defenseSkillSlots : supportSkillSlots;
            if (Owner != null && skills != null)
                foreach (var skill in skills)
                    if (skill != null && skill != skillToIgnore) Owner.GetRuntimeState(skill).Progression.SetEquipped(false);
            if (slots != null) foreach (var uiSlot in slots) if (uiSlot != null && uiSlot != selected) uiSlot.SetTriangleColor(Color.white);
            if (selected != null) selected.SetTriangleColor(Color.white);
        }
        private bool[] CaptureUnlocks(Skill[] skills) => skills?.Select(skill => Owner != null && Owner.IsSkillUnlocked(skill)).ToArray();
        public CharacterSaveData SaveSkillsToData(CharacterSaveData data)
        {
            if (data == null) return null;
            data.SkillAttack = attackSkills != null ? (Skill[])attackSkills.Clone() : Array.Empty<Skill>();
            data.SkillDefense = defenseSkills != null ? (Skill[])defenseSkills.Clone() : Array.Empty<Skill>();
            data.SkillSuport = supportSkills != null ? (Skill[])supportSkills.Clone() : Array.Empty<Skill>();
            data.SkillAttackUnlocked = CaptureUnlocks(attackSkills);
            data.SkillDefenseUnlocked = CaptureUnlocks(defenseSkills);
            data.SkillSupportUnlocked = CaptureUnlocks(supportSkills);
            return data;
        }
        private void RestoreUnlocks(Skill[] skills, bool[] unlocked)
        {
            if (Owner == null) return;
            for (int i = 0; i < skills.Length; i++)
                if (skills[i] != null) Owner.GetRuntimeState(skills[i]).Progression.SetUnlocked(unlocked != null && i < unlocked.Length ? unlocked[i] : skills[i].isUnlocked);
        }
        public void LoadSkillsFromData(CharacterSaveData data)
        {
            if (data == null) return;
            Owner?.ResetRuntimeState();
            attackSkills = data.SkillAttack != null ? (Skill[])data.SkillAttack.Clone() : Array.Empty<Skill>();
            defenseSkills = data.SkillDefense != null ? (Skill[])data.SkillDefense.Clone() : Array.Empty<Skill>();
            supportSkills = data.SkillSuport != null ? (Skill[])data.SkillSuport.Clone() : Array.Empty<Skill>();
            RestoreUnlocks(attackSkills, data.SkillAttackUnlocked);
            RestoreUnlocks(defenseSkills, data.SkillDefenseUnlocked);
            RestoreUnlocks(supportSkills, data.SkillSupportUnlocked);
            RefreshSlots();
        }
        public void AddSkill(Skill newSkill, SkillType skillType)
        {
            if (newSkill == null) return;
            Owner?.UnlockSkill(newSkill);
            var skills = skillType == SkillType.Attack ? attackSkills : skillType == SkillType.Defense ? defenseSkills : supportSkills;
            skills = skills ?? Array.Empty<Skill>();
            if (!skills.Contains(newSkill)) { Array.Resize(ref skills, skills.Length + 1); skills[skills.Length - 1] = newSkill; }
            if (skillType == SkillType.Attack) attackSkills = skills;
            else if (skillType == SkillType.Defense) defenseSkills = skills;
            else supportSkills = skills;
            RefreshSlots();
        }
        public void AdicionarSkill(Skill novaSkill) { AddSkill(novaSkill, SkillType.Attack); UnlockSkill(novaSkill); }
    }
}
