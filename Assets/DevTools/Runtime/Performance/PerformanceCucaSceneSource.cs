using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPGKarawara.DevTools.Performance
{
    // Exact authored ownership. This marker never grants route/creation/rendering acceptance.
    [DisallowMultipleComponent]
    public sealed class PerformanceCucaSceneSource : MonoBehaviour
    {
        public enum Kind { NativeGeometry, DamageGeometry, LeverCamera, DormantCutscene }
        public Kind kind;
        public Animator[] animators = Array.Empty<Animator>();
        public BoxCollider shape, targetShape;
        public DamageCollider damage;
        public AnimationTrigger lever;
        public Camera camera;
        public UniversalAdditionalCameraData cameraData;
        public AudioListener listener;
        public Camera[] cameras = Array.Empty<Camera>();
        public cutSceneFight[] cutscenes = Array.Empty<cutSceneFight>();
        public DestroyAfterTime[] lifetimes = Array.Empty<DestroyAfterTime>();
        public AudioSource audio;
        public CutSceneTrigger trigger;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        struct Binding { internal Component subject; internal PerformanceCucaSceneSource owner; }
        static readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
        PerformanceCucaSceneProof boundProof;
        int[] boundIds;
        internal long MutationRevision { get; private set; }
        internal bool IsValidSource() => PerformanceCucaSceneProof.CanBind(this);
        internal PerformanceCucaSceneProof Freeze() => new PerformanceCucaSceneProof(this);
        internal PerformanceCucaSceneProof FreezeEndpoint() => new PerformanceCucaSceneProof(this, false);
        internal void Bind(PerformanceCucaSceneProof proof, Component[] subjects)
        {
            if (boundProof != null) throw new InvalidOperationException("Cuca source already has a frozen proof owner");
            foreach (var subject in subjects)
                if (bindings.TryGetValue(subject.GetInstanceID(), out var old) && old.owner != this)
                    throw new ArgumentException("Ambiguous Cuca lifecycle subject ownership");
            boundProof = proof; boundIds = new int[subjects.Length];
            for (int i = 0; i < subjects.Length; i++)
            {
                int id = subjects[i].GetInstanceID(); boundIds[i] = id;
                bindings[id] = new Binding { subject = subjects[i], owner = this };
            }
        }
        internal void Unbind(PerformanceCucaSceneProof proof)
        {
            if (!ReferenceEquals(boundProof, proof)) return;
            if (boundIds != null)
                foreach (int id in boundIds)
                    if (bindings.TryGetValue(id, out var binding) && ReferenceEquals(binding.owner, this)) bindings.Remove(id);
            boundProof = null; boundIds = null;
        }
        // Production callbacks call this BEFORE mutating their state. Only an exact
        // frozen subject can invalidate its owner; unknown components remain no-ops.
        public static void NotifyStateChanged(Component subject)
        {
            using (PerformanceProfilerAttribution.LifecycleMarker.Auto())
            {
                if (subject == null || !bindings.TryGetValue(subject.GetInstanceID(), out var binding) ||
                    !ReferenceEquals(binding.subject, subject) || binding.owner == null) return;
                binding.owner.MutationRevision++;
                PerformanceActorLifecycleScope.Notify(subject, PerformanceActorLifecycle.PhaseChanged);
            }
        }
        void Changed(PerformanceActorLifecycle change)
        {
            MutationRevision++;
            PerformanceActorLifecycleScope.Notify(this, change);
        }
        void OnEnable() => Changed(PerformanceActorLifecycle.Enabled);
        void OnDisable() => Changed(PerformanceActorLifecycle.Disabled);
        void OnTransformParentChanged() => Changed(PerformanceActorLifecycle.PhaseChanged);
        void OnDestroy() { Changed(PerformanceActorLifecycle.Destroyed); Unbind(boundProof); }
#else
        public static void NotifyStateChanged(Component subject) { }
#endif
    }
}
