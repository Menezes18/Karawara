using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    // Explicit certified-route coordinator. Parsing alone never grants observed success.
    public sealed class PerformanceCertifiedRouteProcess : IPerformanceTimedSession
    {
        [Serializable] public sealed class ManifestData
        {
            public int schemaVersion, width, height, vsync, targetFrameRate, missionChoice, expectedCreations;
            public string routeId, scene, buildAssemblySha256, playerBuildGuid, distributionSha256, timelineSha256, seedSha256, quality, cameraMode, missionNpcPath, sourceRevision, pausePolicy;
            public double warmupSeconds, captureSeconds, renderScale, maxLatenessSeconds, cameraTurnDegreesPerSecond, transitionEarliestSeconds, transitionLatestSeconds;
            public string[] cameraPaths, counterNames, allowedTutorialPaths;
            public PerformanceBoundedRoutePopulation.ActorGroup[] initialComposition, settledComposition;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public RehearsalPoseCheckpoint[] poseCheckpoints;
        }
        public sealed class FrozenManifest
        {
            readonly string json;
            public readonly string sha256;
            internal FrozenManifest(ManifestData data) { json = JsonConvert.SerializeObject(data); sha256 = Hash(json); }
            public ManifestData Snapshot => JsonConvert.DeserializeObject<ManifestData>(json);
        }
        public static FrozenManifest ParseManifest(string json, string timelineJson, string seedJson, string actualBuildAssemblySha256, string actualPlayerBuildGuid, string actualDistributionSha256)
        {
            try
            {
                Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 262144, "Bounded explicit certified manifest required");
                var fields = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                // Require every declared field explicitly, including settings whose defaults might otherwise look valid.
                foreach (var field in typeof(ManifestData).GetFields())
                    if (field.Name != nameof(ManifestData.poseCheckpoints)) Require(fields[field.Name] != null && fields[field.Name].Type != JTokenType.Null, "Missing manifest field: " + field.Name);
                var data = fields.ToObject<ManifestData>(JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }));
                bool tutorial = data.routeId == "tutorial-exploration-v1" && data.scene == "Tutorial_Final";
                Require(data.schemaVersion == 1 && (tutorial || data.routeId == "video1-exploration-baseline-v1" && data.scene == "Video1"), "Unsupported certified route/schema");
                Require(data.sourceRevision != null && data.sourceRevision.Length == 40 && data.sourceRevision.All(Uri.IsHexDigit), "Explicit source revision required");
                Require(data.counterNames != null && data.counterNames.Length > 0 && data.counterNames.Length <= 64 && data.counterNames.All(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 256) && data.counterNames.Distinct(StringComparer.Ordinal).Count() == data.counterNames.Length, "Bounded unique counter selection required");
                Require(ValidHash(actualBuildAssemblySha256) && ValidHash(data.buildAssemblySha256) && string.Equals(actualBuildAssemblySha256, data.buildAssemblySha256, StringComparison.OrdinalIgnoreCase), "Manifest build identity mismatch");
                Require(Guid.TryParseExact(actualPlayerBuildGuid, "N", out _) && string.Equals(actualPlayerBuildGuid, data.playerBuildGuid, StringComparison.OrdinalIgnoreCase), "Manifest Player build GUID mismatch");
                Require(ValidHash(actualDistributionSha256) && ValidHash(data.distributionSha256) && string.Equals(actualDistributionSha256, data.distributionSha256, StringComparison.OrdinalIgnoreCase), "Manifest complete Player distribution mismatch");
                Require(ValidHash(data.timelineSha256) && string.Equals(Hash(timelineJson), data.timelineSha256, StringComparison.OrdinalIgnoreCase), "Manifest input identity mismatch");
                Require(ValidHash(data.seedSha256) && string.Equals(Hash(seedJson), data.seedSha256, StringComparison.OrdinalIgnoreCase), "Manifest seed identity mismatch");
                Require(data.warmupSeconds == 30 && data.captureSeconds == 120 && data.width == 1920 && data.height == 1080 && data.quality == "High Fidelity" && data.vsync == 0 && data.targetFrameRate == -1 && data.renderScale == 1, "Fixed timing and rendering settings required");
                Require(Finite(data.maxLatenessSeconds) && data.maxLatenessSeconds > 0 && data.maxLatenessSeconds <= .25, "Bounded input lateness required");
                PerformanceRouteRehearsal.ValidateSchedule(timelineJson, 150, data.maxLatenessSeconds);
                if (tutorial) PerformanceProcessHarness.ParseTutorialSeed(seedJson); else PerformanceProcessHarness.ParseVideoSeed(seedJson);
                if (tutorial || data.poseCheckpoints != null && data.poseCheckpoints.Length != 0)
                    PerformanceRoutePoseProof.Create(data.poseCheckpoints, data.maxLatenessSeconds);
                var timeline = JObject.Parse(timelineJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Require(data.cameraMode == "movement-heading-v1" && (string)timeline["cameraMode"] == data.cameraMode &&
                    Finite(data.cameraTurnDegreesPerSecond) && data.cameraTurnDegreesPerSecond >= 10 && data.cameraTurnDegreesPerSecond <= 90 &&
                    (double?)timeline["cameraTurnDegreesPerSecond"] == data.cameraTurnDegreesPerSecond, "Frozen movement-following camera settings required");
                Require(timeline["waypointGroups"] is JObject waypoints && waypoints.Count > 0, "Explicit spatial route required");
                Require(data.cameraPaths != null && data.cameraPaths.Length > 0 && data.cameraPaths.Length <= 8 && data.cameraPaths.All(p => !string.IsNullOrWhiteSpace(p) && p.Length <= 1024) && data.cameraPaths.Distinct(StringComparer.Ordinal).Count() == data.cameraPaths.Length, "Unique expected camera paths required");
                Require(data.pausePolicy == "authored-immediate-v1" && data.allowedTutorialPaths != null && data.allowedTutorialPaths.Length <= 32 && data.allowedTutorialPaths.All(p => !string.IsNullOrWhiteSpace(p) && p.Length <= 1024) && data.allowedTutorialPaths.Distinct(StringComparer.Ordinal).Count() == data.allowedTutorialPaths.Length, "Frozen typed tutorial pause policy required");
                Require(!string.IsNullOrWhiteSpace(data.missionNpcPath) && data.missionNpcPath.Length <= 1024 && (data.missionChoice == 1 || data.missionChoice == 2) && data.expectedCreations > 0 && data.expectedCreations <= 1024, "Explicit typed mission owner/choice required");
                Require(Finite(data.transitionEarliestSeconds) && Finite(data.transitionLatestSeconds) && data.transitionEarliestSeconds >= 0 && data.transitionEarliestSeconds < data.transitionLatestSeconds && data.transitionLatestSeconds < 150, "Ordered bounded transition window required");
                if (tutorial) Require(data.missionChoice == 1 && data.expectedCreations == 1 && data.transitionLatestSeconds <= 30 &&
                    data.initialComposition != null && data.initialComposition.All(g => g != null && g.role != "hostile") && data.settledComposition != null &&
                    data.settledComposition.Any(g => g != null && g.role == "hostile" && g.primaryType == typeof(AISaciCharacterManager).FullName && g.count == 1),
                    "Tutorial requires its observed one-Saci preparation transition before30s.");
                PerformanceBoundedRoutePopulation.ValidateGroups(data.initialComposition);
                PerformanceBoundedRoutePopulation.ValidateGroups(data.settledComposition);
                Require(data.settledComposition.Sum(g => g.count) - data.initialComposition.Sum(g => g.count) == data.expectedCreations, "Mission generation count mismatch");
                foreach (var initial in data.initialComposition)
                {
                    var settled = data.settledComposition.SingleOrDefault(g => Key(g) == Key(initial));
                    Require(settled != null && settled.count >= initial.count && (initial.role == "hostile" || settled.count == initial.count), "Unexpected original composition change");
                }
                foreach (var settled in data.settledComposition)
                {
                    var initial = data.initialComposition.SingleOrDefault(g => Key(g) == Key(settled));
                    if (initial == null || settled.count != initial.count)
                    {
                        var type = typeof(AICharacterManager).Assembly.GetType(settled.primaryType, false);
                        Require(type != null && typeof(AICharacterManager).IsAssignableFrom(type) && settled.role == "hostile" && settled.active == true && settled.enabled == true && settled.spawned == true, "Only declared active production AI additions allowed");
                    }
                }
                return new FrozenManifest(data);
            }
            catch (ArgumentException) { throw; }
            catch (Exception error) { throw new ArgumentException("Invalid certified manifest: " + error.Message, error); }
        }
        // Heavy reconciliation occurs after sampling. The journal covers the whole
        // route, so the expected generation needs no census/rebinding in a timed frame.
        public static PerformanceBoundedRoutePopulation.DomainVerdict ReconcilePopulation(FrozenManifest frozen, double startedAt,
            PerformanceBoundedRoutePopulation.PhaseObservation observed, PerformanceMissionProducerInventory.Row[] declarations)
        {
            try
            {
                Require(frozen != null && Finite(startedAt) && startedAt >= 0 && observed?.before?.actors != null && observed.lifecycle?.initiallyBoundSubjects != null, "Missing actual route/population evidence");
                var manifest = frozen.Snapshot;
                var owners = observed.before.actors.Where(a => a.path == manifest.missionNpcPath && a.role == "ambient-npc" && a.components != null && a.components.Contains(typeof(NPCDialogue).FullName)).ToArray();
                Require(owners.Length == 1, "Actual typed mission owner missing or ambiguous");
                var subjects = observed.lifecycle.initiallyBoundSubjects.Where(s => s.ownerRootId == owners[0].objectId && s.componentType == typeof(NPCDialogue).FullName).ToArray();
                Require(subjects.Length == 1, "Exact initial NPC component binding missing or ambiguous");
                Require(declarations != null && declarations.Length > 0 && declarations.Length <= 1024, "Pre-input production declarations missing");
                var selected = declarations.Where(d => d.npcId == subjects[0].componentId && d.choiceIndex == manifest.missionChoice).ToArray();
                Require(selected.Length == manifest.expectedCreations && selected.All(d => string.IsNullOrEmpty(d.issue) && d.choiceRootId != 0 && d.missionId != 0 && d.existingCreatedId == 0 &&
                    d.frame <= observed.before.frame && d.frame >= 0 && Finite(d.time) && d.time >= 0 && d.time <= observed.lifecycle.startedTime) &&
                    selected.Select(d => d.choiceRootId).Distinct().Count() == 1 && selected.Select(d => d.missionId).Distinct().Count() == 1, "Late, ambiguous or preexisting selected production references");
                Require(observed.aiBefore?.attempts != null && observed.aiAfter?.attempts != null && observed.aiAfter.events != null, "Actual production history missing");
                var oldTokens = observed.aiBefore.attempts.Select(a => a.token).ToArray();
                var added = observed.aiAfter.attempts.Where(a => !oldTokens.Contains(a.token)).ToArray();
                Require(added.Length == manifest.expectedCreations, "Observed production count differs from manifest");
                foreach (var attempt in added)
                {
                    var events = observed.aiAfter.events.Where(e => e.token == attempt.token).ToArray();
                    Require(events.Length > 0 && events.All(e => e.time - startedAt >= manifest.transitionEarliestSeconds && e.time - startedAt <= manifest.transitionLatestSeconds), "Production outside frozen route creation window");
                }
                var expected = new PerformanceBoundedRoutePopulation.TransitionContract {
                    phase = new PerformanceBoundedRoutePopulation.PhaseContract { id = manifest.routeId, scene = manifest.scene,
                        fromTime = startedAt, untilTime = startedAt + manifest.warmupSeconds + manifest.captureSeconds,
                        boundaryTolerance = manifest.maxLatenessSeconds, composition = manifest.initialComposition },
                    resultingComposition = manifest.settledComposition,
                    creations = selected.Select(d => new PerformanceBoundedRoutePopulation.SpawnExpectation { spawnerId = d.spawnerId, prefabId = d.prefabId, primaryType = d.primaryType, count = 1 }).ToArray() };
                return PerformanceBoundedRoutePopulation.EvaluateTransition(expected, observed);
            }
            catch (Exception error) { return new PerformanceBoundedRoutePopulation.DomainVerdict(error.GetType().Name + ": " + error.Message); }
        }
        public sealed class EvidenceReport
        {
            public string measurementArm = "full";
            public bool lightObserversReleased, lightStructureUnchanged, lightPrePostGuardPassed;
            public double lightPreparationStartedElapsed, lightPreparationEndedElapsed;
            public double lightObserversReleaseStartedElapsed, lightObserversReleaseEndedElapsed;
            public PerformanceActorCensus.Report lightWarmupCensus, lightPostCensus;
            public PerformanceAiCreationHistory.Report lightWarmupAi;
            public PerformanceActorLifecycleScope.HistoryReport lightWarmupLifecycle;
            public readonly string lightBoundary = "Light control retains actual replay and frozen poses; full endpoint structure/guards are outside measurement. Detailed source observers stop after warm-up. Endpoints cannot establish transient creation/despawn closure or population/timing acceptance.";
            public bool lightControlCompleted => LightCompletion(this);
            public string manifestSha256;
            public ManifestData manifest;
            public CaptureResult capture;
            public PerformanceBoundedRoutePopulation.DomainVerdict population;
            public PerformanceBoundedRoutePopulation.PhaseObservation populationEvidence;
            public PerformanceMissionProducerInventory.Row[] producerDeclarations;
            public double[][] waypointArrivals, dispatch;
            public bool moved, jumped, cameraMoved, finalRelease, controlsRestored;
            public int guardedFrames;
            public PlannedTutorialJournal.Row[] plannedTutorials;
            public PerformanceProfilerAttribution.Report attribution;
            public PerformanceCheckpointImages.Report checkpointImages;
            public PerformanceRoutePoseProof.Report poses;
            public bool? unexpectedConditionsChanged;
            public readonly List<string> errors = new List<string>();
            bool PoseQualified => manifest == null || manifest.scene != "Tutorial_Final" && (manifest.poseCheckpoints == null || manifest.poseCheckpoints.Length == 0) || poses != null && poses.completed && poses.closurePassed && !poses.faulted;
            public bool routePopulationQualified => measurementArm == "full" && PoseQualified && attribution == null && checkpointImages == null && capture != null && capture.comparisonValid && population != null && population.accepted && moved && jumped && cameraMoved && finalRelease && controlsRestored && errors.Count == 0;
            public bool checkpointImagesCompleted => measurementArm == "checkpoint-images-diagnostic" && manifest?.scene == "Tutorial_Final" && checkpointImages != null && checkpointImages.completed && checkpointImages.requests.Count == 6 && PoseQualified && attribution == null && unexpectedConditionsChanged == false && capture != null && capture.fullWindowCompleted && capture.conditionsChanged && !capture.comparisonValid && capture.errors.Count == 0 && population != null && population.accepted && moved && jumped && cameraMoved && finalRelease && controlsRestored && errors.Count == 0;
            public bool attributionRouteCompleted => PoseQualified && attribution != null && attribution.completed && attribution.restored && !attribution.ownershipLost && unexpectedConditionsChanged == false && capture != null && capture.fullWindowCompleted && capture.errors.Count == 0 && population != null && population.accepted && moved && jumped && cameraMoved && finalRelease && controlsRestored && errors.Count == 0;
            public readonly string boundary = "Timing/route/bounded actor evidence only. Cleanup, tooling overhead, UI/rendering/GC/retention and complete G1B audit are separate.";
        }
        sealed class CameraBinding
        {
            public Camera camera;
            public Rect viewport;
            public float depth, fieldOfView;
            public int display, mask, texture;
        }
        public sealed class PlannedTutorialJournal
        {
            struct Entry { public int bindingIndex, componentId, frame; public double elapsed; }
            public sealed class Row { public string path; public int componentId, frame; public double elapsed; }
            readonly PopUpTutorial[] owners;
            readonly GameObject[] views;
            readonly BoxCollider[] colliders;
            readonly string[] paths;
            readonly int[] ids;
            readonly bool[] allowed, recorded;
            readonly Entry[] entries;
            int count;
            public PlannedTutorialJournal(PopUpTutorial[] actualOwners, string[] allowedPaths)
            {
                Require(actualOwners != null && actualOwners.Length <= 128 && actualOwners.All(p => p != null) && allowedPaths != null && allowedPaths.Length <= 32, "Bounded actual tutorial bindings required");
                owners = (PopUpTutorial[])actualOwners.Clone(); paths = owners.Select(p => TransformPath(p.transform)).ToArray();
                Require(allowedPaths.All(path => paths.Count(p => p == path) == 1), "Frozen tutorial binding missing or ambiguous");
                ids = owners.Select(p => p.GetInstanceID()).ToArray(); views = owners.Select(p => p.UI).ToArray(); colliders = owners.Select(p => p.GetComponent<BoxCollider>()).ToArray();
                allowed = paths.Select(p => allowedPaths.Contains(p)).ToArray(); recorded = new bool[owners.Length]; entries = new Entry[allowedPaths.Length];
            }
            // IDs, paths and references are resolved before warm-up. No DTO/string/boxing here.
            public void Record(int index, int frame, double elapsed)
            {
                Require(index >= 0 && index < owners.Length && allowed[index] && !recorded[index] && count < entries.Length && frame >= 0 && Finite(elapsed) && elapsed >= 0 &&
                    (count == 0 || entries[count - 1].frame <= frame && entries[count - 1].elapsed <= elapsed), "Invalid, duplicate or overflowing planned tutorial event");
                entries[count++] = new Entry { bindingIndex = index, componentId = ids[index], frame = frame, elapsed = elapsed }; recorded[index] = true;
            }
            public void CloseIfPaused(int frame, double elapsed)
            {
                if (Time.timeScale != 0) return;
                int found = -1;
                for (int i = 0; i < owners.Length; i++)
                    if (owners[i] != null && !owners[i].passou && views[i] != null && views[i].activeInHierarchy && colliders[i] != null && !colliders[i].enabled)
                    { Require(found == -1, "Ambiguous tutorial pause"); found = i; }
                Require(found >= 0 && allowed[found] && !recorded[found] && count < entries.Length, "Unknown or repeated tutorial pause");
                owners[found].close(); // Planned production gameplay cost remains part of the route.
                Require(Time.timeScale == 1 && owners[found].passou && !views[found].activeInHierarchy, "Authored planned close did not restore gameplay");
                Record(found, frame, elapsed);
            }
            public Row[] Capture()
            {
                var result = new Row[count];
                for (int i = 0; i < count; i++) result[i] = new Row { path = paths[entries[i].bindingIndex], componentId = entries[i].componentId, frame = entries[i].frame, elapsed = entries[i].elapsed };
                return result;
            }
        }
        FrozenManifest frozen;
        ManifestData manifest;
        PerformanceCaptureController capture;
        PerformanceInputReplay replay;
        PerformanceRoutePoseProof poseProof;
        PerformanceActorRosterContext actorContext;
        PerformanceActorRosterContext lightContext;
        PerformanceActorLifecycleScope actorScope;
        PerformanceAiCreationHistory history;
        PerformanceActorCensus.Report before;
        PerformanceAiCreationHistory.Report aiBefore;
        PlayerManager player;
        PlayerInputManager input;
        PlayerCamera cameraOwner;
        WorldSaveGameManager world;
        PlayerControls controls;
        CameraBinding[] cameras;
        Camera[] cameraScratch;
        InputDevice[] originalDevices, originalAssetDevices;
        bool hadDeviceRestriction;
        InputBinding? originalMask;
        InputAction[] actions;
        bool[] actionStates;
        PopUpTutorial[] popups;
        PlannedTutorialJournal tutorialJournal;
        PerformanceProfilerAttribution attribution;
        PerformanceCheckpointImages checkpointImages;
        Vector3 initialPosition;
        Quaternion initialCameraRotation;
        double startedAt;
        double[] schedule;
        float yawSpeed;
        string saveOverride;
        int next, sceneHandle;
        bool disposed, replayFinished, ending, lightControl, lightPrepared;
        int lightReleaseFrame = -1;
        public EvidenceReport Evidence { get; private set; }
        public bool IsRunning => !disposed && capture != null && (capture.IsCapturing || checkpointImages != null && checkpointImages.AwaitingFiles);
        public int OwnedRecorderCount => capture == null ? 0 : capture.OwnedRecorderCount;
        public bool Qualified => checkpointImages != null ? Evidence?.checkpointImagesCompleted == true : Evidence?.measurementArm == "light-control" ? Evidence.lightControlCompleted : attribution != null ? Evidence?.attributionRouteCompleted == true : Evidence?.routePopulationQualified == true;
        public bool TimingQualified => Evidence?.routePopulationQualified == true;
        public bool PopulationVerified => Evidence?.measurementArm != "light-control" && Evidence?.population?.accepted == true;
        public object EvidenceObject => Evidence;
        PerformanceCertifiedRouteProcess() { }
        public static PerformanceCertifiedRouteProcess Begin(PerformanceProcessResponse request, PerformanceAiCreationHistory history,
            PlayerManager player, PlayerInputManager input, PlayerCamera cameraOwner)
        {
            if (Application.isEditor || request?.success != true || !PerformanceProcessHarness.IsCertifiedRouteMode(request.mode) || history == null || player == null || input == null || cameraOwner == null)
                throw new InvalidOperationException("Actual standalone certified request and production owners required");
            var session = new PerformanceCertifiedRouteProcess();
            try
            {
                string actualHash; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(typeof(PlayerManager).Assembly.Location)) actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                string distribution = ComputeDistributionHash(Path.GetDirectoryName(Application.dataPath));
                session.frozen = ParseManifest(request.config.certifiedManifestJson, request.config.rehearsal.timelineJson, request.config.rehearsal.seedSaveJson, actualHash, Application.buildGUID, distribution);
                session.manifest = session.frozen.Snapshot; session.Evidence = new EvidenceReport { manifestSha256 = session.frozen.sha256, manifest = session.frozen.Snapshot };
                session.lightControl = UseLightControl(request.mode, request.config.measurementArm, session.manifest.scene);
                session.Evidence.measurementArm = session.lightControl ? "light-control" : "full";
                if (request.config.checkpointImages) session.Evidence.measurementArm = "checkpoint-images-diagnostic";
                session.player = player; session.input = input; session.cameraOwner = cameraOwner; session.world = WorldSaveGameManager.instance;
                session.history = history; session.controls = input.playerControls; session.saveOverride = session.world.SaveRootOverride;
                if (session.world.SaveRoot != request.saveRoot || !player.IsInitialized || !player.IsInitialized || input.player != player || cameraOwner.player != player || !cameraOwner.canFollow || cameraOwner.cameraObject == null || !Application.isFocused)
                    throw new InvalidOperationException("Actual healthy owned world/controls/camera and isolated root required");
                session.sceneHandle = SceneManager.GetActiveScene().handle;
                if (SceneManager.GetActiveScene().name != session.manifest.scene) throw new InvalidOperationException("Certified world scene mismatch");
                var loadedCameras = Camera.allCameras;
                if (!loadedCameras.Select(c => TransformPath(c.transform)).OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(session.manifest.cameraPaths.OrderBy(p => p, StringComparer.Ordinal)))
                    throw new InvalidOperationException("Actual active camera paths differ from frozen manifest");
                session.cameras = loadedCameras.Select(c => new CameraBinding { camera = c, viewport = c.rect, depth = c.depth, fieldOfView = c.fieldOfView, display = c.targetDisplay, mask = c.cullingMask, texture = c.targetTexture == null ? 0 : c.targetTexture.GetInstanceID() }).ToArray();
                session.cameraScratch = new Camera[loadedCameras.Length];
                session.originalDevices = InputSystem.devices.ToArray(); session.originalMask = session.controls.asset.bindingMask;
                var restrictions = session.controls.asset.devices; session.hadDeviceRestriction = restrictions.HasValue;
                session.originalAssetDevices = restrictions.HasValue ? restrictions.Value.ToArray() : Array.Empty<InputDevice>();
                var list = new List<InputAction>(); foreach (var action in session.controls.asset) list.Add(action);
                session.actions = list.ToArray(); session.actionStates = session.actions.Select(a => a.enabled).ToArray();
                session.popups = Object.FindObjectsByType<PopUpTutorial>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                session.tutorialJournal = new PlannedTutorialJournal(session.popups, session.manifest.allowedTutorialPaths);
                session.Evidence.producerDeclarations = PerformanceMissionProducerInventory.Capture(Object.FindObjectsByType<NPCDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }); session.before = census.report; session.aiBefore = history.Capture(census);
                if (session.manifest.scene == "Tutorial_Final") Require(census.report.unclassifiedAnimators != null && census.report.unclassifiedAnimators.Length == 0,
                    "Tutorial authored Animator source coverage is incomplete; refuse timing capture.");
                session.actorContext = census.CreateContext();
                if (session.manifest.poseCheckpoints != null && session.manifest.poseCheckpoints.Length != 0)
                { session.poseProof = PerformanceRoutePoseProof.Create(session.manifest.poseCheckpoints, session.manifest.maxLatenessSeconds); session.Evidence.poses = session.poseProof.Evidence; }
                session.schedule = PerformanceRouteRehearsal.ValidateSchedule(request.config.rehearsal.timelineJson, 150, session.manifest.maxLatenessSeconds);
                session.replay = PerformanceInputReplay.Create(new[] { "-karawaraPerformanceReplay" }, session.controls, request.config.rehearsal.timelineJson);
                session.yawSpeed = (float)typeof(PlayerCamera).GetField("leftAndRightRotationSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cameraOwner);
                session.initialPosition = player.transform.position; session.initialCameraRotation = cameraOwner.cameraObject.transform.rotation;
                var root = new GameObject("Explicit certified timing capture"); Object.DontDestroyOnLoad(root);
                session.capture = root.AddComponent<PerformanceCaptureController>();
                session.capture.CameraIdProvider = session.ObserveCameraIdentity;
                session.capture.FinalValidation = session.FinalizeCapture;
                if (!session.capture.Begin(new CaptureConfig { qaEnabled = true, routeId = session.manifest.routeId, runId = "certified-", outputRoot = request.config.rehearsal.outputRoot,
                    commit = session.manifest.sourceRevision, buildId = Application.buildGUID + ":" + distribution, camera = session.frozen.sha256, enemyCount = session.manifest.initialComposition.Where(g => g.role == "hostile").Sum(g => g.count),
                    renderScale = 1, warmupSeconds = 30, captureSeconds = 120, lightControl = session.lightControl, counterNames = session.lightControl ? Array.Empty<string>() : session.manifest.counterNames }))
                    throw new InvalidOperationException("Actual capture allocation/recorder setup rejected");
                session.startedAt = session.capture.StartedTimestampSeconds;
                if (request.config.checkpointImages)
                {
                    session.checkpointImages = new PerformanceCheckpointImages(request.config.rehearsal.outputRoot);
                    session.Evidence.checkpointImages = session.checkpointImages.Evidence;
                }
                session.actorScope = PerformanceActorLifecycleScope.Begin(new[] { "-karawaraPerformanceRoster" }, session.actorContext);
                if (request.mode == "video1-route-attribution")
                {
                    session.attribution = new PerformanceProfilerAttribution(request.config.profilerAttribution, request.config.rehearsal.outputRoot);
                    session.Evidence.attribution = session.attribution.Evidence;
                }
                InputSystem.onBeforeUpdate += session.BeforeInput;
                return session;
            }
            catch { session.Dispose(); throw; }
        }
        public void Poll()
        {
            using (PerformanceProfilerAttribution.PollMarker.Auto()) PollCore();
        }
        void PollCore()
        {
            if (disposed || capture == null) return;
            try
            {
                if (!capture.IsCapturing) { checkpointImages?.CompletePending(); return; }
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                if (lightControl)
                {
                    if (!lightPrepared && elapsed >= 28) PrepareLightControl(elapsed);
                    if (lightPrepared && !Evidence.lightObserversReleased) ReleaseLightObservers();
                    if (Evidence.lightObserversReleased && Evidence.lightObserversReleaseEndedElapsed == 0 && Time.frameCount > lightReleaseFrame)
                    {
                        Evidence.lightObserversReleaseEndedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                        Require(Evidence.lightObserversReleaseEndedElapsed < 30, "Light deferred observer cleanup crossed the measurement boundary");
                    }
                    if (elapsed >= 30) Require(Evidence.lightObserversReleaseEndedElapsed > 0, "Light observer cleanup did not complete before measurement");
                    if (elapsed < 30) ObserveCameraIdentity();
                    else Require(Application.isFocused && player != null && player.IsInitialized && player.IsInitialized && !player.isDead.Value && player.playerNetworkManager.currentHealth.Value > 0 && SceneManager.GetActiveScene().handle == sceneHandle, "Light route focus/scene/player lifetime changed");
                }
                poseProof?.Observe(elapsed, player.transform.position, cameraOwner.cameraObject.transform.rotation, Time.frameCount);
                attribution?.Tick(elapsed);
                checkpointImages?.Tick(elapsed, player.transform, cameraOwner.cameraObject.transform);
                HandlePlannedPause();
                if (Time.timeScale != 1) throw new InvalidOperationException("Unplanned timing pause");
                Evidence.moved |= Vector2.Distance(new Vector2(initialPosition.x, initialPosition.z), new Vector2(player.transform.position.x, player.transform.position.z)) > .05f;
                Evidence.jumped |= player.playerNetworkManager.isJumping.Value;
                Evidence.cameraMoved |= Quaternion.Angle(initialCameraRotation, cameraOwner.cameraObject.transform.rotation) > .1f;
                Evidence.finalRelease = replayFinished && controls.PlayerMovement.Movement.ReadValue<Vector2>() == Vector2.zero && controls.PlayerCamera.Movement.ReadValue<Vector2>() == Vector2.zero &&
                    input.moveAmount == 0 && input.horizontal_Input == 0 && input.vertical_Input == 0 && input.cameraHorizontal_Input == 0 && input.cameraVertical_Input == 0;
                if (Evidence.finalRelease) foreach (var action in actions) if (action.type == InputActionType.Button && action.IsPressed()) { Evidence.finalRelease = false; break; }
                Evidence.guardedFrames++;
            }
            catch (Exception error) { Fail(error); }
        }
        void BeforeInput()
        {
            using (PerformanceProfilerAttribution.InputMarker.Auto()) BeforeInputCore();
        }
        void BeforeInputCore()
        {
            if (!IsRunning || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            try
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                if (Time.timeScale == 0) return;
                int due = PerformanceRouteRehearsal.DuePacketCount(schedule, next, elapsed, manifest.maxLatenessSeconds);
                replayFinished = replay.AdvanceWithWorldTargets(elapsed, cameraOwner.transform.eulerAngles.y, yawSpeed, player.transform.position); next += due;
            }
            catch (Exception error) { Fail(error); }
        }
        string ObserveCameraIdentity()
        {
            using (PerformanceProfilerAttribution.GuardMarker.Auto()) return ObserveCameraIdentityCore();
        }
        string ObserveCameraIdentityCore()
        {
            if (attribution == null && (UnityEngine.Profiling.Profiler.enabled || UnityEngine.Profiling.Profiler.enableAllocationCallstacks)) throw new InvalidOperationException("Profiler instrumentation is active during a timing session.");
            HandlePlannedPause();
            var network = GameBootstrap.Instance;
            if (Time.timeScale != 1 || !Application.isFocused || world == null || world != WorldSaveGameManager.instance || world.player != player || world.SaveRootOverride != saveOverride ||
                network == null || !network.Session.IsActive || !network.Session.IsActive || network.Session.Player != player ||
                player == null || !player.IsInitialized || !player.IsInitialized || player.isDead.Value || player.playerNetworkManager.currentHealth.Value <= 0 ||
                input != PlayerInputManager.instance || input.player != player || input.playerControls != controls || !controls.asset.enabled ||
                cameraOwner != PlayerCamera.instance || cameraOwner.player != player || !cameraOwner.canFollow || SceneManager.GetActiveScene().handle != sceneHandle ||
                Camera.allCamerasCount != cameras.Length || Camera.GetAllCameras(cameraScratch) != cameras.Length)
                throw new InvalidOperationException("Bound timing owner/focus/scene/control/camera set changed");
            foreach (var binding in cameras)
            {
                var camera = binding.camera;
                if (camera == null || !camera.isActiveAndEnabled || Array.IndexOf(cameraScratch, camera) < 0 || camera.rect != binding.viewport || camera.depth != binding.depth || camera.fieldOfView != binding.fieldOfView ||
                    camera.targetDisplay != binding.display || camera.cullingMask != binding.mask || (camera.targetTexture == null ? 0 : camera.targetTexture.GetInstanceID()) != binding.texture)
                    throw new InvalidOperationException("Bound camera rendering context changed");
            }
            return frozen.sha256;
        }
        void HandlePlannedPause()
        {
            if (Time.timeScale == 0)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                if (manifest.scene == "Tutorial_Final" && startedAt > 0 && elapsed >= 30) throw new InvalidOperationException("Tutorial pause after preparation window.");
                tutorialJournal.CloseIfPaused(Time.frameCount, elapsed);
            }
        }
        public static string ComputeDistributionHash(string root)
        {
            var files = Directory.GetFiles(Path.GetFullPath(root), "*", SearchOption.AllDirectories)
                .Select(path => new { path, relative = Path.GetRelativePath(root, path).Replace('\\', '/') }).OrderBy(row => row.relative, StringComparer.Ordinal).ToArray();
            Require(files.Length > 0, "Missing Player distribution files");
            var catalog = new StringBuilder();
            using (var sha = SHA256.Create())
                foreach (var row in files)
                    using (var stream = File.OpenRead(row.path)) catalog.Append(row.relative).Append('|').Append(stream.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "")).Append('\n');
            return Hash(catalog.ToString());
        }
        void Fail(Exception error)
        {
            Evidence.errors.Add(error.ToString());
            if (capture != null && capture.IsCapturing) capture.End();
        }
        void FinalizeCapture(CaptureResult result)
        {
            if (ending) return; ending = true;
            InputSystem.onBeforeUpdate -= BeforeInput;
            Evidence.capture = result;
            try
            {
                if (checkpointImages != null)
                {
                    checkpointImages.Tick(Time.realtimeSinceStartupAsDouble - startedAt, player.transform, cameraOwner.cameraObject.transform);
                    Evidence.unexpectedConditionsChanged = result.conditionsChanged;
                    result.conditionsChanged = true;
                    result.validationUnavailableReasons.Add("Explicit internal checkpoint image diagnostic: never eligible for baseline timing.");
                }
                if (poseProof != null)
                { poseProof.Observe(Time.realtimeSinceStartupAsDouble - startedAt, player.transform.position, cameraOwner.cameraObject.transform.rotation, Time.frameCount); poseProof.Complete(); }
                if (lightControl)
                {
                    FinalizeLightControl(result);
                    return;
                }
                if (attribution != null)
                {
                    Evidence.unexpectedConditionsChanged = result.conditionsChanged;
                    attribution.Dispose();
                    result.conditionsChanged = true;
                    result.validationUnavailableReasons.Add("Explicit profiler attribution session: never eligible for baseline timing.");
                }
                var after = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }); var validation = new CaptureResult(); actorContext.Validate(validation);
                Evidence.plannedTutorials = tutorialJournal.Capture();
                Evidence.populationEvidence = new PerformanceBoundedRoutePopulation.PhaseObservation { before = before, after = after.report, lifecycle = actorScope.CaptureHistory(),
                    aiBefore = aiBefore, aiAfter = history.Capture(after), contextViolationMask = actorContext.SourceViolationMask };
                actorScope.Dispose();
                Evidence.population = ReconcilePopulation(frozen, startedAt, Evidence.populationEvidence, Evidence.producerDeclarations);
                Evidence.waypointArrivals = replay.WaypointArrivals(); Evidence.dispatch = replay.DispatchTimeline();
                replay.Dispose(); replay = null;
                Evidence.controlsRestored = controls.asset.bindingMask.Equals(originalMask) && InputSystem.devices.SequenceEqual(originalDevices) && controls.asset.devices.HasValue == hadDeviceRestriction &&
                    (!hadDeviceRestriction || controls.asset.devices.Value.SequenceEqual(originalAssetDevices)) && actions.Select(a => a.enabled).SequenceEqual(actionStates);
                result.populationVerified = Evidence.population.accepted;
                result.cameraVerified = Evidence.cameraMoved && ObserveCameraIdentity() == frozen.sha256;
                if (!Evidence.moved || !Evidence.jumped || !Evidence.finalRelease || !Evidence.controlsRestored || !replayFinished || Evidence.errors.Count != 0)
                    result.errors.Add("Actual route/control/camera completion proof failed");
                if (!Evidence.population.accepted) result.errors.AddRange(Evidence.population.reasons);
            }
            catch (Exception error) { Evidence.errors.Add(error.ToString()); result.populationVerified = false; result.cameraVerified = false; result.errors.Add("Post-sampling evidence unavailable: " + error.Message); }
            finally { actorScope?.Dispose(); actorContext?.Dispose(); lightContext?.Dispose(); replay?.Dispose(); replay = null; }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; InputSystem.onBeforeUpdate -= BeforeInput;
            try { if (capture != null && capture.IsCapturing) capture.End(); }
            finally
            {
                try { replay?.Dispose(); }
                finally
                {
                    attribution?.Dispose(); actorScope?.Dispose(); actorContext?.Dispose(); lightContext?.Dispose();
                    if (capture != null) { capture.FinalValidation = null; capture.CameraIdProvider = null; Object.Destroy(capture.gameObject); capture = null; }
                }
            }
        }
        static string TransformPath(Transform value) { string path = value.name; while (value.parent != null) { value = value.parent; path = value.name + "/" + path; } return path; }
        static string Key(PerformanceBoundedRoutePopulation.ActorGroup g) => g.role + "|" + g.primaryType + "|" + g.active + "|" + g.enabled + "|" + g.spawned;
        internal static string Hash(string value)
        {
            Require(value != null, "Missing hashed payload");
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        static bool ValidHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool UseLightControl(string mode, string arm, string scene)
        {
            if (arm == null || arm == "full") return false;
            Require(arm == "light-control" && mode == "tutorial-certified-route" && scene == "Tutorial_Final", "Light control requires the explicit Tutorial certified route without attribution");
            return true;
        }
        internal static bool LightStructuralMatch(PerformanceActorCensus.Report before, PerformanceActorCensus.Report after, PerformanceBoundedRoutePopulation.ActorGroup[] expected)
        {
            if (before?.actors == null || after?.actors == null || expected == null || expected.Length == 0 || before.activeScene != after.activeScene ||
                before.unclassifiedAnimators == null || after.unclassifiedAnimators == null || before.unclassifiedAnimators.Length != 0 || after.unclassifiedAnimators.Length != 0 || before.actors.Length != after.actors.Length) return false;
            Func<PerformanceActorCensus.Actor, bool> valid = a => a != null && a.objectId != 0 && a.supportedBinding && a.components != null && a.components.Length > 0 && a.components.All(c => !string.IsNullOrEmpty(c)) && a.components.Distinct().Count() == a.components.Length &&
                !string.IsNullOrEmpty(a.role) && !string.IsNullOrEmpty(a.primaryType) && !string.IsNullOrEmpty(a.path) && a.dead != true &&
                (!a.currentHealth.HasValue || Finite(a.currentHealth.Value) && a.currentHealth.Value > 0) && (!a.maximumHealth.HasValue || Finite(a.maximumHealth.Value) && a.maximumHealth.Value > 0);
            if (!before.actors.All(valid) || !after.actors.All(valid) || before.actors.Select(a => a.objectId).Distinct().Count() != before.actors.Length || after.actors.Select(a => a.objectId).Distinct().Count() != after.actors.Length) return false;
            Func<PerformanceBoundedRoutePopulation.ActorGroup, PerformanceActorCensus.Actor, bool> matches = (g, a) => g != null && g.role == a.role && g.primaryType == a.primaryType && (!g.active.HasValue || g.active.Value == a.active) && (!g.enabled.HasValue || g.enabled.Value == a.enabled) && (!g.spawned.HasValue || g.spawned.Value == a.spawned);
            if (expected.Any(g => g == null || g.count <= 0 || after.actors.Count(a => matches(g, a)) != g.count) || after.actors.Any(a => expected.Count(g => matches(g, a)) != 1)) return false;
            foreach (var old in before.actors)
            {
                var current = after.actors.SingleOrDefault(a => a.objectId == old.objectId);
                if (current == null || current.sceneHandle != old.sceneHandle || current.scene != old.scene || current.path != old.path || current.primaryType != old.primaryType || current.role != old.role || current.active != old.active || current.enabled != old.enabled || current.spawned != old.spawned || current.localCharacterId != old.localCharacterId || current.dead != old.dead || current.maximumHealth != old.maximumHealth || !current.components.OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(old.components.OrderBy(c => c, StringComparer.Ordinal))) return false;
            }
            return true;
        }
        static bool LightCompletion(EvidenceReport evidence) => evidence != null && evidence.measurementArm == "light-control" && evidence.manifest?.scene == "Tutorial_Final" && evidence.capture != null && evidence.capture.fullWindowCompleted && !evidence.capture.conditionsChanged && evidence.capture.errors.Count == 0 &&
            evidence.poses != null && evidence.poses.completed && evidence.poses.closurePassed && !evidence.poses.faulted && evidence.moved && evidence.jumped && evidence.cameraMoved && evidence.finalRelease && evidence.controlsRestored && evidence.lightObserversReleased && evidence.lightStructureUnchanged && evidence.lightPrePostGuardPassed &&
            Finite(evidence.lightPreparationStartedElapsed) && Finite(evidence.lightPreparationEndedElapsed) && evidence.lightPreparationStartedElapsed >= 28 && evidence.lightPreparationStartedElapsed <= evidence.lightPreparationEndedElapsed && evidence.lightPreparationEndedElapsed < 30 &&
            Finite(evidence.lightObserversReleaseStartedElapsed) && Finite(evidence.lightObserversReleaseEndedElapsed) && evidence.lightObserversReleaseStartedElapsed >= evidence.lightPreparationEndedElapsed && evidence.lightObserversReleaseStartedElapsed < evidence.lightObserversReleaseEndedElapsed && evidence.lightObserversReleaseEndedElapsed < 30 && evidence.errors.Count == 0;
        bool LightSettingsMatch() => Screen.width == manifest.width && Screen.height == manifest.height && QualitySettings.names[QualitySettings.GetQualityLevel()] == manifest.quality && QualitySettings.vSyncCount == manifest.vsync && Application.targetFrameRate == manifest.targetFrameRate && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - manifest.renderScale) < .0001;
        void PrepareLightControl(double elapsed)
        {
            Require(elapsed < 30, "Light warm structural preparation missed its pre30s window");
            Evidence.lightPreparationStartedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Require(ObserveCameraIdentity() == frozen.sha256 && LightSettingsMatch(), "Light warm camera/settings guard failed");
            var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
            Require(LightStructuralMatch(census.report, census.report, manifest.settledComposition), "Light warm census does not match the settled Tutorial workload");
            lightContext = census.CreateContext(); var validation = new CaptureResult(); lightContext.Validate(validation);
            Require(!validation.conditionsChanged, "Light warm structural context rejected");
            Evidence.lightWarmupCensus = census.report; Evidence.lightWarmupAi = history.Capture(census);
            Require(Evidence.lightWarmupAi.currentAiHistoryMatched && !Evidence.lightWarmupAi.sourceFaulted && !Evidence.lightWarmupAi.overflow, "Light warm AI creation preparation is incomplete");
            Evidence.lightWarmupLifecycle = actorScope.CaptureHistory();
            Evidence.lightPreparationEndedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Require(Evidence.lightPreparationEndedElapsed < 30, "Light heavy preparation crossed the measurement boundary");
            lightPrepared = true;
        }
        void ReleaseLightObservers()
        {
            if (Evidence.lightObserversReleased) return;
            Require(lightPrepared, "Light observer release requires completed warm preparation");
            Evidence.lightObserversReleaseStartedElapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            Require(Evidence.lightObserversReleaseStartedElapsed < 30, "Light observer release started inside measurement");
            actorScope?.Dispose(); actorContext?.Dispose(); history.Dispose();
            capture.CameraIdProvider = null; capture.EnemyCountProvider = null;
            lightReleaseFrame = Time.frameCount;
            Evidence.lightObserversReleased = true;
        }
        void FinalizeLightControl(CaptureResult result)
        {
            result.populationVerified = false; result.cameraVerified = false; result.comparisonValid = false;
            Require(lightPrepared && Evidence.lightObserversReleased, "Light control did not complete warm preparation/observer release");
            var after = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
            var validation = new CaptureResult(); lightContext.Validate(validation);
            Evidence.lightPostCensus = after.report;
            Evidence.lightStructureUnchanged = !validation.conditionsChanged && LightStructuralMatch(Evidence.lightWarmupCensus, after.report, manifest.settledComposition);
            Evidence.lightPrePostGuardPassed = ObserveCameraIdentity() == frozen.sha256 && LightSettingsMatch();
            Evidence.plannedTutorials = tutorialJournal.Capture();
            Evidence.populationEvidence = new PerformanceBoundedRoutePopulation.PhaseObservation { before = Evidence.lightWarmupCensus, after = after.report, aiBefore = Evidence.lightWarmupAi, contextViolationMask = lightContext.SourceViolationMask };
            Evidence.population = new PerformanceBoundedRoutePopulation.DomainVerdict("Light control observes warm/post endpoints only; measured transient creation/despawn provenance and population acceptance are unavailable.");
            result.validationUnavailableReasons.Add("Light control: endpoint structure and poses only; continuous context/source history and population certification disabled.");
            Evidence.waypointArrivals = replay.WaypointArrivals(); Evidence.dispatch = replay.DispatchTimeline();
            replay.Dispose(); replay = null;
            Evidence.controlsRestored = controls.asset.bindingMask.Equals(originalMask) && InputSystem.devices.SequenceEqual(originalDevices) && controls.asset.devices.HasValue == hadDeviceRestriction && (!hadDeviceRestriction || controls.asset.devices.Value.SequenceEqual(originalAssetDevices)) && actions.Select(a => a.enabled).SequenceEqual(actionStates);
            if (!Evidence.lightStructureUnchanged || !Evidence.lightPrePostGuardPassed || !Evidence.moved || !Evidence.jumped || !Evidence.finalRelease || !Evidence.controlsRestored || !replayFinished || Evidence.errors.Count != 0)
                result.errors.Add("Light actual route/control/endpoint completion proof failed");
        }
        static void Require(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }
    }
#endif
}

