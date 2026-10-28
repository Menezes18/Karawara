using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Added only to already frozen QA actor roots; owner identity survives deferred destruction.
    public sealed class PerformanceActorLifetimeWitness : MonoBehaviour
    {
        PerformanceActorLifecycleScope owner;
        Component actor;
        internal void Bind(PerformanceActorLifecycleScope owner, Component actor) { this.owner = owner; this.actor = actor; }
        internal void Unbind() { owner = null; actor = null; }
        void OnEnable() { owner?.Observe(actor, PerformanceActorLifecycle.Enabled); }
        void OnDisable() { owner?.Observe(actor, PerformanceActorLifecycle.Disabled); }
        void OnDestroy() { owner?.Observe(actor, PerformanceActorLifecycle.Destroyed); Unbind(); }
    }
#endif
}
