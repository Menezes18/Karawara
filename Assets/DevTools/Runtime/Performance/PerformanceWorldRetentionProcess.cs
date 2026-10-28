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
    // Actual production load/return entry points; owned isolated process, never timing acceptance.
    public sealed class PerformanceWorldRetentionProcess : MonoBehaviour
    {
        enum Stage { Menu, Host, World, WorldSettling, Returning, Shutdown, MenuSettling, SnapshotBefore, SnapshotAfter, Done }
        sealed class WorldVisit
        {
            public int visit, sceneHandle, playerId;
            public string scene;
            public float[] position;
            public bool host, owner, grounded;
            public PerformanceAiCreationHistory.Report aiHistory;
        }
        sealed class Transition
        {
            public string stage, scene;
            public int visit, frame;
            public double time;
            public int sceneCheckerCount;
        }
        sealed class Snapshot
        {
            public string path, error;
            public bool completed, succeeded;
            public long bytes;
        }
        PerformanceProcessResponse request;
        WorldSaveGameManager world;
        NetworkManager network;
        PlayerManager player;
        PerformanceAiCreationHistory history;
        AsyncOperation returning;
        Stage stage;
        double deadline, stageAt, groundedSince = -1;
        int visit, completedCycles, shutdownFrame = -1;
        bool initialized, terminal, ownsHost, failed, warmed;
        string outputPath, personalRoot;
        Dictionary<string, string> personalBefore;
        readonly List<string> errors = new List<string>();
        readonly List<WorldVisit> visits = new List<WorldVisit>();
        readonly List<Transition> transitions = new List<Transition>();
        readonly List<Snapshot> snapshots = new List<Snapshot>();
        Snapshot pending;
        public void Initialize(PerformanceProcessResponse value)
        {
            if (Application.isEditor || initialized || value == null || !value.success || value.mode != "world-retention" || value.config?.retention == null)
                throw new InvalidOperationException("Explicit standalone world retention request required.");
            request = value; initialized = true; personalRoot = Application.persistentDataPath;
            outputPath = Path.Combine(request.config.retention.outputRoot, "process-" + Guid.NewGuid().ToString("N") + ".json");
            errors.AddRange(PerformanceTutorialProcess.TakeStartupDiagnostics()); Application.logMessageReceived += OnLog;
            try
            {
                personalBefore = Fingerprints(personalRoot);
                Directory.CreateDirectory(request.config.retention.outputRoot);
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, request.config.quality), true);
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                SetStage(Stage.Menu, 60);
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(false); }
        }
        void SetStage(Stage next, double timeout)
        {
            stage = next; stageAt = Time.realtimeSinceStartupAsDouble; deadline = stageAt + timeout;
            transitions.Add(new Transition { stage = next.ToString(), scene = SceneManager.GetActiveScene().name, visit = visit, frame = Time.frameCount, time = stageAt,
                sceneCheckerCount = UnityEngine.Object.FindObjectsByType<SceneChecker>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length });
        }
        bool SettingsMatch() => Screen.width == 1920 && Screen.height == 1080 && QualitySettings.names[QualitySettings.GetQualityLevel()] == request.config.quality &&
            QualitySettings.vSyncCount == 0 && Application.targetFrameRate == -1 &&
            GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - 1) < .0001;
        void Update()
        {
            if (!initialized || terminal) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now > deadline) throw new TimeoutException("World retention stage " + stage + " visit " + visit);
                if (!failed && errors.Count != 0) throw new InvalidOperationException("Runtime errors during world retention.");
                if (!failed && stage != Stage.Menu && stage != Stage.SnapshotBefore && stage != Stage.SnapshotAfter && (!Application.isFocused || !SettingsMatch()))
                    throw new InvalidOperationException("Focus or rendering conditions changed during retention.");
                switch (stage)
                {
                    case Stage.Menu:
                        world = WorldSaveGameManager.instance; network = NetworkManager.Singleton;
                        if (world == null || network == null || TitleScreenManager.Instance == null || PlayerInputManager.instance?.playerControls == null || !Application.isFocused || !SettingsMatch()) return;
                        if (SceneManager.GetActiveScene().name != "SceneMenu" || network.IsListening || Time.timeScale != 1)
                            throw new InvalidOperationException("Fresh unpaused menu and stopped host required for each visit.");
                        world.SaveRootOverride = request.saveRoot;
                        if (world.SaveRoot != request.saveRoot) throw new InvalidOperationException("Isolated save root rejected.");
                        var writer = new SaveFileDataWriter { saveDataDirectoryPath = request.saveRoot, saveFileName = "characterSlot_01" };
                        var saved = writer.TrySave(PerformanceProcessHarness.ParseVideoSeed(request.config.retention.seedSaveJson));
                        if (!saved.Success) throw new InvalidOperationException("Retention seed write rejected: " + saved.Message);
                        history = PerformanceAiCreationHistory.Begin(new[] { "-karawaraPerformanceRoster" });
                        ownsHost = TitleScreenManager.Instance.TryStartNetworkAsHost();
                        if (!ownsHost) throw new InvalidOperationException("Owned host start rejected.");
                        SetStage(Stage.Host, 60); break;
                    case Stage.Host:
                        player = network.LocalClient?.PlayerObject?.GetComponent<PlayerManager>();
                        if (player == null || !player.IsSpawned || !player.IsOwner || !world.CanCreateNewGame) return;
                        world.RefreshCharacterProfiles(); TitleScreenManager.Instance.SelectCharacterSlot(CharacterSlot.CharacterSlot_01); world.LoadGame();
                        groundedSince = -1; SetStage(Stage.World, 90); break;
                    case Stage.World:
                        var hud = PlayerUIManager.instance?.playerUIHudManager;
                        if (SceneManager.GetActiveScene().name != "Video1" || player == null || !player.IsSpawned || PlayerInputManager.instance?.player != player || hud == null || !hud.IsHealthBarReady) return;
                        if (!player.characterLocomotionManager.isGrounded || Time.timeScale != 1) { groundedSince = -1; return; }
                        if (groundedSince < 0) groundedSince = now;
                        if (now - groundedSince < 1) return;
                        SetStage(Stage.WorldSettling, (warmed ? request.config.retention.settleSeconds : request.config.retention.warmupSeconds) + 15); break;
                    case Stage.WorldSettling:
                        if (player == null || !player.IsSpawned || !player.IsOwner || player.isDead.Value || !network.IsHost || SceneManager.GetActiveScene().name != "Video1" || Time.timeScale != 1)
                            throw new InvalidOperationException("World owner or scene changed during settling.");
                        if (now - stageAt < (warmed ? request.config.retention.settleSeconds : request.config.retention.warmupSeconds)) return;
                        var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                        var report = history.Capture(census);
                        if (!report.currentAiHistoryMatched || report.currentAiCount != 24) throw new InvalidOperationException("Expected actual Video1 AI creation set not matched.");
                        var position = player.transform.position;
                        visits.Add(new WorldVisit { visit = visit, scene = SceneManager.GetActiveScene().name, sceneHandle = SceneManager.GetActiveScene().handle,
                            playerId = player.gameObject.GetInstanceID(), host = network.IsHost, owner = player.IsOwner, grounded = player.characterLocomotionManager.isGrounded,
                            position = new[] { position.x, position.y, position.z }, aiHistory = report });
                        history.Dispose(); history = null;
                        // Invoke the real HUD return action; errors in that path must fail this diagnostic.
                        SetStage(Stage.Returning, 30); PlayerUIManager.instance.playerUIHudManager.ChangeScene(); break;
                    case Stage.Returning:
                        if (returning != null && !returning.isDone) return;
                        if (SceneManager.GetActiveScene().name != "SceneMenu") return;
                        if (ownsHost && network != null && network.IsListening) network.Shutdown();
                        shutdownFrame = -1; SetStage(Stage.Shutdown, 20); break;
                    case Stage.Shutdown:
                        if ((network != null && network.IsListening) || (player != null && player.IsSpawned)) return;
                        if (shutdownFrame < 0) shutdownFrame = Time.frameCount;
                        if (Time.frameCount < shutdownFrame + 2) return;
                        ownsHost = false; player = null; SetStage(Stage.MenuSettling, 15); break;
                    case Stage.MenuSettling:
                        if (SceneManager.GetActiveScene().name != "SceneMenu" || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) || Time.timeScale != 1)
                            throw new InvalidOperationException("Unpaused menu and stopped host required for retention snapshot/cycle boundary.");
                        if (now - stageAt < request.config.retention.settleSeconds) return;
                        if (failed) { Finish(false); return; }
                        if (UnityEngine.Object.FindObjectsByType<SceneChecker>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                            throw new InvalidOperationException("Expected one persistent SceneChecker after normal menu return.");
                        if (!warmed) { warmed = true; TakeSnapshot("menu-before-ten-cycles", Stage.SnapshotBefore); }
                        else
                        {
                            completedCycles++;
                            if (completedCycles == request.config.retention.cycles) TakeSnapshot("menu-after-ten-cycles", Stage.SnapshotAfter);
                            else { visit++; SetStage(Stage.Menu, 60); }
                        }
                        break;
                    case Stage.SnapshotBefore:
                    case Stage.SnapshotAfter:
                        if (!pending.completed) return;
                        if (!pending.succeeded) throw new InvalidOperationException("Memory snapshot failed: " + pending.error);
                        if (stage == Stage.SnapshotAfter) Finish(true);
                        else { visit++; SetStage(Stage.Menu, 60); }
                        break;
                }
            }
            catch (Exception error)
            {
                errors.Add(error.ToString());
                if (failed) { if (ownsHost && network != null && network.IsListening) network.Shutdown(); Finish(false); return; }
                failed = true; history?.Dispose(); history = null;
                // Emergency cleanup is retained as failure, never an accepted production-return cycle.
                returning = SceneManager.GetActiveScene().name == "SceneMenu" ? null : SceneManager.LoadSceneAsync(0);
                SetStage(Stage.Returning, 30);
            }
        }
        void TakeSnapshot(string label, Stage next)
        {
            var result = new Snapshot { path = Path.Combine(request.config.retention.outputRoot, label + "-" + Guid.NewGuid().ToString("N") + ".snap") };
            if (File.Exists(result.path)) throw new IOException("Refuse to overwrite snapshot.");
            pending = result; snapshots.Add(result); SetStage(next, 90);
            Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(result.path, (path, success) =>
            {
                try
                {
                    result.succeeded = success && string.Equals(Path.GetFullPath(path), Path.GetFullPath(result.path), StringComparison.OrdinalIgnoreCase) && File.Exists(result.path);
                    if (result.succeeded) result.bytes = new FileInfo(result.path).Length;
                    result.succeeded &= result.bytes > 0;
                    if (!result.succeeded) result.error = "Requested nonempty snapshot was not written.";
                }
                catch (Exception error) { result.error = error.ToString(); result.succeeded = false; }
                finally { result.completed = true; }
            }, Unity.Profiling.Memory.CaptureFlags.ManagedObjects | Unity.Profiling.Memory.CaptureFlags.NativeObjects | Unity.Profiling.Memory.CaptureFlags.NativeAllocations);
        }
        void Finish(bool cleanupPassed)
        {
            if (terminal) return; terminal = true; history?.Dispose(); history = null; Application.logMessageReceived -= OnLog;
            var personalAfter = CaptureFinalFingerprints(personalRoot, errors);
            bool personalUnchanged = personalBefore != null && personalAfter != null && personalBefore.Count == personalAfter.Count && personalBefore.All(pair => personalAfter.TryGetValue(pair.Key, out var value) && value == pair.Value);
            bool hostStopped = (network == null || !network.IsListening) && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);
            bool passed = !failed && errors.Count == 0 && cleanupPassed && hostStopped && personalUnchanged && completedCycles == 10 && visits.Count == 11 && snapshots.Count == 2 && snapshots.All(s => s.completed && s.succeeded);
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed, cleanupPassed, personalUnchanged, personalBefore, personalAfter,
                mode = request.mode, routeId = request.config.retention.routeId, completedCycles, visits, transitions, snapshots, errors,
                scene = SceneManager.GetActiveScene().name, ownedHostStopped = hostStopped, saveRoot = request.saveRoot,
                readyForTiming = false, populationVerified = false, retentionAccepted = false,
                boundary = "Actual isolated menu-world-menu cycles and raw snapshots only; retained-reference analysis, skill/pet/projectile cycles and G1B acceptance separate" };
            try { using (var stream = new FileStream(outputPath, FileMode.CreateNew)) using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented)); }
            catch (Exception error) { passed = false; Debug.LogError(error); }
            finally { Application.Quit(passed ? 0 : 2); }
        }
        static Dictionary<string, string> CaptureFinalFingerprints(string root, List<string> errors)
        {
            try { return Fingerprints(root); }
            catch (Exception error) { errors.Add("Final personal fingerprint failed: " + error); return null; }
        }
        static Dictionary<string, string> Fingerprints(string root)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root)) return result;
            using (var sha = SHA256.Create()) foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                using (var stream = File.OpenRead(path)) result[path.Substring(root.Length)] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            return result;
        }
        void OnLog(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 256) errors.Add(message + "\n" + stack); }
        void OnDisable()
        {
            if (!initialized || terminal) return;
            failed = true; errors.Add("Owned world retention process disabled before completion.");
            history?.Dispose(); history = null; if (ownsHost && network != null && network.IsListening) network.Shutdown(); Finish(false);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; history?.Dispose(); history = null; }
    }
#endif
}
