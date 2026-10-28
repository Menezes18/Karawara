using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace RPGKarawara.DevTools.Performance
{
    // One explicit QA receiver, never a gameplay registry. Source callbacks do nothing without it.
    public sealed class PerformanceActorLifecycleScope : IDisposable
    {
        public long NotificationCount { get; private set; }
        public bool NotificationFaulted { get; private set; }
        public bool IsDisposed { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        [Serializable] public struct HistoryEvent
        {
            public int componentId, rootId, ownerRootId, frame;
            public double time;
            public string componentType;
            public PerformanceActorLifecycle change;
            public bool initiallyBound;
        }
        [Serializable] public struct SubjectBinding
        {
            public int componentId, rootId, ownerRootId;
            public string componentType;
        }
        [Serializable] public sealed class HistoryReport
        {
            public string boundary = "Observed actor lifecycle identities only; creation provenance, phase reconciliation and route certification remain separate";
            public int startedFrame, endedFrame, dropped;
            public double startedTime, endedTime;
            public long notifications;
            public bool sourceFaulted, overflow, disposed;
            public HistoryEvent[] events;
            public SubjectBinding[] initiallyBoundSubjects;
        }
        struct RawEvent { public HistoryEvent row; public Type componentType; }
        struct RawBinding { public SubjectBinding row; public Type componentType; }
        const int HistoryCapacity = 16384;
        readonly RawEvent[] history = new RawEvent[HistoryCapacity];
        readonly Dictionary<int, RawBinding> boundSubjects = new Dictionary<int, RawBinding>();
        readonly int startedFrame = Time.frameCount;
        readonly double startedTime = Time.realtimeSinceStartupAsDouble;
        int historyCount, dropped, endedFrame;
        double endedTime;
        bool overflow;
        static PerformanceActorLifecycleScope active;
        PerformanceActorRosterContext context;
        readonly List<Action> unsubscribe = new List<Action>();
        readonly List<PerformanceActorLifetimeWitness> witnesses = new List<PerformanceActorLifetimeWitness>();
#endif
        PerformanceActorLifecycleScope() { }
        public static PerformanceActorLifecycleScope Begin(string[] args, PerformanceActorRosterContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int count = 0;
            if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") count++;
            if (count == 0) return null;
            if (count != 1 || context == null || context.IsDisposed) throw new ArgumentException("One explicit roster flag and a live context required.");
            if (active != null) throw new InvalidOperationException("Another source lifecycle scope is already active.");
            var scope = new PerformanceActorLifecycleScope { context = context };
            try
            {
                context.AttachSources(scope);
                var witnessedRoots = new HashSet<GameObject>();
                foreach (var actor in context.SourceActors())
                {
                    int componentId = actor.GetInstanceID(), ownerRootId = context.SourceOwnerRootId(actor);
                    if (!scope.boundSubjects.ContainsKey(componentId)) scope.boundSubjects.Add(componentId, new RawBinding { componentType = actor.GetType(), row = new SubjectBinding {
                        componentId = componentId, rootId = actor.gameObject.GetInstanceID(), ownerRootId = ownerRootId } });
                    else if (scope.boundSubjects[componentId].row.ownerRootId != ownerRootId) throw new InvalidOperationException("Ambiguous bound source owner.");
                    if (witnessedRoots.Add(actor.gameObject))
                    {
                        var witness = actor.gameObject.AddComponent<PerformanceActorLifetimeWitness>();
                        scope.witnesses.Add(witness); witness.Bind(scope, actor);
                    }
                    if (!(actor is CharacterManager character)) continue;
                    var network = character.characterNetworkManager;
                    scope.Watch(network.currentHealth, actor, PerformanceActorLifecycle.HealthChanged);
                    scope.Watch(network.maxHealth, actor, PerformanceActorLifecycle.HealthChanged);
                    scope.Watch(network.isActive, actor, PerformanceActorLifecycle.Disabled);
                    scope.Watch(character.isDead, actor, PerformanceActorLifecycle.Disabled);
                    if (actor is AIBossCharacterManager boss)
                    {
                        scope.Watch(boss.hasBeenAwakened, actor, PerformanceActorLifecycle.PhaseChanged);
                        scope.Watch(boss.hasBeenDefeated, actor, PerformanceActorLifecycle.PhaseChanged);
                        scope.Watch(boss.bossFightIsActive, actor, PerformanceActorLifecycle.PhaseChanged);
                    }
                }
                active = scope;
                return scope;
            }
            catch { scope.Dispose(); throw; }
#else
            return null;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("KARAWARA_QA")]
        public static void Notify(Component actor, PerformanceActorLifecycle change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            using (PerformanceProfilerAttribution.LifecycleMarker.Auto()) active?.Observe(actor, change);
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("KARAWARA_QA")]
        public static void NotifyVisualChild(Component actor, PerformanceActorLifecycle change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            // Only explicitly frozen visual children participate. Normal combat hitboxes
            // are not static scenery and must not invalidate the visual roster.
            using (PerformanceProfilerAttribution.LifecycleMarker.Auto())
                if (active != null && active.context.IsReviewedVisualChild(actor)) active.Observe(actor, change);
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void Watch<T>(NetworkVariable<T> value, Component actor, PerformanceActorLifecycle change) where T : unmanaged, IEquatable<T>
        {
            NetworkVariable<T>.OnValueChangedDelegate callback = (previous, current) => Observe(actor, change);
            value.OnValueChanged += callback;
            unsubscribe.Add(() => value.OnValueChanged -= callback);
        }
        internal void Observe(Component actor, PerformanceActorLifecycle change)
        {
            using (PerformanceProfilerAttribution.LifecycleMarker.Auto()) ObserveCore(actor, change);
        }
        void ObserveCore(Component actor, PerformanceActorLifecycle change)
        {
            if (IsDisposed) return;
            NotificationCount++;
            // Diagnostics must not throw into production lifecycle/health callbacks.
            try
            {
                if (historyCount == HistoryCapacity)
                {
                    overflow = true; dropped++; NotificationFaulted = true; context.MarkSourceFault();
                }
                else
                {
                    if (actor == null) throw new InvalidOperationException("Actor source callback lost its component identity.");
                    int rootId = actor.gameObject.GetInstanceID();
                    int componentId = actor.GetInstanceID();
                    bool bound = boundSubjects.TryGetValue(componentId, out var binding) && binding.row.rootId == rootId && binding.componentType == actor.GetType();
                    history[historyCount++] = new RawEvent { componentType = actor.GetType(), row = new HistoryEvent {
                        componentId = componentId, rootId = rootId, ownerRootId = bound ? binding.row.ownerRootId : 0, frame = Time.frameCount, time = Time.realtimeSinceStartupAsDouble,
                        change = change, initiallyBound = bound } };
                }
                context.ObserveLifecycle(actor, change);
            }
            catch { NotificationFaulted = true; context.MarkSourceFault(); }
        }
        // DTO copies and type-name resolution occur only on explicit export, outside sampling.
        public HistoryReport CaptureHistory()
        {
            var rows = new HistoryEvent[historyCount];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = history[i].row;
                rows[i].componentType = history[i].componentType?.FullName;
            }
            var subjects = new SubjectBinding[boundSubjects.Count]; int index = 0;
            foreach (var binding in boundSubjects.Values)
            {
                subjects[index] = binding.row; subjects[index++].componentType = binding.componentType.FullName;
            }
            return new HistoryReport { startedFrame = startedFrame, startedTime = startedTime,
                endedFrame = IsDisposed ? endedFrame : Time.frameCount, endedTime = IsDisposed ? endedTime : Time.realtimeSinceStartupAsDouble,
                notifications = NotificationCount, sourceFaulted = NotificationFaulted, overflow = overflow, dropped = dropped, disposed = IsDisposed, events = rows, initiallyBoundSubjects = subjects };
        }
#endif
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            endedFrame = Time.frameCount; endedTime = Time.realtimeSinceStartupAsDouble;
            if (ReferenceEquals(active, this)) active = null;
            foreach (var release in unsubscribe) release();
            unsubscribe.Clear();
            foreach (var witness in witnesses)
            {
                if (witness == null) continue;
                witness.Unbind();
                if (Application.isPlaying) UnityEngine.Object.Destroy(witness); else UnityEngine.Object.DestroyImmediate(witness);
            }
            witnesses.Clear();
            context?.DetachSources(this); context = null;
#endif
        }
    }
}
