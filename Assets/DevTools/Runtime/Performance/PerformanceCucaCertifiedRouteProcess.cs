using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Dedicated encounter coordinator. World/host/save lifetime stays with the caller.
    public sealed class PerformanceCucaCertifiedRouteProcess : IPerformanceTimedSession
    {
        const string Mode = "cuca-certified-route", Route = "cuca-encounter-v1";
        const int CounterLimit = 1000000;
        static readonly string[] RosterArgs = { "-karawaraPerformanceRoster" };
        [Serializable] public sealed class ManifestData
        {
            public int schemaVersion, width, height, vsync, targetFrameRate;
            public string routeId, scene, sourceRevision, buildAssemblySha256, playerBuildGuid, distributionSha256, timelineSha256, seedSha256, quality, cameraMode, bossPath;
            public double warmupSeconds, captureSeconds, renderScale, maxLatenessSeconds, cameraTurnDegreesPerSecond;
            public string[] cameraPaths, counterNames;
            public PerformanceBoundedRoutePopulation.ActorGroup[] composition;
            public RehearsalPoseCheckpoint[] poseCheckpoints;
        }
        public sealed class FrozenManifest
        {
            readonly string json;
            public readonly string sha256;
            internal FrozenManifest(ManifestData data) { json = JsonConvert.SerializeObject(data); sha256 = PerformanceCertifiedRouteProcess.Hash(json); }
            public ManifestData Snapshot => JsonConvert.DeserializeObject<ManifestData>(json);
        }
        public static FrozenManifest ParseManifest(string json, string timelineJson, string seedJson, string actualBuildAssemblySha256, string actualPlayerBuildGuid, string actualDistributionSha256)
        {
            try
            {
                Need(!string.IsNullOrWhiteSpace(json) && json.Length <= 262144, "Bounded explicit Cuca manifest required");
                var fields = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                foreach (var field in typeof(ManifestData).GetFields()) Need(fields[field.Name] != null && fields[field.Name].Type != JTokenType.Null, "Missing Cuca manifest field: " + field.Name);
                var data = fields.ToObject<ManifestData>(JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }));
                Need(data.schemaVersion == 1 && data.routeId == Route && data.scene == "CucaLoad", "Wrong Cuca route/schema/scene");
                Need(data.sourceRevision.Length == 40 && data.sourceRevision.All(Uri.IsHexDigit), "Explicit Cuca source revision required");
                Need(HashMatches(data.buildAssemblySha256, actualBuildAssemblySha256) && HashMatches(data.distributionSha256, actualDistributionSha256) && Guid.TryParseExact(actualPlayerBuildGuid, "N", out _) && string.Equals(actualPlayerBuildGuid, data.playerBuildGuid, StringComparison.OrdinalIgnoreCase), "Actual Cuca Player build/distribution identity mismatch");
                Need(HashMatches(data.timelineSha256, PerformanceCertifiedRouteProcess.Hash(timelineJson)) && HashMatches(data.seedSha256, PerformanceCertifiedRouteProcess.Hash(seedJson)), "Cuca replay/seed hash mismatch");
                Need(data.width == 1920 && data.height == 1080 && data.quality == "High Fidelity" && data.vsync == 0 && data.targetFrameRate == -1 && data.warmupSeconds == 30 && data.captureSeconds == 120 && data.renderScale == 1 && Finite(data.maxLatenessSeconds) && data.maxLatenessSeconds > 0 && data.maxLatenessSeconds <= .25, "Fixed Cuca timing/rendering settings required");
                Need(UniquePaths(data.cameraPaths, 8) && ValidPath(data.bossPath), "Explicit unique Cuca camera/boss paths required");
                Need(data.counterNames.Length > 0 && data.counterNames.Length <= 64 && data.counterNames.All(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 256) && data.counterNames.Distinct(StringComparer.Ordinal).Count() == data.counterNames.Length, "Bounded unique Cuca counters required");
                PerformanceBoundedRoutePopulation.ValidateGroups(data.composition);
                Need(data.composition.Where(g => g.primaryType == typeof(CucaBoss).FullName).Sum(g => g.count) == 1 && data.composition.Any(g => g.primaryType == typeof(CucaBoss).FullName && g.role == "hostile" && g.active == true && g.enabled == true && g.spawned == false) && data.composition.Where(g => g.role == "local-player").Sum(g => g.count) == 1, "Exactly one active non-network Cuca and local player required");
                PerformanceProcessHarness.ParseCucaSeed(seedJson);
                PerformanceRouteRehearsal.ValidateSchedule(timelineJson, 150, data.maxLatenessSeconds);
                var timeline = JObject.Parse(timelineJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Need(data.cameraMode == "movement-heading-v1" && (string)timeline["cameraMode"] == data.cameraMode && Finite(data.cameraTurnDegreesPerSecond) && data.cameraTurnDegreesPerSecond >= 10 && data.cameraTurnDegreesPerSecond <= 90 && (double?)timeline["cameraTurnDegreesPerSecond"] == data.cameraTurnDegreesPerSecond, "Frozen Cuca heading camera configuration required");
                PerformanceRoutePoseProof.Create(data.poseCheckpoints, data.maxLatenessSeconds);
                return new FrozenManifest(data);
            }
            catch (ArgumentException) { throw; }
            catch (Exception error) { throw new ArgumentException("Invalid Cuca manifest: " + error.Message, error); }
        }
        public static void ValidateRequest(PerformanceProcessConfig config)
        {
            Need(config != null && config.mode == Mode && config.rehearsal != null, "Explicit Cuca certified request required");
            UseLightControl(config.mode, config.measurementArm, "CucaLoad");
            var route = config.rehearsal;
            Need(config.capture == null && config.profilerAttribution == null && config.retention == null && config.skillRetention == null && route.routeId == Route && route.durationSeconds == 150 && !route.auditAiSpawnerReset && !route.auditMemorySnapshots && route.aiSpawnerResetCycles == 1 && (route.checkpoints == null || route.checkpoints.Length == 0) && !string.IsNullOrWhiteSpace(route.outputRoot), "Cuca certification cannot mix diagnostics, alternate checkpoints or retention");
            Need(config.width == 1920 && config.height == 1080 && config.quality == "High Fidelity" && config.vsync == 0 && config.targetFrameRate == -1, "Fixed Cuca request rendering settings required");
            var declaration = JObject.Parse(config.certifiedManifestJson ?? "{}");
            var manifest = ParseManifest(config.certifiedManifestJson, route.timelineJson, route.seedSaveJson, (string)declaration["buildAssemblySha256"], (string)declaration["playerBuildGuid"], (string)declaration["distributionSha256"]).Snapshot;
            Need(route.maxLatenessSeconds == manifest.maxLatenessSeconds, "Cuca replay lateness differs from manifest");
        }
        [Serializable] public sealed class CounterSnapshot
        {
            public bool active;
            public int activationCount, projectileAttackCount, createdProjectiles, laserAttackCount, laserActivationCount, retreatCount;
        }
        public sealed class CounterVerdict
        {
            public bool accepted;
            public long activationDelta, projectileAttackDelta, createdProjectileDelta, laserAttackDelta, laserActivationDelta, retreatDelta;
            public string[] reasons;
        }
        public static CounterVerdict ValidateCounters(CounterSnapshot before, CounterSnapshot after)
        {
            var verdict = new CounterVerdict { reasons = Array.Empty<string>() };
            try
            {
                Need(before != null && after != null && before.active && after.active, "Cuca must already be active and remain active");
                var first = CounterValues(before); var last = CounterValues(after);
                for (int i = 0; i < first.Length; i++) Need(first[i] >= 0 && first[i] <= CounterLimit && last[i] >= first[i] && last[i] <= CounterLimit, "Cuca method counter reset, negative, overflow or bound violation");
                verdict.activationDelta = (long)after.activationCount - before.activationCount;
                verdict.projectileAttackDelta = (long)after.projectileAttackCount - before.projectileAttackCount;
                verdict.createdProjectileDelta = (long)after.createdProjectiles - before.createdProjectiles;
                verdict.laserAttackDelta = (long)after.laserAttackCount - before.laserAttackCount;
                verdict.laserActivationDelta = (long)after.laserActivationCount - before.laserActivationCount;
                verdict.retreatDelta = (long)after.retreatCount - before.retreatCount;
                // Each burst creates its first projectile synchronously, then two at 0.1s intervals.
                // A boundary can therefore carry at most the final two projectiles of one burst.
                long carryIn = 3L * before.projectileAttackCount - before.createdProjectiles;
                long carryOut = 3L * after.projectileAttackCount - after.createdProjectiles;
                Need(carryIn >= 0 && carryIn <= 2 && carryOut >= 0 && carryOut <= 2, "Impossible Cuca absolute projectile burst counters");
                Need(verdict.activationDelta == 0 && verdict.projectileAttackDelta >= 1 && verdict.createdProjectileDelta >= 3 && verdict.createdProjectileDelta <= 3 * verdict.projectileAttackDelta + carryIn && verdict.laserAttackDelta >= 1 && verdict.laserActivationDelta >= 1, "Missing actual Cuca projectile/laser method deltas or invalid burst count");
                verdict.accepted = true;
            }
            catch (Exception error) { verdict.reasons = new[] { error.GetType().Name + ": " + error.Message }; }
            return verdict;
        }
        public static PerformanceBoundedRoutePopulation.DomainVerdict ReconcilePopulation(FrozenManifest frozen, double startedAt, PerformanceBoundedRoutePopulation.PhaseObservation observed)
        {
            try
            {
                Need(frozen != null && Finite(startedAt) && startedAt >= 0 && observed?.before?.actors != null && observed.after?.actors != null, "Missing actual Cuca population evidence");
                var manifest = frozen.Snapshot;
                Need(observed.before.unclassifiedAnimators != null && observed.after.unclassifiedAnimators != null && observed.before.unclassifiedAnimators.Length == 0 && observed.after.unclassifiedAnimators.Length == 0, "Cuca Animator source coverage incomplete");
                var first = observed.before.actors.Where(a => a.primaryType == typeof(CucaBoss).FullName).ToArray();
                var last = observed.after.actors.Where(a => a.primaryType == typeof(CucaBoss).FullName).ToArray();
                Need(first.Length == 1 && last.Length == 1 && first[0].path == manifest.bossPath && last[0].path == manifest.bossPath && first[0].objectId == last[0].objectId && first[0].currentHealth > 30 && last[0].currentHealth > 30 && first[0].components.Contains(typeof(HealthManager).FullName) && last[0].components.Contains(typeof(HealthManager).FullName), "Exact living Cuca owner/health binding missing or replaced");
                return PerformanceBoundedRoutePopulation.EvaluatePhase(new PerformanceBoundedRoutePopulation.PhaseContract { id = Route, scene = manifest.scene, fromTime = startedAt, untilTime = startedAt + 150, boundaryTolerance = manifest.maxLatenessSeconds, composition = manifest.composition }, observed);
            }
            catch (Exception error) { return new PerformanceBoundedRoutePopulation.DomainVerdict(error.GetType().Name + ": " + error.Message); }
        }
        public sealed class EvidenceReport
        {
            public string measurementArm = "full";
            public bool lightObserversReleased, lightStructureUnchanged, lightPrePostGuardPassed;
            public double lightPreparationStartedElapsed, lightPreparationEndedElapsed, lightObserversReleaseStartedElapsed, lightObserversReleaseEndedElapsed;
            public PerformanceActorCensus.Report lightWarmupCensus, lightPostCensus;
            public PerformanceAiCreationHistory.Report lightWarmupAi;
            public PerformanceActorLifecycleScope.HistoryReport lightWarmupLifecycle;
            public CounterSnapshot lightWarmupBoss;
            public bool lightControlCompleted => LightCompletion(this);
            public readonly string lightBoundary = "Light Cuca control retains actual input, poses and natural encounter counters. Detailed observers are removed before30s and endpoint context checks are outside sampling. Endpoints do not establish measured transient actor/effect provenance or population/timing acceptance.";
            public string manifestSha256;
            public ManifestData manifest;
            public CaptureResult capture;
            public PerformanceBoundedRoutePopulation.PhaseObservation populationEvidence;
            public PerformanceBoundedRoutePopulation.DomainVerdict population;
            public PerformanceRoutePoseProof.Report poses;
            public CounterSnapshot bossBefore, bossAfter;
            public CounterVerdict encounter;
            public double[][] waypointArrivals, dispatch;
            public bool moved, jumped, cameraMoved, finalRelease, controlsRestored;
            public int guardedFrames;
            public readonly List<string> errors = new List<string>();
            public bool routePopulationQualified => measurementArm != "light-control" && capture != null && capture.comparisonValid && population?.accepted == true && poses != null && poses.completed && poses.closurePassed && !poses.faulted && encounter?.accepted == true && moved && jumped && cameraMoved && finalRelease && controlsRestored && guardedFrames > 0 && errors.Count == 0;
            public readonly string boundary = "Observed Cuca encounter method deltas, route poses, bounded actors and timing only. Complete rendering/native effect creation, GC, retention, observer overhead and G1B acceptance remain separate.";
        }
        sealed class CameraBinding
        {
            public Camera value;
            public Transform parent;
            public Rect viewport;
            public float depth, fov, size;
            public int display, mask;
            public bool orthographic;
            public RenderTexture texture;
        }
        sealed class BossBinding
        {
            internal readonly CucaBoss boss;
            internal readonly HealthManager health;
            readonly Animator animator;
            readonly RuntimeAnimatorController controller;
            readonly NavMeshAgent agent;
            readonly GameObject player, projectile, vfx;
            readonly Transform spawn, origin, parent;
            readonly LineRenderer line;
            readonly float maximumHealth, cooldown, detection, stayClose, retreat, attackCooldown, speed, delay, activation, multiplier;
            readonly string property;
            readonly int layers;
            internal BossBinding(CucaBoss boss, PlayerManager player)
            {
                this.boss = boss; health = boss.GetComponent<HealthManager>(); animator = boss.GetComponent<Animator>(); agent = boss.GetComponent<NavMeshAgent>();
                Need(health != null && animator != null && animator.runtimeAnimatorController != null && agent != null && boss.player == player.gameObject && boss.magicProjectile != null && boss.magicSpawnPoint != null && boss.laserOrigin != null && boss.VfxLaser != null && boss.lineRenderer != null, "Actual initialized Cuca driver graph required");
                controller = animator.runtimeAnimatorController; this.player = player.gameObject; projectile = boss.magicProjectile; spawn = boss.magicSpawnPoint; origin = boss.laserOrigin; vfx = boss.VfxLaser; line = boss.lineRenderer; parent = boss.transform.parent;
                maximumHealth = health.maxHealth; cooldown = boss.cooldownTime; detection = boss.detectionRange; stayClose = boss.stayCloseDuration; retreat = boss.retreatDistance; attackCooldown = boss.attackCooldown; speed = boss.laserSpeed; delay = boss.laserDelay; activation = boss.activationRange; multiplier = boss.multiplierSpeed; property = boss.tamanhoProperty; layers = boss.hitLayers;
                Need(Finite(maximumHealth) && maximumHealth > 30 && Finite(cooldown) && cooldown > 0 && Finite(attackCooldown) && attackCooldown > 0 && Matches(), "Healthy active Cuca with configured production cooldown required");
            }
            internal bool Matches() => boss != null && boss.isActiveAndEnabled && boss.QaIsActive && health != null && health.isActiveAndEnabled && Finite(health.currentHealth) && health.currentHealth > 30 && health.currentHealth <= maximumHealth && health.maxHealth == maximumHealth && animator != null && animator.isActiveAndEnabled && controller != null && animator.runtimeAnimatorController == controller && agent != null && agent.isActiveAndEnabled &&
                player != null && boss.player == player && projectile != null && boss.magicProjectile == projectile && spawn != null && boss.magicSpawnPoint == spawn && origin != null && boss.laserOrigin == origin && vfx != null && boss.VfxLaser == vfx && line != null && boss.lineRenderer == line && boss.transform.parent == parent &&
                boss.cooldownTime == cooldown && boss.detectionRange == detection && boss.stayCloseDuration == stayClose && boss.retreatDistance == retreat && boss.attackCooldown == attackCooldown && boss.laserSpeed == speed && boss.laserDelay == delay && boss.activationRange == activation && boss.multiplierSpeed == multiplier && boss.tamanhoProperty == property && boss.hitLayers == layers;
        }
        FrozenManifest frozen;
        ManifestData manifest;
        PerformanceCaptureController capture;
        PerformanceInputReplay replay;
        PerformanceRoutePoseProof poseProof;
        PerformanceActorRosterContext actorContext;
        PerformanceActorRosterContext lightContext;
        PerformanceActorLifecycleScope actorScope;
        PerformanceActorCensus.Report before;
        PerformanceAiCreationHistory history;
        PerformanceAiCreationHistory.Report aiBefore;
        PlayerManager player;
        PlayerInputManager input;
        PlayerCamera cameraOwner;
        Camera productionCamera;
        WorldSaveGameManager world;
        GameBootstrap network;
        PlayerControls controls;
        BossBinding boss;
        CounterSnapshot counterBefore;
        CameraBinding[] cameras;
        Camera[] cameraScratch;
        InputDevice[] originalDevices, originalAssetDevices, leasedDevices, leasedSystemDevices;
        InputBinding? originalMask;
        bool hadDeviceRestriction;
        InputAction[] actions;
        bool[] actionStates;
        CursorLockMode originalCursor;
        bool originalCursorVisible;
        string saveOverride;
        int sceneHandle, next, lightReleaseFrame;
        double startedAt;
        double[] schedule;
        float yawSpeed;
        Vector3 initialPosition;
        Quaternion initialRotation;
        bool disposed, ending, replayFinished, lightControl, lightPrepared;
        public EvidenceReport Evidence { get; private set; }
        public bool IsRunning => !disposed && capture != null && capture.IsCapturing;
        public int OwnedRecorderCount => capture == null ? 0 : capture.OwnedRecorderCount;
        public bool Qualified => Evidence?.measurementArm == "light-control" ? Evidence.lightControlCompleted : Evidence?.routePopulationQualified == true;
        public bool TimingQualified => Evidence?.routePopulationQualified == true;
        public bool PopulationVerified => Evidence?.measurementArm != "light-control" && Evidence?.population?.accepted == true;
        public object EvidenceObject => Evidence;
        PerformanceCucaCertifiedRouteProcess() { }
        public static PerformanceCucaCertifiedRouteProcess Begin(PerformanceProcessResponse request, PerformanceAiCreationHistory history, PlayerManager player, PlayerInputManager input, PlayerCamera cameraOwner)
        {
            if (Application.isEditor || request?.success != true || request.mode != Mode || history == null || player == null || input == null || cameraOwner == null) throw new InvalidOperationException("Actual standalone Cuca request and production owners required");
            ValidateRequest(request.config);
            var session = new PerformanceCucaCertifiedRouteProcess();
            try
            {
                string assembly; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(typeof(PlayerManager).Assembly.Location)) assembly = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                string distribution = PerformanceCertifiedRouteProcess.ComputeDistributionHash(Path.GetDirectoryName(Application.dataPath));
                session.frozen = ParseManifest(request.config.certifiedManifestJson, request.config.rehearsal.timelineJson, request.config.rehearsal.seedSaveJson, assembly, Application.buildGUID, distribution);
                session.manifest = session.frozen.Snapshot; session.Evidence = new EvidenceReport { manifestSha256 = session.frozen.sha256, manifest = session.frozen.Snapshot };
                session.lightControl = UseLightControl(request.mode, request.config.measurementArm, session.manifest.scene);
                session.Evidence.measurementArm = session.lightControl ? "light-control" : "full";
                session.player = player; session.input = input; session.cameraOwner = cameraOwner; session.productionCamera = cameraOwner.cameraObject; session.history = history;
                session.world = WorldSaveGameManager.instance; session.network = GameBootstrap.Instance; session.controls = input.playerControls;
                Need(session.world != null && session.world.SaveRoot == request.saveRoot && session.controls != null && session.productionCamera != null && session.network != null, "Actual isolated Cuca host/controls/camera required");
                session.saveOverride = session.world.SaveRootOverride; session.sceneHandle = SceneManager.GetActiveScene().handle;
                Need(SceneManager.GetActiveScene().name == "CucaLoad", "Actual Cuca scene required");
                var bosses = Object.FindObjectsByType<CucaBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Need(bosses.Length == 1 && TransformPath(bosses[0].transform) == session.manifest.bossPath, "Exact sole Cuca owner differs from manifest");
                session.boss = new BossBinding(bosses[0], player); session.counterBefore = ReadCounters(bosses[0]); session.Evidence.bossBefore = CopyCounters(session.counterBefore);
                var loaded = Camera.allCameras;
                Need(loaded.Contains(session.productionCamera) && loaded.Select(c => TransformPath(c.transform)).OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(session.manifest.cameraPaths.OrderBy(p => p, StringComparer.Ordinal)), "Actual Cuca camera paths differ from manifest");
                session.cameras = loaded.Select(c => new CameraBinding { value = c, parent = c.transform.parent, viewport = c.rect, depth = c.depth, fov = c.fieldOfView, size = c.orthographicSize, orthographic = c.orthographic, display = c.targetDisplay, mask = c.cullingMask, texture = c.targetTexture == null ? null : c.targetTexture }).ToArray(); session.cameraScratch = new Camera[loaded.Length];
                session.originalDevices = InputSystem.devices.ToArray(); session.originalMask = session.controls.asset.bindingMask; var restriction = session.controls.asset.devices; session.hadDeviceRestriction = restriction.HasValue; session.originalAssetDevices = restriction.HasValue ? restriction.Value.ToArray() : Array.Empty<InputDevice>();
                var list = new List<InputAction>(); foreach (var action in session.controls.asset) list.Add(action); session.actions = list.ToArray(); session.actionStates = session.actions.Select(a => a.enabled).ToArray();
                session.originalCursor = Cursor.lockState; session.originalCursorVisible = Cursor.visible;
                session.initialPosition = player.transform.position; session.initialRotation = session.productionCamera.transform.rotation;
                session.yawSpeed = (float)typeof(PlayerCamera).GetField("leftAndRightRotationSpeed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cameraOwner);
                Need(Finite(session.yawSpeed) && session.yawSpeed > 0, "Actual production camera yaw speed required");
                var census = PerformanceActorCensus.Capture(RosterArgs); session.before = census.report;
                Need(census.report.unclassifiedAnimators != null && census.report.unclassifiedAnimators.Length == 0 && census.report.actors != null && census.report.actors.All(a => a.supportedBinding), "Complete typed Cuca census/Animator coverage required");
                session.aiBefore = history.Capture(census); session.actorContext = census.CreateContext();
                session.poseProof = PerformanceRoutePoseProof.Create(session.manifest.poseCheckpoints, session.manifest.maxLatenessSeconds); session.Evidence.poses = session.poseProof.Evidence;
                session.schedule = PerformanceRouteRehearsal.ValidateSchedule(request.config.rehearsal.timelineJson, 150, session.manifest.maxLatenessSeconds);
                session.replay = PerformanceInputReplay.Create(new[] { "-karawaraPerformanceReplay" }, session.controls, request.config.rehearsal.timelineJson); session.leasedDevices = session.controls.asset.devices.Value.ToArray(); session.leasedSystemDevices = InputSystem.devices.ToArray();
                session.ValidateOwners();
                var root = new GameObject("Explicit Cuca encounter timing capture"); Object.DontDestroyOnLoad(root); session.capture = root.AddComponent<PerformanceCaptureController>(); if (!session.lightControl) session.capture.CameraIdProvider = session.ObserveCameraIdentity; session.capture.FinalValidation = session.FinalizeCapture;
                Need(session.capture.Begin(new CaptureConfig { qaEnabled = true, routeId = Route, runId = "cuca-certified-", outputRoot = request.config.rehearsal.outputRoot, commit = session.manifest.sourceRevision, buildId = Application.buildGUID + ":" + distribution, camera = session.frozen.sha256, enemyCount = session.manifest.composition.Where(g => g.role == "hostile").Sum(g => g.count), renderScale = 1, warmupSeconds = 30, captureSeconds = 120, lightControl = session.lightControl, counterNames = session.lightControl ? Array.Empty<string>() : session.manifest.counterNames }), "Cuca recorder/capture setup rejected");
                session.startedAt = session.capture.StartedTimestampSeconds;
                session.actorScope = PerformanceActorLifecycleScope.Begin(RosterArgs, session.actorContext);
                InputSystem.onBeforeUpdate += session.BeforeInput;
                return session;
            }
            catch { session.Dispose(); throw; }
        }
        public void Poll()
        {
            using (PerformanceProfilerAttribution.PollMarker.Auto())
            {
                if (!IsRunning) return;
                try
                {
                    double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                    if (lightControl)
                    {
                        if (!lightPrepared && elapsed >= 28) PrepareLightControl(elapsed);
                        if (lightPrepared && !Evidence.lightObserversReleased) ReleaseLightObservers();
                        if (Evidence.lightObserversReleased && Evidence.lightObserversReleaseEndedElapsed == 0 && Time.frameCount > lightReleaseFrame)
                        {
                            Evidence.lightObserversReleaseEndedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                            Need(Evidence.lightObserversReleaseEndedElapsed < 30, "Cuca deferred observer cleanup crossed measurement");
                        }
                        if (elapsed >= 30) Need(Evidence.lightObserversReleaseEndedElapsed > 0, "Cuca observer cleanup did not complete before30s");
                        if (elapsed < 30) ValidateOwners(); else ValidateLightOwners();
                    }
                    else ValidateOwners();
                    poseProof.Observe(elapsed, player.transform.position, productionCamera.transform.rotation, Time.frameCount);
                    Evidence.moved |= Vector2.Distance(new Vector2(initialPosition.x, initialPosition.z), new Vector2(player.transform.position.x, player.transform.position.z)) > .05f;
                    Evidence.jumped |= player.playerNetworkManager.isJumping.Value;
                    Evidence.cameraMoved |= Quaternion.Angle(initialRotation, productionCamera.transform.rotation) > .1f;
                    Evidence.finalRelease = NeutralReleased(); Evidence.guardedFrames++;
                }
                catch (Exception error) { Fail(error); }
            }
        }
        void BeforeInput()
        {
            using (PerformanceProfilerAttribution.InputMarker.Auto())
            {
                if (!IsRunning || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                try
                {
                    double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                    int due = PerformanceRouteRehearsal.DuePacketCount(schedule, next, elapsed, manifest.maxLatenessSeconds);
                    replayFinished = replay.AdvanceWithWorldTargets(elapsed, cameraOwner.transform.eulerAngles.y, yawSpeed, player.transform.position); next += due;
                }
                catch (Exception error) { Fail(error); }
            }
        }
        string ObserveCameraIdentity() { using (PerformanceProfilerAttribution.GuardMarker.Auto()) { ValidateOwners(); return frozen.sha256; } }
        void ValidateOwners()
        {
            Need(!UnityEngine.Profiling.Profiler.enabled && !UnityEngine.Profiling.Profiler.enableAllocationCallstacks, "Profiler instrumentation active during Cuca timing");
            Need(Time.timeScale == 1 && Application.isFocused && world != null && world == WorldSaveGameManager.instance && world.player == player && world.SaveRootOverride == saveOverride && network != null && network == GameBootstrap.Instance && network.Session.IsActive && network.Session.IsActive && player != null && player.IsInitialized && player.IsInitialized && network.Session.Player == player && !player.isDead.Value && player.playerNetworkManager.currentHealth.Value > 0 &&
                input == PlayerInputManager.instance && input.player == player && input.playerControls == controls && controls.asset.enabled && controls.PlayerMovement.enabled && controls.PlayerActions.enabled && controls.PlayerCamera.enabled && cameraOwner == PlayerCamera.instance && cameraOwner.player == player && cameraOwner.canFollow && cameraOwner.cameraObject == productionCamera && SceneManager.GetActiveScene().handle == sceneHandle && boss.Matches(), "Cuca world/host/player/controls/boss/focus changed or an unplanned pause occurred");
            Need(actorContext == null || actorContext.SourceViolationMask == 0, "Observed Cuca actor lifecycle/phase violation");
            Need(actorScope == null || !actorScope.NotificationFaulted, "Cuca lifecycle source journal faulted");
            var counters = boss.boss;
            Need(CounterInRange(counters.QaActivationCount, counterBefore.activationCount) && counters.QaActivationCount == counterBefore.activationCount && CounterInRange(counters.QaProjectileAttackCount, counterBefore.projectileAttackCount) && CounterInRange(counters.QaProjectileCreatedCount, counterBefore.createdProjectiles) && CounterInRange(counters.QaLaserAttackCount, counterBefore.laserAttackCount) && CounterInRange(counters.QaLaserActivationCount, counterBefore.laserActivationCount) && CounterInRange(counters.QaRetreatCount, counterBefore.retreatCount), "Cuca method counter reset/overflow/configuration changed");
            Need(Camera.allCamerasCount == cameras.Length && Camera.GetAllCameras(cameraScratch) == cameras.Length, "Cuca camera set changed");
            foreach (var binding in cameras)
            {
                var c = binding.value;
                Need(c != null && c.isActiveAndEnabled && Array.IndexOf(cameraScratch, c) >= 0 && c.transform.parent == binding.parent && c.rect == binding.viewport && c.depth == binding.depth && c.fieldOfView == binding.fov && c.orthographic == binding.orthographic && c.orthographicSize == binding.size && c.targetDisplay == binding.display && c.cullingMask == binding.mask && (binding.texture == null ? c.targetTexture == null : binding.texture != null && c.targetTexture == binding.texture), "Frozen Cuca camera rendering context changed");
            }
            if (leasedDevices == null || replay == null) return;
            var devices = controls.asset.devices;
            Need(controls.asset.bindingMask == null && devices.HasValue && devices.Value.Count == leasedDevices.Length && InputSystem.devices.Count == leasedSystemDevices.Length, "Cuca replay restriction/device ownership lost");
            for (int i = 0; i < leasedDevices.Length; i++) Need(leasedDevices[i] != null && leasedDevices[i].added && devices.Value[i] == leasedDevices[i], "Owned Cuca virtual devices changed");
            for (int i = 0; i < leasedSystemDevices.Length; i++) Need(InputSystem.devices[i] == leasedSystemDevices[i], "Cuca system device set changed");
            foreach (var action in actions) Need(action.enabled, "Cuca replay action ownership changed");
        }
        bool NeutralReleased()
        {
            if (!replayFinished || next != schedule.Length || controls.PlayerMovement.Movement.ReadValue<Vector2>() != Vector2.zero || controls.PlayerCamera.Movement.ReadValue<Vector2>() != Vector2.zero || input.moveAmount != 0 || input.horizontal_Input != 0 || input.vertical_Input != 0 || input.cameraHorizontal_Input != 0 || input.cameraVertical_Input != 0) return false;
            foreach (var action in actions) if (action.type == InputActionType.Button && action.IsPressed()) return false;
            return true;
        }
        void Fail(Exception error) { Evidence?.errors.Add(error.ToString()); if (capture != null && capture.IsCapturing) capture.End(); }
        void FinalizeCapture(CaptureResult result)
        {
            if (ending) return; ending = true; InputSystem.onBeforeUpdate -= BeforeInput;
            Evidence.capture = result; result.populationVerified = false; result.cameraVerified = false;
            // Close the observed interval before costly census/export work, preserving
            // actual journal endpoint times without doing actor polls in timed frames.
            actorScope?.Dispose();
            try
            {
                if (boss?.boss != null)
                {
                    Evidence.bossAfter = ReadCounters(boss.boss);
                    Evidence.encounter = ValidateCounters(counterBefore, Evidence.bossAfter);
                }
                ValidateOwners();
                poseProof.Observe(Time.realtimeSinceStartupAsDouble - startedAt, player.transform.position, productionCamera.transform.rotation, Time.frameCount); poseProof.Complete();
                Need(Time.realtimeSinceStartupAsDouble - startedAt >= 150, "Full Cuca route clock was not completed");
                Evidence.finalRelease = NeutralReleased();
                if (lightControl)
                {
                    FinalizeLightControl(result);
                    return;
                }
                var after = PerformanceActorCensus.Capture(RosterArgs); var validation = new CaptureResult(); actorContext.Validate(validation);
                Evidence.populationEvidence = new PerformanceBoundedRoutePopulation.PhaseObservation { before = before, after = after.report, lifecycle = actorScope.CaptureHistory(), aiBefore = aiBefore, aiAfter = history.Capture(after), contextViolationMask = actorContext.SourceViolationMask };
                Evidence.population = ReconcilePopulation(frozen, startedAt, Evidence.populationEvidence);
                if (validation.conditionsChanged) result.errors.AddRange(validation.errors);
                if (!Evidence.population.accepted) result.errors.AddRange(Evidence.population.reasons);
                if (!Evidence.encounter.accepted) result.errors.AddRange(Evidence.encounter.reasons);
                result.populationVerified = Evidence.population.accepted && !validation.conditionsChanged;
                result.cameraVerified = Evidence.cameraMoved && poseProof.IsComplete;
                if (!Evidence.moved || !Evidence.jumped || !Evidence.cameraMoved || !Evidence.finalRelease || !replayFinished || Evidence.errors.Count != 0) result.errors.Add("Actual Cuca route/control/pose completion proof failed");
            }
            catch (Exception error) { Evidence.errors.Add(error.ToString()); result.errors.Add("Cuca final evidence failed: " + error.Message); result.populationVerified = false; result.cameraVerified = false; }
            finally
            {
                try { ReleaseControls(); }
                catch (Exception error) { Evidence.errors.Add(error.ToString()); result.errors.Add("Cuca controls restoration failed: " + error.Message); }
                finally { actorScope?.Dispose(); actorContext?.Dispose(); lightContext?.Dispose(); }
                if (!Evidence.controlsRestored || Evidence.errors.Count != 0) result.errors.Add("Cuca controls or observed completion proof failed");
            }
        }
        void ReleaseControls()
        {
            try
            {
                if (replay != null)
                {
                    Evidence.dispatch = replay.DispatchTimeline(); Evidence.waypointArrivals = replay.WaypointArrivals();
                    replay.Dispose(); replay = null;
                }
            }
            finally
            {
                if (controls != null && actions != null)
                {
                    for (int i = 0; i < actions.Length; i++) if (actionStates[i]) actions[i].Enable(); else actions[i].Disable();
                    Cursor.lockState = originalCursor; Cursor.visible = originalCursorVisible;
                    Evidence.controlsRestored = controls.asset.bindingMask.Equals(originalMask) && InputSystem.devices.SequenceEqual(originalDevices) && controls.asset.devices.HasValue == hadDeviceRestriction && (!hadDeviceRestriction || controls.asset.devices.Value.SequenceEqual(originalAssetDevices)) && actions.Select(a => a.enabled).SequenceEqual(actionStates) && Cursor.lockState == originalCursor && Cursor.visible == originalCursorVisible;
                }
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; InputSystem.onBeforeUpdate -= BeforeInput;
            try { if (capture != null && capture.IsCapturing) capture.End(); }
            finally
            {
                try { ReleaseControls(); }
                finally { actorScope?.Dispose(); actorContext?.Dispose(); lightContext?.Dispose(); if (lightControl) history?.Dispose(); if (capture != null) { capture.FinalValidation = null; capture.CameraIdProvider = null; Object.Destroy(capture.gameObject); capture = null; } }
            }
        }
        static bool UseLightControl(string mode, string arm, string scene)
        {
            if (arm == null || arm == "full") return false;
            Need(arm == "light-control" && mode == Mode && scene == "CucaLoad", "Cuca light control requires its explicit certified encounter without attribution");
            return true;
        }
        static bool LightCompletion(EvidenceReport evidence) => evidence != null && evidence.measurementArm == "light-control" && evidence.manifest?.scene == "CucaLoad" &&
            evidence.capture != null && evidence.capture.conditions?.lightControl == true && evidence.capture.fullWindowCompleted && !evidence.capture.conditionsChanged && !evidence.capture.comparisonValid && !evidence.capture.populationVerified && !evidence.capture.cameraVerified && evidence.capture.errors.Count == 0 &&
            evidence.poses != null && evidence.poses.completed && evidence.poses.closurePassed && !evidence.poses.faulted && evidence.encounter?.accepted == true && evidence.moved && evidence.jumped && evidence.cameraMoved && evidence.finalRelease && evidence.controlsRestored && evidence.guardedFrames > 0 &&
            evidence.lightObserversReleased && evidence.lightStructureUnchanged && evidence.lightPrePostGuardPassed &&
            Finite(evidence.lightPreparationStartedElapsed) && Finite(evidence.lightPreparationEndedElapsed) && evidence.lightPreparationStartedElapsed >= 28 && evidence.lightPreparationStartedElapsed <= evidence.lightPreparationEndedElapsed && evidence.lightPreparationEndedElapsed < 30 &&
            Finite(evidence.lightObserversReleaseStartedElapsed) && Finite(evidence.lightObserversReleaseEndedElapsed) && evidence.lightObserversReleaseStartedElapsed >= evidence.lightPreparationEndedElapsed && evidence.lightObserversReleaseStartedElapsed < evidence.lightObserversReleaseEndedElapsed && evidence.lightObserversReleaseEndedElapsed < 30 && evidence.errors.Count == 0;
        bool LightSettingsMatch() => Screen.width == manifest.width && Screen.height == manifest.height && QualitySettings.names[QualitySettings.GetQualityLevel()] == manifest.quality && QualitySettings.vSyncCount == manifest.vsync && Application.targetFrameRate == manifest.targetFrameRate && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - manifest.renderScale) < .0001;
        void ValidateLightOwners()
        {
            // Intentionally no source/context polls, camera inventories or full frozen-driver traversal.
            Need(Time.timeScale == 1 && Application.isFocused && SceneManager.GetActiveScene().handle == sceneHandle && player != null && player.IsInitialized && player.IsInitialized && !player.isDead.Value && player.playerNetworkManager.currentHealth.Value > 0 &&
                productionCamera != null && cameraOwner != null && cameraOwner.cameraObject == productionCamera && boss?.boss != null && boss.boss.isActiveAndEnabled && boss.boss.QaIsActive && boss.health != null && Finite(boss.health.currentHealth) && boss.health.currentHealth > 30, "Cuca light focus/scene/player/boss/camera lifetime changed");
        }
        void PrepareLightControl(double elapsed)
        {
            Need(elapsed < 30, "Cuca light preparation missed its pre30s window");
            Evidence.lightPreparationStartedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            ValidateOwners(); Need(LightSettingsMatch(), "Cuca light warm rendering settings changed");
            var census = PerformanceActorCensus.Capture(RosterArgs);
            Need(PerformanceCertifiedRouteProcess.LightStructuralMatch(census.report, census.report, manifest.composition), "Cuca light warm census differs from frozen composition");
            lightContext = census.CreateEndpointContext(); var validation = new CaptureResult(); lightContext.Validate(validation);
            Need(!validation.conditionsChanged, "Cuca light warm context rejected");
            Evidence.lightWarmupCensus = census.report; Evidence.lightWarmupAi = history.Capture(census);
            // Native Cuca is not an AICharacterManager; a valid zero-AI census is not required to claim currentAiHistoryMatched.
            Need(!Evidence.lightWarmupAi.sourceFaulted && !Evidence.lightWarmupAi.overflow && Evidence.lightWarmupAi.failedAttempts == 0, "Cuca light warm AI observer faulted");
            Evidence.lightWarmupLifecycle = actorScope.CaptureHistory(); Evidence.lightWarmupBoss = ReadCounters(boss.boss);
            Evidence.lightPreparationEndedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Need(Evidence.lightPreparationEndedElapsed < 30, "Cuca heavy preparation crossed measurement");
            lightPrepared = true;
        }
        void ReleaseLightObservers()
        {
            if (Evidence.lightObserversReleased) return;
            Need(lightPrepared, "Cuca light observer release requires warm preparation");
            Evidence.lightObserversReleaseStartedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Need(Evidence.lightObserversReleaseStartedElapsed < 30, "Cuca observer release started inside measurement");
            actorScope?.Dispose(); actorContext?.Dispose(); history.Dispose();
            capture.CameraIdProvider = null; capture.EnemyCountProvider = null;
            lightReleaseFrame = Time.frameCount; Evidence.lightObserversReleased = true;
        }
        void FinalizeLightControl(CaptureResult result)
        {
            result.populationVerified = false; result.cameraVerified = false; result.comparisonValid = false;
            Need(lightPrepared && Evidence.lightObserversReleased && Evidence.lightObserversReleaseEndedElapsed > 0, "Cuca light warm preparation/observer cleanup incomplete");
            var after = PerformanceActorCensus.Capture(RosterArgs); var validation = new CaptureResult(); lightContext.Validate(validation);
            using (var postContext = after.CreateContext())
            {
                var postValidation = new CaptureResult(); postContext.Validate(postValidation);
                Evidence.lightStructureUnchanged = !validation.conditionsChanged && !postValidation.conditionsChanged && PerformanceCertifiedRouteProcess.LightStructuralMatch(Evidence.lightWarmupCensus, after.report, manifest.composition);
            }
            ValidateOwners(); Evidence.lightPrePostGuardPassed = LightSettingsMatch();
            Evidence.lightPostCensus = after.report;
            Evidence.encounter = ValidateCounters(Evidence.lightWarmupBoss, Evidence.bossAfter);
            Evidence.populationEvidence = new PerformanceBoundedRoutePopulation.PhaseObservation { before = Evidence.lightWarmupCensus, after = after.report, aiBefore = Evidence.lightWarmupAi, contextViolationMask = lightContext.SourceViolationMask };
            Evidence.population = new PerformanceBoundedRoutePopulation.DomainVerdict("Light Cuca control observes warm/post endpoints only; measured transient population provenance and acceptance are unavailable.");
            result.validationUnavailableReasons.Add("Light Cuca control: zero recorders/providers, endpoint structure, poses and natural encounter counters; no continuous source/context or population certification.");
            if (!Evidence.lightStructureUnchanged || !Evidence.lightPrePostGuardPassed || !Evidence.encounter.accepted || !Evidence.moved || !Evidence.jumped || !Evidence.cameraMoved || !Evidence.finalRelease || !replayFinished || Evidence.errors.Count != 0)
                result.errors.Add("Cuca light route/encounter/endpoint proof failed");
        }
        static CounterSnapshot ReadCounters(CucaBoss b) => new CounterSnapshot { active = b.QaIsActive, activationCount = b.QaActivationCount, projectileAttackCount = b.QaProjectileAttackCount, createdProjectiles = b.QaProjectileCreatedCount, laserAttackCount = b.QaLaserAttackCount, laserActivationCount = b.QaLaserActivationCount, retreatCount = b.QaRetreatCount };
        static CounterSnapshot CopyCounters(CounterSnapshot b) => new CounterSnapshot { active = b.active, activationCount = b.activationCount, projectileAttackCount = b.projectileAttackCount, createdProjectiles = b.createdProjectiles, laserAttackCount = b.laserAttackCount, laserActivationCount = b.laserActivationCount, retreatCount = b.retreatCount };
        static int[] CounterValues(CounterSnapshot b) => new[] { b.activationCount, b.projectileAttackCount, b.createdProjectiles, b.laserAttackCount, b.laserActivationCount, b.retreatCount };
        static bool CounterInRange(int actual, int initial) => initial >= 0 && actual >= initial && actual <= CounterLimit;
        static bool ValidPath(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 1024;
        static bool UniquePaths(string[] paths, int maximum) => paths != null && paths.Length > 0 && paths.Length <= maximum && paths.All(ValidPath) && paths.Distinct(StringComparer.Ordinal).Count() == paths.Length;
        static bool HashMatches(string expected, string actual) => expected != null && actual != null && expected.Length == 64 && actual.Length == 64 && expected.All(Uri.IsHexDigit) && actual.All(Uri.IsHexDigit) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static string TransformPath(Transform value) { string path = value.name; while (value.parent != null) { value = value.parent; path = value.name + "/" + path; } return path; }
        static void Need(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }
    }
#endif
}
