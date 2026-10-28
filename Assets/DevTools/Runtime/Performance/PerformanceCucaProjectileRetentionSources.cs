using System;
using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Passive, exclusive diagnostic journal. Ordinary gameplay never depends on a subscriber.
    public sealed class PerformanceCucaProjectileRetentionSources : IDisposable
    {
        static PerformanceCucaProjectileRetentionSources active;
        readonly Action<CucaBoss, GameObject, GameObject, int, int> callback;
        public bool Faulted { get; private set; }
        PerformanceCucaProjectileRetentionSources(Action<CucaBoss, GameObject, GameObject, int, int> callback) { this.callback = callback; }
        public static PerformanceCucaProjectileRetentionSources Open(Action<CucaBoss, GameObject, GameObject, int, int> callback)
        {
            if (!Application.isPlaying || callback == null || active != null) throw new InvalidOperationException("Exclusive live Cuca projectile observation required.");
            return active = new PerformanceCucaProjectileRetentionSources(callback);
        }
        public static void ProjectileCreated(CucaBoss owner, GameObject prefab, GameObject instance, int attackOrdinal, int projectileOrdinal)
        {
            var scope = active; if (scope == null) return;
            try { scope.callback(owner, prefab, instance, attackOrdinal, projectileOrdinal); }
            catch { scope.Faulted = true; }
        }
        public void Dispose() { if (ReferenceEquals(active, this)) active = null; }
    }
#endif
}
