using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Owns only the timed UI route. The caller retains world, host, seed and save cleanup.
    public sealed class PerformanceUiRouteProcess : IPerformanceTimedSession
    {
        const string Route = "tutorial-ui-minimap-v1";
        const string Mode = "tutorial-ui-certified-route";
        static readonly string[] RosterArgs = { "-karawaraPerformanceRoster" };
        [Serializable] public sealed class ManifestData
        {
            public int schemaVersion, width, height, vsync, targetFrameRate;
            public string routeId, scene, sourceRevision, buildAssemblySha256, playerBuildGuid, distributionSha256, timelineSha256, seedSha256, quality;
            public double warmupSeconds, captureSeconds, renderScale, maxLatenessSeconds, positionTolerance, cameraAngleTolerance;
            public string[] cameraPaths, counterNames;
            public string miniMapPath, pausePanelPath, skillPanelPath;
            public PerformanceBoundedRoutePopulation.ActorGroup[] composition;
            public float[] initialPosition, initialCameraRotation;
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
                Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 262144, "Bounded explicit UI manifest required");
                var fields = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                foreach (var field in typeof(ManifestData).GetFields()) Require(fields[field.Name] != null && fields[field.Name].Type != JTokenType.Null, "Missing UI manifest field: " + field.Name);
                var data = fields.ToObject<ManifestData>(JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }));
                Require(data.schemaVersion == 1 && data.routeId == Route && data.scene == "Tutorial_Final", "Wrong UI route/schema/scene");
                Require(data.sourceRevision.Length == 40 && data.sourceRevision.All(Uri.IsHexDigit), "Explicit source revision required");
                Require(HashMatches(data.buildAssemblySha256, actualBuildAssemblySha256) && HashMatches(data.distributionSha256, actualDistributionSha256), "UI Player assembly/distribution identity mismatch");
                Require(Guid.TryParseExact(actualPlayerBuildGuid, "N", out _) && string.Equals(data.playerBuildGuid, actualPlayerBuildGuid, StringComparison.OrdinalIgnoreCase), "UI Player GUID mismatch");
                Require(HashMatches(data.timelineSha256, PerformanceCertifiedRouteProcess.Hash(timelineJson)) && HashMatches(data.seedSha256, PerformanceCertifiedRouteProcess.Hash(seedJson)), "UI timeline/seed identity mismatch");
                Require(data.warmupSeconds == 30 && data.captureSeconds == 120 && data.width == 1920 && data.height == 1080 && data.quality == "High Fidelity" && data.vsync == 0 && data.targetFrameRate == -1 && data.renderScale == 1 && data.maxLatenessSeconds == .25, "Fixed UI timing/rendering settings required");
                Require(data.positionTolerance == .25 && data.cameraAngleTolerance == 1 && data.initialPosition.Length == 3 && data.initialPosition.All(v => Finite(v)) && data.initialCameraRotation.Length == 4 && data.initialCameraRotation.All(v => Finite(v)) && Math.Abs(data.initialCameraRotation.Sum(v => (double)v * v) - 1) <= .001, "Frozen finite stationary pose and fixed tolerances required");
                Require(UniquePaths(data.cameraPaths, 8) && ValidPath(data.miniMapPath) && ValidPath(data.pausePanelPath) && ValidPath(data.skillPanelPath) && data.pausePanelPath != data.skillPanelPath, "Unique explicit UI camera/panel/source paths required");
                Require(data.counterNames.Length > 0 && data.counterNames.Length <= 64 && data.counterNames.All(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 256) && data.counterNames.Distinct(StringComparer.Ordinal).Count() == data.counterNames.Length, "Bounded unique UI counters required");
                PerformanceBoundedRoutePopulation.ValidateGroups(data.composition);
                PerformanceProcessHarness.ParseTutorialSeed(seedJson);
                ValidateNeutralTimeline(timelineJson);
                return new FrozenManifest(data);
            }
            catch (ArgumentException) { throw; }
            catch (Exception error) { throw new ArgumentException("Invalid UI manifest: " + error.Message, error); }
        }
        public static void ValidateRequest(PerformanceProcessConfig config)
        {
            Require(config != null && config.mode == Mode && config.rehearsal != null, "Explicit UI request required");
            UsesLightControl(config.measurementArm);
            var rehearsal = config.rehearsal;
            Require(config.capture == null && config.profilerAttribution == null && config.retention == null && config.skillRetention == null && rehearsal.routeId == Route && rehearsal.durationSeconds == 150 && rehearsal.maxLatenessSeconds == .25 && !rehearsal.requireObservedJump && !rehearsal.auditAiSpawnerReset && !rehearsal.auditMemorySnapshots && (rehearsal.checkpoints == null || rehearsal.checkpoints.Length == 0), "UI route cannot mix movement, diagnostics, retention or audit options");
            Require(config.width == 1920 && config.height == 1080 && config.quality == "High Fidelity" && config.vsync == 0 && config.targetFrameRate == -1 && !string.IsNullOrWhiteSpace(rehearsal.outputRoot), "Fixed UI request rendering/output settings required");
            var identity = JObject.Parse(config.certifiedManifestJson);
            ParseManifest(config.certifiedManifestJson, rehearsal.timelineJson, rehearsal.seedSaveJson, (string)identity["buildAssemblySha256"], (string)identity["playerBuildGuid"], (string)identity["distributionSha256"]);
        }
        static void ValidateNeutralTimeline(string json)
        {
            PerformanceRouteRehearsal.ValidateSchedule(json, 150, .25);
            var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Require(root.Properties().All(p => p.Name == "schemaVersion" || p.Name == "frames"), "UI replay must use a stationary neutral timeline");
            var frames = (JArray)root["frames"];
            Require((double)frames[0]["timeSeconds"] == 0, "UI neutral lease must start at zero");
            foreach (JObject frame in frames)
            {
                Require(frame.Properties().All(p => p.Name == "timeSeconds" || p.Name == "keys" || p.Name == "mouseDeltaX" || p.Name == "mouseDeltaY" || p.Name == "leftButton" || p.Name == "rightButton" || p.Name == "middleButton"), "Unexpected UI replay input field");
                Require(((JArray)frame["keys"]).Count == 0 && ((double?)frame["mouseDeltaX"] ?? 0) == 0 && ((double?)frame["mouseDeltaY"] ?? 0) == 0 && !((bool?)frame["leftButton"] ?? false) && !((bool?)frame["rightButton"] ?? false) && !((bool?)frame["middleButton"] ?? false), "UI replay must remain neutral");
            }
        }
        public static int ExpectedPhase(double elapsed)
        {
            Require(Finite(elapsed) && elapsed >= 0, "Finite nonnegative UI clock required");
            return elapsed < 60 ? 0 : elapsed < 90 ? 1 : elapsed < 120 ? 2 : 3;
        }
        public static int ActionForTransition(int index)
        {
            Require(index >= 0 && index < 3, "Exactly three UI transitions allowed");
            return index == 0 ? 1 : 2;
        }
        [Serializable] public struct TransitionRow { public int index, action, frame; public double scheduledElapsed, actualElapsed, lateness; }
        public sealed class EvidenceReport
        {
            public float[] initialCameraPosition;
            public string measurementArm = "full";
            public bool lightObserversReleased, lightStructureUnchanged, lightPrePostGuardPassed;
            public double lightPreparationStartedElapsed, lightPreparationEndedElapsed, lightObserversReleaseStartedElapsed, lightObserversReleaseEndedElapsed;
            public PerformanceActorCensus.Report lightWarmupCensus, lightPostCensus;
            public PerformanceAiCreationHistory.Report lightWarmupAi;
            public PerformanceActorLifecycleScope.HistoryReport lightWarmupLifecycle;
            public bool lightControlCompleted => LightControlComplete(this);
            public readonly string lightBoundary = "Light UI control preserves authored phases, input, poses and endpoint structure. Detailed observers stop before measurement. Transient creation/despawn closure and population/timing certification are unavailable.";
            public string manifestSha256;
            public ManifestData manifest;
            public CaptureResult capture;
            public PerformanceCheckpointImages.Report checkpointImages;
            public bool? unexpectedConditionsChanged;
            public PerformanceBoundedRoutePopulation.PhaseObservation populationEvidence;
            public PerformanceBoundedRoutePopulation.DomainVerdict population;
            public TransitionRow[] phaseTransitions;
            public int[] phaseObservedFrames;
            public bool preparationOpened, preparationClosed, finalRelease, controlsRestored, stationaryCameraVerified;
            public double preparationStartedAt, preparationEndedAt;
            public double[][] dispatch;
            public readonly List<string> errors = new List<string>();
            bool CommonRouteProof => population != null && population.accepted && preparationOpened && preparationClosed && stationaryCameraVerified && finalRelease && controlsRestored && phaseTransitions != null && phaseTransitions.Length == 3 && phaseObservedFrames != null && phaseObservedFrames.Length == 4 && phaseObservedFrames.All(n => n > 0) && errors.Count == 0;
            public bool routePopulationQualified => measurementArm == "full" && checkpointImages == null && capture != null && capture.comparisonValid && CommonRouteProof;
            public bool checkpointImagesCompleted => measurementArm == "checkpoint-images-diagnostic" && manifest?.scene == "Tutorial_Final" && checkpointImages != null && checkpointImages.completed && checkpointImages.requests.Count == 6 && unexpectedConditionsChanged == false && capture != null && capture.fullWindowCompleted && capture.conditionsChanged && !capture.comparisonValid && capture.errors.Count == 0 && CommonRouteProof;
            public readonly string boundary = "UI route timing, stationary camera and bounded actor evidence only; rendering, GC, retention, tooling overhead and complete G1B acceptance remain separate.";
        }
        sealed class CameraBinding
        {
            public Camera camera;
            public Transform parent;
            public Rect rect;
            public float depth, fov, size;
            public int display, mask, texture;
            public bool orthographic;
        }
        FrozenManifest frozen;
        ManifestData manifest;
        PerformanceCaptureController capture;
        PerformanceCheckpointImages checkpointImages;
        PerformanceInputReplay replay;
        PerformanceActorRosterContext actorContext, lightContext;
        PerformanceActorLifecycleScope actorScope;
        PerformanceActorCensus.Report before;
        PerformanceAiCreationHistory.Report aiBefore;
        PerformanceAiCreationHistory history;
        PlayerManager player;
        PlayerInputManager input;
        PlayerCamera cameraOwner;
        Camera productionCamera, minimapCamera;
        SystemMiniMap minimap;
        PlayerUIManager uiOwner;
        PlayerUIHudManager hud;
        GameObject pausePanel, skillPanel, minimapTarget;
        WorldSaveGameManager world;
        Unity.Netcode.NetworkManager network;
        PlayerControls controls;
        CameraBinding[] cameras;
        Camera[] cameraScratch;
        Transform[] pathSubjects, pathParents;
        InputAction[] actions;
        bool[] actionStates;
        InputDevice[] originalDevices, originalAssetDevices, leasedDevices;
        InputBinding? originalMask;
        bool hadRestriction;
        string saveOverride;
        int sceneHandle, nextPacket, transitionCount;
        readonly int[] phaseCounts = new int[4];
        readonly int[] lastPhaseFrames = { -1, -1, -1, -1 };
        readonly TransitionRow[] transitions = new TransitionRow[3];
        double[] schedule;
        double startedAt;
        Vector3 initialPosition, initialCameraPosition;
        Quaternion initialRotation;
        bool disposed, ending, ownsUi, replayFinished, lightControl, lightPrepared;
        int lightReleaseFrame;
        public EvidenceReport Evidence { get; private set; }
        public bool IsRunning => !disposed && capture != null && (capture.IsCapturing || checkpointImages != null && checkpointImages.AwaitingFiles);
        public int OwnedRecorderCount => capture == null ? 0 : capture.OwnedRecorderCount;
        public bool Qualified => Evidence?.measurementArm == "checkpoint-images-diagnostic" ? Evidence.checkpointImagesCompleted : Evidence?.measurementArm == "light-control" ? Evidence.lightControlCompleted : Evidence != null && Evidence.routePopulationQualified;
        public bool TimingQualified => Evidence?.routePopulationQualified == true;
        public bool PopulationVerified => Evidence?.measurementArm != "light-control" && Evidence?.population?.accepted == true;
        public object EvidenceObject => Evidence;
        PerformanceUiRouteProcess() { }

        public static PerformanceUiRouteProcess Begin(PerformanceProcessResponse request, PerformanceAiCreationHistory history, PlayerManager player, PlayerInputManager input, PlayerCamera cameraOwner)
        {
            if (Application.isEditor || request?.success != true || request.mode != Mode || history == null || player == null || input == null || cameraOwner == null) throw new InvalidOperationException("Actual standalone UI request and production owners required");
            ValidateRequest(request.config);
            var session = new PerformanceUiRouteProcess();
            try
            {
                string assemblyHash;
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(typeof(PlayerManager).Assembly.Location)) assemblyHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                string distribution = PerformanceCertifiedRouteProcess.ComputeDistributionHash(Path.GetDirectoryName(Application.dataPath));
                session.frozen = ParseManifest(request.config.certifiedManifestJson, request.config.rehearsal.timelineJson, request.config.rehearsal.seedSaveJson, assemblyHash, Application.buildGUID, distribution);
                session.manifest = session.frozen.Snapshot;
                session.lightControl = UsesLightControl(request.config.measurementArm);
                session.Evidence = new EvidenceReport { manifestSha256 = session.frozen.sha256, manifest = session.frozen.Snapshot, measurementArm = session.lightControl ? "light-control" : "full" };
                if (request.config.checkpointImages) session.Evidence.measurementArm = "checkpoint-images-diagnostic";
                session.player = player; session.input = input; session.cameraOwner = cameraOwner; session.history = history;
                session.world = WorldSaveGameManager.instance; session.network = Unity.Netcode.NetworkManager.Singleton;
                session.controls = input.playerControls;
                Require(session.world != null && session.world.SaveRoot == request.saveRoot && session.controls != null && session.network != null, "Actual isolated host and controls required");
                session.saveOverride = session.world.SaveRootOverride; session.sceneHandle = SceneManager.GetActiveScene().handle;
                Require(SceneManager.GetActiveScene().name == session.manifest.scene, "UI route scene mismatch");
                session.productionCamera = cameraOwner.cameraObject;
                session.uiOwner = PlayerUIManager.instance; session.hud = session.uiOwner == null ? null : session.uiOwner.playerUIHudManager;
                Require(session.hud != null && session.productionCamera != null, "Actual HUD and production camera required");
                session.pausePanel = typeof(PlayerUIHudManager).GetField("pauseMenu", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(session.hud) as GameObject;
                session.skillPanel = session.hud.pauseSkillMenu;
                Require(session.pausePanel != null && session.skillPanel != null && TransformPath(session.pausePanel.transform) == session.manifest.pausePanelPath && TransformPath(session.skillPanel.transform) == session.manifest.skillPanelPath, "Frozen production HUD panels required");
                var sources = Object.FindObjectsByType<SystemMiniMap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(m => TransformPath(m.transform) == session.manifest.miniMapPath).ToArray();
                Require(sources.Length == 1, "Exact minimap source missing or ambiguous"); session.minimap = sources[0];
                session.minimapCamera = session.minimap.miniMapCamera; session.minimapTarget = session.minimap.m_Target;
                Require(session.minimapCamera != null && IsOwnedMinimapTarget(session.minimapTarget, player.gameObject, cameraOwner.transform, session.productionCamera.transform) && session.minimap.isActiveAndEnabled && session.minimap == SystemMiniMap.ActiveMiniMap, "Live player minimap source/camera required");
                var loadedCameras = Camera.allCameras;
                Require(loadedCameras.Contains(session.productionCamera) && loadedCameras.Contains(session.minimapCamera) && loadedCameras.Select(c => TransformPath(c.transform)).OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(session.manifest.cameraPaths.OrderBy(p => p, StringComparer.Ordinal)), "Actual camera paths differ from UI manifest");
                session.cameras = loadedCameras.Select(c => new CameraBinding { camera = c, parent = c.transform.parent, rect = c.rect, depth = c.depth, fov = c.fieldOfView, size = c.orthographicSize, orthographic = c.orthographic, display = c.targetDisplay, mask = c.cullingMask, texture = c.targetTexture == null ? 0 : c.targetTexture.GetInstanceID() }).ToArray();
                session.cameraScratch = new Camera[loadedCameras.Length];
                // Freeze ancestry once; polling compares references without path strings or scene scans.
                var subjects = new HashSet<Transform>();
                foreach (var item in loadedCameras.Select(c => c.transform).Concat(new[] { player.transform, cameraOwner.transform, session.hud.transform, session.pausePanel.transform, session.skillPanel.transform, session.minimap.transform }))
                    for (var ancestor = item; ancestor != null; ancestor = ancestor.parent) subjects.Add(ancestor);
                session.pathSubjects = subjects.ToArray(); session.pathParents = session.pathSubjects.Select(t => t.parent).ToArray();
                session.initialPosition = new Vector3(session.manifest.initialPosition[0], session.manifest.initialPosition[1], session.manifest.initialPosition[2]);
                session.initialRotation = new Quaternion(session.manifest.initialCameraRotation[0], session.manifest.initialCameraRotation[1], session.manifest.initialCameraRotation[2], session.manifest.initialCameraRotation[3]);
                session.initialCameraPosition = session.productionCamera.transform.position;
                session.Evidence.initialCameraPosition = new[] { session.initialCameraPosition.x, session.initialCameraPosition.y, session.initialCameraPosition.z };
                session.originalDevices = InputSystem.devices.ToArray(); session.originalMask = session.controls.asset.bindingMask;
                var restrictions = session.controls.asset.devices; session.hadRestriction = restrictions.HasValue;
                session.originalAssetDevices = restrictions.HasValue ? restrictions.Value.ToArray() : Array.Empty<InputDevice>();
                var actionList = new List<InputAction>(); foreach (var action in session.controls.asset) actionList.Add(action);
                session.actions = actionList.ToArray(); session.actionStates = session.actions.Select(a => a.enabled).ToArray();
                session.ValidateOwners(); session.ValidatePhase(0, false);
                session.Evidence.preparationStartedAt = Time.realtimeSinceStartupAsDouble;
                session.ownsUi = true; session.hud.ActivatePause(1); session.ValidatePhase(1, true); session.Evidence.preparationOpened = true;
                session.hud.ActivatePause(1); session.ownsUi = false; session.ValidatePhase(0, true); session.Evidence.preparationClosed = true;
                session.Evidence.preparationEndedAt = Time.realtimeSinceStartupAsDouble;
                var census = PerformanceActorCensus.Capture(RosterArgs); session.before = census.report; session.aiBefore = history.Capture(census);
                Require(session.before.unclassifiedAnimators != null && session.before.unclassifiedAnimators.Length == 0 && session.before.actors != null && session.before.actors.All(a => a.supportedBinding), "UI actor/Animator source coverage incomplete");
                session.actorContext = census.CreateContext();
                session.schedule = PerformanceRouteRehearsal.ValidateSchedule(request.config.rehearsal.timelineJson, 150, .25);
                session.replay = PerformanceInputReplay.Create(new[] { "-karawaraPerformanceReplay" }, session.controls, request.config.rehearsal.timelineJson);
                session.leasedDevices = session.controls.asset.devices.Value.ToArray();
                session.ValidateOwners(); session.ValidatePhase(0, true);
                var root = new GameObject("Explicit UI route timing capture"); Object.DontDestroyOnLoad(root);
                session.capture = root.AddComponent<PerformanceCaptureController>();
                session.capture.CameraIdProvider = session.lightControl ? null : session.ObserveCameraIdentity; session.capture.FinalValidation = session.FinalizeCapture;
                Require(session.capture.Begin(new CaptureConfig { qaEnabled = true, routeId = Route, runId = "ui-certified-", outputRoot = request.config.rehearsal.outputRoot, commit = session.manifest.sourceRevision, buildId = Application.buildGUID + ":" + distribution, camera = session.frozen.sha256, enemyCount = session.manifest.composition.Where(g => g.role == "hostile").Sum(g => g.count), renderScale = 1, warmupSeconds = 30, captureSeconds = 120, lightControl = session.lightControl, counterNames = session.lightControl ? Array.Empty<string>() : session.manifest.counterNames }), "UI capture allocation/recorder setup rejected");
                session.startedAt = session.capture.StartedTimestampSeconds;
                if (request.config.checkpointImages)
                {
                    session.checkpointImages = new PerformanceCheckpointImages(request.config.rehearsal.outputRoot);
                    session.Evidence.checkpointImages = session.checkpointImages.Evidence;
                }
                session.actorScope = PerformanceActorLifecycleScope.Begin(RosterArgs, session.actorContext);
                InputSystem.onBeforeUpdate += session.BeforeInput;
                return session;
            }
            catch { session.Dispose(); throw; }
        }
        public void Poll()
        {
            if (disposed || capture == null) return;
            try {
                if (!capture.IsCapturing) { checkpointImages?.CompletePending(); return; }
                if (lightControl)
                {
                    double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                    if (!lightPrepared && elapsed >= 28) PrepareLightControl(elapsed);
                    if (Evidence.lightObserversReleased && Evidence.lightObserversReleaseEndedElapsed == 0 && Time.frameCount > lightReleaseFrame)
                    {
                        Evidence.lightObserversReleaseEndedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                        Require(Evidence.lightObserversReleaseEndedElapsed < 30, "Light UI deferred observer cleanup crossed measurement");
                    }
                    if (elapsed >= 30) Require(Evidence.lightObserversReleaseEndedElapsed > 0, "Light UI observer cleanup did not finish before measurement");
                }
                ObserveCameraIdentity();
                checkpointImages?.Tick(Time.realtimeSinceStartupAsDouble - startedAt, player.transform, cameraOwner.cameraObject.transform);
            }
            catch (Exception error) { Fail(error); }
        }
        void BeforeInput()
        {
            if (!IsRunning || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            try
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                int due = PerformanceRouteRehearsal.DuePacketCount(schedule, nextPacket, elapsed, manifest.maxLatenessSeconds);
                replayFinished = replay.Advance(elapsed); nextPacket += due;
            }
            catch (Exception error) { Fail(error); }
        }
        string ObserveCameraIdentity()
        {
            using (PerformanceProfilerAttribution.UiMarker.Auto())
            {
            ValidateOwners();
            double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            int phase = ExpectedPhase(elapsed);
            ValidatePhase(transitionCount, true);
            if (phase != transitionCount)
            {
                Require(phase == transitionCount + 1 && transitionCount < 3, "UI phase skipped or replayed");
                double scheduled = 60 + 30 * transitionCount;
                Require(elapsed - scheduled <= manifest.maxLatenessSeconds, "UI transition exceeded frozen lateness");
                int action = ActionForTransition(transitionCount);
                ownsUi = true; hud.ActivatePause(action);
                double actual = Time.realtimeSinceStartupAsDouble - startedAt;
                transitions[transitionCount] = new TransitionRow { index = transitionCount, action = action, frame = Time.frameCount, scheduledElapsed = scheduled, actualElapsed = actual, lateness = actual - scheduled };
                transitionCount++;
                if (transitionCount == 3) ownsUi = false;
                Require(actual - scheduled <= manifest.maxLatenessSeconds, "Production UI transition completed late");
                ValidatePhase(transitionCount, true);
            }
            if (lastPhaseFrames[phase] != Time.frameCount) { lastPhaseFrames[phase] = Time.frameCount; phaseCounts[phase]++; }
            return frozen.sha256;
        
            }
        }
        void ValidateOwners()
        {
            Require(!UnityEngine.Profiling.Profiler.enabled && !UnityEngine.Profiling.Profiler.enableAllocationCallstacks, "Profiler instrumentation active during UI timing");
            Require(Application.isFocused, "UI application focus changed");
            Require(player != null && player.IsOwner && player.IsSpawned && !player.isDead.Value && player.playerNetworkManager.currentHealth.Value > 0, "UI player owner/spawn/living state changed");
            Require(world != null && world == WorldSaveGameManager.instance && world.player == player && world.SaveRootOverride == saveOverride, "UI world/save owner changed");
            Require(network != null && network == Unity.Netcode.NetworkManager.Singleton && network.IsListening && network.IsHost && network.LocalClient?.PlayerObject == player.NetworkObject, "UI network host/player owner changed");
            Require(input == PlayerInputManager.instance && input.player == player && input.playerControls == controls, "UI input owner changed");
            Require(cameraOwner == PlayerCamera.instance && cameraOwner.player == player && cameraOwner.canFollow && cameraOwner.cameraObject == productionCamera, "UI production camera owner/follow state changed");
            Require(SceneManager.GetActiveScene().handle == sceneHandle, "UI active scene changed");
            Require(uiOwner != null && uiOwner == PlayerUIManager.instance && uiOwner.playerUIHudManager == hud && hud != null && hud.isActiveAndEnabled && pausePanel != null && skillPanel != null && hud.pauseSkillMenu == skillPanel && minimap != null && minimap == SystemMiniMap.ActiveMiniMap && minimap.isActiveAndEnabled && minimap.miniMapCamera == minimapCamera && minimap.m_Target == minimapTarget && IsOwnedMinimapTarget(minimapTarget, player.gameObject, cameraOwner.transform, productionCamera.transform), "UI HUD/minimap source identities changed");
            Require(Vector3.Distance(initialPosition, player.transform.position) <= manifest.positionTolerance && StationaryCameraPositionMatches(initialCameraPosition, productionCamera.transform.position, manifest.positionTolerance) && Quaternion.Angle(initialRotation, productionCamera.transform.rotation) <= manifest.cameraAngleTolerance, "Stationary UI player/camera pose exceeded frozen bounds");
            for (int i = 0; i < pathSubjects.Length; i++) Require(pathSubjects[i] != null && pathSubjects[i].parent == pathParents[i], "UI owner ancestry changed");
            Require(Camera.allCamerasCount == cameras.Length && Camera.GetAllCameras(cameraScratch) == cameras.Length, "UI camera set changed");
            foreach (var binding in cameras)
            {
                var camera = binding.camera;
                Require(camera != null && camera.isActiveAndEnabled && Array.IndexOf(cameraScratch, camera) >= 0 && camera.transform.parent == binding.parent && camera.rect == binding.rect && camera.depth == binding.depth && camera.fieldOfView == binding.fov && camera.orthographic == binding.orthographic && camera.orthographicSize == binding.size && camera.targetDisplay == binding.display && camera.cullingMask == binding.mask && (camera.targetTexture == null ? 0 : camera.targetTexture.GetInstanceID()) == binding.texture, "Frozen UI camera rendering context changed");
            }
            if (leasedDevices != null && replay != null)
            {
                var devices = controls.asset.devices;
                Require(controls.asset.bindingMask == null && devices.HasValue && devices.Value.Count == leasedDevices.Length, "Neutral UI replay restriction ownership lost");
                for (int i = 0; i < leasedDevices.Length; i++) Require(leasedDevices[i] != null && leasedDevices[i].added && devices.Value[i] == leasedDevices[i], "Neutral UI replay devices changed");
            }
            actorContext?.ReadLivingHostileCount();
        }
        void ValidatePhase(int phase, bool cursor)
        {
            bool paused = phase == 1 || phase == 2;
            Require(pausePanel.activeSelf == (phase == 1) && skillPanel.activeSelf == (phase == 2) && pausePanel.activeInHierarchy == (phase == 1) && skillPanel.activeInHierarchy == (phase == 2) && Time.timeScale == (paused ? 0 : 1) && controls.PlayerMovement.enabled == !paused && controls.PlayerActions.enabled == !paused && controls.PlayerCamera.enabled, "Unexpected production UI panel/timeScale/action-map state");
            if (replay != null)
                foreach (var action in actions)
                    Require(action.enabled == (!paused || action.actionMap != controls.PlayerMovement.Get() && action.actionMap != controls.PlayerActions.Get()), "UI replay action enablement ownership changed");
            if (cursor) Require(Cursor.lockState == (paused ? CursorLockMode.None : CursorLockMode.Locked) && Cursor.visible == paused, "Unexpected production UI cursor state");
        }
        bool NeutralReleased()
        {
            if (!replayFinished || nextPacket != schedule.Length || controls.PlayerMovement.Movement.ReadValue<Vector2>() != Vector2.zero || controls.PlayerCamera.Movement.ReadValue<Vector2>() != Vector2.zero || input.moveAmount != 0 || input.horizontal_Input != 0 || input.vertical_Input != 0 || input.cameraHorizontal_Input != 0 || input.cameraVertical_Input != 0) return false;
            foreach (var action in actions) if (action.type == InputActionType.Button && action.IsPressed()) return false;
            return true;
        }
        void Fail(Exception error)
        {
            Evidence?.errors.Add(error.ToString());
            if (capture != null && capture.IsCapturing) capture.End();
        }
        void FinalizeCapture(CaptureResult result)
        {
            if (ending) return; ending = true; InputSystem.onBeforeUpdate -= BeforeInput;
            Evidence.capture = result;
            result.populationVerified = false; result.cameraVerified = false;
            try
            {
                if (checkpointImages != null)
                {
                    checkpointImages.Tick(Time.realtimeSinceStartupAsDouble - startedAt, player.transform, cameraOwner.cameraObject.transform);
                    Evidence.unexpectedConditionsChanged = result.conditionsChanged;
                    result.conditionsChanged = true;
                    result.validationUnavailableReasons.Add("Explicit internal checkpoint image diagnostic: never eligible for baseline timing.");
                }
                ObserveCameraIdentity();
                Require(Time.realtimeSinceStartupAsDouble - startedAt >= 150 && transitionCount == 3 && phaseCounts.All(n => n > 0), "Missing full UI clock/transition/phase proof");
                Evidence.finalRelease = NeutralReleased();
                Require(Evidence.finalRelease, "Missing observed UI neutral release");
                Evidence.stationaryCameraVerified = true;
                if (lightControl) { FinalizeLightControl(result); return; }
                var after = PerformanceActorCensus.Capture(RosterArgs); var validation = new CaptureResult(); actorContext.Validate(validation);
                Require(after.report.unclassifiedAnimators != null && after.report.unclassifiedAnimators.Length == 0, "Postflight UI Animator coverage missing");
                Evidence.populationEvidence = new PerformanceBoundedRoutePopulation.PhaseObservation { before = before, after = after.report, lifecycle = actorScope.CaptureHistory(), aiBefore = aiBefore, aiAfter = history.Capture(after), contextViolationMask = actorContext.SourceViolationMask };
                Evidence.population = PerformanceBoundedRoutePopulation.EvaluatePhase(new PerformanceBoundedRoutePopulation.PhaseContract { id = Route, scene = manifest.scene, fromTime = startedAt, untilTime = startedAt + 150, boundaryTolerance = manifest.maxLatenessSeconds, composition = manifest.composition }, Evidence.populationEvidence);
                if (validation.conditionsChanged) result.errors.AddRange(validation.errors);
                if (!Evidence.population.accepted) result.errors.AddRange(Evidence.population.reasons);
                result.populationVerified = Evidence.population.accepted && !validation.conditionsChanged;
                result.cameraVerified = Evidence.stationaryCameraVerified;
            }
            catch (Exception error) { Evidence.errors.Add(error.ToString()); result.errors.Add("UI post-sampling evidence failed: " + error.Message); result.populationVerified = false; result.cameraVerified = false; }
            finally
            {
                Evidence.phaseTransitions = transitions.Take(transitionCount).ToArray(); Evidence.phaseObservedFrames = (int[])phaseCounts.Clone();
                try { ReleaseControls(); }
                catch (Exception error) { Evidence.errors.Add(error.ToString()); result.errors.Add("UI release/restoration failed: " + error.Message); }
                finally { actorScope?.Dispose(); actorContext?.Dispose(); lightContext?.Dispose(); }
                if (!Evidence.controlsRestored || Evidence.errors.Count != 0) result.errors.Add("Actual UI controls or route completion proof failed");
            }
        }
        void ReleaseControls()
        {
            // Close owned production UI before replay restores original action enablement.
            try
            {
                if (ownsUi && hud != null)
                {
                    if (skillPanel != null && skillPanel.activeSelf) hud.ActivatePause(2);
                    else if (pausePanel != null && pausePanel.activeSelf) hud.ActivatePause(1);
                    ownsUi = false;
                }
            }
            finally
            {
                if (replay != null) { Evidence.dispatch = replay.DispatchTimeline(); replay.Dispose(); replay = null; }
                if (controls != null && actions != null)
                {
                    // Preparation uses production EnableInput; restore the state from before that pair as well.
                    for (int i = 0; i < actions.Length; i++)
                        if (actionStates[i]) actions[i].Enable(); else actions[i].Disable();
                    Evidence.controlsRestored = controls.asset.bindingMask.Equals(originalMask) && InputSystem.devices.SequenceEqual(originalDevices) && controls.asset.devices.HasValue == hadRestriction && (!hadRestriction || controls.asset.devices.Value.SequenceEqual(originalAssetDevices)) && actions.Select(a => a.enabled).SequenceEqual(actionStates);
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
                finally
                {
                    actorScope?.Dispose(); actorContext?.Dispose(); lightContext?.Dispose();
                    if (capture != null) { capture.FinalValidation = null; capture.CameraIdProvider = null; Object.Destroy(capture.gameObject); capture = null; }
                }
            }
        }
        public static bool UsesLightControl(string arm)
        {
            Require(arm == null || arm == "full" || arm == "light-control", "Unknown UI measurement arm");
            return arm == "light-control";
        }
        public static bool LightControlComplete(EvidenceReport row)
        {
            if (row == null || row.measurementArm != "light-control" || row.manifest?.scene != "Tutorial_Final" || row.capture == null || !row.capture.fullWindowCompleted || row.capture.conditionsChanged || row.capture.errors.Count != 0 || row.errors.Count != 0 ||
                !row.preparationOpened || !row.preparationClosed || !row.stationaryCameraVerified || !row.finalRelease || !row.controlsRestored || !row.lightObserversReleased || !row.lightStructureUnchanged || !row.lightPrePostGuardPassed ||
                !Finite(row.lightPreparationStartedElapsed) || !Finite(row.lightPreparationEndedElapsed) || row.lightPreparationStartedElapsed < 28 || row.lightPreparationStartedElapsed > row.lightPreparationEndedElapsed || row.lightPreparationEndedElapsed >= 30 ||
                !Finite(row.lightObserversReleaseStartedElapsed) || !Finite(row.lightObserversReleaseEndedElapsed) || row.lightObserversReleaseStartedElapsed < row.lightPreparationEndedElapsed || row.lightObserversReleaseStartedElapsed >= row.lightObserversReleaseEndedElapsed || row.lightObserversReleaseEndedElapsed >= 30 ||
                row.phaseTransitions == null || row.phaseTransitions.Length != 3 || row.phaseObservedFrames == null || row.phaseObservedFrames.Length != 4 || row.phaseObservedFrames.Any(n => n <= 0)) return false;
            for (int i = 0; i < 3; i++)
            {
                var transition = row.phaseTransitions[i];
                double scheduled = 60 + 30 * i;
                if (transition.index != i || transition.action != ActionForTransition(i) || transition.frame < 0 || i > 0 && transition.frame <= row.phaseTransitions[i - 1].frame || transition.scheduledElapsed != scheduled ||
                    !Finite(transition.actualElapsed) || !Finite(transition.lateness) || transition.actualElapsed < scheduled || transition.actualElapsed - scheduled > .25 || transition.lateness < 0 || transition.lateness > .25 || Math.Abs(transition.actualElapsed - scheduled - transition.lateness) > .000001) return false;
            }
            return true;
        }
        bool LightSettingsMatch() => Screen.width == manifest.width && Screen.height == manifest.height && QualitySettings.names[QualitySettings.GetQualityLevel()] == manifest.quality && QualitySettings.vSyncCount == manifest.vsync && Application.targetFrameRate == manifest.targetFrameRate && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - manifest.renderScale) < .0001;
        void PrepareLightControl(double elapsed)
        {
            Require(elapsed < 30, "Light UI preparation missed premeasurement window");
            Evidence.lightPreparationStartedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            ValidateOwners(); ValidatePhase(0, true);
            Require(LightSettingsMatch(), "Light UI warm rendering settings changed");
            var census = PerformanceActorCensus.Capture(RosterArgs);
            Require(PerformanceCertifiedRouteProcess.LightStructuralMatch(census.report, census.report, manifest.composition), "Light UI warm census differs from frozen composition");
            lightContext = census.CreateContext(); var validation = new CaptureResult(); lightContext.Validate(validation);
            Require(!validation.conditionsChanged, "Light UI warm structural context changed");
            Evidence.lightWarmupCensus = census.report; Evidence.lightWarmupAi = history.Capture(census);
            // A zero-AI UI census must carry an exact empty creation history; the
            // generic nonempty-match flag deliberately stays false for that case.
            PerformanceBoundedRoutePopulation.ValidateObservedAiHistory(Evidence.lightWarmupAi, census.report);
            Evidence.lightWarmupLifecycle = actorScope.CaptureHistory();
            Evidence.lightPreparationEndedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Require(Evidence.lightPreparationEndedElapsed < 30, "Light UI preparation crossed measurement");
            lightPrepared = true;
            Evidence.lightObserversReleaseStartedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Require(Evidence.lightObserversReleaseStartedElapsed < 30, "Light UI observer release crossed measurement");
            actorScope?.Dispose(); actorContext?.Dispose(); history.Dispose(); actorScope = null; actorContext = null;
            capture.CameraIdProvider = null; capture.EnemyCountProvider = null;
            lightReleaseFrame = Time.frameCount; Evidence.lightObserversReleased = true;
        }
        void FinalizeLightControl(CaptureResult result)
        {
            Require(lightPrepared && Evidence.lightObserversReleased && Evidence.lightObserversReleaseEndedElapsed > 0, "Light UI preparation/release incomplete");
            var after = PerformanceActorCensus.Capture(RosterArgs);
            var warmValidation = new CaptureResult(); lightContext.Validate(warmValidation);
            using (var postContext = after.CreateContext())
            {
                var postValidation = new CaptureResult(); postContext.Validate(postValidation);
                Evidence.lightStructureUnchanged = !warmValidation.conditionsChanged && !postValidation.conditionsChanged && PerformanceCertifiedRouteProcess.LightStructuralMatch(Evidence.lightWarmupCensus, after.report, manifest.composition);
            }
            Evidence.lightPostCensus = after.report; Evidence.lightPrePostGuardPassed = LightSettingsMatch();
            Evidence.populationEvidence = new PerformanceBoundedRoutePopulation.PhaseObservation { before = Evidence.lightWarmupCensus, after = after.report, aiBefore = Evidence.lightWarmupAi, contextViolationMask = lightContext.SourceViolationMask };
            Evidence.population = new PerformanceBoundedRoutePopulation.DomainVerdict("Light UI warm/post endpoints only; measured transient creation/despawn provenance and population acceptance unavailable.");
            result.populationVerified = false; result.cameraVerified = false; result.comparisonValid = false;
            result.validationUnavailableReasons.Add("Light UI diagnostic preserves actual authored phases and endpoint structure; continuous actor/proof/history scans and native counters disabled before measurement.");
            if (!Evidence.lightStructureUnchanged || !Evidence.lightPrePostGuardPassed) result.errors.Add("Light UI endpoint structure/settings changed");
        }
        public static bool StationaryCameraPositionMatches(Vector3 initial, Vector3 actual, double tolerance) => Finite(tolerance) && tolerance >= 0 && Vector3.Distance(initial, actual) <= tolerance;
        public static bool IsOwnedMinimapTarget(GameObject target, GameObject playerRoot, Transform cameraOwnerRoot, Transform productionCamera)
        {
            return target != null && playerRoot != null && cameraOwnerRoot != null && productionCamera != null &&
                (target == playerRoot || target.transform == productionCamera.parent && target.transform.IsChildOf(cameraOwnerRoot) && target.CompareTag("TargetPlayerMiniMap"));
        }
        static bool ValidPath(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 1024;
        static bool UniquePaths(string[] paths, int maximum) => paths != null && paths.Length > 0 && paths.Length <= maximum && paths.All(ValidPath) && paths.Distinct(StringComparer.Ordinal).Count() == paths.Length;
        static bool HashMatches(string expected, string actual) => expected != null && actual != null && expected.Length == 64 && actual.Length == 64 && expected.All(Uri.IsHexDigit) && actual.All(Uri.IsHexDigit) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static string TransformPath(Transform value) { string path = value.name; while (value.parent != null) { value = value.parent; path = value.name + "/" + path; } return path; }
        static void Require(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }
    }
#endif
}

