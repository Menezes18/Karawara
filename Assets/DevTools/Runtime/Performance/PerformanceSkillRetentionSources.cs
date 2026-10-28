using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Explicit diagnostic owner only. Never throws from production fireball callbacks.
    public sealed class PerformanceSkillRetentionSources : IDisposable
    {
        static PerformanceSkillRetentionSources active;
        readonly Action<FireballShooter, GameObject, GameObject> callback;
        Keyboard keyboard;
        public static Keyboard SkillKeyboard => active?.keyboard ?? Keyboard.current;
        public bool Faulted { get; private set; }
        public static PerformanceSkillRetentionSources Open(Action<FireballShooter, GameObject, GameObject> callback, Keyboard keyboard = null)
        {
            if (!Application.isPlaying || callback == null || active != null) throw new InvalidOperationException("Exclusive live skill-retention source ownership required.");
            if (keyboard != null && !keyboard.added) throw new InvalidOperationException("Owned live diagnostic keyboard required.");
            return active = new PerformanceSkillRetentionSources(callback) { keyboard = keyboard };
        }
        PerformanceSkillRetentionSources(Action<FireballShooter, GameObject, GameObject> callback) { this.callback = callback; }
        public static void ProjectileCreated(FireballShooter owner, GameObject prefab, GameObject instance)
        {
            var scope = active; if (scope == null) return;
            try { scope.callback(owner, prefab, instance); } catch { scope.Faulted = true; }
        }
        public void Dispose() { if (ReferenceEquals(active, this)) active = null; keyboard = null; }
    }
#endif
}
