using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Shared production world/host lifetime. Discovery and explicit certified timing use separate executors/verdicts.
    public sealed class PerformanceTutorialProcess : MonoBehaviour
    {
        internal static bool RequiresDiscoveryScreenshots(string mode) => mode != null && mode != "tutorial-rehearsal" && !PerformanceProcessHarness.IsCertifiedRouteMode(mode) && !PerformanceProcessHarness.IsUiRouteMode(mode) && !PerformanceProcessHarness.IsCucaCertifiedRouteMode(mode);
        PerformanceProcessResponse request;
        WorldSaveGameManager world;
        NetworkManager network;
        PlayerManager player;
        PerformanceRouteRehearsal rehearsal;
        IPerformanceTimedSession certified;
        PerformanceRenderingInventory renderingHold;
        PerformanceRouteRehearsal.Result observation;
        PerformanceActorCensus.Report actorPreflight, actorPostflight;
        object[] cucaEncounterBefore, cucaEncounterAfter;
        PerformanceActorRosterContext actorContext;
        PerformanceActorLifecycleScope actorScope;
        PerformanceActorLifecycleScope.HistoryReport actorLifecycleHistory;
        PerformanceMissionProducerInventory.Row[] missionProducerDeclarations;
        PerformanceActorPhase postMissionActorPhase;
        bool postMissionActorPhaseStopAttempted;
        PerformanceUiAuxiliaryPhase postMissionUiPhase;
        bool postMissionUiPhaseStopAttempted;
        PerformanceAiCreationHistory aiCreationHistory;
        PerformanceAiCreationHistory.Report aiCreationBeforeRoute, aiCreationAtMissionReady, aiCreationAfterRoute, aiCreationAfterReset, aiCreationFinal;
        PerformanceActorCensus.Report aiResetBefore, aiResetAfter;
        bool aiResetRequested, aiResetObserved;
        double aiResetAt;
        int aiResetErrorCount, bossReferencesBeforeReset, bossReferencesAfterReset, liveBossReferencesBeforeReset, liveBossReferencesAfterReset;
        int aiResetCompleted;
        [Serializable] sealed class AiResetCycle
        {
            public int cycle, aiCount, matchedAiCount, attemptCount, eventCount, bossReferences, liveBossReferences;
            public bool passed;
        }
        readonly List<AiResetCycle> aiResetCycles = new List<AiResetCycle>();
        [Serializable] sealed class MemorySnapshotResult
        {
            public string path, error;
            public bool completed, succeeded;
            public long bytes;
        }
        readonly List<MemorySnapshotResult> memorySnapshots = new List<MemorySnapshotResult>();
        MemorySnapshotResult pendingMemorySnapshot;
        string missionActorCandidate;
        double missionActorStableSince;
        PerformanceMinimapContext uiContext;
        PerformanceUiSourceContext uiSourceContext;
        PerformanceAuxiliaryContext auxiliaryContext;
        PerformanceAuxiliaryContext.Checkpoint auxiliaryPreflight, auxiliaryFrozen, auxiliaryPostflight;
        CaptureResult auxiliaryValidation;
        PerformanceUiSourceContext.Checkpoint uiSourcePreflight, uiSourceFrozen, uiSourcePostflight;
        CaptureResult uiSourceValidation;
        PerformanceMinimapContext.Checkpoint uiPreflight, uiFrozen, uiPostflight;
        CaptureResult uiValidation;
        CaptureResult actorValidation;
        long actorNotifications;
        bool actorScopeFaulted;
        readonly List<string> errors = new List<string>();
        readonly List<string> tutorialActions = new List<string>();
        [Serializable] internal sealed class MissionTargetState
        {
            public string name, counterText, rewardName;
            public bool active, completed;
            public int enemyReferences, activeEnemyReferences;
        }
        [Serializable] internal sealed class MissionState
        {
            public double elapsed;
            public bool present, dialogueVisible, choiceVisible;
            public int npcId;
            public MissionTargetState choice1, choice2;
        }
        readonly List<MissionState> missionStates = new List<MissionState>();
        NPCDialogue[] missionOwners = Array.Empty<NPCDialogue>();
        double nextMissionSample;
        static MissionTargetState CaptureMissionTarget(GameObject target)
        {
            if (target == null) return null;
            var mission = target.GetComponent<MissionManager>();
            if (mission == null) return null;
            return new MissionTargetState { name = target.name, active = target.activeInHierarchy,
                completed = mission.isMissionCompleted, enemyReferences = mission.enemies?.Count ?? 0,
                activeEnemyReferences = mission.enemies?.Count(e => e != null && e.activeSelf) ?? 0,
                counterText = mission.enemiesRemainingText == null ? null : mission.enemiesRemainingText.text,
                rewardName = mission.reward == null ? null : mission.reward.name };
        }
        static MissionState CaptureMissionState(NPCDialogue npc, double elapsed)
        {
            if (npc == null) return new MissionState { elapsed = elapsed };
            return new MissionState { elapsed = elapsed, present = true, npcId = npc.GetInstanceID(),
                dialogueVisible = npc.dialogueUI != null && npc.dialogueUI.activeInHierarchy,
                choiceVisible = npc.canvasEscolha != null && npc.canvasEscolha.activeInHierarchy,
                choice1 = CaptureMissionTarget(npc.escolha1), choice2 = CaptureMissionTarget(npc.escolha2) };
        }
        sealed class Screenshot { public string path; public double requestedElapsed; public int frame; }
        readonly List<Screenshot> screenshots = new List<Screenshot>();
        readonly string screenshotRunId = Guid.NewGuid().ToString("N");
        double ownerBoundAt;
        int nextScreenshot;
        Dictionary<string, string> personalBefore;
        string personalRoot, outputPath;
        double deadline;
        float groundedSince = -1;
        int state, shutdownFrame = -1;
        bool ownsHost, terminal, initialized;
        AsyncOperation returning;
        static readonly List<string> startupErrors = new List<string>();
        internal static void PrepareStartupDiagnostics()
        {
            Application.logMessageReceived -= OnStartupLog;
            startupErrors.Clear(); Application.logMessageReceived += OnStartupLog;
        }
        internal static List<string> TakeStartupDiagnostics()
        {
            Application.logMessageReceived -= OnStartupLog;
            var collected = new List<string>(startupErrors); startupErrors.Clear(); return collected;
        }
        static void OnStartupLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && startupErrors.Count < 256)
                startupErrors.Add(message + "\n" + stack);
        }

        public void Initialize(PerformanceProcessResponse request)
        {
            if (Application.isEditor || initialized || request == null || !request.success || !PerformanceProcessHarness.IsRehearsalMode(request.mode))
                throw new InvalidOperationException("Explicit standalone tutorial request required.");
            this.request = request; initialized = true;
            personalRoot = Application.persistentDataPath;
            outputPath = Path.Combine(request.config.rehearsal.outputRoot, "process-" + Guid.NewGuid().ToString("N") + ".json");
            errors.AddRange(TakeStartupDiagnostics());
            Application.logMessageReceived += OnLog;
            try
            {
                personalBefore = Fingerprints(personalRoot);
                Directory.CreateDirectory(request.config.rehearsal.outputRoot);
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, request.config.quality), true);
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                SetState(0, 60);
                Debug.Log("QA production-scene rehearsal started: " + outputPath);
            }
            catch (Exception error) { errors.Add("Initialization failure: " + error); Finish(false); }
        }
        void SetState(int next, double seconds) { state = next; deadline = Time.realtimeSinceStartupAsDouble + seconds; }
        bool SettingsMatch() => Screen.width == 1920 && Screen.height == 1080 &&
            QualitySettings.names[QualitySettings.GetQualityLevel()] == request.config.quality &&
            QualitySettings.vSyncCount == 0 && Application.targetFrameRate == -1 &&
            GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - 1) < .0001;
        void Update()
        {
            using (PerformanceProfilerAttribution.ProcessMarker.Auto())
            {
            if (!initialized || terminal) return;
            try
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Tutorial process stage " + state);
                if (state < 4 && errors.Count != 0) throw new InvalidOperationException("Runtime errors were observed.");
                if (state == 0)
                {
                    world = WorldSaveGameManager.instance; network = NetworkManager.Singleton;
                    if (world == null || network == null || TitleScreenManager.Instance == null || PlayerInputManager.instance?.playerControls == null || !Application.isFocused || !SettingsMatch()) return;
                    if (SceneManager.GetActiveScene().name != "SceneMenu" || network.IsListening || Time.timeScale != 1)
                        throw new InvalidOperationException($"Fresh unpaused SceneMenu and stopped network required (scene={SceneManager.GetActiveScene().name}, listening={network.IsListening}, host={network.IsHost}, timeScale={Time.timeScale}).");
                    world.SaveRootOverride = request.saveRoot;
                    if (world.SaveRoot != request.saveRoot) throw new InvalidOperationException("QA root rejected before host.");
                    aiCreationHistory = PerformanceAiCreationHistory.Begin(new[] { "-karawaraPerformanceRoster" });
                    ownsHost = TitleScreenManager.Instance.TryStartNetworkAsHost();
                    if (!ownsHost) throw new InvalidOperationException("Production host startup rejected.");
                    SetState(1, 60);
                }
                else if (state == 1)
                {
                    player = network.LocalClient?.PlayerObject?.GetComponent<PlayerManager>();
                    if (player == null || !player.IsSpawned || !player.IsOwner || !world.CanCreateNewGame) return;
                    if (PerformanceProcessHarness.IsSeededMode(request.mode))
                    {
                        // Prepare after the actual host HUD has loaded its authored assets,
                        // before any seed is loaded. Serialization now uses this process's IDs.
                        var skillUi = PlayerUIManager.instance?.GetComponentInChildren<SkillTreeUiManager>(true);
                        if (skillUi == null) return;
                        var catalog = (skillUi.attackSkills ?? Array.Empty<Skill>()).Concat(skillUi.defenseSkills ?? Array.Empty<Skill>()).Concat(skillUi.supportSkills ?? Array.Empty<Skill>()).ToArray();
                        var seed = PerformanceProcessHarness.IsCucaMode(request.mode) ? PerformanceProcessHarness.BindCucaSeed(request.config.rehearsal.seedSaveJson, catalog) : PerformanceProcessHarness.IsVideoMode(request.mode)
                            ? PerformanceProcessHarness.BindVideoSeed(request.config.rehearsal.seedSaveJson, catalog)
                            : PerformanceProcessHarness.BindTutorialSeed(request.config.rehearsal.seedSaveJson, catalog);
                        var writer = new SaveFileDataWriter { saveDataDirectoryPath = request.saveRoot, saveFileName = "characterSlot_01" };
                        var written = writer.TrySave(seed);
                        if (!written.Success) throw new InvalidOperationException("QA scene seed rejected: " + written.Message);
                        world.RefreshCharacterProfiles();
                        TitleScreenManager.Instance.SelectCharacterSlot(CharacterSlot.CharacterSlot_01);
                        world.LoadGame();
                    }
                    else
                    {
                        var created = world.TryCreateNewGame();
                        if (!created.Success) throw new InvalidOperationException(created.Message);
                    }
                    SetState(2, 90);
                }
                else if (state == 2)
                {
                    var input = PlayerInputManager.instance;
                    var hud = PlayerUIManager.instance?.playerUIHudManager;
                    string expectedScene = PerformanceProcessHarness.RehearsalScene(request.mode);
                    if (SceneManager.GetActiveScene().name != expectedScene || player == null || !player.IsSpawned || input == null || input.player != player || hud == null || !hud.IsHealthBarReady) return;
                    if (Time.timeScale == 0)
                    {
                        var popups = FindObjectsByType<PopUpTutorial>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                            .Where(p => !p.passou && p.UI != null && p.UI.activeInHierarchy && p.GetComponent<BoxCollider>() != null && !p.GetComponent<BoxCollider>().enabled).ToArray();
                        if (popups.Length != 1) throw new InvalidOperationException("Unknown or ambiguous tutorial pause.");
                        tutorialActions.Add(popups[0].name + ": actual authored close"); popups[0].close();
                    }
                    if (!player.characterLocomotionManager.isGrounded || Time.timeScale != 1) { groundedSince = -1; return; }
                    if (groundedSince < 0) groundedSince = Time.time;
                    if (Time.time - groundedSince < 1) return;
                    if (!SettingsMatch()) throw new InvalidOperationException("Requested rendering settings changed before rehearsal.");
                    if (PerformanceProcessHarness.IsCucaMode(request.mode)) PerformanceCucaSourceBinding.Bind(SceneManager.GetActiveScene());
                    if (PerformanceProcessHarness.IsCertifiedRouteMode(request.mode) || PerformanceProcessHarness.IsUiRouteMode(request.mode) || PerformanceProcessHarness.IsCucaCertifiedRouteMode(request.mode))
                    {
                        certified = PerformanceProcessHarness.IsCucaCertifiedRouteMode(request.mode)
                            ? (IPerformanceTimedSession)PerformanceCucaCertifiedRouteProcess.Begin(request, aiCreationHistory, player, input, PlayerCamera.instance)
                            : PerformanceProcessHarness.IsUiRouteMode(request.mode)
                            ? (IPerformanceTimedSession)PerformanceUiRouteProcess.Begin(request, aiCreationHistory, player, input, PlayerCamera.instance)
                            : PerformanceCertifiedRouteProcess.Begin(request, aiCreationHistory, player, input, PlayerCamera.instance);
                        SetState(3, 165); return;
                    }
                    var config = request.config.rehearsal;
                    missionOwners = FindObjectsByType<NPCDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    missionProducerDeclarations = PerformanceMissionProducerInventory.Capture(missionOwners);
                    var minimap = SystemMiniMap.ActiveMiniMap?.miniMapCamera;
                    var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    actorPreflight = census.report;
                    if (PerformanceProcessHarness.IsCucaMode(request.mode)) cucaEncounterBefore = CaptureCucaEncounter();
                    aiCreationBeforeRoute = aiCreationHistory.Capture(census);
                    actorContext = census.CreateContext();
                    actorScope = PerformanceActorLifecycleScope.Begin(new[] { "-karawaraPerformanceRoster" }, actorContext);
                    uiContext = census.CreateUiContext();
                    uiPreflight = uiContext.CaptureCheckpoint("loaded-ui-settling");
                    uiSourceContext = census.CreateUiSourceContext();
                    if (request.mode == "cuca-route-rehearsal") uiSourceContext.BindCucaCombatDamageFeedback(request.mode);
                    uiSourcePreflight = uiSourceContext.CaptureCheckpoint("loaded-other-ui-settling");
                    auxiliaryContext = census.CreateAuxiliaryContext(); auxiliaryPreflight = auxiliaryContext.CaptureCheckpoint("loaded-auxiliary-settling");
                    rehearsal = PerformanceRouteRehearsal.Begin(new[] { "-karawaraPerformanceRehearsal" },
                        new PerformanceRouteRehearsal.Config(config.routeId, config.timelineJson, config.outputRoot, config.durationSeconds, config.maxLatenessSeconds,
                            FindObjectsByType<PopUpTutorial>(FindObjectsInactive.Exclude, FindObjectsSortMode.None),
                            minimap != null && minimap.isActiveAndEnabled && minimap.targetTexture != null ? new[] { minimap } : Array.Empty<Camera>(),
                            PerformanceProcessHarness.BuildRehearsalCheckpoints(config)),
                        player, input, PlayerCamera.instance);
                    if (rehearsal == null) throw new InvalidOperationException("Actual owner rehearsal rejected.");
                    ownerBoundAt = Time.realtimeSinceStartupAsDouble;
                    SetState(3, config.durationSeconds + 15);
                }
                else if (state == 3)
                {
                    if (certified != null)
                    {
                        certified.Poll();
                        if (certified.IsRunning) return;
                        if (!certified.Qualified)
                            errors.Add("Requested route evidence rejected; inspect certifiedEvidence and attribution status.");
                        BeginCleanup(); return;
                    }
                    double missionElapsed = Time.realtimeSinceStartupAsDouble - ownerBoundAt;
                    if (missionElapsed >= nextMissionSample)
                    {
                        foreach (var npc in missionOwners)
                            if (missionStates.Count < 512) missionStates.Add(CaptureMissionState(npc, missionElapsed));
                        TryBeginPostMissionActorPhase(missionElapsed);
                        nextMissionSample = missionElapsed + .5;
                    }
                    postMissionUiPhase?.Poll();
                    if (uiContext != null && !uiContext.IsDisposed)
                    {
                        uiContext.Poll();
                        if (!uiContext.IsFrozen && Time.realtimeSinceStartupAsDouble - ownerBoundAt >= 30)
                        { uiContext.Freeze(); uiFrozen = uiContext.CaptureCheckpoint("ui-after-30s-warmup"); }
                    }
                    if (uiSourceContext != null && !uiSourceContext.IsDisposed)
                    {
                        uiSourceContext.Poll();
                        if (!uiSourceContext.IsFrozen && Time.realtimeSinceStartupAsDouble - ownerBoundAt >= 30)
                        { uiSourceContext.Freeze(); uiSourceFrozen = uiSourceContext.CaptureCheckpoint("other-ui-after-30s-warmup"); }
                    }
                    if (auxiliaryContext != null && !auxiliaryContext.IsDisposed)
                    {
                        auxiliaryContext.Poll(); if (!auxiliaryContext.IsFrozen && Time.realtimeSinceStartupAsDouble - ownerBoundAt >= 30)
                        { auxiliaryContext.Freeze(); auxiliaryFrozen = auxiliaryContext.CaptureCheckpoint("auxiliary-after-30s-warmup"); }
                    }
                    if (!SettingsMatch()) throw new InvalidOperationException("Rendering settings changed during functional rehearsal.");
                    if (rehearsal.IsRunning && request.mode != "tutorial-rehearsal" && nextScreenshot < 6)
                    {
                        double elapsed = Time.realtimeSinceStartupAsDouble - ownerBoundAt;
                        double at = nextScreenshot == 5 ? request.config.rehearsal.durationSeconds - 1 : nextScreenshot * (PerformanceProcessHarness.IsPathInspectionMode(request.mode) ? 4 : 30);
                        if (elapsed >= at)
                        {
                            string path = Path.Combine(request.config.rehearsal.outputRoot, "route-view-" + screenshotRunId + "-" + nextScreenshot.ToString("D2") + ".png");
                            if (File.Exists(path)) throw new IOException("Refuse to overwrite an existing discovery screenshot.");
                            ScreenCapture.CaptureScreenshot(path);
                            screenshots.Add(new Screenshot { path = path, requestedElapsed = elapsed, frame = Time.frameCount }); nextScreenshot++;
                        }
                    }
                    if (rehearsal.IsRunning) return;
                    StopRehearsal();
                    bool controlsProtocol = request.mode == "tutorial-rehearsal";
                    bool functionalAccepted = FunctionalObservationAccepted(observation, controlsProtocol || request.config.rehearsal.requireObservedJump, controlsProtocol);
                    if (!functionalAccepted)
                        errors.Add("Functional observation failed: " + observation?.endReason + "; export=" + observation?.exportError);
                    if (request.config.renderingInspection != null)
                    {
                        if (!functionalAccepted || errors.Count != 0) throw new InvalidOperationException("Rendering hold requires successful restored-controls discovery before cleanup.");
                        renderingHold = PerformanceRenderingInventory.Begin(player, request.config.renderingInspection, request.config.rehearsal.outputRoot, Time.realtimeSinceStartupAsDouble - ownerBoundAt);
                        SetState(9, 125); return;
                    }
                    if (request.config.rehearsal.auditAiSpawnerReset)
                    {
                        var actorPhase = postMissionActorPhase?.Evidence; var uiPhase = postMissionUiPhase?.Evidence;
                        bool phasesAccepted = actorPhase != null && actorPhase.completed && !actorPhase.sourceFaulted && !actorPhase.validation.conditionsChanged &&
                            uiPhase != null && uiPhase.completed && !uiPhase.minimapValidation.conditionsChanged && !uiPhase.uiValidation.conditionsChanged && !uiPhase.auxiliaryValidation.conditionsChanged;
                        if (CanAuditAiReset(functionalAccepted, phasesAccepted, aiCreationAfterRoute != null && aiCreationAfterRoute.currentAiHistoryMatched))
                        {
                            aiResetBefore = actorPostflight; aiResetRequested = true;
                            aiResetErrorCount = errors.Count;
                            bossReferencesBeforeReset = WorldAIManager.instance.QaBossReferences;
                            liveBossReferencesBeforeReset = WorldAIManager.instance.QaLiveBossReferences;
                            if (request.config.rehearsal.auditMemorySnapshots) BeginMemorySnapshot("before-ai-reset", 7);
                            else StartNextAiReset();
                            return;
                        }
                        errors.Add("AI reset audit rejected: completed functional/source/history evidence required.");
                    }
                    BeginCleanup();
                }
                else if (state == 9)
                {
                    if (renderingHold == null) throw new InvalidOperationException("Rendering hold owner unavailable.");
                    if (renderingHold.Poll()) BeginCleanup();
                }
                else if (state == 6)
                {
                    if (Time.realtimeSinceStartupAsDouble - aiResetAt < 1) return;
                    var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    aiResetAfter = census.report; aiCreationAfterReset = aiCreationHistory.Capture(census);
                    bossReferencesAfterReset = WorldAIManager.instance.QaBossReferences;
                    liveBossReferencesAfterReset = WorldAIManager.instance.QaLiveBossReferences;
                    aiResetObserved = errors.Count == aiResetErrorCount && aiCreationAfterReset.currentAiHistoryMatched &&
                        aiCreationAfterReset.currentAiCount == aiCreationAfterRoute.currentAiCount &&
                        bossReferencesAfterReset == bossReferencesBeforeReset && liveBossReferencesAfterReset == liveBossReferencesBeforeReset;
                    aiResetCompleted++;
                    aiResetCycles.Add(new AiResetCycle { cycle = aiResetCompleted, passed = aiResetObserved,
                        aiCount = aiCreationAfterReset.currentAiCount ?? -1, matchedAiCount = aiCreationAfterReset.matchedAiCount ?? -1,
                        attemptCount = aiCreationAfterReset.attempts.Length, eventCount = aiCreationAfterReset.events.Length,
                        bossReferences = bossReferencesAfterReset, liveBossReferences = liveBossReferencesAfterReset });
                    if (!aiResetObserved) errors.Add("AI reset diagnostic failed: runtime error or current history/population mismatch.");
                    if (!aiResetObserved) BeginCleanup();
                    else if (aiResetCompleted < request.config.rehearsal.aiSpawnerResetCycles) StartNextAiReset();
                    else if (request.config.rehearsal.auditMemorySnapshots) BeginMemorySnapshot("after-ai-reset-" + aiResetCompleted, 8);
                    else BeginCleanup();
                }
                else if (state == 7 || state == 8)
                {
                    if (!pendingMemorySnapshot.completed) return;
                    if (!pendingMemorySnapshot.succeeded || errors.Count != aiResetErrorCount)
                        throw new InvalidOperationException("Memory snapshot audit failed: " + pendingMemorySnapshot.error);
                    if (state == 7) StartNextAiReset(); else BeginCleanup();
                }
                else if (state == 4)
                {
                    if (returning != null && !returning.isDone) return;
                    if (ownsHost && network != null && network.IsListening) network.Shutdown();
                    SetState(5, 20);
                }
                else if (state == 5 && (network == null || !network.IsListening) && (player == null || !player.IsSpawned))
                {
                    if (shutdownFrame < 0) shutdownFrame = Time.frameCount;
                    if (Time.frameCount >= shutdownFrame + 2) Finish(true);
                }
            }
            catch (Exception error)
            {
                errors.Add(error.ToString());
                if (state < 4 || state >= 6) BeginCleanup();
                else { if (ownsHost && network != null && network.IsListening) network.Shutdown(); Finish(false); }
            }
        
            }
        }
        static bool FunctionalObservationAccepted(PerformanceRouteRehearsal.Result result, bool requireJump, bool requireCamera) =>
            result != null && result.passed && result.observedMovement && result.observedFinalRelease && result.controlsRestored &&
            (!requireJump || result.observedJump) && (!(requireCamera || result.cameraMode == "movement-heading-v1") || result.observedCameraMovement);
        static bool CanAuditAiReset(bool functionalAccepted, bool phasesAccepted, bool historyAccepted) => functionalAccepted && phasesAccepted && historyAccepted;
        void StartNextAiReset()
        {
            aiResetAt = Time.realtimeSinceStartupAsDouble; SetState(6, 15);
            WorldAIManager.instance.ResetAllCharacters(); // Production API diagnostic, outside replay; not a player rest-action claim.
        }
        void BeginMemorySnapshot(string label, int nextState)
        {
            var result = new MemorySnapshotResult { path = Path.Combine(request.config.rehearsal.outputRoot, label + "-" + screenshotRunId + ".snap") };
            if (File.Exists(result.path)) throw new IOException("Refuse to overwrite a memory snapshot.");
            pendingMemorySnapshot = result; memorySnapshots.Add(result); SetState(nextState, 90);
            Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(result.path, (path, success) =>
            {
                try
                {
                    result.succeeded = success && string.Equals(Path.GetFullPath(path), Path.GetFullPath(result.path), StringComparison.OrdinalIgnoreCase) && File.Exists(result.path);
                    if (result.succeeded) result.bytes = new FileInfo(result.path).Length;
                    result.succeeded &= result.bytes > 0;
                    if (!result.succeeded) result.error = "Unity did not write a successful nonempty snapshot at the requested path.";
                }
                catch (Exception error) { result.error = error.ToString(); result.succeeded = false; }
                finally { result.completed = true; }
            }, Unity.Profiling.Memory.CaptureFlags.ManagedObjects | Unity.Profiling.Memory.CaptureFlags.NativeObjects | Unity.Profiling.Memory.CaptureFlags.NativeAllocations);
        }
        void TryBeginPostMissionActorPhase(double elapsed)
        {
            if (postMissionActorPhase != null) return;
            var selected = missionOwners.Where(n => n != null && n.dialogueUI != null && !n.dialogueUI.activeInHierarchy &&
                n.canvasEscolha != null && !n.canvasEscolha.activeInHierarchy)
                .SelectMany(n => new[] { n.escolha1, n.escolha2 }).Where(t => t != null && t.activeInHierarchy).ToArray();
            if (selected.Length != 1) { missionActorCandidate = null; return; }
            var mission = selected[0].GetComponent<MissionManager>();
            var spawners = selected[0].GetComponentsInChildren<AICharacterSpawner>(true);
            if (mission == null || mission.isMissionCompleted || mission.enemies == null || spawners.Length == 0 ||
                mission.enemies.Count != spawners.Length || mission.enemies.Any(e => e == null || !e.activeInHierarchy) ||
                mission.enemies.Distinct().Count() != mission.enemies.Count)
            { missionActorCandidate = null; return; }
            var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
            if (census.report.actors.Any(a => !a.supportedBinding)) { missionActorCandidate = null; return; }
            if (census.report.uiIcons.Any(i => !i.supportedBinding) || census.report.uiSources.Any(s => !s.referenceValidated || !s.lifecycleBound) ||
                census.report.auxiliarySources.Any(s => !s.supportedBinding)) { missionActorCandidate = null; return; }
            foreach (var spawner in spawners)
            {
                var link = census.report.spawnerAssociations.SingleOrDefault(s => s.spawnerId == spawner.GetInstanceID());
                if (link == null || !link.linked || !link.createdActive || !mission.enemies.Contains(spawner.QaSpawned))
                { missionActorCandidate = null; return; }
            }
            // Diagnostic settling only. Match retained identities/state, never moving transforms or actor counts alone.
            string signature = JsonConvert.SerializeObject(new { target = selected[0].GetInstanceID(),
                enemies = mission.enemies.Select(e => e.GetInstanceID()).OrderBy(id => id).ToArray(),
                actors = census.report.actors.OrderBy(a => a.objectId).Select(a => new { a.objectId, a.sceneHandle, a.role,
                    a.active, a.enabled, a.supportedBinding, a.spawned, a.networkObjectId, a.maximumHealth, a.dead }).ToArray(),
                links = census.report.spawnerAssociations,
                icons = census.report.uiIcons.OrderBy(i => i.creatorId).ToArray(),
                ui = census.report.uiSources.OrderBy(s => s.animatorId).ToArray(),
                auxiliary = census.report.auxiliarySources.OrderBy(s => s.markerId).Select(s => new { s.markerId, s.animatorId, s.driverId,
                    s.prefabId, s.spawnedId, s.cameraId, s.particleId, s.active, s.animatorEnabled, s.particlePlaying, s.particlePaused,
                    s.emissionEnabled, s.cameraEnabled, s.listenerEnabled, s.destroyDelay }).ToArray() });
            if (!MissionSourcesSettled(signature, elapsed)) return;
            aiCreationAtMissionReady = aiCreationHistory.Capture(census);
            postMissionActorPhase = PerformanceActorPhase.Begin(census, actorContext, actorScope, elapsed);
            postMissionUiPhase = new PerformanceUiAuxiliaryPhase();
            postMissionUiPhase.Begin(census, uiContext, uiSourceContext, auxiliaryContext, elapsed);
        }
        bool MissionSourcesSettled(string signature, double elapsed)
        {
            if (signature != missionActorCandidate) { missionActorCandidate = signature; missionActorStableSince = elapsed; return false; }
            return elapsed - missionActorStableSince >= 5;
        }
        void StopRehearsal(bool collectActorEvidence = true)
        {
            certified?.Dispose();
            try
            {
                if (collectActorEvidence && postMissionUiPhase != null && !postMissionUiPhaseStopAttempted)
                {
                    postMissionUiPhaseStopAttempted = true;
                    try
                    {
                        postMissionUiPhase.Complete(Time.realtimeSinceStartupAsDouble - ownerBoundAt);
                        var evidence = postMissionUiPhase.Evidence;
                        if (evidence.minimapValidation.conditionsChanged || evidence.uiValidation.conditionsChanged || evidence.auxiliaryValidation.conditionsChanged)
                            errors.Add("Separate post-mission UI/auxiliary context changed or faulted.");
                    }
                    catch (Exception error) { errors.Add("Post-mission UI/auxiliary evidence unavailable: " + error); }
                }
                if (collectActorEvidence && postMissionActorPhase != null && !postMissionActorPhaseStopAttempted)
                {
                    postMissionActorPhaseStopAttempted = true;
                    try
                    {
                        postMissionActorPhase.Complete(Time.realtimeSinceStartupAsDouble - ownerBoundAt);
                        if (postMissionActorPhase.Evidence.sourceFaulted || postMissionActorPhase.Evidence.validation.conditionsChanged)
                        {
                            errors.Add("Separate post-mission actor context changed or faulted.");
                            errors.AddRange(postMissionActorPhase.Evidence.validation.errors);
                        }
                    }
                    catch (Exception error) { errors.Add("Post-mission actor evidence unavailable: " + error); }
                }
                if (rehearsal != null)
                {
                    rehearsal.Dispose(); observation = rehearsal.LastResult;
                    Destroy(rehearsal.gameObject); rehearsal = null;
                }
                if (actorScope != null)
                {
                    actorNotifications = actorScope.NotificationCount; actorScopeFaulted = actorScope.NotificationFaulted;
                    actorLifecycleHistory = actorScope.CaptureHistory();
                    actorScope.Dispose(); actorScope = null;
                    if (collectActorEvidence)
                    {
                        var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                        actorPostflight = census.report;
                        if (PerformanceProcessHarness.IsCucaMode(request.mode)) cucaEncounterAfter = CaptureCucaEncounter();
                        aiCreationAfterRoute = aiCreationHistory?.Capture(census);
                        actorValidation = new CaptureResult(); actorContext.Validate(actorValidation);
                        if (actorScopeFaulted || actorValidation.conditionsChanged)
                        {
                            errors.Add("Frozen actor source context changed or faulted during rehearsal.");
                            errors.AddRange(actorValidation.errors);
                        }
                    }
                }
            }
            finally
            {
                postMissionActorPhase?.Dispose();
                postMissionUiPhase?.Dispose();
                try
                {
                    if (collectActorEvidence && uiContext != null)
                    {
                        if (!uiContext.IsDisposed) { uiPostflight = uiContext.CaptureCheckpoint("ui-postflight"); uiValidation = new CaptureResult(); uiContext.Validate(uiValidation); }
                        else { uiPostflight = postMissionUiPhase?.Evidence.previousMinimap; uiValidation = postMissionUiPhase?.Evidence.previousMinimapValidation; }
                        if (uiValidation == null || uiContext.SourceFaulted || uiValidation.conditionsChanged) errors.Add("Minimap UI source context changed, faulted or unavailable during rehearsal.");
                    }
                }
                finally
                {
                    try
                    {
                        if (collectActorEvidence && uiSourceContext != null)
                        {
                            if (!uiSourceContext.IsDisposed) { uiSourcePostflight = uiSourceContext.CaptureCheckpoint("other-ui-postflight"); uiSourceValidation = new CaptureResult(); uiSourceContext.Validate(uiSourceValidation); }
                            else { uiSourcePostflight = postMissionUiPhase?.Evidence.previousUi; uiSourceValidation = postMissionUiPhase?.Evidence.previousUiValidation; }
                            if (uiSourceValidation == null || uiSourceContext.SourceFaulted || uiSourceValidation.conditionsChanged) errors.Add("Other UI source context changed, faulted or unavailable during rehearsal.");
                        }
                    }
                    finally
                    {
                        try
                        {
                            if (collectActorEvidence && auxiliaryContext != null)
                            {
                                if (!auxiliaryContext.IsDisposed) { auxiliaryPostflight = auxiliaryContext.CaptureCheckpoint("auxiliary-postflight"); auxiliaryValidation = new CaptureResult(); auxiliaryContext.Validate(auxiliaryValidation); }
                                else { auxiliaryPostflight = postMissionUiPhase?.Evidence.previousAuxiliary; auxiliaryValidation = postMissionUiPhase?.Evidence.previousAuxiliaryValidation; }
                                if (auxiliaryValidation == null || auxiliaryContext.SourceFaulted || auxiliaryValidation.conditionsChanged) errors.Add("Auxiliary source context changed, faulted or unavailable during rehearsal.");
                            }
                        }
                        finally { auxiliaryContext?.Dispose(); auxiliaryContext = null; uiSourceContext?.Dispose(); uiSourceContext = null; uiContext?.Dispose(); uiContext = null; actorContext?.Dispose(); actorContext = null; }
                    }
                }
            }
        }
        void BeginCleanup()
        {
            renderingHold?.Dispose();
            StopRehearsal();
            CloseAiCreationHistory();
            SetState(4, 30);
            returning = SceneManager.GetActiveScene().name == "SceneMenu" ? null : SceneManager.LoadSceneAsync(0);
        }
        void Finish(bool cleanupPassed)
        {
            if (terminal) return;
            renderingHold?.Dispose();
            CloseAiCreationHistory();
            terminal = true; Application.logMessageReceived -= OnLog;
            Dictionary<string, string> personalAfter = null;
            bool personalUnchanged = false;
            try
            {
                personalAfter = Fingerprints(personalRoot);
                personalUnchanged = personalBefore != null && personalBefore.Count == personalAfter.Count && personalBefore.All(p => personalAfter.TryGetValue(p.Key, out var hash) && hash == p.Value);
            }
            catch (Exception error) { errors.Add("Personal fingerprint unavailable: " + error); }
            if (RequiresDiscoveryScreenshots(request.mode))
            {
                try
                {
                    if (screenshots.Count != 6 || screenshots.Any(s => !File.Exists(s.path) || new FileInfo(s.path).Length == 0))
                        errors.Add("Discovery screenshot set is incomplete.");
                }
                catch (Exception error) { errors.Add("Screenshot verification unavailable: " + error); }
            }
            bool passed = cleanupPassed && errors.Count == 0 && personalUnchanged && (certified != null ? certified.Qualified : observation != null && observation.passed);
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed, cleanupPassed, personalUnchanged,
                personalBefore, personalAfter, readyForTiming = certified != null && passed && certified.TimingQualified,
                populationVerified = certified?.PopulationVerified == true,
                boundary = request.config?.renderingInspection != null ? "Standalone paused rendering inspection only; no timing/population/G1B acceptance." : request.config?.checkpointImages == true ? "Standalone internal checkpoint image diagnostic only; timing ineligible. Request frame/time is not exact present frame. Full G1B remains separate." : request.mode == "video1-route-attribution" ? "Explicit diagnostic profiler attribution only; timing is ineligible. Full G1B remains separate." : certified != null ? "Explicit timing/route/population evidence. Rendering/GC/retention/tooling-overhead and full G1B acceptance remain separate." : "Standalone actual production-scene controls discovery only; no frozen route, population or timing acceptance",
                processId = System.Diagnostics.Process.GetCurrentProcess().Id, development = Debug.isDebugBuild,
                mode = request.mode, routeId = request.config?.rehearsal?.routeId, requiredJump = request.config?.rehearsal?.requireObservedJump ?? false,
                scene = SceneManager.GetActiveScene().name, ownedHostStopped = network == null || !network.IsListening,
                saveRoot = request.saveRoot, observationPath = observation?.outputPath, certifiedEvidence = certified?.EvidenceObject, errors, tutorialActions, missionStates,
                cucaEncounterBefore, cucaEncounterAfter, actorPreflight, actorPostflight, actorNotifications, actorScopeFaulted, actorLifecycleHistory, missionProducerDeclarations, postMissionActorPhase = postMissionActorPhase?.Evidence,
                postMissionUiPhase = postMissionUiPhase?.Evidence,
                aiCreationBeforeRoute, aiCreationAtMissionReady, aiCreationAfterRoute, aiCreationAfterReset, aiCreationFinal,
                aiResetRequested, aiResetObserved, aiResetBefore, aiResetAfter,
                aiResetCompleted, aiResetCycles, memorySnapshots, renderingInspection = renderingHold?.Evidence,
                bossReferencesBeforeReset, bossReferencesAfterReset, liveBossReferencesBeforeReset, liveBossReferencesAfterReset,
                screenshots, uiPreflight, uiFrozen, uiPostflight,
                auxiliaryPreflight, auxiliaryFrozen, auxiliaryPostflight, auxiliaryConditionsChanged = auxiliaryValidation?.conditionsChanged, auxiliaryValidationReasons = auxiliaryValidation?.validationUnavailableReasons,
                uiSourcePreflight, uiSourceFrozen, uiSourcePostflight, uiSourceConditionsChanged = uiSourceValidation?.conditionsChanged, uiSourceValidationReasons = uiSourceValidation?.validationUnavailableReasons,
                uiConditionsChanged = uiValidation?.conditionsChanged, uiValidationReasons = uiValidation?.validationUnavailableReasons,
                actorConditionsChanged = actorValidation?.conditionsChanged, actorValidationReasons = actorValidation?.validationUnavailableReasons };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                using (var stream = new FileStream(outputPath, FileMode.CreateNew)) using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
                Debug.Log("QA production-scene rehearsal completed: " + outputPath);
            }
            catch (Exception error) { passed = false; Debug.LogError(error); }
            finally { if (!Application.isEditor) Application.Quit(passed ? 0 : 2); }
        }
        static Dictionary<string, string> Fingerprints(string root)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root)) return hashes;
            using (var sha = SHA256.Create())
                foreach (string path in Directory.GetFiles(root, "characterSlot_*"))
                    using (var stream = File.OpenRead(path)) hashes[Path.GetFileName(path)] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            return hashes;
        }
        static object[] CaptureCucaEncounter() => FindObjectsByType<CucaBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(b => b.gameObject.scene.name == "CucaLoad")
            .Select(b => (object)new { objectId = b.GetInstanceID(), active = b.QaIsActive, retreating = b.QaIsRetreating,
                activations = b.QaActivationCount, projectileAttacks = b.QaProjectileAttackCount, laserAttacks = b.QaLaserAttackCount,
                projectilesCreated = b.QaProjectileCreatedCount, laserActivations = b.QaLaserActivationCount,
                activationRange = b.activationRange, detectionRange = b.detectionRange, stayCloseDuration = b.stayCloseDuration,
                attackCooldown = b.attackCooldown, retreatDistance = b.retreatDistance,
                position = new[] { b.transform.position.x, b.transform.position.y, b.transform.position.z },
                boundary = "Read-only counters on actual production methods; no forced activation, attacks, damage or acceptance" }).ToArray();
        void OnLog(string message, string stack, LogType type)
        {
            using (PerformanceProfilerAttribution.LogMarker.Auto())
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); 
            }
        }
        void OnDisable()
        {
            if (!initialized || terminal) return;
            renderingHold?.Dispose();
            errors.Add("Explicit Tutorial process owner was disabled before completion.");
            try { StopRehearsal(false); }
            catch (Exception error) { errors.Add("Disabled owner cleanup: " + error); }
            try { if (ownsHost && network != null && network.IsListening) network.Shutdown(); }
            catch (Exception error) { errors.Add("Disabled owner host shutdown: " + error); }
            Finish(false);
        }
        void CloseAiCreationHistory()
        {
            if (aiCreationHistory == null) return;
            try { if (request?.config?.measurementArm != "light-control") aiCreationFinal = aiCreationHistory.Capture(); }
            catch (Exception error) { errors.Add("AI history terminal evidence unavailable: " + error); }
            finally { aiCreationHistory.Dispose(); aiCreationHistory = null; }
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            renderingHold?.Dispose();
            try { StopRehearsal(false); }
            catch (Exception error) { errors.Add("Destroyed owner cleanup: " + error); }
            CloseAiCreationHistory();
        }
    }
#endif
}


