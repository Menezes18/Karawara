using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Explicit bounded QA observer. Numeric identities only: never retains destroyed actors/prefabs.
    public sealed class PerformanceAiCreationHistory : IDisposable
    {
        public enum Stage { Requested, Instantiated, NetworkSpawned, Registered, Finished, DespawnRequested, Despawned }
        [Serializable] public struct Attempt
        {
            public long token;
            public int spawnerId, prefabId, createdId, sceneHandle;
            public ulong networkObjectId;
            public Stage lastStage;
            public bool spawned, registered, finished, invalid, despawnRequested, despawned;
        }
        [Serializable] public struct Event
        {
            public long token;
            public int frame;
            public double time;
            public Stage stage;
        }
        [Serializable] public sealed class Report
        {
            public string boundary = "Observed AICharacterSpawner/BossSpawnerInput/WorldAIManager paths only; no global creation/native/rendering/timing certification", coverageUnavailableReason;
            public bool sourceFaulted, overflow, currentAiHistoryMatched, creationPathsVerified = false;
            public int failedAttempts, dropped, startedFrame;
            public int? currentAiCount, matchedAiCount;
            public int[] preexistingAiIds, unmatchedAiIds;
            public long[] orphanLiveAttempts;
            public Attempt[] attempts;
            public Event[] events;
            public string[] stageNames;
        }
        const int AttemptCapacity = 1024, EventCapacity = 8192;
        static PerformanceAiCreationHistory active;
        static long sequence;
        readonly Attempt[] attempts = new Attempt[AttemptCapacity];
        readonly Event[] events = new Event[EventCapacity];
        readonly long baseToken;
        readonly int startedFrame;
        int[] preexisting;
        int length, eventLength, dropped;
        bool disposed, overflow, faulted;
        PerformanceAiCreationHistory()
        {
            baseToken = checked(++sequence * AttemptCapacity); startedFrame = Time.frameCount;
            preexisting = UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a => a.gameObject.scene.IsValid()).Select(a => a.gameObject.GetInstanceID()).ToArray();
        }
        public static PerformanceAiCreationHistory Begin(string[] args)
        {
            int count = 0; if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") count++;
            if (count == 0) return null;
            if (count != 1 || !Application.isPlaying) throw new ArgumentException("One explicit roster flag and live runtime required for AI history.");
            if (active != null) throw new InvalidOperationException("Another AI history observer owns callbacks.");
            return active = new PerformanceAiCreationHistory();
        }
        static GameObject SourcePrefab(Component source)
        {
            if (source is AICharacterSpawner ai) return ai.QaPrefab;
            if (source is BossSpawnerInput boss) return boss.bossPrefab;
            throw new ArgumentException("Explicit production AI or boss spawner required.");
        }
        internal static long StartAttempt(Component source)
        {
            using (PerformanceProfilerAttribution.HistoryMarker.Auto())
            {
            var owner = active; if (owner == null) return 0;
            try
            {
                if (source == null) { owner.faulted = true; return 0; }
                if (owner.length == AttemptCapacity) { owner.overflow = true; owner.dropped++; return 0; }
                var prefab = SourcePrefab(source);
                int index = owner.length++;
                owner.attempts[index] = new Attempt { token = owner.baseToken + index + 1, spawnerId = source.GetInstanceID(),
                    prefabId = prefab == null ? 0 : prefab.GetInstanceID(), lastStage = Stage.Requested };
                owner.Record(owner.attempts[index].token, Stage.Requested); return owner.attempts[index].token;
            }
            catch { owner.faulted = true; return 0; }
        
            }
        }
        int Index(long token)
        {
            long index = token - baseToken - 1;
            if (index < 0 || index >= length || attempts[(int)index].token != token) { faulted = true; return -1; }
            return (int)index;
        }
        void Record(long token, Stage stage)
        {
            if (eventLength == EventCapacity) { overflow = true; dropped++; return; }
            events[eventLength++] = new Event { token = token, stage = stage, frame = Time.frameCount, time = Time.realtimeSinceStartupAsDouble };
        }
        internal static void Observe(long token, Component source, GameObject created, Stage stage)
        {
            using (PerformanceProfilerAttribution.HistoryMarker.Auto())
            {
            var owner = active; if (owner == null || token == 0) return;
            try
            {
                int index = owner.Index(token); if (index < 0) return;
                var entry = owner.attempts[index];
                var prefab = source == null ? null : SourcePrefab(source);
                if (source == null || source.GetInstanceID() != entry.spawnerId || prefab == null || prefab.GetInstanceID() != entry.prefabId ||
                    created == null || !created.scene.IsValid() || stage != entry.lastStage + 1 || stage < Stage.Instantiated || stage > Stage.Registered)
                    entry.invalid = true;
                if (created != null)
                {
                    int id = created.GetInstanceID();
                    if (entry.createdId != 0 && entry.createdId != id) entry.invalid = true;
                    entry.createdId = id; entry.sceneHandle = created.scene.handle;
                    var network = created.GetComponent<NetworkObject>();
                    if (stage == Stage.NetworkSpawned)
                    {
                        entry.spawned = network != null && network.IsSpawned;
                        if (entry.spawned) entry.networkObjectId = network.NetworkObjectId; else entry.invalid = true;
                    }
                    if (stage == Stage.Registered)
                    {
                        var actor = created.GetComponent<AICharacterManager>();
                        entry.registered = actor != null && actor.IsSpawned && WorldAIManager.instance != null && WorldAIManager.instance.QaIsRegistered(actor);
                        if (!entry.registered || network == null || !network.IsSpawned || network.NetworkObjectId != entry.networkObjectId) entry.invalid = true;
                    }
                }
                entry.lastStage = stage; owner.attempts[index] = entry; owner.Record(token, stage);
            }
            catch { owner.faulted = true; }
        
            }
        }
        internal static void FinishAttempt(long token)
        {
            using (PerformanceProfilerAttribution.HistoryMarker.Auto())
            {
            var owner = active; if (owner == null || token == 0) return;
            try
            {
                int index = owner.Index(token); if (index < 0) return;
                var entry = owner.attempts[index];
                if (entry.finished) entry.invalid = true;
                entry.finished = true; entry.lastStage = Stage.Finished; owner.attempts[index] = entry; owner.Record(token, Stage.Finished);
            }
            catch { owner.faulted = true; }
        
            }
        }
        internal static long StartDespawn(AICharacterManager actor)
        {
            using (PerformanceProfilerAttribution.HistoryMarker.Auto())
            {
            var owner = active; if (owner == null) return 0;
            try
            {
                if (actor != null && actor.IsSpawned)
                    for (int i = owner.length - 1; i >= 0; i--)
                    {
                        var entry = owner.attempts[i];
                        if (entry.createdId != actor.gameObject.GetInstanceID() || entry.networkObjectId != actor.NetworkObjectId || !entry.registered || entry.despawned) continue;
                        if (entry.despawnRequested) entry.invalid = true;
                        entry.despawnRequested = true; entry.lastStage = Stage.DespawnRequested; owner.attempts[i] = entry; owner.Record(entry.token, Stage.DespawnRequested); return entry.token;
                    }
                owner.faulted = true; return 0;
            }
            catch { owner.faulted = true; return 0; }
        
            }
        }
        internal static void FinishDespawn(long token, bool succeeded)
        {
            using (PerformanceProfilerAttribution.HistoryMarker.Auto())
            {
            var owner = active; if (owner == null || token == 0) return;
            try
            {
                int index = owner.Index(token); if (index < 0) return;
                var entry = owner.attempts[index];
                if (!entry.despawnRequested || entry.despawned || !succeeded) entry.invalid = true;
                entry.despawned = succeeded; entry.lastStage = Stage.Despawned; owner.attempts[index] = entry; owner.Record(token, Stage.Despawned);
            }
            catch { owner.faulted = true; }
        
            }
        }
        public Report Capture(PerformanceActorCensus.Snapshot census = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PerformanceAiCreationHistory));
            var rows = new Attempt[length]; Array.Copy(attempts, rows, length);
            var messages = new Event[eventLength]; Array.Copy(events, messages, eventLength);
            var report = new Report { startedFrame = startedFrame, sourceFaulted = faulted, overflow = overflow, dropped = dropped,
                preexistingAiIds = (int[])preexisting.Clone(), attempts = rows, events = messages, stageNames = Enum.GetNames(typeof(Stage)),
                failedAttempts = rows.Count(a => !a.finished || !a.spawned || !a.registered || a.invalid), coverageUnavailableReason = "No current census supplied." };
            if (census?.report == null) return report;
            var current = UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(a => a.gameObject.scene.IsValid()).ToArray();
            var unmatched = current.Where(actor => !actor.IsSpawned ||
                !census.report.actors.Any(a => a.objectId == actor.gameObject.GetInstanceID() && a.supportedBinding && a.spawned && a.networkObjectId == actor.NetworkObjectId) ||
                rows.Count(a => a.createdId == actor.gameObject.GetInstanceID() && a.networkObjectId == actor.NetworkObjectId && a.finished && a.registered && a.spawned && !a.invalid && !a.despawnRequested && !a.despawned &&
                    census.report.spawnerAssociations.Any(s => s.linked && s.spawnerId == a.spawnerId && s.prefabId == a.prefabId && s.createdObjectId == a.createdId && s.networkObjectId == a.networkObjectId)) != 1)
                .Select(a => a.gameObject.GetInstanceID()).ToArray();
            var orphan = rows.Where(a => a.registered && !a.despawned && !current.Any(c => c.IsSpawned && c.gameObject.GetInstanceID() == a.createdId && c.NetworkObjectId == a.networkObjectId)).Select(a => a.token).ToArray();
            report.currentAiCount = current.Length; report.matchedAiCount = current.Length - unmatched.Length;
            report.unmatchedAiIds = unmatched; report.orphanLiveAttempts = orphan;
            report.currentAiHistoryMatched = current.Length > 0 && unmatched.Length == 0 && orphan.Length == 0 && preexisting.Length == 0 &&
                !overflow && !faulted && report.failedAttempts == 0 && !rows.Any(a => a.despawnRequested && !a.despawned);
            report.coverageUnavailableReason = report.currentAiHistoryMatched ? null : "Observed AI creation history does not completely match this current typed AI set.";
            return report;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (ReferenceEquals(active, this)) active = null;
            Array.Clear(attempts, 0, attempts.Length); Array.Clear(events, 0, events.Length); preexisting = Array.Empty<int>();
        }
    }
#endif
}
