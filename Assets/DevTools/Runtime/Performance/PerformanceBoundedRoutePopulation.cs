using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Explicit, outside-sampling reconciliation. This verdict is not a timing/rendering certificate.
    public static class PerformanceBoundedRoutePopulation
    {
        [Serializable] public sealed class ActorGroup
        {
            public string role, primaryType;
            public int count;
            public bool? active, enabled, spawned;
        }
        [Serializable] public sealed class PhaseContract
        {
            public string id, scene;
            public double fromTime, untilTime, boundaryTolerance;
            public ActorGroup[] composition;
        }
        [Serializable] public sealed class SpawnExpectation
        {
            // Resolved from exact reviewed producer/prefab references, never a name-only lookup.
            public int spawnerId, prefabId, count;
            public string primaryType;
        }
        [Serializable] public sealed class TransitionContract
        {
            public PhaseContract phase;
            public ActorGroup[] resultingComposition;
            public SpawnExpectation[] creations;
        }
        [Serializable] public sealed class PhaseObservation
        {
            public PerformanceActorCensus.Report before, after;
            public PerformanceActorLifecycleScope.HistoryReport lifecycle;
            public PerformanceAiCreationHistory.Report aiBefore, aiAfter;
            public int? contextViolationMask;
        }
        public sealed class DomainVerdict
        {
            public readonly string scope = "bounded-typed-actor-population";
            public readonly bool accepted;
            public readonly string[] reasons;
            internal DomainVerdict(string reason) { accepted = reason == null; reasons = reason == null ? Array.Empty<string>() : new[] { reason }; }
        }
        public static DomainVerdict EvaluatePhase(PhaseContract expected, PhaseObservation observed) => Judge(() =>
        {
            Shared(expected, observed, false);
            Composition(expected.composition, observed.after);
            Continuity(observed.before, observed.after, false);
            Ensure(observed.aiBefore.attempts.SequenceEqual(observed.aiAfter.attempts) && observed.aiBefore.events.SequenceEqual(observed.aiAfter.events), "stable-history-changed");
            JournalEvents(observed, new Dictionary<int, PerformanceAiCreationHistory.Attempt>());
        });
        public static DomainVerdict EvaluateTransition(TransitionContract expected, PhaseObservation observed) => Judge(() =>
        {
            Ensure(expected != null, "transition-contract-missing");
            Shared(expected.phase, observed, true);
            Composition(expected.resultingComposition, observed.after);
            Continuity(observed.before, observed.after, true);
            var old = observed.aiBefore.attempts.ToDictionary(a => a.token);
            var after = observed.aiAfter.attempts.ToDictionary(a => a.token);
            Ensure(old.All(pair => after.TryGetValue(pair.Key, out var row) && row.Equals(pair.Value)), "prior-creation-history-rewritten");
            Ensure(observed.aiAfter.events.Take(observed.aiBefore.events.Length).SequenceEqual(observed.aiBefore.events), "prior-creation-events-rewritten");
            var added = after.Values.Where(a => !old.ContainsKey(a.token)).ToArray();
            Ensure(expected.creations != null && expected.creations.Length > 0 && expected.creations.Length <= 1024 && added.Length == expected.creations.Length, "creation-composition-count");
            Ensure(expected.creations.All(e => e != null && e.spawnerId != 0 && e.prefabId != 0 && e.count == 1 && !string.IsNullOrWhiteSpace(e.primaryType)) &&
                expected.creations.Select(e => e.spawnerId).Distinct().Count() == expected.creations.Length, "ambiguous-expected-creator");
            var newActors = observed.after.actors.Where(a => !observed.before.actors.Any(b => b.objectId == a.objectId)).ToDictionary(a => a.objectId);
            Ensure(added.Length == newActors.Count && added.All(a => !a.despawnRequested && !a.despawned && newActors.ContainsKey(a.createdId)), "unmatched-new-actor-or-transient");
            foreach (var row in added)
            {
                var matches = expected.creations.Where(e => e.spawnerId == row.spawnerId && e.prefabId == row.prefabId && e.primaryType == newActors[row.createdId].primaryType).ToArray();
                Ensure(matches.Length == 1, "wrong-production-creator-or-prefab");
                var events = observed.aiAfter.events.Where(e => e.token == row.token).ToArray();
                Ensure(events[0].time >= observed.lifecycle.startedTime && events[events.Length - 1].time <= observed.lifecycle.endedTime, "creation-outside-transition-window");
            }
            JournalEvents(observed, added.ToDictionary(a => a.createdId));
        });
        static DomainVerdict Judge(Action body)
        {
            try { body(); return new DomainVerdict(null); }
            catch (Exception error) { return new DomainVerdict(error.GetType().Name + ": " + error.Message); }
        }
        static void Shared(PhaseContract contract, PhaseObservation observation, bool transition)
        {
            Ensure(contract != null && !string.IsNullOrWhiteSpace(contract.id) && !string.IsNullOrWhiteSpace(contract.scene) &&
                Finite(contract.fromTime) && Finite(contract.untilTime) && contract.fromTime >= 0 && contract.untilTime > contract.fromTime &&
                Finite(contract.boundaryTolerance) && contract.boundaryTolerance > 0 && contract.boundaryTolerance <= .5, "invalid-phase-contract");
            Ensure(observation?.before != null && observation.after != null && observation.lifecycle != null && observation.contextViolationMask.HasValue, "phase-evidence-missing");
            Ensure(observation.contextViolationMask >= 0 && (transition ? (observation.contextViolationMask.Value & ~32) == 0 : observation.contextViolationMask == 0), "context-reference-lifetime-or-phase-violation");
            Ensure(observation.before.activeScene == contract.scene && observation.after.activeScene == contract.scene, "active-scene-mismatch");
            var before = Census(observation.before); var after = Census(observation.after);
            Composition(contract.composition, observation.before);
            var journal = observation.lifecycle;
            Ensure(!journal.sourceFaulted && !journal.overflow && journal.dropped == 0 && journal.events != null && journal.events.Length <= 16384 &&
                journal.notifications == journal.events.Length && journal.initiallyBoundSubjects != null && journal.initiallyBoundSubjects.Length > 0, "source-journal-missing-faulted-or-overflow");
            Ensure(journal.startedFrame == observation.before.frame && journal.endedFrame == observation.after.frame && journal.endedFrame >= journal.startedFrame &&
                Finite(journal.startedTime) && Finite(journal.endedTime) && journal.endedTime >= journal.startedTime &&
                Math.Abs(journal.startedTime - contract.fromTime) <= contract.boundaryTolerance && Math.Abs(journal.endedTime - contract.untilTime) <= contract.boundaryTolerance, "source-observation-window-mismatch");
            Ensure(journal.initiallyBoundSubjects.All(s => s.componentId != 0 && s.rootId != 0 && before.ContainsKey(s.ownerRootId) && !string.IsNullOrWhiteSpace(s.componentType)) &&
                journal.initiallyBoundSubjects.Select(s => s.componentId).Distinct().Count() == journal.initiallyBoundSubjects.Length &&
                before.Keys.All(id => journal.initiallyBoundSubjects.Any(s => s.ownerRootId == id)), "source-subject-owner-coverage");
            int frame = journal.startedFrame; double time = journal.startedTime;
            foreach (var row in journal.events)
            {
                Ensure(row.componentId != 0 && row.rootId != 0 && !string.IsNullOrWhiteSpace(row.componentType) &&
                    row.change >= PerformanceActorLifecycle.Enabled && row.change <= PerformanceActorLifecycle.PhaseChanged &&
                    row.frame >= frame && row.frame <= journal.endedFrame && Finite(row.time) && row.time >= time && row.time <= journal.endedTime, "invalid-or-unordered-source-event");
                frame = row.frame; time = row.time;
            }
            AiProof(observation.aiBefore, observation.before, before);
            AiProof(observation.aiAfter, observation.after, after);
        }
        static Dictionary<int, PerformanceActorCensus.Actor> Census(PerformanceActorCensus.Report census)
        {
            Ensure(census.actors != null && census.actors.Length > 0 && census.actors.Length <= 4096 && census.frame >= 0 && census.spawnerAssociations != null, "typed-census-missing");
            Ensure(census.actors.All(a => a != null && a.objectId != 0 && a.supportedBinding && !string.IsNullOrWhiteSpace(a.primaryType) &&
                !string.IsNullOrWhiteSpace(a.role) && a.components != null && a.components.Contains(a.primaryType) && a.components.All(c => !string.IsNullOrWhiteSpace(c)) &&
                a.components.Distinct().Count() == a.components.Length && a.dead != true &&
                (!a.currentHealth.HasValue || (a.maximumHealth.HasValue && Finite(a.currentHealth.Value) && Finite(a.maximumHealth.Value) && a.currentHealth > 0 && a.maximumHealth > 0 && a.currentHealth <= a.maximumHealth))), "unsupported-duplicate-component-or-nonliving-actor");
            Ensure(census.actors.Select(a => a.objectId).Distinct().Count() == census.actors.Length, "duplicate-actor-root");
            foreach (var actor in census.actors) Ensure(SupportedRole(ResolveType(actor.primaryType)) == actor.role, "unsupported-actor-role-or-type");
            return census.actors.ToDictionary(a => a.objectId);
        }
        static void Composition(ActorGroup[] expected, PerformanceActorCensus.Report census)
        {
            ValidateGroups(expected);
            Ensure(expected.Sum(g => g.count) == census.actors.Length &&
                expected.All(g => census.actors.Count(a => a.role == g.role && a.primaryType == g.primaryType && a.active == g.active && a.enabled == g.enabled && a.spawned == g.spawned) == g.count), "actor-composition-mismatch");
        }
        internal static void ValidateGroups(ActorGroup[] expected)
        {
            var roles = new[] { "local-player", "hostile", "ambient-npc", "ambient-animal", "world-visual", "companion" };
            Ensure(expected != null && expected.Length > 0 && expected.Length <= 512 && expected.All(g => g != null && roles.Contains(g.role) && !string.IsNullOrWhiteSpace(g.primaryType) && g.count > 0 && g.count <= 4096 &&
                g.active.HasValue && g.enabled.HasValue && g.spawned.HasValue), "invalid-expected-composition");
            foreach (var group in expected) Ensure(SupportedRole(ResolveType(group.primaryType)) == group.role, "unsupported-expected-role-or-type");
            Ensure(expected.Select(g => g.role + "|" + g.primaryType + "|" + g.active + "|" + g.enabled + "|" + g.spawned).Distinct().Count() == expected.Length && expected.Sum(g => g.count) <= 4096, "invalid-expected-composition");
        }
        static void Continuity(PerformanceActorCensus.Report before, PerformanceActorCensus.Report after, bool additions)
        {
            Ensure(additions || before.actors.Length == after.actors.Length, "phase-population-count-changed");
            foreach (var old in before.actors)
            {
                var current = after.actors.SingleOrDefault(a => a.objectId == old.objectId);
                Ensure(current != null && current.role == old.role && current.primaryType == old.primaryType && current.scene == old.scene && current.sceneHandle == old.sceneHandle &&
                    current.active == old.active && current.enabled == old.enabled && current.spawned == old.spawned && current.networkObjectId == old.networkObjectId &&
                    current.dead == old.dead && current.maximumHealth == old.maximumHealth && current.components.OrderBy(c => c).SequenceEqual(old.components.OrderBy(c => c)), "actor-replaced-missing-or-state-changed");
            }
        }
        internal static void ValidateObservedAiHistory(PerformanceAiCreationHistory.Report history, PerformanceActorCensus.Report census)
        {
            Ensure(census != null, "typed-census-missing");
            AiProof(history, census, Census(census));
        }
        static void AiProof(PerformanceAiCreationHistory.Report history, PerformanceActorCensus.Report census, Dictionary<int, PerformanceActorCensus.Actor> actors)
        {
            Ensure(history != null && !history.sourceFaulted && !history.overflow && history.dropped == 0 && history.failedAttempts == 0 && history.startedFrame >= 0 && history.startedFrame <= census.frame &&
                history.preexistingAiIds != null && history.preexistingAiIds.Length == 0 && history.unmatchedAiIds != null && history.unmatchedAiIds.Length == 0 &&
                history.orphanLiveAttempts != null && history.orphanLiveAttempts.Length == 0 && history.attempts != null && history.attempts.Length <= 1024 && history.events != null && history.events.Length <= 8192, "ai-creation-evidence-missing-or-faulted");
            Ensure(history.attempts.Select(a => a.token).Distinct().Count() == history.attempts.Length, "duplicate-creation-token");
            var tokens = new HashSet<long>(history.attempts.Select(a => a.token));
            int frame = history.startedFrame; double time = 0;
            foreach (var row in history.events)
            {
                Ensure(tokens.Contains(row.token) && row.frame >= frame && row.frame <= census.frame && Finite(row.time) && row.time >= time, "ai-event-order-or-token");
                frame = row.frame; time = row.time;
            }
            foreach (var row in history.attempts)
            {
                Ensure(row.token != 0 && row.spawnerId != 0 && row.prefabId != 0 && row.createdId != 0 && row.spawned && row.registered && row.finished && !row.invalid && row.despawnRequested == row.despawned, "failed-or-unmatched-creation");
                var stages = history.events.Where(e => e.token == row.token).Select(e => (int)e.stage).ToArray();
                int last = row.despawned ? 6 : 4;
                Ensure(stages.SequenceEqual(Enumerable.Range(0, last + 1)) && (int)row.lastStage == last, "incomplete-creation-stages");
            }
            var live = history.attempts.Where(a => !a.despawned).ToArray();
            Ensure(live.Select(a => a.createdId).Distinct().Count() == live.Length && live.Select(a => a.spawnerId).Distinct().Count() == live.Length, "ambiguous-live-creator");
            var typed = actors.Values.Where(IsAi).ToArray();
            Ensure(history.currentAiCount == typed.Length && history.matchedAiCount == typed.Length && live.Length == typed.Length, "typed-ai-count-mismatch");
            foreach (var row in live)
            {
                Ensure(actors.TryGetValue(row.createdId, out var actor) && IsAi(actor) && actor.role == "hostile" && actor.spawned && actor.networkObjectId == row.networkObjectId && actor.sceneHandle == row.sceneHandle, "ai-census-identity-mismatch");
                Ensure(census.spawnerAssociations.Count(s => s.linked && s.uniqueOwner && s.spawned && s.supportedActor && s.spawnerId == row.spawnerId && s.prefabId == row.prefabId &&
                    s.createdObjectId == row.createdId && s.actorObjectId == row.createdId && s.networkObjectId == row.networkObjectId) == 1, "ai-creator-census-link-mismatch");
            }
        }
        static void JournalEvents(PhaseObservation observation, Dictionary<int, PerformanceAiCreationHistory.Attempt> added)
        {
            var bindings = observation.lifecycle.initiallyBoundSubjects.ToDictionary(s => s.componentId);
            var unknownRoots = new HashSet<int>();
            foreach (var row in observation.lifecycle.events)
            {
                if (row.initiallyBound)
                {
                    Ensure(bindings.TryGetValue(row.componentId, out var binding) && binding.rootId == row.rootId && binding.ownerRootId == row.ownerRootId && binding.componentType == row.componentType, "source-binding-identity-mismatch");
                    Ensure(row.change == PerformanceActorLifecycle.HealthChanged, "unexpected-bound-actor-lifecycle");
                }
                else
                {
                    bool found = added.TryGetValue(row.rootId, out var creation);
                    Ensure(!bindings.ContainsKey(row.componentId) && row.ownerRootId == 0 && found, "unknown-source-without-production-creation");
                    var actor = observation.after.actors.Single(a => a.objectId == row.rootId);
                    Ensure(row.componentType == actor.primaryType && (row.change == PerformanceActorLifecycle.Enabled || row.change == PerformanceActorLifecycle.NetworkSpawned || row.change == PerformanceActorLifecycle.HealthChanged), "unexpected-generated-actor-component-or-lifecycle");
                    var birth = observation.aiAfter.events.Where(e => e.token == creation.token).ToArray();
                    Ensure(row.time >= birth[0].time && row.time <= observation.lifecycle.endedTime && row.frame >= birth[0].frame, "source-before-production-request");
                    if (row.change != PerformanceActorLifecycle.HealthChanged) Ensure(row.time <= birth[4].time && row.frame <= birth[4].frame, "source-birth-outside-production-attempt");
                    unknownRoots.Add(row.rootId);
                }
            }
            Ensure(unknownRoots.SetEquals(added.Keys), "generated-source-roots-not-observed");
            foreach (var root in added.Keys)
            {
                var rows = observation.lifecycle.events.Where(e => !e.initiallyBound && e.rootId == root).ToArray();
                Ensure(rows.Select(e => e.componentId).Distinct().Count() == 1 && rows.Any(e => e.change == PerformanceActorLifecycle.Enabled) && rows.Any(e => e.change == PerformanceActorLifecycle.NetworkSpawned), "generated-source-birth-incomplete-or-replaced");
            }
        }
        static Type ResolveType(string name)
        {
            var type = name == typeof(UnityEngine.Animator).FullName ? typeof(UnityEngine.Animator) : typeof(AICharacterManager).Assembly.GetType(name, false);
            Ensure(type != null && typeof(UnityEngine.Behaviour).IsAssignableFrom(type), "unresolved-actor-type");
            return type;
        }
        static string SupportedRole(Type type)
        {
            if (typeof(PlayerManager).IsAssignableFrom(type)) return "local-player";
            if (typeof(AICharacterManager).IsAssignableFrom(type) || typeof(CucaBoss).IsAssignableFrom(type) || typeof(HealthManager).IsAssignableFrom(type)) return "hostile";
            if (typeof(NPCDialogue).IsAssignableFrom(type) || typeof(NPCMovement).IsAssignableFrom(type) || typeof(RandomNPCMovement).IsAssignableFrom(type) || typeof(NPCATIVAR).IsAssignableFrom(type)) return "ambient-npc";
            if (typeof(AraraMovement).IsAssignableFrom(type)) return "ambient-animal";
            if (typeof(PerformanceAuthoredVisual).IsAssignableFrom(type) || typeof(PerformanceSceneAnimatorSource).IsAssignableFrom(type) || type == typeof(PerformanceCucaSceneSource) || type == typeof(UnityEngine.Animator)) return "world-visual";
            if (typeof(WolfAI).IsAssignableFrom(type) || typeof(PetAttackController).IsAssignableFrom(type)) return "companion";
            throw new ArgumentException("unsupported-actor-type");
        }
        static bool IsAi(PerformanceActorCensus.Actor actor) => typeof(AICharacterManager).IsAssignableFrom(ResolveType(actor.primaryType));
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static void Ensure(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }
    }
#endif
}
