using System;

namespace Karawara.Core.Progression
{
    public sealed class SkillRuntimeState
    {
        public string DefinitionId { get; }
        public float CooldownSeconds { get; }
        public SkillProgressState Progression { get; } = new SkillProgressState();
        private float? lastActivationTime;

        public SkillRuntimeState(string definitionId, float cooldownSeconds)
        {
            if (float.IsNaN(cooldownSeconds) || float.IsInfinity(cooldownSeconds) || cooldownSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds));
            DefinitionId = definitionId;
            CooldownSeconds = cooldownSeconds;
        }
        public bool TryActivate(float gameplayTime)
        {
            if (!Progression.IsUnlocked || Progress(gameplayTime) > 0) return false;
            lastActivationTime = gameplayTime;
            return true;
        }
        public float Progress(float gameplayTime)
        {
            if (!lastActivationTime.HasValue || CooldownSeconds <= 0) return 0;
            return Math.Max(0, Math.Min(1, 1 - (gameplayTime - lastActivationTime.Value) / CooldownSeconds));
        }
        public void ResetCooldown() => lastActivationTime = null;
    }
}
