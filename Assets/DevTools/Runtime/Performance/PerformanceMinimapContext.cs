using System;
using System.MiniMap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGKarawara.DevTools.Performance
{
    public enum PerformanceMinimapEvent { Created, Enabled, Disabled, Destroyed, ParentChanged }

    // Separate UI ownership/protocol. Never contributes to the living-hostile count.
    public sealed class PerformanceMinimapContext : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool SourceFaulted { get; private set; }
        public long EventCount { get; private set; }
        [Serializable] public sealed class IconState
        {
            public int creatorId, iconId, witnessId, animatorId, targetId, parentId, canvasId, spriteId;
            public bool active, iconEnabled, animatorEnabled, graphicEnabled;
            public float? alpha;
        }
        [Serializable] public sealed class Checkpoint
        {
            public string label, utc;
            public bool frozen, conditionsChanged, sourceFaulted;
            public long eventCount;
            public IconState[] icons;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        sealed class Entry
        {
            public MiniMapEntity creator;
            public IconBaseMiniMapIcon icon;
            public PerformanceMinimapIconWitness witness;
            public Transform target, parent;
            public Image graphic;
            public CanvasGroup group;
            public Animator animator;
            public RuntimeAnimatorController controller;
            public Canvas canvas;
            public Scene creatorScene, iconScene;
            public bool active, creatorActive, creatorEnabled, iconEnabled, animatorEnabled, graphicEnabled, canvasEnabled, canvasActive;
        }
        static PerformanceMinimapContext active;
        Entry[] entries;
        Scene scene;
        bool invalid;
        static Entry CreateEntry(MiniMapEntity creator)
        {
            var icon = creator == null ? null : creator.IconInstance as IconBaseMiniMapIcon;
            if (creator == null || icon == null || !creator.gameObject.scene.IsValid() || !icon.gameObject.scene.IsValid() || creator.Target == null)
                throw new ArgumentException("Actual creator, retained icon and target references required.");
            var witness = icon.GetComponent<PerformanceMinimapIconWitness>();
            var graphic = icon.TargetGraphic; var group = icon.m_CanvasGroup;
            if (witness == null || !witness.enabled || witness.icon != icon || graphic == null || graphic.gameObject != icon.gameObject ||
                group == null || group.gameObject != icon.gameObject || graphic.canvas == null)
                throw new ArgumentException("Exact icon prefab witness, local Graphic/CanvasGroup and actual Canvas required.");
            return new Entry { creator = creator, icon = icon, witness = witness, graphic = graphic, group = group,
                animator = icon.GetComponent<Animator>(), target = creator.Target, creatorScene = creator.gameObject.scene, iconScene = icon.gameObject.scene };
        }
        internal static bool CanBind(MiniMapEntity creator) { try { CreateEntry(creator); return true; } catch (ArgumentException) { return false; } }
#endif
        PerformanceMinimapContext() { }
        public static PerformanceMinimapContext Begin(string[] args, MiniMapEntity[] creators)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int flags = 0; if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") flags++;
            if (flags == 0) return null;
            if (flags != 1 || !Application.isPlaying || creators == null || creators.Length > 4096) throw new ArgumentException("One explicit roster flag and bounded runtime UI references required.");
            if (active != null) throw new InvalidOperationException("Another minimap source context owns the callbacks.");
            var context = new PerformanceMinimapContext { entries = new Entry[creators.Length], scene = SceneManager.GetActiveScene() };
            for (int i = 0; i < creators.Length; i++)
            {
                var entry = CreateEntry(creators[i]);
                for (int j = 0; j < i; j++) if (context.entries[j].creator == entry.creator || context.entries[j].icon == entry.icon)
                    throw new ArgumentException("Each creator/icon identity must be bound once.");
                context.entries[i] = entry;
            }
            active = context;
            return context;
#else
            return null;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("KARAWARA_QA")]
        public static void Notify(PerformanceMinimapIconWitness witness, PerformanceMinimapEvent change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var context = active;
            if (context == null || context.IsDisposed) return;
            try
            {
                context.EventCount++;
                if (witness == null || witness.icon == null) { context.invalid = true; context.SourceFaulted = true; return; }
                bool known = false;
                foreach (var entry in context.entries) if (ReferenceEquals(entry.witness, witness)) { known = true; break; }
                if (!known || change == PerformanceMinimapEvent.Destroyed || context.IsFrozen) context.invalid = true;
            }
            catch { context.SourceFaulted = true; context.invalid = true; }
#endif
        }
        public void Freeze()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed();
            if (IsFrozen) throw new InvalidOperationException("The same UI context cannot be rebased after freezing.");
            Poll(); if (invalid) throw new InvalidOperationException("Minimap creator/reference/lifetime changed before freezing.");
            foreach (var entry in entries)
            {
                entry.parent = entry.icon.transform.parent; entry.canvas = entry.graphic.canvas;
                entry.active = entry.icon.gameObject.activeInHierarchy; entry.iconEnabled = entry.icon.enabled;
                entry.creatorActive = entry.creator.gameObject.activeInHierarchy; entry.creatorEnabled = entry.creator.enabled;
                entry.graphicEnabled = entry.graphic.enabled; entry.canvasEnabled = entry.canvas.enabled; entry.canvasActive = entry.canvas.gameObject.activeInHierarchy;
                entry.animatorEnabled = entry.animator != null && entry.animator.enabled;
                entry.controller = entry.animator == null ? null : entry.animator.runtimeAnimatorController;
            }
            IsFrozen = true;
#endif
        }
        public void Poll()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed();
            if (SceneManager.GetActiveScene() != scene) invalid = true;
            foreach (var entry in entries)
            {
                if (entry.creator == null || entry.icon == null || entry.witness == null || !entry.witness.enabled || entry.witness.icon != entry.icon ||
                    entry.creator.IconInstance != entry.icon || entry.creator.Target != entry.target || entry.target == null ||
                    entry.graphic == null || entry.icon.TargetGraphic != entry.graphic || entry.graphic.canvas == null || entry.group == null || entry.icon.m_CanvasGroup != entry.group ||
                    entry.icon.gameObject.scene != entry.iconScene || entry.creator.gameObject.scene != entry.creatorScene)
                { invalid = true; continue; }
                if (!ReferenceEquals(entry.animator, null) && entry.animator == null) invalid = true;
                if (!IsFrozen) continue;
                if (entry.icon.transform.parent != entry.parent || entry.graphic.canvas != entry.canvas || entry.canvas == null ||
                    entry.icon.gameObject.activeInHierarchy != entry.active || entry.icon.enabled != entry.iconEnabled ||
                    entry.creator.enabled != entry.creatorEnabled || entry.creator.gameObject.activeInHierarchy != entry.creatorActive ||
                    entry.graphic.enabled != entry.graphicEnabled || entry.canvas.enabled != entry.canvasEnabled || entry.canvas.gameObject.activeInHierarchy != entry.canvasActive ||
                    (entry.animator != null && (entry.animator.enabled != entry.animatorEnabled || entry.animator.runtimeAnimatorController != entry.controller))) invalid = true;
            }
#endif
        }
        public Checkpoint CaptureCheckpoint(string label)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            Poll();
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Checkpoint label required.");
            var icons = new IconState[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; var icon = entry.icon; var graphic = entry.graphic; var canvas = graphic == null ? null : graphic.canvas;
                icons[i] = new IconState { creatorId = entry.creator == null ? 0 : entry.creator.GetInstanceID(), iconId = icon == null ? 0 : icon.GetInstanceID(),
                    witnessId = entry.witness == null ? 0 : entry.witness.GetInstanceID(), animatorId = entry.animator == null ? 0 : entry.animator.GetInstanceID(),
                    targetId = entry.target == null ? 0 : entry.target.GetInstanceID(), parentId = icon == null || icon.transform.parent == null ? 0 : icon.transform.parent.GetInstanceID(),
                    canvasId = canvas == null ? 0 : canvas.GetInstanceID(), spriteId = graphic == null || graphic.sprite == null ? 0 : graphic.sprite.GetInstanceID(),
                    active = icon != null && icon.gameObject.activeInHierarchy, iconEnabled = icon != null && icon.enabled,
                    animatorEnabled = entry.animator != null && entry.animator.enabled, graphicEnabled = graphic != null && graphic.enabled,
                    alpha = entry.group == null ? (float?)null : entry.group.alpha };
            }
            return new Checkpoint { label = label, utc = DateTime.UtcNow.ToString("O"), frozen = IsFrozen, conditionsChanged = invalid,
                eventCount = EventCount, sourceFaulted = SourceFaulted, icons = icons };
#else
            return null;
#endif
        }
        public void Validate(CaptureResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (result == null) throw new ArgumentNullException(nameof(result)); Poll();
            result.populationVerified = false; result.comparisonValid = false;
            result.validationUnavailableReasons.Add("UI source binding is not full creation/native-component closure or an accepted rendering checkpoint protocol.");
            if (!IsFrozen) result.validationUnavailableReasons.Add("UI context was not frozen after settling.");
            if (invalid || SourceFaulted) { result.conditionsChanged = true; const string error = "Frozen minimap UI source/reference/lifetime changed (latched)."; if (!result.errors.Contains(error)) result.errors.Add(error); }
#endif
        }
        public void Dispose()
        {
            if (IsDisposed) return; IsDisposed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (ReferenceEquals(active, this)) active = null;
            Array.Clear(entries, 0, entries.Length);
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void ThrowIfDisposed() { if (IsDisposed) throw new ObjectDisposedException(nameof(PerformanceMinimapContext)); }
#endif
    }
}
