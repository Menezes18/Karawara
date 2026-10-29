namespace Karawara.Core.Progression
{
    /// <summary>Progression owned by one user, never by the content definition.</summary>
    public sealed class SkillProgressState
    {
        public bool IsUnlocked { get; private set; }
        public bool IsEquipped { get; private set; }
        public void Unlock() => SetUnlocked(true);
        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            if (!unlocked) IsEquipped = false;
        }
        public void SetEquipped(bool equipped) => IsEquipped = equipped && IsUnlocked;
    }
}
