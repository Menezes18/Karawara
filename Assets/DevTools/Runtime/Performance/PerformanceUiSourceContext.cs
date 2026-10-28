using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace RPGKarawara.DevTools.Performance
{
    public enum PerformanceUiSourceEvent { Created, Enabled, Disabled, Destroyed, ParentChanged, LoadingRequest, HiddenRequest, SkillRestart, SkillCompleted, FadeTrigger, HitEffect }
    public sealed class PerformanceUiSourceContext : IDisposable
    {
        [Serializable] public struct SourceEvent { public int frame, sourceId; public PerformanceUiSourceEvent kind; public bool frozen, combatAllowed; }
        [Serializable] public sealed class Checkpoint
        {
            public string label, utc;
            public bool frozen, conditionsChanged, sourceFaulted;
            public long eventCount;
            public string combatMode;
            public int combatDamageOwnerId;
            public long combatHitEffectCount;
            public SourceEvent[] events;
            public PerformanceUiSourceInventory.Row[] sources;
        }
        public bool IsDisposed { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool SourceFaulted { get; private set; }
        public long EventCount { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        sealed class Node { public Transform transform, parent; public bool active; public Scene scene; }
        sealed class Entry
        {
            public MonoBehaviour owner;
            public Animator animator;
            public PerformanceUiSourceWitness witness;
            public Image image;
            public Canvas canvas;
            public UnityEngine.Object[] references;
            public Node[] nodes;
            public Behaviour[] behaviours;
            public bool[] enabled;
            public RuntimeAnimatorController controller;
        }
        static PerformanceUiSourceContext active;
        Entry[] entries;
        MonoBehaviour[] owners;
        readonly SourceEvent[] events = new SourceEvent[256];
        int eventLength;
        bool invalid;
        Scene scene;
        System.MiniMap.MiniMapUI combatDamageOwner;
        string combatMode;
        long combatHitEffectCount;
        internal static Animator FindAnimator(MonoBehaviour owner, int id)
        {
            if (owner == null) return null;
            foreach (var animator in owner.GetComponentsInChildren<Animator>(true)) if (animator.GetInstanceID() == id) return animator;
            return null;
        }
        internal static bool HasWitness(MonoBehaviour owner, PerformanceUiSourceInventory.Row row)
        {
            if (!row.referenceValidated) return false;
            var animator = FindAnimator(owner, row.animatorId); var witness = animator == null ? null : animator.GetComponent<PerformanceUiSourceWitness>();
            return witness != null && witness.enabled && witness.animator == animator;
        }
        static UnityEngine.Object[] References(MonoBehaviour owner)
        {
            if (owner is LoadScene loading) return new UnityEngine.Object[] { loading.image };
            if (owner is FadeImages skill) return new UnityEngine.Object[] { skill.galhoFadeCanvas, skill.leafFadeGalho, skill.painel, skill.fillImage, skill.fillIcon };
            if (owner is FadeController fade) return new UnityEngine.Object[] { fade.animator };
            if (owner is System.MiniMap.MiniMapUI minimap) return new UnityEngine.Object[] { minimap.hitAnimator };
            throw new ArgumentException("Unsupported UI owner");
        }
        static bool ReferencesMatch(Entry e)
        {
            var r = e.references;
            foreach (var reference in r) if (reference == null) return false;
            if (e.owner is LoadScene loading) return loading.image == r[0];
            if (e.owner is FadeImages skill) return skill.galhoFadeCanvas == r[0] && skill.leafFadeGalho == r[1] && skill.painel == r[2] && skill.fillImage == r[3] && skill.fillIcon == r[4];
            if (e.owner is FadeController fade) return fade.animator == r[0];
            if (e.owner is System.MiniMap.MiniMapUI minimap) return minimap.hitAnimator == r[0];
            return false;
        }
        static Entry Create(MonoBehaviour owner, PerformanceUiSourceInventory.Row row)
        {
            if (!HasWitness(owner, row)) throw new ArgumentException("Exact valid UI source, local enabled witness and retained Animator required");
            var animator = FindAnimator(owner, row.animatorId);
            var image = animator.GetComponent<Image>(); if (owner is FadeController && image == null) image = animator.GetComponentInChildren<Image>(true);
            var entry = new Entry { owner = owner, animator = animator, image = image, witness = animator.GetComponent<PerformanceUiSourceWitness>(),
                canvas = image.GetComponentInParent<Canvas>(true), references = References(owner) };
            var transforms = new HashSet<Transform>();
            Action<Transform> add = t => { while (t != null) { transforms.Add(t); t = t.parent; } };
            add(owner.transform); add(animator.transform); add(image.transform); if (entry.canvas != null) add(entry.canvas.transform);
            var behaviours = new List<Behaviour> { owner, animator, image, entry.witness }; if (entry.canvas != null) behaviours.Add(entry.canvas);
            foreach (var reference in entry.references)
            {
                var go = reference as GameObject; if (reference is Component c) go = c.gameObject;
                add(go.transform); if (reference is Behaviour b && !behaviours.Contains(b)) behaviours.Add(b);
            }
            var nodes = new List<Node>(); foreach (var t in transforms) nodes.Add(new Node { transform = t, parent = t.parent, scene = t.gameObject.scene });
            entry.nodes = nodes.ToArray(); entry.behaviours = behaviours.ToArray(); entry.enabled = new bool[entry.behaviours.Length]; return entry;
        }
#endif
        PerformanceUiSourceContext() { }
        public static PerformanceUiSourceContext Begin(string[] args, MonoBehaviour[] owners)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int flags = 0; if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") flags++;
            if (flags == 0) return null;
            if (flags != 1 || !Application.isPlaying || owners == null || owners.Length > 4096) throw new ArgumentException("One opt-in and bounded runtime owners required");
            if (active != null) throw new InvalidOperationException("UI source callbacks already owned");
            var seen = new HashSet<MonoBehaviour>(); foreach (var owner in owners) if (owner == null || !seen.Add(owner)) throw new ArgumentException("Unique live UI owners required");
            var rows = PerformanceUiSourceInventory.Capture(args, owners); var entries = new List<Entry>(); var represented = new HashSet<MonoBehaviour>();
            foreach (var row in rows)
            {
                MonoBehaviour owner = null; foreach (var candidate in owners) if (candidate.GetInstanceID() == row.ownerId) { owner = candidate; break; }
                entries.Add(Create(owner, row)); represented.Add(owner);
            }
            if (represented.Count != owners.Length) throw new ArgumentException("Each declared UI owner must retain valid witnessed references");
            var context = new PerformanceUiSourceContext { entries = entries.ToArray(), owners = (MonoBehaviour[])owners.Clone(), scene = SceneManager.GetActiveScene() }; active = context; return context;
#else
            return null;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("KARAWARA_QA")]
        public static void Notify(PerformanceUiSourceWitness source, PerformanceUiSourceEvent change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var context = active; if (context == null || context.IsDisposed) return;
            try { bool known = false; foreach (var entry in context.entries) if (ReferenceEquals(entry.witness, source)) { known = true; break; } context.Record(source, change, known); }
            catch { context.invalid = true; context.SourceFaulted = true; }
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("KARAWARA_QA")]
        public static void NotifyOwner(MonoBehaviour source, PerformanceUiSourceEvent change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var context = active; if (context == null || context.IsDisposed) return;
            try { bool known = false; foreach (var entry in context.entries) if (ReferenceEquals(entry.owner, source)) { known = true; break; } context.Record(source, change, known); }
            catch { context.invalid = true; context.SourceFaulted = true; }
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void Record(MonoBehaviour source, PerformanceUiSourceEvent change, bool known)
        {
            bool allowed = known && source != null && ReferenceEquals(source, combatDamageOwner) && change == PerformanceUiSourceEvent.HitEffect;
            EventCount++;
            if (eventLength < events.Length) events[eventLength++] = new SourceEvent { frame = Time.frameCount, sourceId = source == null ? 0 : source.GetInstanceID(), kind = change, frozen = IsFrozen, combatAllowed = allowed };
            else invalid = true;
            if (allowed) combatHitEffectCount++;
            if (!known || source == null || change == PerformanceUiSourceEvent.Destroyed || IsFrozen && !allowed) invalid = true;
        }
        void ThrowIfDisposed() { if (IsDisposed) throw new ObjectDisposedException(nameof(PerformanceUiSourceContext)); }
#endif
        // One explicit combat phase may animate the exact retained minimap damage source.
        // This records API activity, not proof that a particular health change caused it.
        public void BindCucaCombatDamageFeedback(string mode)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed();
            if (IsFrozen || combatMode != null || scene.name != "CucaLoad" ||
                mode != "cuca-route-rehearsal" && mode != "cuca-certified-route")
                throw new ArgumentException("One pre-freeze explicit Cuca combat phase required.");
            System.MiniMap.MiniMapUI owner = null;
            foreach (var entry in entries)
                if (entry.owner is System.MiniMap.MiniMapUI candidate)
                {
                    if (owner != null || candidate.hitAnimator != entry.animator || !ReferencesMatch(entry))
                        throw new InvalidOperationException("Exactly one retained minimap damage source required.");
                    owner = candidate;
                }
            if (owner == null) throw new InvalidOperationException("Cuca minimap damage source unavailable.");
            Poll(); if (invalid) throw new InvalidOperationException("Invalid UI source cannot bind combat activity.");
            combatDamageOwner = owner; combatMode = mode;
#endif
        }
        public void Poll()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed(); if (SceneManager.GetActiveScene() != scene) invalid = true;
            foreach (var e in entries)
            {
                if (e.owner == null || e.animator == null || e.witness == null || !e.witness.enabled || e.witness.animator != e.animator || e.image == null || !ReferencesMatch(e)) { invalid = true; continue; }
                if (!ReferenceEquals(e.canvas, null) && e.canvas == null || e.image.GetComponentInParent<Canvas>(true) != e.canvas) invalid = true;
                foreach (var n in e.nodes) if (n.transform == null || n.transform.gameObject.scene != n.scene || IsFrozen && (n.transform.parent != n.parent || n.transform.gameObject.activeInHierarchy != n.active)) invalid = true;
                for (int i = 0; i < e.behaviours.Length; i++) if (e.behaviours[i] == null || IsFrozen && e.behaviours[i].enabled != e.enabled[i]) invalid = true;
                if (IsFrozen && e.animator.runtimeAnimatorController != e.controller) invalid = true;
            }
#endif
        }
        public void Freeze()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed(); if (IsFrozen) throw new InvalidOperationException("Frozen UI cannot be rebased"); Poll(); if (invalid) throw new InvalidOperationException("UI references/lifetime changed before freeze");
            foreach (var e in entries) { foreach (var n in e.nodes) { n.parent = n.transform.parent; n.active = n.transform.gameObject.activeInHierarchy; } for (int i = 0; i < e.behaviours.Length; i++) e.enabled[i] = e.behaviours[i].enabled; e.controller = e.animator.runtimeAnimatorController; } IsFrozen = true;
#endif
        }
        public Checkpoint CaptureCheckpoint(string label)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            Poll(); if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Label required"); var copy = new SourceEvent[eventLength]; Array.Copy(events, copy, eventLength);
            return new Checkpoint { label = label, utc = DateTime.UtcNow.ToString("O"), frozen = IsFrozen, conditionsChanged = invalid, sourceFaulted = SourceFaulted, eventCount = EventCount, combatMode = combatMode, combatDamageOwnerId = combatDamageOwner == null ? 0 : combatDamageOwner.GetInstanceID(), combatHitEffectCount = combatHitEffectCount, events = copy, sources = PerformanceUiSourceInventory.Capture(new[] { "-karawaraPerformanceRoster" }, owners) };
#else
            return null;
#endif
        }
        public void Validate(CaptureResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (result == null) throw new ArgumentNullException(nameof(result)); Poll(); result.populationVerified = false; result.comparisonValid = false;
            result.validationUnavailableReasons.Add("UI callbacks are not full native/transient/never-active creation closure or accepted rendering checkpoints."); if (!IsFrozen) result.validationUnavailableReasons.Add("UI sources not frozen after settling.");
            if (invalid || SourceFaulted) { result.conditionsChanged = true; const string message = "UI source reference/lifetime/API changed (latched)."; if (!result.errors.Contains(message)) result.errors.Add(message); }
#endif
        }
        public void Dispose()
        {
            if (IsDisposed) return; IsDisposed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (ReferenceEquals(active, this)) active = null; Array.Clear(entries, 0, entries.Length); Array.Clear(owners, 0, owners.Length); combatDamageOwner = null;
#endif
        }
    }
}
