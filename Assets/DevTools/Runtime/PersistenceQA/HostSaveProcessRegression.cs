using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using RPGKarawara.SkillTree;
using Karawara.Core.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGKarawara.DevTools.PersistenceQA
{
    [Serializable] public sealed class HostSaveRequest
    {
        public bool handled, success;
        public string action, saveRoot, outputRoot, expectedPath;
        public int slot;
        public int[] slots;
        public List<string> errors = new List<string>();
    }
    [Serializable] public sealed class HostSaveOutcome
    {
        public bool success, supportedHostPassed, skillRoundTripPassed;
        public string status;
        public int exitCode;
    }
    public static class HostSaveProcessRegression
    {
        public static HostSaveRequest ParseRequest(string[] args, string personalRoot)
        {
            var result = new HostSaveRequest();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            args = args ?? Array.Empty<string>();
            if (Count(args, "-karawaraHostSaveRegression") == 0) return result;
            result.handled = true;
            try
            {
                result.action = Value(args, "-karawaraHostSaveRegression");
                Require(result.action == "write-three" || result.action == "read", "Supported actions are write-three and read.");
                Require(Count(args, "-karawaraSaveRegression") == 0 && Count(args, "-karawaraPerformanceConfig") == 0, "Do not combine independent QA bootstraps.");
                result.saveRoot = Absolute(Value(args, "-karawaraSaveRoot"));
                result.outputRoot = Absolute(Value(args, "-karawaraHostSaveOutput"));
                string personal = Absolute(personalRoot);
                Separate(result.saveRoot, personal); Separate(result.outputRoot, personal); Separate(result.outputRoot, result.saveRoot);
                RejectLinks(result.saveRoot); RejectLinks(result.outputRoot);
                Require(!File.Exists(result.saveRoot) && !File.Exists(result.outputRoot), "QA roots must not be existing files.");
                if (result.action == "write-three")
                {
                    Require(Count(args, "-karawaraHostSaveSlot") == 0 && Count(args, "-karawaraHostSaveExpected") == 0, "Writer always creates three slots and accepts no reader parameters.");
                    Require(!Directory.Exists(result.saveRoot) || !Directory.EnumerateFileSystemEntries(result.saveRoot).Any(), "Writer requires a new empty QA save root.");
                    result.slots = new[] { 1, 2, 3 };
                }
                else
                {
                    int slot;
                    Require(int.TryParse(Value(args, "-karawaraHostSaveSlot"), out slot) && slot >= 1 && slot <= 3, "Read requires a slot from 1 to 3.");
                    result.slot = slot; result.slots = new[] { slot };
                    result.expectedPath = Absolute(Value(args, "-karawaraHostSaveExpected"));
                    RejectLinks(result.expectedPath);
                    Require(File.Exists(result.expectedPath), "Expected writer manifest must exist.");
                }
                result.success = true;
            }
            catch (Exception error) { result.errors.Add(error.GetType().Name + ": " + error.Message); }
#endif
            return result;
        }
        public static bool SupportedScalarsEqual(CharacterSaveData actual, CharacterSaveData expected) => actual != null && expected != null &&
            Finite(actual.xPosition) && Finite(actual.yPosition) && Finite(actual.zPosition) && Finite(expected.xPosition) && Finite(expected.yPosition) && Finite(expected.zPosition) &&
            actual.characterName == expected.characterName && actual.sceneIndex == expected.sceneIndex && actual.xPosition == expected.xPosition && actual.yPosition == expected.yPosition && actual.zPosition == expected.zPosition &&
            actual.currentHealth == expected.currentHealth && actual.vitality == expected.vitality && actual.endurance == expected.endurance;
        public static HostSaveOutcome EvaluateOutcome(bool scalarHostPassed, bool spawnPassed, bool applyOncePassed, bool skillRoundTripPassed, bool cleanupPassed)
        {
            bool host = scalarHostPassed && spawnPassed && applyOncePassed && cleanupPassed;
            return new HostSaveOutcome { success = host && skillRoundTripPassed, supportedHostPassed = host, skillRoundTripPassed = skillRoundTripPassed,
                status = !host ? "Failed" : skillRoundTripPassed ? "Passed" : "LegacySkillsUnresolved", exitCode = !host ? 1 : skillRoundTripPassed ? 0 : 3 };
        }
        public static bool IsStableGroundWindow(Vector3[] positions)
        {
            if (positions == null || positions.Length < 5) return false;
            for (int i = 0; i < positions.Length; i++)
            {
                if (!Finite(positions[i].x) || !Finite(positions[i].y) || !Finite(positions[i].z)) return false;
                for (int j = 0; j < i; j++) if (Vector3.Distance(positions[i], positions[j]) > .15f) return false;
            }
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        static HostRuntimeLogCollector activatedLogs;
        public static HostSaveOutcome EvaluateObservedOutcome(bool scalarHostPassed, bool spawnPassed, bool applyOncePassed, bool skillRoundTripPassed, bool cleanupPassed, HostRuntimeLogCollector collector) =>
            EvaluateOutcome(scalarHostPassed && collector != null && collector.Failures.Count == 0, spawnPassed, applyOncePassed, skillRoundTripPassed, cleanupPassed);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetObservation() { activatedLogs?.Dispose(); activatedLogs = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeginObservation()
        {
            if (Application.isEditor) return;
            var request = ParseRequest(Environment.GetCommandLineArgs(), Application.persistentDataPath);
            if (request.handled && request.success) activatedLogs = new HostRuntimeLogCollector();
        }
        static int Count(string[] args, string flag) => args.Count(a => a == flag);
        static string Value(string[] args, string flag)
        {
            Require(Count(args, flag) == 1, "Exactly one " + flag + " is required.");
            int index = Array.IndexOf(args, flag);
            Require(index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]) && !args[index + 1].StartsWith("-"), "Missing value for " + flag);
            return args[index + 1];
        }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string Absolute(string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path), "Paths must be fully qualified.");
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Require(full != Path.GetPathRoot(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "Filesystem roots cannot be QA paths.");
            return full;
        }
        static void Separate(string first, string second)
        {
            var comparison = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Require(!first.Equals(second, comparison) && !first.StartsWith(second + Path.DirectorySeparatorChar, comparison) && !second.StartsWith(first + Path.DirectorySeparatorChar, comparison), "Personal/save/output roots must not overlap.");
        }
        static void RejectLinks(string path)
        {
            for (string item = path; !string.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
                Require(!(File.Exists(item) || Directory.Exists(item)) || (File.GetAttributes(item) & FileAttributes.ReparsePoint) == 0, "QA paths cannot traverse links/junctions.");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor) return; // Actual fresh processes only; the Editor fixture invokes pure contracts.
            var request = ParseRequest(Environment.GetCommandLineArgs(), Application.persistentDataPath);
            if (!request.handled) return;
            if (!request.success) { Debug.LogError("QA host request rejected: " + string.Join("; ", request.errors)); activatedLogs?.Dispose(); Application.Quit(1); return; }
            HostSaveManifest expected = null;
            try
            {
                if (request.action == "read")
                {
                    Require(new FileInfo(request.expectedPath).Length <= 4 * 1024 * 1024, "Expected manifest exceeds 4 MiB.");
                    expected = JsonUtility.FromJson<HostSaveManifest>(File.ReadAllText(request.expectedPath));
                    Require(expected != null && expected.schemaVersion == 1 && expected.completed && expected.action == "write-three" && expected.slots != null && expected.slots.Count == 3, "Completed three-slot writer manifest is required.");
                    Require(expected.scalarHostPassed && expected.spawnPassed && expected.applyOncePassed && expected.cleanupPassed && expected.personalFilesUnchanged, "Writer's supported host/cleanup evidence must pass.");
                    Require(expected.executableHash == HostSaveProcess.HashExecutable(), "Reader must use the same actual executable as writer.");
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        var matches = expected.slots.Where(s => s.slot == slot).ToArray();
                        Require(matches.Length == 1 && matches[0].data != null && matches[0].filename == "characterSlot_0" + slot && matches[0].data.sceneIndex == 1 && SupportedScalarsEqual(matches[0].data, matches[0].data), "Malformed expected slot/scene/scalars.");
                        Require(!string.IsNullOrWhiteSpace(matches[0].data.characterName) && matches[0].data.currentHealth > 1 && matches[0].data.vitality > 0 && matches[0].data.endurance > 0, "Expected supported live state is invalid.");
                        _ = Convert.ToString(matches[0].data.characterName);
                        Require(matches[0].equipped != null && matches[0].attack != null && matches[0].defense != null && matches[0].support != null && !string.IsNullOrWhiteSpace(matches[0].rawSaveJson), "Expected writer skill/raw evidence missing.");
                        string primary = Path.Combine(request.saveRoot, matches[0].filename);
                        Require(File.Exists(primary) && HostSaveProcess.Hash(primary) == matches[0].primaryHash, "Reader input differs from writer's committed slot " + slot);
                    }
                }
                var owned = new GameObject("Karawara QA actual-host process");
                UnityEngine.Object.DontDestroyOnLoad(owned);
                owned.AddComponent<HostSaveProcess>().Initialize(request, expected, activatedLogs);
            }
            catch (Exception error) { Debug.LogError("QA host bootstrap rejected before host: " + error); activatedLogs?.Dispose(); Application.Quit(1); }
        }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Subscribes to actual main-thread Unity log delivery; stores failures without generating recursive logs.
    public sealed class HostRuntimeLogCollector : IDisposable
    {
        readonly List<string> failures = new List<string>();
        readonly System.Collections.ObjectModel.ReadOnlyCollection<string> view;
        bool disposed;
        public IReadOnlyList<string> Failures => view;
        public HostRuntimeLogCollector() { view = failures.AsReadOnly(); Application.logMessageReceived += Observe; }
        void Observe(string message, string trace, LogType type)
        {
            if (!disposed && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                failures.Add(type + ": " + message + "\n" + trace);
        }
        public void Dispose() { if (disposed) return; disposed = true; Application.logMessageReceived -= Observe; }
    }
    [Serializable] public sealed class HostSkillObservation
    {
        public bool missing, unlocked, active;
        public string signature, type, authoredName, objectName;
        public int instanceId;
    }
    [Serializable] public sealed class HostGroundObservation
    {
        public int frame;
        public float gameTime, timeScale, footGap;
        public Vector3 position, normal;
        public bool hit, grounded;
        public string collider;
    }
    [Serializable] public sealed class HostSlotObservation
    {
        public int slot, observedFrames, restoreEvents;
        public string filename, primaryHash, backupHash, rawSaveJson, error;
        public CharacterSaveData data;
        public bool scalarHostPassed, spawnPassed, applyOncePassed, skillRoundTripPassed, exactRestoreObserved, host, owner, spawned, hudReady, cameraReady;
        public float hudHealth;
        public long generation;
        public int localCharacterId;
        public Vector3 actualRestore;
        public HostSkillObservation[] equipped, attack, defense, support;
        public List<HostGroundObservation> ground = new List<HostGroundObservation>();
        public List<string> skillErrors = new List<string>();
    }
    [Serializable] public sealed class HostSaveManifest
    {
        public int schemaVersion = 1, processId, exitCode, actualSkillCatalogCount;
        public string utc, finishedUtc, action, saveRoot, executableHash, expectedManifestHash, diagnosticPath, stage, status;
        public bool completed, success, supportedHostPassed, scalarHostPassed, spawnPassed, applyOncePassed, skillRoundTripPassed, cleanupPassed, personalFilesUnchanged;
        public List<HostSlotObservation> slots = new List<HostSlotObservation>();
        public List<string> errors = new List<string>(), tutorialActions = new List<string>(), limits = new List<string>();
        public List<string> runtimeErrors = new List<string>();
        public List<string> personalBefore = new List<string>(), personalAfter = new List<string>();
    }
    // One explicit QA process, actual production Title/NGO/load/capture paths; no direct ApplyLegacySave or skill repair.
    public sealed class HostSaveProcess : MonoBehaviour
    {
        HostSaveRequest request;
        HostSaveManifest report, expected;
        HostSlotObservation current, expectedSlot;
        WorldSaveGameManager manager;
        PlayerManager player;
        SkillTreeUiManager skillUi;
        Skill[] catalog;
        Dictionary<string, Skill[]> catalogBySignature;
        bool ownsHost, terminal, inputCaptured, previousInputEnabled, previousInvulnerable, invulnerabilityCaptured;
        int state, writerSlot, stableFrames, initialFrame, perturbHealth;
        double deadline, nextSample;
        float observationGameTime;
        Action<int, int> healthChanged;
        Action<Vector3, Vector3> positionChanged;
        Dictionary<string, string> inputHashes;
        HostRuntimeLogCollector runtimeLogs;
        int shutdownFrame = -1;

        public void Initialize(HostSaveRequest value, HostSaveManifest manifest, HostRuntimeLogCollector collector)
        {
            request = value; expected = manifest; runtimeLogs = collector;
            Check(runtimeLogs != null, "Runtime errors must be observed from activation before any host command.");
            report = new HostSaveManifest { utc = DateTime.UtcNow.ToString("O"), action = request.action, saveRoot = request.saveRoot, processId = System.Diagnostics.Process.GetCurrentProcess().Id, executableHash = HashExecutable() };
            report.personalBefore = PersonalFingerprints();
            if (request.action == "read") { report.expectedManifestHash = Hash(request.expectedPath); inputHashes = SaveFingerprints(request.saveRoot); }
            Directory.CreateDirectory(request.outputRoot);
            report.diagnosticPath = Path.Combine(request.outputRoot, "qa-host-process-" + Guid.NewGuid().ToString("N") + ".json");
            report.limits.Add("Explicit functional QA: player input blocked and invulnerability enabled. No performance or natural-input gameplay acceptance.");
            report.limits.Add("CurrentStamina and secondsPlayed are DTO-only: no current production live capture/apply binding. World dictionaries/equipment are not verified.");
            report.limits.Add("Skill signatures are diagnostic comparators against the pre-load actual catalog, not a new save schema or runtime repair. Ordinary Release activation omission requires its own real build/process evidence.");
            SetState(0, "Awaiting actual SceneMenu", 30);
        }
        void Update()
        {
            if (terminal || report == null) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (state >= 2 && state <= 5) { BlockInput(); ContinueTutorial(); }
                if (state == 0)
                {
                    manager = WorldSaveGameManager.instance;
                    if (SceneManager.GetActiveScene().name == "SceneMenu" && manager != null && TitleScreenManager.Instance != null && GameBootstrap.Instance != null)
                    {
                        Check(manager.SaveRoot == request.saveRoot, "Validated QA save root must be effective before host startup.");
                        Check(!GameBootstrap.Instance.Session.IsActive, "Do not reuse an existing host/client.");
                        Check(TitleScreenManager.Instance.TryStartLocalRuntime(), "Actual production Title host startup failed: " + TitleScreenManager.Instance.SessionStatus);
                        ownsHost = true;
                        if (File.Exists(Path.Combine(request.saveRoot, "characterSlot_01"))) _ = GameBootstrap.Instance.Session.LoadGameAsync(1);
                        else _ = GameBootstrap.Instance.Session.StartNewGameAsync(1);
                        SetState(1, "Awaiting actual local session/HUD/camera/skills", 60);
                    }
                }
                else if (state == 1)
                {
                    player = FindLocalPlayer();
                    if (Ready(false))
                    {
                        Check(UnityEngine.InputSystem.Keyboard.current != null, "Production Keyboard.current readiness missing; headless substitution forbidden.");
                        CaptureCatalog();
                        previousInvulnerable = player.characterNetworkManager.isInvulnerable.Value; invulnerabilityCaptured = true;
                        player.characterNetworkManager.isInvulnerable.Value = true; BlockInput();
                        healthChanged = (before, after) => { if (current != null && expectedSlot != null && after == expectedSlot.data.currentHealth) current.restoreEvents++; };
                        positionChanged = (before, after) =>
                        {
                            if (current != null && expectedSlot != null && SceneManager.GetActiveScene().buildIndex == expectedSlot.data.sceneIndex &&
                                Vector3.Distance(after, Pose(expectedSlot.data)) < .0001f && Vector3.Distance(player.transform.position, Pose(expectedSlot.data)) < .0001f)
                            { current.actualRestore = player.transform.position; current.exactRestoreObserved = true; }
                        };
                        player.playerNetworkManager.currentHealth.OnValueChanged += healthChanged;

                        BeginSlot();
                    }
                }
                else if (state == 2)
                {
                    if (Ready(true) && ApplicationRecorded())
                    {
                        if (request.action == "read")
                        {
                            if (!current.exactRestoreObserved && Vector3.Distance(player.transform.position, Pose(expectedSlot.data)) < .0001f && Vector3.Distance(player.transform.position, Pose(expectedSlot.data)) < .0001f)
                            { current.actualRestore = player.transform.position; current.exactRestoreObserved = true; }
                            Check(current.exactRestoreObserved, "Actual saved XYZ restore was not observed before physics; no inferred/projected acceptance.");
                            Check(current.restoreEvents == 1, "Fresh reader requires exactly one initial health restoration event; observed " + current.restoreEvents);
                            var live = LiveData(); live.xPosition = current.actualRestore.x; live.yPosition = current.actualRestore.y; live.zPosition = current.actualRestore.z;
                            current.scalarHostPassed = HostSaveProcessRegression.SupportedScalarsEqual(live, expectedSlot.data) && HudHealth() == expectedSlot.data.currentHealth;
                            Check(current.scalarHostPassed, "Actual host name/scene/HP/vitality/endurance/XYZ/HUD mismatch.");
                            current.data = Scalars(expectedSlot.data);
                            ObserveSkills(current, false);
                            observationGameTime = Time.time; initialFrame = Time.frameCount; nextSample = 0;
                            SetState(4, "Observing actual fresh-process restored grounding", 45);
                        }
                        else { stableFrames = 0; initialFrame = Time.frameCount; nextSample = 0; SetState(3, "Naturally settling production new-game pose", 60); }
                    }
                }
                else if (state == 3)
                {
                    SampleGround();
                    var last = current.ground.LastOrDefault();
                    stableFrames = last != null && Grounded(last, .25f) ? stableFrames + 1 : 0;
                    if (Time.frameCount - initialFrame >= 30 && stableFrames >= 5 && current.ground.Count >= 5 && current.ground.Skip(current.ground.Count - 5).All(g => Grounded(g, .25f)) &&
                        HostSaveProcessRegression.IsStableGroundWindow(current.ground.Skip(current.ground.Count - 5).Select(g => g.position).ToArray()) && Time.time > current.ground[0].gameTime)
                        WriteGroundedSlot();
                }
                else if (state == 4)
                {
                    SampleGround(); current.observedFrames = Time.frameCount - initialFrame;
                    Check(player.playerNetworkManager.currentHealth.Value == expectedSlot.data.currentHealth && HudHealth() == expectedSlot.data.currentHealth, "Restored HP/HUD changed during protected observation.");
                    Check(Math.Abs(player.transform.position.y - expectedSlot.data.yPosition) <= 1.5f && Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(expectedSlot.data.xPosition, expectedSlot.data.zPosition)) <= .25f, "Restored player left measured saved-pose envelope.");
                    if (current.observedFrames >= 30 && Time.time - observationGameTime >= 3)
                    {
                        current.spawnPassed = current.ground.Count >= 3 && current.ground.Skip(current.ground.Count - 3).All(g => Grounded(g, 1.5f));
                        Check(current.spawnPassed, "Actual restored player did not remain safely grounded.");
                        perturbHealth = expectedSlot.data.currentHealth - 1;
                        Check(perturbHealth > 0, "HP perturbation must remain positive.");
                        player.playerNetworkManager.currentHealth.Value = perturbHealth;
                        initialFrame = Time.frameCount; SetState(5, "Testing completed-generation no-reapply after HP perturbation", 10);
                    }
                }
                else if (state == 5)
                {
                    Check(!manager.ApplyPendingSaveWhenReady(), "Completed generation reapplied through readiness path.");
                    Check(player.playerNetworkManager.currentHealth.Value == perturbHealth && HudHealth() == perturbHealth, "Completed generation overwrote HP/HUD perturbation.");
                    Check(current.restoreEvents == 1, "Unexpected additional saved-health restoration event after completed generation.");
                    if (Time.frameCount - initialFrame >= 30)
                    {
                        current.applyOncePassed = ApplicationRecorded();
                        Check(current.applyOncePassed, "Completed generation bookkeeping missing after no-reapply observation.");
                        Check(inputHashes.All(pair => File.Exists(pair.Key) && Hash(pair.Key) == pair.Value) && SaveFingerprints(request.saveRoot).Count == inputHashes.Count, "Reader changed QA save candidates.");
                        CompleteSlots();
                    }
                }
                else if (state == 6)
                {
                    if ((GameBootstrap.Instance == null || !GameBootstrap.Instance.Session.IsActive) && (player == null || !player.IsInitialized))
                    {
                        if (shutdownFrame < 0) shutdownFrame = Time.frameCount;
                        // Observe production updates after the real player teardown before writing terminal evidence.
                        if (Time.frameCount - shutdownFrame >= 2) { report.cleanupPassed = true; Finish(); return; }
                    }
                    else shutdownFrame = -1;
                }
                if (!terminal && now >= deadline) throw new TimeoutException("Stage deadline: " + report.stage + "; timeScale=" + Time.timeScale + "; gameTime=" + Time.time);
            }
            catch (Exception error) { Fail(error.GetBaseException().Message); }
        }
        void BeginSlot()
        {
            int slot = request.action == "read" ? request.slot : ++writerSlot;
            current = new HostSlotObservation { slot = slot, filename = "characterSlot_0" + slot }; report.slots.Add(current);
            expectedSlot = request.action == "read" ? expected.slots.Single(s => s.slot == slot) : null;
            if (request.action == "read")
            {
                TitleScreenManager.Instance.currentSelectedSlot = Slot(slot);
                var disk = manager.TryLoadSelectedSlot(Slot(slot));
                Check(disk.Success && HostSaveProcessRegression.SupportedScalarsEqual(disk.Value, expectedSlot.data), "Typed selected slot did not match expected transport fields.");
                current.rawSaveJson = File.ReadAllText(Path.Combine(request.saveRoot, current.filename));
                current.primaryHash = Hash(Path.Combine(request.saveRoot, current.filename));
                manager.LoadGame();
            }
            else
            {
                var created = manager.TryCreateNewGame();
                Check(created.Success && manager.currentCharacterSlotBeingUsed == Slot(slot), "Actual new-game command failed/selected wrong empty slot: " + created.Message);
            }
            Check(string.IsNullOrEmpty(manager.SaveStatus) && manager.currentCharacterData != null && manager.currentCharacterData.sceneIndex == 1, "Actual accepted save/new-game must target enabled Tutorial_Final without load errors.");
            SetState(2, "Awaiting production scene/readiness/completed save generation for slot " + slot, 120);
        }
        void WriteGroundedSlot()
        {
            Check(current.ground.Count >= 5 && HostSaveProcessRegression.IsStableGroundWindow(current.ground.Skip(current.ground.Count - 5).Select(g => g.position).ToArray()), "Actual recent grounded position window changed before gameplay capture.");
            var network = player.playerNetworkManager;
            network.characterName.Value = Convert.ToString("QA Host Slot " + current.slot);
            network.vitality.Value = 5 + current.slot * 5; network.endurance.Value = 10 + current.slot; network.currentHealth.Value = 60 + 10 * current.slot;
            var skills = player.GetComponent<PlayerSkillManager>();
            for (int i = 0; i < skills.slot.Length; i++) { if (skills.slot[i] == null) skills.slot[i] = new SkillSlot(); skills.slot[i].skillSlot = null; }
            Skill chosen = catalog.Length > 0 ? catalog[(current.slot - 1) % catalog.Length] : null;
            if (skills.slot.Length > 0) skills.slot[0].skillSlot = chosen;
            skillUi.attackSkills = chosen != null && chosen.skillType == SkillType.Attack ? new[] { chosen } : Array.Empty<Skill>();
            skillUi.defenseSkills = chosen != null && chosen.skillType == SkillType.Defense ? new[] { chosen } : Array.Empty<Skill>();
            skillUi.supportSkills = chosen != null && chosen.skillType == SkillType.Support ? new[] { chosen } : Array.Empty<Skill>();
            Vector3 actual = player.transform.position;
            var saved = manager.TrySaveGame(); Check(saved.Success, "Actual grounded TrySaveGame failed: " + saved.Message);
            var loaded = manager.TryLoadSelectedSlot(Slot(current.slot)); Check(loaded.Success, "Committed gameplay save could not be read: " + loaded.Message);
            var live = LiveData();
            current.data = Scalars(loaded.Value);
            current.scalarHostPassed = HostSaveProcessRegression.SupportedScalarsEqual(live, current.data) && HudHealth() == current.data.currentHealth;
            Check(current.scalarHostPassed, "Actual gameplay capture differs from live supported state/HUD.");
            Check(Pose(current.data) == actual, "Grounded save did not record actual player position.");
            current.spawnPassed = true; current.actualRestore = actual;
            current.applyOncePassed = ApplicationRecorded() && !manager.ApplyPendingSaveWhenReady();
            Check(current.applyOncePassed, "New-game generation was not complete before saving.");
            current.rawSaveJson = File.ReadAllText(Path.Combine(request.saveRoot, current.filename));
            current.primaryHash = Hash(Path.Combine(request.saveRoot, current.filename));
            string backup = Path.Combine(request.saveRoot, current.filename + ".bak"); current.backupHash = File.Exists(backup) ? Hash(backup) : null;
            ObserveSkills(current, true);
            Snapshot();
            if (writerSlot < 3) BeginSlot(); else CompleteSlots();
        }
        void CompleteSlots()
        {
            Snapshot();
            report.scalarHostPassed = report.slots.Count == request.slots.Length && report.slots.All(s => s.scalarHostPassed);
            report.spawnPassed = report.slots.All(s => s.spawnPassed);
            report.applyOncePassed = report.slots.All(s => s.applyOncePassed);
            report.skillRoundTripPassed = report.slots.All(s => s.skillRoundTripPassed);
            Cleanup();
        }
        void CaptureCatalog()
        {
            // Outside any measurement; actual authored catalog references only, never a clone or a repaired loaded array.
            catalog = Resources.FindObjectsOfTypeAll<Skill>().Where(s => s != null).OrderBy(Signature, StringComparer.Ordinal).ToArray();
            catalogBySignature = catalog.GroupBy(Signature).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            report.actualSkillCatalogCount = catalog.Length;
        }
        void ObserveSkills(HostSlotObservation target, bool writer)
        {
            var skills = player.GetComponent<PlayerSkillManager>();
            var equipped = skills.slot.Select(s => s != null ? s.skillSlot : null).ToArray();
            target.equipped = Describe(equipped); target.attack = Describe(skillUi.attackSkills); target.defense = Describe(skillUi.defenseSkills); target.support = Describe(skillUi.supportSkills);
            bool canonical = Canonical(equipped) && Canonical(skillUi.attackSkills) && Canonical(skillUi.defenseSkills) && Canonical(skillUi.supportSkills);
            if (catalog.Length < 3) target.skillErrors.Add("Fewer than three actual distinct pre-load skills; three-slot skill isolation is unverified.");
            if (!canonical) target.skillErrors.Add("Loaded skill is missing from or ambiguous against pre-load actual catalog; no reference repaired.");
            target.skillRoundTripPassed = canonical && catalog.Length >= 3;
            if (!writer)
            {
                target.skillRoundTripPassed &= SameSkills(target.equipped, expectedSlot.equipped) && SameSkills(target.attack, expectedSlot.attack) && SameSkills(target.defense, expectedSlot.defense) && SameSkills(target.support, expectedSlot.support);
                if (!target.skillRoundTripPassed) target.skillErrors.Add("Actual fresh-process equipped/UI Skill[] differs from writer's preserved observations.");
            }
            else
            {
                var disk = manager.TryLoadSelectedSlot(Slot(target.slot));
                target.skillRoundTripPassed &= disk.Success && SameSkills(Describe(disk.Value.skillsSlot), target.equipped) && SameSkills(Describe(disk.Value.SkillAttack), target.attack) && SameSkills(Describe(disk.Value.SkillDefense), target.defense) && SameSkills(Describe(disk.Value.SkillSuport), target.support);
                if (!target.skillRoundTripPassed) target.skillErrors.Add("Same-process serialized skill check is unresolved; raw JSON retained, no repair.");
            }
        }
        bool Canonical(Skill[] values) => values == null || values.All(s => s == null || (catalogBySignature.TryGetValue(Signature(s), out var matches) && matches.Length == 1 && matches[0] == s));
        static HostSkillObservation[] Describe(Skill[] values) => values == null ? null : values.Select(s => s == null ? new HostSkillObservation { missing = true } : new HostSkillObservation { signature = Signature(s), type = s.GetType().FullName, authoredName = s.name, objectName = ((UnityEngine.Object)s).name, instanceId = s.GetInstanceID(), unlocked = s.isUnlocked, active = s.active }).ToArray();
        static bool SameSkills(HostSkillObservation[] actual, HostSkillObservation[] wanted) => actual != null && wanted != null && actual.Length == wanted.Length && actual.Zip(wanted, (a, b) => a.missing == b.missing && (a.missing || (a.signature == b.signature && a.unlocked == b.unlocked && a.active == b.active))).All(b => b);
        static string Signature(Skill skill) => HashText(skill.GetType().FullName + "|" + ((UnityEngine.Object)skill).name + "|" + skill.name + "|" + skill.skillType + "|" + skill.cooldownDuration.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + skill.description);
        void SampleGround()
        {
            if (Time.realtimeSinceStartupAsDouble < nextSample) return;
            nextSample = Time.realtimeSinceStartupAsDouble + .1;
            var controller = player.characterController;
            Vector3 foot = player.transform.TransformPoint(controller.center) - Vector3.up * (controller.height * player.transform.lossyScale.y * .5f);
            var sample = new HostGroundObservation { frame = Time.frameCount, gameTime = Time.time, timeScale = Time.timeScale, position = player.transform.position, grounded = player.characterLocomotionManager.isGrounded };
            var hits = Physics.RaycastAll(foot + Vector3.up * 2, Vector3.down, 8, ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.gameObject.scene.buildIndex == 1 && h.collider.GetComponentInParent<CharacterManager>() == null).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) { sample.hit = true; sample.footGap = foot.y - hits[0].point.y; sample.normal = hits[0].normal; sample.collider = hits[0].collider.name; }
            current.ground.Add(sample);
        }
        static bool Grounded(HostGroundObservation value, float maximumGap) => value.hit && value.grounded && value.timeScale > 0 && value.footGap >= -.25f && value.footGap <= maximumGap && Vector3.Dot(value.normal, Vector3.up) > .3f;
        bool Ready(bool world)
        {
            skillUi = PlayerUIManager.instance != null ? PlayerUIManager.instance.GetComponentInChildren<SkillTreeUiManager>(true) : null;
            var hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
            var skills = player != null ? player.GetComponent<PlayerSkillManager>() : null;
            return GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsActive && GameBootstrap.Instance.Session.IsActive && player != null && player.IsInitialized && player.IsInitialized && manager != null && manager.player == player &&
                player.playerNetworkManager != null && player.playerStatsManager != null && player.characterController != null && hud != null && hud.IsHealthBarReady && skillUi != null && skills != null && skills.slot != null && skills.slot.Length >= 3 &&
                PlayerCamera.instance != null && PlayerCamera.instance.player == player && PlayerCamera.instance.cameraObject != null &&
                (!world || (SceneManager.GetActiveScene().buildIndex == 1 && SceneManager.GetActiveScene().isLoaded && !Loading() && PlayerCamera.instance.canFollow && PlayerCamera.instance.cameraObject.isActiveAndEnabled));
        }
        bool Loading() => (bool)typeof(WorldSaveGameManager).GetField("sceneLoading", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        bool ApplicationRecorded()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            long generation = (long)typeof(WorldSaveGameManager).GetField("loadGeneration", flags).GetValue(manager);
            var players = (Dictionary<PlayerManager, long>)typeof(WorldSaveGameManager).GetField("appliedPlayers", flags).GetValue(manager);
            long applied; bool recorded = generation > 0 && players.TryGetValue(player, out applied) && generation == applied;
            if (current != null) current.generation = generation;
            return recorded;
        }
        static PlayerManager FindLocalPlayer() => GameBootstrap.Instance != null && GameBootstrap.Instance.Session != null && GameBootstrap.Instance.Session.Player != null ? GameBootstrap.Instance.Session.Player : null;
        static CharacterSlot Slot(int slot) => slot == 1 ? CharacterSlot.CharacterSlot_01 : slot == 2 ? CharacterSlot.CharacterSlot_02 : CharacterSlot.CharacterSlot_03;
        static Vector3 Pose(CharacterSaveData value) => new Vector3(value.xPosition, value.yPosition, value.zPosition);
        CharacterSaveData LiveData()
        {
            var n = player.playerNetworkManager; var p = player.transform.position;
            return new CharacterSaveData { characterName = n.characterName.Value.ToString(), sceneIndex = SceneManager.GetActiveScene().buildIndex, xPosition = p.x, yPosition = p.y, zPosition = p.z, currentHealth = n.currentHealth.Value, vitality = n.vitality.Value, endurance = n.endurance.Value };
        }
        static CharacterSaveData Scalars(CharacterSaveData value) => new CharacterSaveData { characterName = value.characterName, sceneIndex = value.sceneIndex, xPosition = value.xPosition, yPosition = value.yPosition, zPosition = value.zPosition, currentHealth = value.currentHealth, currentStamina = value.currentStamina, secondsPlayed = value.secondsPlayed, vitality = value.vitality, endurance = value.endurance };
        float HudHealth()
        {
            var hud = PlayerUIManager.instance.playerUIHudManager;
            var bar = (UI_StatBar)typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            return bar.GetComponent<Slider>().value;
        }
        void Snapshot()
        {
            if (current == null || player == null) return;
            current.host = GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsActive; current.owner = player.IsInitialized; current.spawned = player.IsInitialized;
            current.localCharacterId = player.LocalId; current.hudReady = PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHudManager.IsHealthBarReady;
            current.hudHealth = current.hudReady ? HudHealth() : -1; current.cameraReady = PlayerCamera.instance != null && PlayerCamera.instance.player == player && PlayerCamera.instance.canFollow;
        }
        void BlockInput()
        {
            if (PlayerInputManager.instance == null) return;
            if (!inputCaptured) { previousInputEnabled = PlayerInputManager.instance.enabled; inputCaptured = true; }
            PlayerInputManager.instance.enabled = false; PlayerInputManager.instance.playerControls?.Disable();
        }
        void ContinueTutorial()
        {
            if (SceneManager.GetActiveScene().buildIndex != 1) { Check(Time.timeScale > 0, "Unexpected pause outside Tutorial_Final; no forced resume."); return; }
            var scene = SceneManager.GetActiveScene();
            var popups = UnityEngine.Object.FindObjectsByType<PopUpTutorial>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(p => p.isActiveAndEnabled && p.gameObject.scene == scene && !p.passou && p.UI != null && p.UI.activeSelf && p.UI.activeInHierarchy).ToArray();
            if (popups.Length == 0 && Time.timeScale > 0) return;
            var hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
            var pause = hud != null ? typeof(PlayerUIHudManager).GetField("pauseMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud) as GameObject : null;
            bool hudPaused = (pause != null && pause.activeSelf) || (hud != null && hud.pauseSkillMenu != null && hud.pauseSkillMenu.activeSelf);
            bool cutscene = UnityEngine.Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(d => d.isActiveAndEnabled && d.state == UnityEngine.Playables.PlayState.Playing) ||
                UnityEngine.Object.FindObjectsByType<AnimationTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(a => a.isActiveAndEnabled && a.cam != null && a.cam.isActiveAndEnabled) ||
                UnityEngine.Object.FindObjectsByType<GateActivator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(g => g.isActiveAndEnabled && g.cameraAtivar != null && g.cameraAtivar.activeInHierarchy);
            Check(popups.Length == 1 && !hudPaused && !cutscene, "Unsupported pause/tutorial/cutscene context; no forced resume.");
            var popup = popups[0]; float before = Time.timeScale;
            popup.close();
            report.tutorialActions.Add("Authored PopUpTutorial.close " + popup.name + "; frame=" + Time.frameCount + "; timeScale=" + before + "→" + Time.timeScale);
            Check(popup.passou && !popup.UI.activeSelf && Time.timeScale > 0, "Authored tutorial Continue did not complete; no forced correction.");
        }
        void SetState(int value, string description, double timeout)
        { state = value; report.stage = description; deadline = Time.realtimeSinceStartupAsDouble + timeout; Debug.Log("QA host process stage: " + description); }
        void Fail(string error)
        {
            report.errors.Add(error); if (current != null) current.error = error;
            Debug.LogError("QA host process failed: " + error);
            if (state == 6) { report.cleanupPassed = false; Finish(); }
            else { try { Cleanup(); } catch (Exception cleanup) { report.errors.Add("Cleanup: " + cleanup.GetBaseException().Message); Finish(); } }
        }
        void Cleanup()
        {
            Unsubscribe();
            if (player != null && invulnerabilityCaptured && player.IsInitialized) player.characterNetworkManager.isInvulnerable.Value = previousInvulnerable;
            if (inputCaptured && PlayerInputManager.instance != null) { PlayerInputManager.instance.enabled = previousInputEnabled; if (previousInputEnabled) PlayerInputManager.instance.playerControls?.Enable(); }
            if (ownsHost && GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsActive) _ = GameBootstrap.Instance.Session.ReturnToMenuAsync();
            SetState(6, "Awaiting owned host shutdown", 20);
        }
        void Unsubscribe()
        {
            if (player != null && player.playerNetworkManager != null)
            {
                if (healthChanged != null) player.playerNetworkManager.currentHealth.OnValueChanged -= healthChanged;

            }
            healthChanged = null; positionChanged = null;
        }
        void Finish()
        {
            if (terminal) return;
            terminal = true;
            try
            {
                report.personalAfter = PersonalFingerprints(); report.personalFilesUnchanged = report.personalBefore.SequenceEqual(report.personalAfter);
                if (!report.personalFilesUnchanged) report.errors.Add("Personal save fingerprints changed.");
                report.runtimeErrors = runtimeLogs != null ? runtimeLogs.Failures.ToList() : new List<string> { "Runtime error collector missing." };
                var outcome = HostSaveProcessRegression.EvaluateObservedOutcome(report.scalarHostPassed && report.errors.Count == 0 && report.personalFilesUnchanged, report.spawnPassed, report.applyOncePassed, report.skillRoundTripPassed, report.cleanupPassed, runtimeLogs);
                report.success = outcome.success; report.supportedHostPassed = outcome.supportedHostPassed; report.status = outcome.status; report.exitCode = outcome.exitCode;
                report.finishedUtc = DateTime.UtcNow.ToString("O"); report.completed = true;
                using (var stream = new FileStream(report.diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(JsonUtility.ToJson(report, true));
                Debug.Log("QA host process result: " + report.status + "; " + report.diagnosticPath);
            }
            catch (Exception error) { report.exitCode = 1; Debug.LogError("QA host diagnostic output failed: " + error); }
            finally { runtimeLogs?.Dispose(); }
            Application.Quit(report.exitCode);
            Destroy(gameObject);
        }
        void OnDestroy() { try { Unsubscribe(); if (!terminal && report != null) { report.errors.Add("Owned coordinator destroyed before completion."); report.cleanupPassed = false; Finish(); } } finally { runtimeLogs?.Dispose(); } }
        void OnApplicationQuit() { try { if (!terminal && report != null) { report.errors.Add("Application quit before QA completion."); report.cleanupPassed = false; Finish(); } } finally { runtimeLogs?.Dispose(); } }
        static void Check(bool value, string message) => HostSaveProcessRegression.Require(value, message);
        internal static string HashExecutable() => Hash(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        internal static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        static string HashText(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
        static Dictionary<string, string> SaveFingerprints(string root)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int slot = 1; slot <= 3; slot++) foreach (string suffix in new[] { "", ".bak", ".tmp" })
            { string path = Path.Combine(root, "characterSlot_0" + slot + suffix); if (File.Exists(path)) result[path] = Hash(path); }
            return result;
        }
        static List<string> PersonalFingerprints()
        {
            var result = new List<string>();
            for (int slot = 1; slot <= 3; slot++) foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.Combine(Application.persistentDataPath, "characterSlot_0" + slot + suffix);
                if (!File.Exists(path)) { result.Add(path + "|missing"); continue; }
                var info = new FileInfo(path); result.Add(path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks + "|" + Hash(path));
            }
            return result;
        }
    }
#endif
}
