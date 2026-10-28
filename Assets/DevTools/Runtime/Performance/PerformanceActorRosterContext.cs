using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Performance
{
    public enum PerformanceActorLifecycle
    {
        Enabled, Disabled, Destroyed, LocalInitialized, LocalShutdown, HealthChanged, PhaseChanged
    }

    // Explicit QA binding. Complete route creation-path closure remains a separate acceptance gate.
    public sealed class PerformanceActorRosterContext : IDisposable
    {
        bool disposed;
        public bool IsDisposed => disposed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        enum Role { Player, Hostile, Npc, Animal, Companion, WorldVisual }
        [Flags] enum Violation { None = 0, Lifetime = 1, References = 2, LivingState = 4, Phase = 8, Scene = 16, UnknownActor = 32 }
        sealed class Entry
        {
            public Component actor;
            public GameObject root;
            public Role role;
            public Scene scene;
            public Behaviour behaviour;
            public Behaviour[] npcControllers = Array.Empty<Behaviour>();
            public bool[] npcEnabled = Array.Empty<bool>();
            public Animator[] visualAnimators = Array.Empty<Animator>();
            public bool[] visualEnabled = Array.Empty<bool>();
            public bool[] visualActive = Array.Empty<bool>();
            public RuntimeAnimatorController[] visualControllers = Array.Empty<RuntimeAnimatorController>();
            public PerformanceAuthoredVisual authoredVisual;
            public PerformanceSceneAnimatorSource.FrozenSource sceneVisualProof;
            public PerformanceCucaSceneProof cucaVisualProof;
            public PerformanceLoadedVisualProof loadedVisualProof;
            public Animator authoredAnimator;
            public MonoBehaviour[] reviewedChildren = Array.Empty<MonoBehaviour>();
            public bool[] childEnabled, childActive, hitboxEnabled, hitboxTrigger;
            public Transform[] childParents;
            public Collider[] hitboxes;
            public Camera[] reviewedCameras;
            public AudioListener[] reviewedListeners;
            public Animation[] reviewedAnimations;
            public AnimationClip[] reviewedClips;
            public bool[] cameraEnabled, listenerEnabled, animationEnabled, animationPlaying, animationAutomatic;
            public CharacterManager character;
            public CharacterNetworkManager network;
            public HealthManager health;
            public bool active, enabled, healthEnabled, spawned, living, aiDisabled;
            public int maximumNetworkHealth;
            public float maximumLegacyHealth;
        }
        readonly Entry[] entries;
        readonly GameObject[] effects;
        readonly Scene activeScene;
        readonly bool declaredCreationClosure, unsupportedRoles;
        Violation violations;
        internal int SourceViolationMask => (int)violations;
        PerformanceActorLifecycleScope sourceScope;
        bool sourceScopeWasAttached;
        internal Component[] SourceActors()
        {
            ThrowIfDisposed();
            var actors = new System.Collections.Generic.List<Component>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                actors.Add(entries[i].actor);
                if (entries[i].loadedVisualProof != null) actors.Add(entries[i].loadedVisualProof.Owner);
                if (entries[i].health != null && entries[i].health != entries[i].actor) actors.Add(entries[i].health);
                if (entries[i].network != null && entries[i].network != entries[i].actor) actors.Add(entries[i].network);
                foreach (var controller in entries[i].npcControllers) if (controller != entries[i].actor) actors.Add(controller);
                foreach (var animator in entries[i].visualAnimators) if (animator != entries[i].actor) actors.Add(animator);
                foreach (var child in entries[i].reviewedChildren) actors.Add(child);
                if (entries[i].sceneVisualProof != null) foreach (var renderer in entries[i].sceneVisualProof.Subjects()) actors.Add(renderer);
                if (entries[i].cucaVisualProof != null) foreach (var subject in entries[i].cucaVisualProof.Subjects()) actors.Add(subject);
            }
            return actors.ToArray();
        }
        internal int SourceOwnerRootId(Component subject)
        {
            ThrowIfDisposed();
            foreach (var entry in entries)
            {
                if (entry.loadedVisualProof != null && ReferenceEquals(subject, entry.loadedVisualProof.Owner)) return entry.root.GetInstanceID();
                if (ReferenceEquals(subject, entry.actor) || ReferenceEquals(subject, entry.health) || ReferenceEquals(subject, entry.network)) return entry.root.GetInstanceID();
                foreach (var actor in entry.npcControllers) if (ReferenceEquals(subject, actor)) return entry.root.GetInstanceID();
                foreach (var actor in entry.visualAnimators) if (ReferenceEquals(subject, actor)) return entry.root.GetInstanceID();
                foreach (var actor in entry.reviewedChildren) if (ReferenceEquals(subject, actor)) return entry.root.GetInstanceID();
                if (entry.sceneVisualProof != null && entry.sceneVisualProof.Owns(subject)) return entry.root.GetInstanceID();
                if (entry.cucaVisualProof != null && entry.cucaVisualProof.Owns(subject)) return entry.root.GetInstanceID();
            }
            throw new ArgumentException("Source subject has no exact bound actor owner.");
        }
        internal void AttachSources(PerformanceActorLifecycleScope scope)
        {
            ThrowIfDisposed();
            if (sourceScope != null) throw new InvalidOperationException("Roster already has a source scope.");
            sourceScope = scope;
            sourceScopeWasAttached = true;
        }
        internal void DetachSources(PerformanceActorLifecycleScope scope) { if (ReferenceEquals(sourceScope, scope)) sourceScope = null; }
        internal void MarkSourceFault() { violations |= Violation.References; }
        internal bool IsReviewedVisualChild(Component actor)
        {
            if (disposed) return false;
            foreach (var entry in entries)
            {
                if (entry.cucaVisualProof != null && entry.cucaVisualProof.Owns(actor)) return true;
                foreach (var child in entry.reviewedChildren) if (ReferenceEquals(actor, child)) return true;
            }
            return false;
        }

        PerformanceActorRosterContext(Entry[] entries, GameObject[] effects, bool closure, bool unsupported)
        {
            this.entries = entries;
            this.effects = effects;
            declaredCreationClosure = closure;
            unsupportedRoles = unsupported;
            activeScene = SceneManager.GetActiveScene();
        }
#else
        PerformanceActorRosterContext() { }
#endif
        public static PerformanceActorRosterContext Create(string[] args, Component[] actors, string[] roles, GameObject[] effectRoots, bool creationPathsCovered)
            => CreateCore(args, actors, roles, effectRoots, creationPathsCovered, true);
        internal static PerformanceActorRosterContext CreateEndpointOnly(string[] args, Component[] actors, string[] roles, GameObject[] effectRoots, bool creationPathsCovered)
            => CreateCore(args, actors, roles, effectRoots, creationPathsCovered, false);
        static PerformanceActorRosterContext CreateCore(string[] args, Component[] actors, string[] roles, GameObject[] effectRoots, bool creationPathsCovered, bool observeCucaCallbacks)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int count = 0;
            if (args != null) for (int i = 0; i < args.Length; i++) if (args[i] == "-karawaraPerformanceRoster") count++;
            if (count == 0) return null;
            if (count != 1 || actors == null || roles == null || effectRoots == null || actors.Length != roles.Length || actors.Length > 4096 || effectRoots.Length > 4096)
                throw new ArgumentException("Require one roster flag, bounded non-null actor/role/effect arrays and one role per actor.");
            var entries = new Entry[actors.Length];
            bool unsupported = false;
            try
            {
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor == null) throw new ArgumentException("Actor references must be present.");
                var role = ParseRole(roles[i]);
                if (!Supports(actor, role)) throw new ArgumentException("Actor type does not support its declared role.");
                for (int j = 0; j < i; j++) if (entries[j].root == actor.gameObject) throw new ArgumentException("Each actor root must be bound exactly once.");
                var entry = new Entry { actor = actor, root = actor.gameObject, role = role, scene = actor.gameObject.scene,
                    behaviour = actor as Behaviour, character = actor as CharacterManager };
                entries[i] = entry; // Retain partial ownership for exception cleanup.
                if (role == Role.Npc)
                {
                    var controllers = new System.Collections.Generic.List<Behaviour>();
                    foreach (var component in entry.root.GetComponents<Behaviour>()) if (AmbientNpcController(component)) controllers.Add(component);
                    if (controllers.Count > 16) throw new ArgumentException("Ambient NPC controller set exceeds its preflight bound.");
                    entry.npcControllers = controllers.ToArray(); entry.npcEnabled = new bool[controllers.Count];
                    for (int j = 0; j < controllers.Count; j++) entry.npcEnabled[j] = controllers[j].enabled;
                }
                if (role == Role.Animal || role == Role.WorldVisual)
                {
                    if (actor is Animator loadedAnimator)
                    {
                        var owner = PerformanceLoadedVisualProof.FindOwner(loadedAnimator.gameObject);
                        if (owner != null) entry.loadedVisualProof = PerformanceLoadedVisualProof.Freeze(owner);
                    }
                    if (actor is PerformanceSceneAnimatorSource sceneVisual)
                    { entry.sceneVisualProof = sceneVisual.Freeze(); entry.reviewedChildren = sceneVisual.DriverSubjects(); }
                    if (actor is PerformanceCucaSceneSource cucaVisual) entry.cucaVisualProof = observeCucaCallbacks ? cucaVisual.Freeze() : cucaVisual.FreezeEndpoint();
                    entry.authoredVisual = actor as PerformanceAuthoredVisual;
                    if (entry.authoredVisual != null)
                    {
                        entry.authoredAnimator = entry.authoredVisual.visual;
                        entry.reviewedChildren = (MonoBehaviour[])entry.authoredVisual.reviewedComponents.Clone();
                    }
                    if (entry.reviewedChildren.Length > 0)
                    {
                        int n = entry.reviewedChildren.Length;
                        entry.childEnabled = new bool[n]; entry.childActive = new bool[n]; entry.childParents = new Transform[n];
                        entry.hitboxes = new Collider[n]; entry.hitboxEnabled = new bool[n]; entry.hitboxTrigger = new bool[n];
                        entry.reviewedCameras = new Camera[n]; entry.reviewedListeners = new AudioListener[n]; entry.reviewedAnimations = new Animation[n]; entry.reviewedClips = new AnimationClip[n];
                        entry.cameraEnabled = new bool[n]; entry.listenerEnabled = new bool[n]; entry.animationEnabled = new bool[n]; entry.animationPlaying = new bool[n]; entry.animationAutomatic = new bool[n];
                        for (int j = 0; j < n; j++)
                        {
                            var child = entry.reviewedChildren[j];
                            entry.childEnabled[j] = child.enabled; entry.childActive[j] = child.gameObject.activeInHierarchy; entry.childParents[j] = child.transform.parent;
                            if (child is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)
                            {
                                var camera = child.GetComponent<Camera>(); var listener = child.GetComponent<AudioListener>(); var animation = child.GetComponent<Animation>();
                                entry.reviewedCameras[j] = camera; entry.reviewedListeners[j] = listener; entry.reviewedAnimations[j] = animation;
                                entry.cameraEnabled[j] = camera.enabled; entry.listenerEnabled[j] = listener.enabled; entry.animationEnabled[j] = animation.enabled;
                                entry.animationPlaying[j] = animation.isPlaying; entry.animationAutomatic[j] = animation.playAutomatically;
                                var clip = animation.clip; entry.reviewedClips[j] = clip == null ? null : clip;
                            }
                            if (child is DamageCollider damage)
                            {
                                var hitbox = damage.QaDamageCollider; entry.hitboxes[j] = hitbox;
                                entry.hitboxEnabled[j] = hitbox.enabled; entry.hitboxTrigger[j] = hitbox.isTrigger;
                            }
                        }
                    }
                    entry.visualAnimators = entry.root.GetComponentsInChildren<Animator>(true);
                    if (entry.visualAnimators.Length > 256) throw new ArgumentException("Animated workload exceeds its preflight bound.");
                    entry.visualEnabled = new bool[entry.visualAnimators.Length];
                    entry.visualActive = new bool[entry.visualAnimators.Length];
                    entry.visualControllers = new RuntimeAnimatorController[entry.visualAnimators.Length];
                    for (int j = 0; j < entry.visualAnimators.Length; j++)
                    {
                        entry.visualEnabled[j] = entry.visualAnimators[j].enabled;
                        entry.visualActive[j] = entry.visualAnimators[j].gameObject.activeInHierarchy;
                        entry.visualControllers[j] = entry.visualAnimators[j].runtimeAnimatorController;
                    }
                }
                if (entry.character != null)
                {
                    entry.network = entry.character.characterNetworkManager;
                    if (entry.network == null) throw new ArgumentException("Character binding requires its actual network state component.");
                    entry.spawned = entry.character.IsInitialized;
                    entry.maximumNetworkHealth = entry.network.maxHealth.Value;
                }
                if (actor is CucaBoss || actor is HealthManager)
                {
                    entry.health = entry.root.GetComponent<HealthManager>();
                    if (entry.health == null || !Finite(entry.health.currentHealth) || !Finite(entry.health.maxHealth) || entry.health.maxHealth <= 0)
                        throw new ArgumentException("Legacy hostile requires its actual finite HealthManager state.");
                    entry.healthEnabled = entry.health.enabled;
                    entry.maximumLegacyHealth = entry.health.maxHealth;
                }
                entry.active = entry.root.activeInHierarchy;
                entry.enabled = entry.behaviour != null && entry.behaviour.enabled;
                entry.aiDisabled = actor is AICharacterManager ai && ai.isDisabled;
                entry.living = Living(entry);
                entries[i] = entry;
                unsupported |= role == Role.Animal || role == Role.Companion || role == Role.WorldVisual;
            }
            var copiedEffects = new GameObject[effectRoots.Length];
            for (int i = 0; i < copiedEffects.Length; i++)
            {
                var effect = effectRoots[i];
                if (effect == null) throw new ArgumentException("Effect roots must be present.");
                for (int j = 0; j < i; j++) if (copiedEffects[j] == effect) throw new ArgumentException("Effect roots must be unique.");
                for (int j = 0; j < entries.Length; j++) if (entries[j].root == effect) throw new ArgumentException("Actor roots cannot be effect exemptions.");
                // Preflight discovery only: a transient exemption must not hide a living/controller workload.
                var contents = effect.GetComponentsInChildren<Component>(true);
                bool knownProjectile = false;
                for (int j = 0; j < contents.Length; j++)
                {
                    if (contents[j] == null) throw new ArgumentException("Effect roots cannot contain missing scripts.");
                    if (ActorComponent(contents[j])) throw new ArgumentException("Effect exemption contains an actor/controller component.");
                    knownProjectile |= contents[j] is ProjectileMovement;
                }
                if (!knownProjectile) throw new ArgumentException("This staged context supports only explicitly bound ProjectileMovement effects.");
                copiedEffects[i] = effect;
            }
            return new PerformanceActorRosterContext(entries, copiedEffects, creationPathsCovered, unsupported);
            }
            catch
            {
                foreach (var entry in entries) entry?.cucaVisualProof?.Dispose();
                throw;
            }
#else
            return null;
#endif
        }

        public int ReadLivingHostileCount()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed();
            Poll();
            if (violations != Violation.None) throw new InvalidOperationException("Frozen actor roster changed; lifecycle/living/reference violation remains latched.");
            int count = 0;
            for (int i = 0; i < entries.Length; i++) if (entries[i].role == Role.Hostile && Living(entries[i])) count++;
            return count;
#else
            return 0;
#endif
        }

        public void ObserveLifecycle(Component actor, PerformanceActorLifecycle change)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            ThrowIfDisposed();
            if (actor == null || change < PerformanceActorLifecycle.Enabled || change > PerformanceActorLifecycle.PhaseChanged)
                throw new ArgumentException("A present lifecycle subject and a defined typed transition are required.");
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                bool matches = actor == entry.actor || actor == entry.health || actor == entry.network;
                if (!matches) for (int j = 0; j < entry.npcControllers.Length; j++) if (actor == entry.npcControllers[j]) { matches = true; break; }
                if (!matches) for (int j = 0; j < entry.visualAnimators.Length; j++) if (actor == entry.visualAnimators[j]) { matches = true; break; }
                if (!matches) for (int j = 0; j < entry.reviewedChildren.Length; j++) if (ReferenceEquals(actor, entry.reviewedChildren[j])) { matches = true; break; }
                if (!matches && entry.loadedVisualProof != null) matches = ReferenceEquals(actor, entry.loadedVisualProof.Owner);
                if (!matches && entry.sceneVisualProof != null) matches = entry.sceneVisualProof.Owns(actor);
                if (!matches && entry.cucaVisualProof != null) matches = entry.cucaVisualProof.Owns(actor);
                if (!matches) continue;
                if (change == PerformanceActorLifecycle.HealthChanged)
                {
                    if (Living(entry) != entry.living ||
                        (entry.health != null && (!Finite(entry.health.currentHealth) || entry.health.maxHealth != entry.maximumLegacyHealth)) ||
                        (entry.network != null && entry.network.maxHealth.Value != entry.maximumNetworkHealth))
                        violations |= Violation.LivingState;
                }
                else violations |= change == PerformanceActorLifecycle.PhaseChanged ? Violation.Phase : Violation.Lifetime;
                return;
            }
            // Only the exact preflight-reviewed root is exempt; new actor controllers are never exempt.
            if (!ActorComponent(actor))
            {
                var root = actor.gameObject;
                for (int i = 0; i < effects.Length; i++) if (effects[i] != null && root == effects[i] && actor is ProjectileMovement) return;
            }
            violations |= Violation.UnknownActor;
#endif
        }

        public void Validate(CaptureResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (result == null) throw new ArgumentNullException(nameof(result));
            ThrowIfDisposed();
            Poll();
            // Source events alone do not establish all creation paths or a complete accepted route census.
            result.populationVerified = false;
            result.comparisonValid = false;
            AddReason(result, "Actor roster certification unavailable: complete creation-path provenance and accepted route census remain unproven.");
            if (!sourceScopeWasAttached) AddReason(result, "An explicit source lifecycle scope was not attached to this context.");
            if (!declaredCreationClosure) AddReason(result, "Actor creation-path closure was not declared.");
            if (unsupportedRoles) AddReason(result, "Animated animal, world visual or companion creation/component-transition closure remains unproven.");
            if (violations != Violation.None)
            {
                result.conditionsChanged = true;
                string error = "Frozen actor roster lifecycle/living/reference/phase boundary changed (latched). [" + violations + "]";
                if (!result.errors.Contains(error)) result.errors.Add(error);
            }
#endif
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            sourceScope?.Dispose();
            foreach (var entry in entries) entry?.cucaVisualProof?.Dispose();
            Array.Clear(entries, 0, entries.Length);
            Array.Clear(effects, 0, effects.Length);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(PerformanceActorRosterContext)); }
        void Poll()
        {
            if (SceneManager.GetActiveScene() != activeScene) violations |= Violation.Scene;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.actor == null || entry.root == null || entry.behaviour == null || entry.root.scene != entry.scene)
                { violations |= Violation.References; continue; }
                if (entry.root.activeInHierarchy != entry.active || entry.behaviour.enabled != entry.enabled)
                    violations |= Violation.Lifetime;
                if (entry.loadedVisualProof != null && !entry.loadedVisualProof.Matches()) violations |= Violation.References;
                if (entry.sceneVisualProof != null && !entry.sceneVisualProof.Matches()) violations |= Violation.References;
                if (entry.cucaVisualProof != null && !entry.cucaVisualProof.Matches()) violations |= Violation.References;
                if (entry.authoredVisual != null && entry.authoredVisual.visual != entry.authoredAnimator)
                    violations |= Violation.References;
                if (entry.authoredVisual != null || entry.sceneVisualProof != null)
                {
                    var declared = entry.authoredVisual != null ? entry.authoredVisual.reviewedComponents : entry.reviewedChildren;
                    if (declared == null || declared.Length != entry.reviewedChildren.Length) violations |= Violation.References;
                    for (int j = 0; j < entry.reviewedChildren.Length; j++)
                    {
                        var child = entry.reviewedChildren[j];
                        if (declared == null || j >= declared.Length || declared[j] != child) violations |= Violation.References;
                        if (child == null || child.transform.parent != entry.childParents[j] || !child.transform.IsChildOf(entry.root.transform))
                        { violations |= Violation.References; continue; }
                        if (child.enabled != entry.childEnabled[j] || child.gameObject.activeInHierarchy != entry.childActive[j]) violations |= Violation.Lifetime;
                        if (child is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)
                        {
                            var camera = entry.reviewedCameras[j]; var listener = entry.reviewedListeners[j]; var animation = entry.reviewedAnimations[j]; var clip = entry.reviewedClips[j];
                            if (camera == null || listener == null || animation == null || child.GetComponent<Camera>() != camera || child.GetComponent<AudioListener>() != listener || child.GetComponent<Animation>() != animation ||
                                (!ReferenceEquals(clip, null) && clip == null) || animation.clip != clip) violations |= Violation.References;
                            else if (camera.enabled != entry.cameraEnabled[j] || listener.enabled != entry.listenerEnabled[j] || animation.enabled != entry.animationEnabled[j] || animation.isPlaying != entry.animationPlaying[j] || animation.playAutomatically != entry.animationAutomatic[j]) violations |= Violation.Lifetime;
                        }
                        if (child is DamageCollider damage)
                        {
                            var hitbox = entry.hitboxes[j];
                            if (hitbox == null || damage.QaDamageCollider != hitbox) violations |= Violation.References;
                            else if (hitbox.enabled != entry.hitboxEnabled[j] || hitbox.isTrigger != entry.hitboxTrigger[j]) violations |= Violation.Lifetime;
                        }
                    }
                }
                for (int j = 0; j < entry.npcControllers.Length; j++)
                {
                    var controller = entry.npcControllers[j];
                    if (controller == null) violations |= Violation.References;
                    else if (controller.enabled != entry.npcEnabled[j]) violations |= Violation.Lifetime;
                }
                for (int j = 0; j < entry.visualAnimators.Length; j++)
                {
                    var animator = entry.visualAnimators[j];
                    if (animator == null || !(animator.transform == entry.root.transform || animator.transform.IsChildOf(entry.root.transform)))
                        violations |= Violation.References;
                    else if (animator.enabled != entry.visualEnabled[j] || animator.gameObject.activeInHierarchy != entry.visualActive[j] ||
                        animator.runtimeAnimatorController != entry.visualControllers[j])
                        violations |= Violation.Lifetime;
                }
                if (entry.character != null)
                {
                    if (entry.network == null || entry.character.characterNetworkManager != entry.network || entry.character.IsInitialized != entry.spawned)
                        violations |= Violation.References;
                    else if (entry.network.maxHealth.Value != entry.maximumNetworkHealth) violations |= Violation.LivingState;
                }
                if (entry.health != null)
                {
                    if (entry.health.enabled != entry.healthEnabled) violations |= Violation.Lifetime;
                    if (!Finite(entry.health.currentHealth) || entry.health.maxHealth != entry.maximumLegacyHealth) violations |= Violation.LivingState;
                }
                else if (entry.actor is CucaBoss || entry.actor is HealthManager) violations |= Violation.References;
                if (entry.actor is AICharacterManager ai && ai.isDisabled != entry.aiDisabled) violations |= Violation.Lifetime;
                if (Living(entry) != entry.living) violations |= Violation.LivingState;
            }
        }
        static bool Living(Entry entry)
        {
            if (entry.actor == null || entry.root == null || !entry.root.activeInHierarchy || entry.behaviour == null || !entry.behaviour.enabled) return false;
            if (entry.character != null)
                return entry.character.IsInitialized && entry.network != null && entry.network.isActive.Value &&
                    !entry.character.isDead.Value && entry.network.currentHealth.Value > 0;
            if (entry.health != null) return entry.health.isActiveAndEnabled && Finite(entry.health.currentHealth) && entry.health.currentHealth > 30;
            return true; // Healthless ambient/companion entries are workload members, never hostile counts.
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Role ParseRole(string role)
        {
            switch (role)
            {
                case "local-player": return Role.Player;
                case "hostile": return Role.Hostile;
                case "ambient-npc": return Role.Npc;
                case "ambient-animal": return Role.Animal;
                case "companion": return Role.Companion;
                case "world-visual": return Role.WorldVisual;
                default: throw new ArgumentException("Unknown actor role.");
            }
        }
        static bool Supports(Component actor, Role role)
        {
            switch (role)
            {
                case Role.Player: return actor is PlayerManager;
                case Role.Hostile: return actor is AICharacterManager || actor is CucaBoss || actor is HealthManager;
                case Role.Npc: return AmbientNpcController(actor);
                case Role.Animal: return actor is Animator || actor is AraraMovement;
                case Role.WorldVisual: return actor is Animator || (actor is PerformanceAuthoredVisual visual && visual.IsValidSource()) || (actor is PerformanceSceneAnimatorSource sceneVisual && sceneVisual.IsValidSource()) || (actor is PerformanceCucaSceneSource cucaVisual && cucaVisual.IsValidSource());
                case Role.Companion: return actor is WolfAI || actor is PetAttackController;
                default: return false;
            }
        }
        internal static bool AmbientNpcController(Component actor) => actor is NPCDialogue || actor is NPCMovement || actor is RandomNPCMovement || actor is NPCATIVAR;
        static bool ActorComponent(Component actor) => actor is CharacterManager || actor is CucaBoss || actor is HealthManager ||
            actor is NPCDialogue || actor is BossAISystem || actor is BossControl || actor is WolfAI || actor is PetAttackController ||
            actor is RandomNPCMovement || actor is NPCMovement || actor is NPCStart || actor is NPCATIVAR || actor is NPC || actor is AraraMovement || actor is PerformanceAuthoredVisual || actor is PerformanceSceneAnimatorSource || actor is PerformanceCucaSceneSource;
        static void AddReason(CaptureResult result, string reason)
        { if (!result.validationUnavailableReasons.Contains(reason)) result.validationUnavailableReasons.Add(reason); }
#endif
    }
}
