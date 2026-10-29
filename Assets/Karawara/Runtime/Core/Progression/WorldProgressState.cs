using System.Collections.Generic;

namespace Karawara.Core.Progression
{
    /// <summary>Local world state keyed by existing IDs. Persistence translation belongs to phase 3B.</summary>
    public sealed class WorldProgressState
    {
        public IDictionary<int, bool> Checkpoints { get; } = new Dictionary<int, bool>();
        public IDictionary<int, bool> BossesAwakened { get; } = new Dictionary<int, bool>();
        public IDictionary<int, bool> BossesDefeated { get; } = new Dictionary<int, bool>();
        public IDictionary<string, bool> Missions { get; } = new Dictionary<string, bool>();
        public IDictionary<string, bool> PersistentObjects { get; } = new Dictionary<string, bool>();
    }
}
