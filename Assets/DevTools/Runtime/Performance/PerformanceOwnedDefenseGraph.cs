#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
using System;
using System.Collections.Generic;
using System.Linq;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Performance
{
    // Explicit standalone diagnostic treatment only, never installed on production skills automatically.
    public sealed class PerformanceOwnedDefenseGraph : IDisposable
    {
        readonly DefenseSkill skill;
        readonly GameObject original;
        readonly bool treatment;
        readonly List<VisualEffectAsset> originals = new List<VisualEffectAsset>();
        readonly List<VisualEffectAsset> graphs = new List<VisualEffectAsset>();
        readonly List<object> observations = new List<object>();
        GameObject holder, prototype;
        bool bound, disposed, bindingRestored, abortCleanupRequested;
        int originalPrefabId, prototypeId;
        int[] originalGraphIds, ownedGraphIds;
        VisualEffectAsset[] castBindings;
        public GameObject Prototype => prototype;
        public VisualEffectAsset[] Graphs => graphs.ToArray();
        public bool CleanupConflict { get; private set; }
        public bool CleanupComplete => disposed && holder == null && prototype == null && (!treatment || graphs.All(g => g == null));
        public int ObservedCasts { get; private set; }
        public bool EveryObservedCastHadActiveBatch { get; private set; } = true;
        public int SettledNativeZeroObservations { get; private set; }

        PerformanceOwnedDefenseGraph(DefenseSkill skill, bool treatment) { this.skill = skill; this.treatment = treatment; original = skill.vfxPrefab; }

        public static PerformanceOwnedDefenseGraph Open(DefenseSkill skill, Transform owner)
            => Prepare(skill, owner, true);
        public static PerformanceOwnedDefenseGraph OpenControl(DefenseSkill skill, Transform owner)
            => Prepare(skill, owner, false);
        static PerformanceOwnedDefenseGraph Prepare(DefenseSkill skill, Transform owner, bool treatment)
        {
            if (skill == null || owner == null || skill.vfxPrefab == null) throw new ArgumentException("Actual defense skill, owner and prefab required.");
            if (skill.vfxPrefab.scene.IsValid()) throw new InvalidOperationException("A scene instance cannot be reused as an authored diagnostic source.");
            if (!skill.vfxPrefab.activeSelf) throw new InvalidOperationException("Authored defense prefab must create an active cast.");
            var scope = new PerformanceOwnedDefenseGraph(skill, treatment);
            try
            {
                scope.originalPrefabId = scope.original.GetInstanceID();
                scope.holder = new GameObject("QA owned defense prototype holder");
                scope.holder.SetActive(false); scope.holder.transform.SetParent(owner, false);
                // Parent inactivity suppresses activation; activeSelf is preserved for Skill.Execute's Instantiate.
                scope.prototype = Object.Instantiate(scope.original, scope.holder.transform);
                scope.prototype.name = scope.original.name;
                var effects = scope.prototype.GetComponentsInChildren<VisualEffect>(true);
                if (effects.Length == 0) throw new InvalidOperationException("Defense prefab has no VisualEffect.");
                scope.castBindings = new VisualEffectAsset[effects.Length];
                var copies = new Dictionary<VisualEffectAsset, VisualEffectAsset>();
                for (int index = 0; index < effects.Length; index++)
                {
                    var effect = effects[index];
                    var source = effect.visualEffectAsset;
                    if (source == null) throw new InvalidOperationException("Missing source VFX graph.");
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = treatment ? Object.Instantiate(source) : source;
                        if (treatment) { copy.name = "QA owned " + source.name; if (copy == source) throw new InvalidOperationException("VFX asset copy aliases source."); }
                        copies.Add(source, copy); scope.originals.Add(source); scope.graphs.Add(copy);
                    }
                    scope.castBindings[index] = copy;
                    // Inactivity alone still registers a native instance. Keep the template graph-unbound.
                    effect.visualEffectAsset = null;
                }
                if (scope.prototype.activeInHierarchy || scope.prototype.activeSelf != scope.original.activeSelf)
                    throw new InvalidOperationException("Diagnostic prototype activation changed.");
                scope.prototypeId = scope.prototype.GetInstanceID();
                scope.originalGraphIds = scope.originals.Select(g => g.GetInstanceID()).ToArray();
                scope.ownedGraphIds = treatment ? scope.graphs.Select(g => g.GetInstanceID()).ToArray() : Array.Empty<int>();
                skill.vfxPrefab = scope.prototype; scope.bound = true;
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        public void BindCastGraphs(GameObject cast)
        {
            if (disposed || cast == null || cast == prototype || !cast.scene.IsValid()) throw new InvalidOperationException("Actual cast instance required.");
            var effects = cast.GetComponentsInChildren<VisualEffect>(true);
            if (effects.Length != castBindings.Length || effects.Any(v => v.visualEffectAsset != null))
                throw new InvalidOperationException("Cast no longer matches graph-unbound prototype.");
            for (int i = 0; i < effects.Length; i++) effects[i].visualEffectAsset = castBindings[i];
            if (prototype.GetComponentsInChildren<VisualEffect>(true).Any(v => v.visualEffectAsset != null))
                throw new InvalidOperationException("Binding a cast changed the inactive prototype.");
        }

        public void Observe(string phase, int cycle, GameObject cast = null)
        {
            if (disposed || skill == null || skill.vfxPrefab != prototype || prototype == null || prototype.activeInHierarchy)
                throw new InvalidOperationException("Diagnostic owned graph scope changed.");
            var castEffects = cast == null ? Array.Empty<VisualEffect>() : cast.GetComponentsInChildren<VisualEffect>(true);
            if (cast != null && (!cast.activeInHierarchy || castEffects.Length == 0 || castEffects.Any(v => !v.enabled || !v.gameObject.activeInHierarchy || !graphs.Contains(v.visualEffectAsset))))
                throw new InvalidOperationException("Actual defense cast did not activate owned VFX bindings.");
            var counters = graphs.Select(g =>
            {
                var info = VFXManager.GetBatchedEffectInfo(g);
                return new { graphId = g.GetInstanceID(), reportedBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(g),
                    info.activeInstanceCount, info.activeBatchCount, info.inactiveBatchCount, info.totalCPUSizeInBytes, info.totalGPUSizeInBytes };
            }).ToArray();
            bool exactInstances = cast == null ? counters.All(c => c.activeInstanceCount == 0) :
                counters.All(c => c.activeInstanceCount == castEffects.Count(v => v.visualEffectAsset.GetInstanceID() == c.graphId));
            if (cast != null) { ObservedCasts++; EveryObservedCastHadActiveBatch &= exactInstances; }
            observations.Add(new { phase, cycle, frame = Time.frameCount, realtime = Time.realtimeSinceStartupAsDouble,
                castId = cast == null ? (int?)null : cast.GetInstanceID(), castActive = cast != null && cast.activeInHierarchy,
                castVfxCount = castEffects.Length, counters,
                castParticleCounts = castEffects.Select(v => v.aliveParticleCount).ToArray(), castCulled = castEffects.Select(v => v.culled).ToArray(),
                prototypeBoundGraphCount = prototype.GetComponentsInChildren<VisualEffect>(true).Count(v => v.visualEffectAsset != null),
                sourceReportedBytes = originals.Select(g => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(g)).ToArray() });
            if (cast != null && !exactInstances) throw new InvalidOperationException("Native cast count differs from actual active VFX components; possible prototype pin.");
            if (phase.StartsWith("skills-", StringComparison.Ordinal))
            {
                if (!exactInstances) throw new InvalidOperationException("Settled checkpoint still has an active native VFX instance.");
                SettledNativeZeroObservations++;
            }
        }

        public object Report => new { treatmentEnabled = treatment, bindingProtocol = "graph-null-prototype-and-late-binding-both-arms-v2", originalPrefabId, prototypeId, originalGraphIds, ownedGraphIds,
            ObservedCasts, EveryObservedCastHadActiveBatch,
            SettledNativeZeroObservations,
            disposed, bindingRestored, abortCleanupRequested, cleanupComplete = CleanupComplete, cleanupConflict = CleanupConflict, observations,
            boundary = "QA-only inactive-parent prototype with authored activeSelf preserved and owned graph copies. Actual production Skill.Execute unchanged. Snapshots may intentionally contain this inactive prototype/graphs; casts are separately journaled. No production fix, physical/GPU release, visual parity or acceptance inferred." };

        public void RestoreBinding()
        {
            if (bindingRestored) return; bindingRestored = true;
            if (skill != null)
            {
                if (bound && skill.vfxPrefab == prototype) skill.vfxPrefab = original;
                else if (skill.vfxPrefab != original) CleanupConflict = true;
            }
        }
        public void DisposeAfterCasts(GameObject[] ownedCasts, Action completed)
        {
            RestoreBinding();
            if (abortCleanupRequested) return; abortCleanupRequested = true;
            var casts = ownedCasts.Where(c => c != null).Distinct().ToArray();
            if (casts.Any(c => !c.scene.IsValid() || c == prototype || !c.GetComponentsInChildren<VisualEffect>(true).Any(v => graphs.Contains(v.visualEffectAsset))))
                throw new InvalidOperationException("Abort cleanup accepts only journaled graph casts, never prefab/source objects.");
            var owner = new GameObject("QA owned graph abort disposal"); Object.DontDestroyOnLoad(owner);
            owner.AddComponent<PerformanceOwnedGraphDisposal>().Begin(this, casts, completed);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; RestoreBinding();
            DestroyOwned(holder);
            if (treatment) foreach (var graph in graphs) DestroyOwned(graph);
        }
        static void DestroyOwned(Object value)
        {
            if (value == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(value); return; }
#endif
            Object.Destroy(value);
        }
    }

    public sealed class PerformanceOwnedGraphDisposal : MonoBehaviour
    {
        PerformanceOwnedDefenseGraph scope;
        GameObject[] casts;
        bool completed;
        public void Begin(PerformanceOwnedDefenseGraph value, GameObject[] ownedCasts, Action done)
        { scope = value; casts = ownedCasts; StartCoroutine(Release(done)); }
        System.Collections.IEnumerator Release(Action done)
        {
            foreach (var cast in casts) if (cast != null) Object.Destroy(cast);
            yield return null; // Observe deferred cast destruction before releasing shared graph data.
            if (casts.Any(c => c != null)) yield return null;
            if (casts.Any(c => c != null)) throw new InvalidOperationException("Owned cast failed deferred destruction.");
            scope.Dispose();
            yield return null;
            completed = true;
            try { done?.Invoke(); } finally { Object.Destroy(gameObject); }
        }
        void OnDestroy()
        {
            if (completed || scope == null) return;
            scope.RestoreBinding();
            // Application shutdown fallback; no complete-cleanup claim without the observed coroutine.
            if (casts != null) foreach (var cast in casts) if (cast != null) Object.Destroy(cast);
            scope.Dispose();
        }
    }
}
#endif
