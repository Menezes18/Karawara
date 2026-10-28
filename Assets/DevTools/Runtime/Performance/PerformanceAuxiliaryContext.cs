using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
namespace RPGKarawara.DevTools.Performance
{
    public enum PerformanceAuxiliaryEvent { Created, Enabled, Disabled, Destroyed, ParentChanged, SpawnRequested, ElementChanged, DestroyScheduled }
    public sealed class PerformanceAuxiliaryContext : IDisposable
    {
        [Serializable] public sealed class Row
        {
            public int markerId, animatorId, driverId, prefabId, spawnedId, cameraId, particleId;
            public string kind, scene, path;
            public bool supportedBinding, active, animatorEnabled, creationPathsVerified = false, lifecycleCertified = false, renderingCertified = false;
            public bool? particlePlaying, particlePaused, emissionEnabled, cameraEnabled, listenerEnabled;
            public int? particleCount;
            public float? destroyDelay;
        }
        [Serializable] public sealed class Checkpoint { public string label, utc; public bool frozen, conditionsChanged, sourceFaulted; public long eventCount; public Row[] sources; }
        public bool IsDisposed { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool SourceFaulted { get; private set; }
        public long EventCount { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        sealed class Entry
        {
            public PerformanceAuxiliarySource marker;
            public Animator animator;
            public Component driver;
            public Behaviour behaviour;
            public ParticleSystem particle;
            public ParticleSystemRenderer particleRenderer;
            public Camera camera;
            public AudioListener listener;
            public UniversalAdditionalCameraData cameraData;
            public AICharacterSpawner spawner;
            public DestroyAfterTime lifetime;
            public GameObject prefab, spawned;
            public AICharacterManager manager;
            public Material water, fire;
            public Transform parent;
            public Scene scene;
            public RuntimeAnimatorController controller;
            public bool active, animatorEnabled, driverEnabled, playing, paused, emission, rendererEnabled, cameraEnabled, listenerEnabled, dataEnabled, boss;
            public Element element;
            public float delay;
        }
        static PerformanceAuxiliaryContext active;
        Entry[] entries;
        Scene scene;
        bool invalid;
        static T Live<T>(T value) where T : UnityEngine.Object => value == null ? null : value;
        static bool Lost(UnityEngine.Object value) => !ReferenceEquals(value, null) && value == null;
        internal static bool CanBind(PerformanceAuxiliarySource source) => source != null && source.gameObject.scene.IsValid() && source.IsValidSource();
        static Entry Create(PerformanceAuxiliarySource marker)
        {
            if (!CanBind(marker)) throw new ArgumentException("Exact reviewed auxiliary marker, Animator and local source component required");
            var e = new Entry { marker = marker, animator = marker.visual, driver = marker.source, behaviour = marker.source as Behaviour, particle = marker.source as ParticleSystem,
                camera = Live(marker.GetComponent<Camera>()), listener = Live(marker.GetComponent<AudioListener>()), cameraData = Live(marker.GetComponent<UniversalAdditionalCameraData>()),
                particleRenderer = Live(marker.GetComponent<ParticleSystemRenderer>()), spawner = marker.source as AICharacterSpawner, lifetime = marker.source as DestroyAfterTime,
                scene = marker.gameObject.scene };
            if (e.spawner != null) { e.prefab = Live(e.spawner.QaPrefab); e.spawned = Live(e.spawner.QaSpawned); e.manager = Live(e.spawner.characterManager); e.water = Live(e.spawner.agua); e.fire = Live(e.spawner.fogo); e.boss = e.spawner.boss; e.element = e.spawner.characterElement; }
            if (e.lifetime != null) e.delay = e.lifetime.QaDestroyDelay;
            return e;
        }
#endif
        PerformanceAuxiliaryContext() { }
        public static PerformanceAuxiliaryContext Begin(string[] args, PerformanceAuxiliarySource[] sources)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int flags = 0; if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") flags++;
            if (flags == 0) return null;
            if (flags != 1 || !Application.isPlaying || sources == null || sources.Length > 4096) throw new ArgumentException("One explicit opt-in and bounded runtime source references required");
            if (active != null) throw new InvalidOperationException("Auxiliary callbacks already owned");
            var entries = new Entry[sources.Length]; for (int i = 0; i < sources.Length; i++) { entries[i] = Create(sources[i]); for (int j = 0; j < i; j++) if (sources[i] == sources[j] || entries[i].animator == entries[j].animator) throw new ArgumentException("Duplicate auxiliary source identity"); }
            var context = new PerformanceAuxiliaryContext { entries = entries, scene = SceneManager.GetActiveScene() }; active = context; return context;
#else
            return null;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("KARAWARA_QA")]
        public static void Notify(MonoBehaviour source, PerformanceAuxiliaryEvent change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var context = active; if (context == null || context.IsDisposed) return;
            try
            {
                context.EventCount++; bool known = false; foreach (var e in context.entries) if (ReferenceEquals(e.marker, source) || ReferenceEquals(e.driver, source)) { known = true; break; }
                if (!known || source == null || change == PerformanceAuxiliaryEvent.Destroyed || change == PerformanceAuxiliaryEvent.SpawnRequested || change == PerformanceAuxiliaryEvent.DestroyScheduled || context.IsFrozen) context.invalid = true;
            }
            catch { context.invalid = true; context.SourceFaulted = true; }
#endif
        }
        public void Poll()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed(); if (SceneManager.GetActiveScene() != scene) invalid = true;
            foreach (var e in entries)
            {
                if (e.marker == null || !e.marker.enabled || e.animator == null || e.driver == null || e.marker.visual != e.animator || e.marker.source != e.driver || e.marker.gameObject.scene != e.scene) { invalid = true; continue; }
                if (Lost(e.particleRenderer) || Lost(e.camera) || Lost(e.listener) || Lost(e.cameraData)) { invalid = true; continue; }
                if (e.spawner != null && (Lost(e.prefab) || Lost(e.spawned) || Lost(e.manager) || Lost(e.water) || Lost(e.fire) || e.spawner.QaPrefab != e.prefab || e.spawner.QaSpawned != e.spawned || e.spawner.characterManager != e.manager || e.spawner.agua != e.water || e.spawner.fogo != e.fire || e.spawner.boss != e.boss || e.spawner.characterElement != e.element)) invalid = true;
                if (e.lifetime != null && e.lifetime.QaDestroyDelay != e.delay) invalid = true;
                if (!IsFrozen) continue;
                if (e.marker.transform.parent != e.parent || e.marker.gameObject.activeInHierarchy != e.active || e.animator.enabled != e.animatorEnabled || e.animator.runtimeAnimatorController != e.controller || e.behaviour != null && e.behaviour.enabled != e.driverEnabled) invalid = true;
                if (e.particle != null && (e.particle.isPlaying != e.playing || e.particle.isPaused != e.paused || e.particle.emission.enabled != e.emission || e.particleRenderer.enabled != e.rendererEnabled)) invalid = true;
                if (e.camera != null && (e.camera.enabled != e.cameraEnabled || e.listener.enabled != e.listenerEnabled || e.cameraData.enabled != e.dataEnabled)) invalid = true;
            }
#endif
        }
        public void Freeze()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed(); if (IsFrozen) throw new InvalidOperationException("Auxiliary snapshot cannot be rebased"); Poll(); if (invalid) throw new InvalidOperationException("Auxiliary identity/reference/creation changed before freeze");
            foreach (var e in entries)
            {
                e.parent = e.marker.transform.parent; e.active = e.marker.gameObject.activeInHierarchy; e.animatorEnabled = e.animator.enabled; e.controller = e.animator.runtimeAnimatorController; e.driverEnabled = e.behaviour != null && e.behaviour.enabled;
                if (e.particle != null) { e.playing = e.particle.isPlaying; e.paused = e.particle.isPaused; e.emission = e.particle.emission.enabled; e.rendererEnabled = e.particleRenderer.enabled; }
                if (e.camera != null) { e.cameraEnabled = e.camera.enabled; e.listenerEnabled = e.listener.enabled; e.dataEnabled = e.cameraData.enabled; }
            }
            IsFrozen = true;
#endif
        }
        public static Row[] CaptureRows(PerformanceAuxiliarySource[] sources)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var rows = new Row[sources.Length]; for (int i = 0; i < rows.Length; i++) rows[i] = Describe(sources[i]); return rows;
#else
            return Array.Empty<Row>();
#endif
        }
        public Checkpoint CaptureCheckpoint(string label)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            Poll(); if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Checkpoint label required"); var rows = new Row[entries.Length]; for (int i = 0; i < rows.Length; i++) rows[i] = Describe(entries[i].marker);
            return new Checkpoint { label = label, utc = DateTime.UtcNow.ToString("O"), frozen = IsFrozen, conditionsChanged = invalid, sourceFaulted = SourceFaulted, eventCount = EventCount, sources = rows };
#else
            return null;
#endif
        }
        public void Validate(CaptureResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (result == null) throw new ArgumentNullException(nameof(result)); Poll(); result.populationVerified = false; result.comparisonValid = false;
            result.validationUnavailableReasons.Add("Auxiliary bindings do not prove full creation/native-component/renderer closure or accepted visual checkpoints."); if (!IsFrozen) result.validationUnavailableReasons.Add("Auxiliary sources not frozen after settling");
            if (invalid || SourceFaulted) { result.conditionsChanged = true; const string message = "Auxiliary source lifecycle/reference/native state changed (latched)."; if (!result.errors.Contains(message)) result.errors.Add(message); }
#endif
        }
        public void Dispose()
        {
            if (IsDisposed) return; IsDisposed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (ReferenceEquals(active, this)) active = null; Array.Clear(entries, 0, entries.Length);
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void ThrowIfDisposed() { if (IsDisposed) throw new ObjectDisposedException(nameof(PerformanceAuxiliaryContext)); }
        static Row Describe(PerformanceAuxiliarySource marker)
        {
            if (marker == null) return new Row { kind = "missing" };
            var particle = marker.source as ParticleSystem; var spawner = marker.source as AICharacterSpawner; var lifetime = marker.source as DestroyAfterTime; var camera = marker.GetComponent<Camera>(); var listener = marker.GetComponent<AudioListener>();
            string path = marker.name; for (var t = marker.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return new Row { markerId = marker.GetInstanceID(), animatorId = marker.visual == null ? 0 : marker.visual.GetInstanceID(), driverId = marker.source == null ? 0 : marker.source.GetInstanceID(), kind = particle != null ? "particle" : spawner != null ? "spawner-visual" : lifetime != null ? "camera-sequence" : "missing",
                scene = marker.gameObject.scene.name, path = path, supportedBinding = CanBind(marker), active = marker.gameObject.activeInHierarchy, animatorEnabled = marker.visual != null && marker.visual.enabled,
                particleId = particle == null ? 0 : particle.GetInstanceID(), particlePlaying = particle == null ? (bool?)null : particle.isPlaying, particlePaused = particle == null ? (bool?)null : particle.isPaused, emissionEnabled = particle == null ? (bool?)null : particle.emission.enabled, particleCount = particle == null ? (int?)null : particle.particleCount,
                prefabId = spawner == null || spawner.QaPrefab == null ? 0 : spawner.QaPrefab.GetInstanceID(), spawnedId = spawner == null || spawner.QaSpawned == null ? 0 : spawner.QaSpawned.GetInstanceID(), cameraId = camera == null ? 0 : camera.GetInstanceID(), cameraEnabled = camera == null ? (bool?)null : camera.enabled, listenerEnabled = listener == null ? (bool?)null : listener.enabled, destroyDelay = lifetime == null ? (float?)null : lifetime.QaDestroyDelay };
        }
#endif
    }
}
