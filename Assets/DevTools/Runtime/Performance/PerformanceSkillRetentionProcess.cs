using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using RPGKarawara.SkillTree;
using Karawara.Core.Characters;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Separate actual production skill lifecycle diagnostic, never baseline timing.
    public sealed class PerformanceSkillRetentionProcess : MonoBehaviour
    {
        enum Stage { Menu, ServicesReady, World, Warmup, Cast, Settling, ControlSettling, SnapshotBefore, SnapshotAfter, Returning, Shutdown, MenuSettling, Done }
        struct Effect
        {
            public int cycle, slot, instanceId, activationSkillId, declaredSkillPrefabId, createdFrame, destroyedFrame;
            public int? creatorId, prefabId;
            public bool projectile;
            public double createdTime, destroyedTime;
            [JsonIgnore] public GameObject live;
        }
        sealed class Snapshot { public string path, error; public bool completed, succeeded; public long bytes; public double requestedRealtimeSeconds, completedRealtimeSeconds; }
        sealed class Cycle { public int index, rootEffects, projectiles; public bool warmup, passed; public double seconds; }
        sealed class LiveImage { public string path; public int cycle, requestedFrame; public double requestedRealtime; }
        const int Capacity = 8192;
        readonly Effect[] effects = new Effect[Capacity];
        readonly Dictionary<FireballShooter, int> shooters = new Dictionary<FireballShooter, int>(32);
        readonly HashSet<FireballShooter> emittedSources = new HashSet<FireballShooter>();
        readonly List<Cycle> cycles = new List<Cycle>(12);
        readonly List<Snapshot> snapshots = new List<Snapshot>(2);
        readonly List<LiveImage> liveImages = new List<LiveImage>(1);
        readonly List<string> errors = new List<string>();
        PerformanceProcessResponse request;
        SkillRetentionConfig config;
        PerformanceSkillRetentionSources sources;
        PerformanceOwnedDefenseGraph ownedDefenseGraph;
        int ownedGraphObservedCycle;
        bool abortGraphCleanupStarted;
        PerformanceInputReplay idleInput;
        PlayerControls controls;
        Keyboard ownedKeyboard;
        InputDevice[] previousDevices, previousRestriction;
        InputBinding? previousMask;
        bool inputRestored = true;
        WorldSaveGameManager world;
        GameBootstrap network;
        PlayerManager player;
        PlayerSkillManager skills;
        Skill[] bound;
        GameObject[] prefabs;
        string[] loadedSkillCatalog = Array.Empty<string>();
        string[] restoredSkillSlots = Array.Empty<string>();
        Stage stage;
        Snapshot pending;
        Dictionary<string, string> personalBefore;
        string personalRoot, outputPath;
        double stageAt, deadline, cycleAt, stableSince = -1;
        int effectCount, projectileCount, cycleStart, projectileStart, cycleIndex, measuredCycles, shutdownFrame = -1;
        int controlBaselineEffectCount = -1;
        float baselineDamage;
        bool baselinePower, baselineInvulnerable, baselineBlocking, initialized, terminal, failed, ownsHost, warmed, projectileRequired, sourceFault, overflow;
        public void Initialize(PerformanceProcessResponse value)
        {
            if (Application.isEditor || initialized || value == null || !value.success || value.mode != "skill-retention" || value.config?.skillRetention == null)
                throw new InvalidOperationException("Explicit standalone skill retention request required.");
            request = value; config = value.config.skillRetention; initialized = true; personalRoot = Application.persistentDataPath;
            cycles.Capacity = config.warmupCycles + config.cycles; snapshots.Capacity = config.snapshotControl ? 3 : 1 + config.cycles / 10;
            outputPath = Path.Combine(config.outputRoot, "process-" + Guid.NewGuid().ToString("N") + ".json");
            errors.AddRange(PerformanceTutorialProcess.TakeStartupDiagnostics()); Application.logMessageReceived += OnLog;
            try
            {
                personalBefore = Fingerprints(personalRoot); Directory.CreateDirectory(config.outputRoot);
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed); QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, request.config.quality), true);
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; SetStage(Stage.Menu, 60);
            }
            catch (Exception error) { errors.Add(error.ToString()); failed = true; Finish(false); }
        }
        void SetStage(Stage next, double timeout) { stage = next; stageAt = Time.realtimeSinceStartupAsDouble; deadline = stageAt + timeout; }
        bool SettingsMatch() => Screen.width == 1920 && Screen.height == 1080 && QualitySettings.names[QualitySettings.GetQualityLevel()] == request.config.quality && QualitySettings.vSyncCount == 0 && Application.targetFrameRate == -1 && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - 1) < .0001;
        void Update()
        {
            if (network != null) player = network.Session.Player;
            if (!initialized || terminal) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now > deadline) throw new TimeoutException("Skill retention stage " + stage + " cycle " + cycleIndex);
                if (!failed && errors.Count != 0) throw new InvalidOperationException("Runtime errors during skill retention.");
                if (!failed && stage != Stage.Menu && stage != Stage.SnapshotBefore && stage != Stage.SnapshotAfter && (!Application.isFocused || !SettingsMatch())) throw new InvalidOperationException("Focus/settings changed during skill retention.");
                if (!failed && stage >= Stage.Warmup && stage <= Stage.SnapshotAfter && (player == null || !player.IsInitialized || !player.IsInitialized || player.isDead.Value || network == null || !network.Session.IsActive || SceneManager.GetActiveScene().name != "Video1" || Time.timeScale != 1)) throw new InvalidOperationException("Actual skill workload player/host/scene changed.");
                if (!failed && sources != null && (sources.Faulted || sourceFault || overflow)) throw new InvalidOperationException("Skill source journal fault or overflow.");
                if (!failed && idleInput != null)
                {
                    if (!ReferenceEquals(PerformanceSkillRetentionSources.SkillKeyboard, ownedKeyboard) || !ownedKeyboard.added || PlayerInputManager.instance?.playerControls != controls || !controls.asset.devices.HasValue || controls.asset.devices.Value.Count != 2 || !controls.asset.devices.Value.Contains(ownedKeyboard)) throw new InvalidOperationException("Owned idle input lease changed during retention: currentKeyboard=" + Keyboard.current?.deviceId + ", ownedKeyboard=" + ownedKeyboard?.deviceId + ", sameControls=" + ReferenceEquals(PlayerInputManager.instance?.playerControls, controls) + ", restrictedDevices=" + (controls.asset.devices.HasValue ? string.Join(",", controls.asset.devices.Value.Select(d => d.deviceId)) : "none"));
                    idleInput.Advance(0);
                }
                Sweep();
                switch (stage)
                {
                    case Stage.Menu:
                        world = WorldSaveGameManager.instance; network = GameBootstrap.Instance;
                        if (world == null || network == null || TitleScreenManager.Instance == null || PlayerInputManager.instance?.playerControls == null || !Application.isFocused || !SettingsMatch()) return;
                        if (SceneManager.GetActiveScene().name != "SceneMenu" || network.Session.IsActive || Time.timeScale != 1) throw new InvalidOperationException("Fresh unpaused SceneMenu/stopped host required.");
                        world.SaveRootOverride = request.saveRoot; if (world.SaveRoot != request.saveRoot) throw new InvalidOperationException("QA save root rejected.");
                        ownsHost = TitleScreenManager.Instance.TryStartLocalRuntime(); if (!ownsHost) throw new InvalidOperationException("Owned host start rejected.");
                        SetStage(Stage.ServicesReady, 60); break;
                    case Stage.ServicesReady:
                        player = network.Session.Player?.GetComponent<PlayerManager>();
                        var ui = PlayerUIManager.instance?.GetComponentInChildren<SkillTreeUiManager>(true);
                        if (!world.CanCreateNewGame || ui == null) return;
                        // Owned/unlocked arrays and the UI's earlier Awake snapshot can
                        // omit assets loaded later. Resolve only actual current-process assets.
                        var catalog = Resources.FindObjectsOfTypeAll<Skill>();
                        loadedSkillCatalog = catalog.Select(s => ((Object)s).name + "|" + s.GetType().FullName).OrderBy(s => s).ToArray();
                        var seed = PerformanceProcessHarness.BindVideoSeed(config.seedSaveJson, catalog);
                        bound = seed.skillsSlot;
                        if (bound == null || bound.Length != 3) throw new InvalidOperationException("Exact three bound skill slots required.");
                        if (ownedDefenseGraph == null)
                        {
                            ownedDefenseGraph = config.ownedDefenseGraph ? PerformanceOwnedDefenseGraph.Open((DefenseSkill)bound[1], transform) : PerformanceOwnedDefenseGraph.OpenControl((DefenseSkill)bound[1], transform);
                            ownedDefenseGraph.Observe("prepared-before-warmup", 0);
                        }
                        prefabs = new GameObject[3];
                        for (int i = 0; i < 3; i++)
                        {
                            if (bound[i] == null || ((Object)bound[i]).name != config.skillNames[i] || bound[i].GetType().FullName != config.skillTypes[i] || (int)bound[i].skillType != i || bound[i].cooldownDuration <= 0 || bound[i].cooldownDuration > 60) throw new InvalidOperationException("Actual authored skill binding/lifetime mismatch.");
                            prefabs[i] = i == 0 ? bound[i].prefab : i == 1 ? ((DefenseSkill)bound[i]).vfxPrefab : ((SupportSkill)bound[i]).skillPrefabJacare != null ? ((SupportSkill)bound[i]).skillPrefabJacare : ((SupportSkill)bound[i]).healCirclePrefab;
                            if (prefabs[i] == null) throw new InvalidOperationException("Declared skill has no actual effect prefab.");
                            projectileRequired |= prefabs[i].GetComponentsInChildren<FireballShooter>(true).Length != 0;
                        }
                        var writer = new SaveFileDataWriter { saveDataDirectoryPath = request.saveRoot, saveFileName = "characterSlot_01" };
                        var written = writer.TrySave(seed); if (!written.Success) throw new InvalidOperationException(written.Message);
                        world.RefreshCharacterProfiles(); TitleScreenManager.Instance.SelectCharacterSlot(CharacterSlot.CharacterSlot_01); world.LoadGame();
                        SetStage(Stage.World, 90); break;
                    case Stage.World:
                        if (SceneManager.GetActiveScene().name != "Video1" || player == null || !player.IsInitialized || PlayerUIManager.instance?.playerUIHudManager?.IsHealthBarReady != true || PlayerInputManager.instance?.player != player || !player.characterLocomotionManager.isGrounded || Time.timeScale != 1) return;
                        skills = player.GetComponent<PlayerSkillManager>(); if (skills == null || skills.slot == null || skills.slot.Length != 3) throw new InvalidOperationException("Actual player skill manager required.");
                        // HUD/grounded can become ready before the normal asynchronous save
                        // application finishes. Observe restoration; never assign slots here.
                        restoredSkillSlots = skills.slot.Select(s => s?.skillSlot == null ? "missing" : ((Object)s.skillSlot).name + "|" + s.skillSlot.GetInstanceID() + "|unlocked=" + s.skillSlot.isUnlocked).ToArray();
                        for (int i = 0; i < 3; i++) if (skills.slot[i] == null || !ReferenceEquals(skills.slot[i].skillSlot, bound[i]) || !bound[i].isUnlocked) return;
                        baselineDamage = skills.damageSpirit; baselinePower = player.playerNetworkManager.isPowerPet.Value; baselineInvulnerable = player.characterNetworkManager.isInvulnerable.Value; baselineBlocking = player.playerNetworkManager.isBlocking.Value;
                        controls = PlayerInputManager.instance.playerControls; previousDevices = InputSystem.devices.ToArray(); previousMask = controls.asset.bindingMask; previousRestriction = controls.asset.devices.HasValue ? controls.asset.devices.Value.ToArray() : null;
                        inputRestored = false;
                        idleInput = PerformanceInputReplay.Create(new[] { "-karawaraPerformanceReplay" }, controls, "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]}]}");
                        ownedKeyboard = controls.asset.devices.Value.OfType<Keyboard>().Single();
                        sources = PerformanceSkillRetentionSources.Open(ProjectileCreated, ownedKeyboard);
                        SetStage(Stage.Warmup, config.warmupSeconds + 10); break;
                    case Stage.Warmup:
                        if (now - stageAt >= config.warmupSeconds) StartCycle(); break;
                    case Stage.Cast:
                        for (int slot = 0; slot < 3; slot++) Activate(slot);
                        stableSince = -1; SetStage(Stage.Settling, config.cycleTimeoutSeconds); break;
                    case Stage.Settling:
                        if (ownedDefenseGraph != null && ownedGraphObservedCycle < cycleIndex && now - cycleAt >= .25)
                        {
                            var cast = effects.Take(effectCount).Where(e => e.cycle == cycleIndex && e.slot == 1 && !e.projectile && e.live != null).Select(e => e.live).Single();
                            ownedDefenseGraph.Observe("cast-live", cycleIndex, cast); ownedGraphObservedCycle = cycleIndex;
                            // One warm-up checkpoint, before the first native snapshot. Never timing evidence.
                            if (cycleIndex == 1)
                            {
                                var image = new LiveImage { path = Path.Combine(config.outputRoot, "defense-warmup-live-" + Guid.NewGuid().ToString("N") + ".png"), cycle = cycleIndex, requestedFrame = Time.frameCount, requestedRealtime = now };
                                if (File.Exists(image.path)) throw new IOException("Exclusive live image required.");
                                ScreenCapture.CaptureScreenshot(image.path); liveImages.Add(image);
                            }
                        }
                        bool ready = Alive() == 0 && skills.damageSpirit == baselineDamage && player.playerNetworkManager.isPowerPet.Value == baselinePower && player.characterNetworkManager.isInvulnerable.Value == baselineInvulnerable && player.playerNetworkManager.isBlocking.Value == baselineBlocking;
                        for (int i = 0; i < 3; i++) ready &= skills.slot[i].canUse && !bound[i].IsOnCooldown && ReferenceEquals(skills.slot[i].skillSlot, bound[i]);
                        if (!ready) { stableSince = -1; return; }
                        if (stableSince < 0) stableSince = now;
                        if (now - stableSince < config.settleSeconds) return;
                        bool emitted = AllSourcesEmitted() && (!projectileRequired || projectileCount > projectileStart);
                        cycles.Add(new Cycle { index = cycleIndex, warmup = !warmed, rootEffects = effectCount - cycleStart - (projectileCount - projectileStart), projectiles = projectileCount - projectileStart, passed = emitted, seconds = now - cycleAt });
                        shooters.Clear(); emittedSources.Clear();
                        if (!emitted) throw new InvalidOperationException("Required actual FireballShooter projectile was not observed; attempted casts are not projectile workload.");
                        if (!warmed && cycleIndex == config.warmupCycles)
                        {
                            if (config.snapshotControl) controlBaselineEffectCount = effectCount;
                            TakeSnapshot(config.snapshotControl ? SnapshotControlCheckpoint(0) : SnapshotCheckpoint(0, config.cycles), Stage.SnapshotBefore);
                        }
                        else if (warmed)
                        {
                            measuredCycles++;
                            string checkpoint = SnapshotCheckpoint(measuredCycles, config.cycles);
                            if (checkpoint != null) TakeSnapshot(checkpoint, Stage.SnapshotAfter); else StartCycle();
                        }
                        else StartCycle();
                        break;
                    case Stage.ControlSettling:
                        RequireControlIdle();
                        if (now - pending.completedRealtimeSeconds >= config.controlIntervalSeconds) TakeSnapshot(SnapshotControlCheckpoint(snapshots.Count), Stage.SnapshotAfter);
                        break;
                    case Stage.SnapshotBefore:
                    case Stage.SnapshotAfter:
                        if (!pending.completed) return;
                        if (!pending.succeeded) throw new InvalidOperationException("Requested native snapshot failed: " + pending.error);
                        if (config.snapshotControl)
                        {
                            RequireControlIdle(); warmed = true;
                            if (snapshots.Count < 3) SetStage(Stage.ControlSettling, config.controlIntervalSeconds + 10); else BeginReturn();
                        }
                        else if (stage == Stage.SnapshotBefore) { warmed = true; StartCycle(); }
                        else if (!failed && measuredCycles < config.cycles) StartCycle();
                        else BeginReturn(); break;
                    case Stage.Returning:
                        if (SceneManager.GetActiveScene().name != "SceneMenu") return;
                        DisposeOwnedDefenseGraphs();
                        if (ownsHost && network != null && network.Session.IsActive) _ = network.Session.ReturnToMenuAsync();
                        SetStage(Stage.Shutdown, 20); break;
                    case Stage.Shutdown:
                        if ((network != null && network.Session.IsActive) || (player != null && player.IsInitialized)) return;
                        if (shutdownFrame < 0) shutdownFrame = Time.frameCount;
                        if (Time.frameCount < shutdownFrame + 2) return;
                        ownsHost = false; SetStage(Stage.MenuSettling, 10); break;
                    case Stage.MenuSettling:
                        if (now - stageAt >= 3) Finish(true); break;
                }
            }
            catch (Exception error)
            {
                errors.Add(error.ToString());
                if (failed) { if (ownsHost && network != null && network.Session.IsActive) _ = network.Session.ReturnToMenuAsync(); Finish(false); return; }
                failed = true;
                if (warmed && snapshots.Count < 1 + config.cycles / 10 && stage == Stage.Settling) TakeSnapshot("skills-after-failed-cycle", Stage.SnapshotAfter);
                else BeginReturn();
            }
        }
        void StartCycle() { cycleIndex++; cycleStart = effectCount; projectileStart = projectileCount; cycleAt = Time.realtimeSinceStartupAsDouble; SetStage(Stage.Cast, 5); }
        static string SnapshotControlCheckpoint(int index) => index >= 0 && index < 3 ? "skills-snapshot-control-" + index : null;
        void RequireControlIdle()
        {
            if (!config.snapshotControl || measuredCycles != 0 || effectCount != controlBaselineEffectCount || Alive() != 0 || skills.damageSpirit != baselineDamage || player.playerNetworkManager.isPowerPet.Value != baselinePower || player.characterNetworkManager.isInvulnerable.Value != baselineInvulnerable || player.playerNetworkManager.isBlocking.Value != baselineBlocking)
                throw new InvalidOperationException("Snapshot-only control observed an activation, live effect or changed skill state after baseline.");
            for (int i = 0; i < 3; i++) if (!skills.slot[i].canUse || bound[i].IsOnCooldown || !ReferenceEquals(skills.slot[i].skillSlot, bound[i])) throw new InvalidOperationException("Snapshot-only control skill state changed.");
        }
        static string SnapshotCheckpoint(int completed, int requested)
        {
            if (requested != 10 && requested != 20) throw new ArgumentOutOfRangeException(nameof(requested));
            if (completed == 0) return "skills-before-ten-cycles";
            if (completed == 10) return "skills-after-ten-cycles";
            if (completed == 20 && requested == 20) return "skills-after-twenty-cycles";
            return null;
        }
        bool CompletedProtocol()
        {
            if (config == null) return false;
            if (config.snapshotControl) return config.cycles == 0 && measuredCycles == 0 && config.warmupCycles == 2 && cycles.Count == 2 && cycles.All(c => c.passed && c.warmup) && controlBaselineEffectCount >= 0 && effectCount == controlBaselineEffectCount && Alive() == 0 && snapshots.Count == 3 && snapshots.All(s => s.completed && s.succeeded && !string.IsNullOrWhiteSpace(s.path)) && snapshots.Select(s => s.path).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3 && ValidControlSnapshotTimes();
            return (config.cycles == 10 || config.cycles == 20) && measuredCycles == config.cycles && cycles.Count == config.warmupCycles + config.cycles && cycles.All(c => c.passed) && snapshots.Count == 1 + config.cycles / 10 && snapshots.All(s => s.completed && s.succeeded);
        }
        bool ValidControlSnapshotTimes()
        {
            if (double.IsNaN(config.controlIntervalSeconds) || double.IsInfinity(config.controlIntervalSeconds) || config.controlIntervalSeconds < 3 || config.controlIntervalSeconds > 600) return false;
            for (int i = 0; i < snapshots.Count; i++)
            {
                var row = snapshots[i];
                if (double.IsNaN(row.requestedRealtimeSeconds) || double.IsInfinity(row.requestedRealtimeSeconds) || double.IsNaN(row.completedRealtimeSeconds) || double.IsInfinity(row.completedRealtimeSeconds) || row.requestedRealtimeSeconds < 0 || row.completedRealtimeSeconds < row.requestedRealtimeSeconds || (i > 0 && row.requestedRealtimeSeconds - snapshots[i - 1].completedRealtimeSeconds < config.controlIntervalSeconds)) return false;
            }
            return true;
        }
        void Activate(int slot)
        {
            if (!skills.slot[slot].canUse || bound[slot].IsOnCooldown || !bound[slot].isUnlocked) throw new InvalidOperationException("Production skill is locked/on cooldown before the next cast.");
            var before = new HashSet<GameObject>(Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            int first = effectCount;
            skills.QaActivateSkill(slot);
            foreach (var root in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!before.Contains(root) && (root.transform.parent == null || before.Contains(root.transform.parent.gameObject)))
                {
                    AddEffect(root, false, slot, bound[slot].GetInstanceID(), prefabs[slot].GetInstanceID());
                    if (slot == 1) ownedDefenseGraph?.BindCastGraphs(root);
                    foreach (var shooter in root.GetComponentsInChildren<FireballShooter>(true))
                    {
                        RegisterShooter(shooter);
                    }
                }
            if (effectCount == first || skills.slot[slot].canUse) throw new InvalidOperationException("Actual production activation/effect creation not observed for slot " + slot);
        }
        void AddEffect(GameObject root, bool projectile, int slot, int creatorId, int prefabId)
        {
            if (root == null || effectCount == Capacity) { overflow = true; return; }
            effects[effectCount++] = new Effect { cycle = cycleIndex, slot = slot, live = root, instanceId = root.GetInstanceID(), activationSkillId = projectile ? 0 : creatorId, declaredSkillPrefabId = projectile ? 0 : prefabId, creatorId = projectile ? (int?)creatorId : null, prefabId = projectile ? (int?)prefabId : null, projectile = projectile, createdFrame = Time.frameCount, createdTime = Time.realtimeSinceStartupAsDouble };
        }
        void RegisterShooter(FireballShooter shooter)
        {
            if (shooter == null || shooter.fireballPrefab == null || shooters.ContainsKey(shooter)) throw new InvalidOperationException("Missing or duplicate actual projectile source.");
            shooters.Add(shooter, shooter.fireballPrefab.GetInstanceID()); projectileRequired = true;
        }
        bool AllSourcesEmitted() => shooters.Keys.All(owner => emittedSources.Contains(owner));
        void ProjectileCreated(FireballShooter owner, GameObject prefab, GameObject instance)
        {
            if (owner == null || prefab == null || instance == null || !shooters.TryGetValue(owner, out int expected) || expected != prefab.GetInstanceID() || !ReferenceEquals(owner.fireballPrefab, prefab)) { sourceFault = true; return; }
            AddEffect(instance, true, 0, owner.GetInstanceID(), expected); emittedSources.Add(owner); projectileCount++;
        }
        void Sweep()
        {
            for (int i = 0; i < effectCount; i++)
                if (!ReferenceEquals(effects[i].live, null) && effects[i].live == null)
                { effects[i].live = null; effects[i].destroyedFrame = Time.frameCount; effects[i].destroyedTime = Time.realtimeSinceStartupAsDouble; }
        }
        int Alive() { int count = 0; for (int i = 0; i < effectCount; i++) if (effects[i].live != null) count++; return count; }
        void TakeSnapshot(string label, Stage next)
        {
            ownedDefenseGraph?.Observe(label, cycleIndex);
            var result = new Snapshot { path = Path.Combine(config.outputRoot, label + "-" + Guid.NewGuid().ToString("N") + ".snap"), requestedRealtimeSeconds = Time.realtimeSinceStartupAsDouble };
            if (File.Exists(result.path)) throw new IOException("Exclusive snapshot path required.");
            pending = result; snapshots.Add(result); SetStage(next, 90);
            Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(result.path, (path, success) =>
            {
                try { result.succeeded = success && string.Equals(Path.GetFullPath(path), Path.GetFullPath(result.path), StringComparison.OrdinalIgnoreCase) && File.Exists(result.path); if (result.succeeded) result.bytes = new FileInfo(result.path).Length; result.succeeded &= result.bytes > 0; }
                catch (Exception error) { result.error = error.ToString(); result.succeeded = false; }
                finally { result.completedRealtimeSeconds = Time.realtimeSinceStartupAsDouble; result.completed = true; }
            }, Unity.Profiling.Memory.CaptureFlags.ManagedObjects | Unity.Profiling.Memory.CaptureFlags.NativeObjects | Unity.Profiling.Memory.CaptureFlags.NativeAllocations | Unity.Profiling.Memory.CaptureFlags.NativeAllocationSites | Unity.Profiling.Memory.CaptureFlags.NativeStackTraces);
        }
        void BeginReturn()
        {
            sources?.Dispose(); sources = null; ReleaseInput(); SetStage(Stage.Returning, 30);
            if (SceneManager.GetActiveScene().name != "SceneMenu")
            {
                if (!failed && PlayerUIManager.instance?.playerUIHudManager != null) PlayerUIManager.instance.playerUIHudManager.ChangeScene();
                else { Time.timeScale = 1; SceneManager.LoadSceneAsync(0); }
            }
        }
        void ReleaseInput()
        {
            if (idleInput == null) return;
            var owned = idleInput; idleInput = null;
            try
            {
                owned.Dispose();
                inputRestored = InputSystem.devices.SequenceEqual(previousDevices) && controls.asset.bindingMask.Equals(previousMask) && controls.asset.devices.HasValue == (previousRestriction != null) && (previousRestriction == null || controls.asset.devices.Value.SequenceEqual(previousRestriction));
            }
            catch (Exception error) { inputRestored = false; errors.Add("Owned input cleanup failed: " + error); }
        }
        void DisposeOwnedDefenseGraphs()
        {
            if (ownedDefenseGraph == null) return;
            ownedDefenseGraph.RestoreBinding();
            if (ownedDefenseGraph.CleanupComplete) return;
            if (Alive() != 0) { errors.Add("Owned graph cleanup deferred: actual casts still live."); return; }
            ownedDefenseGraph.Dispose();
        }
        void Finish(bool cleanupPassed)
        {
            if (!terminal && ownedDefenseGraph != null && !ownedDefenseGraph.CleanupComplete && Alive() != 0)
            {
                if (abortGraphCleanupStarted) return; abortGraphCleanupStarted = true;
                var casts = effects.Take(effectCount).Where(e => e.slot == 1 && !e.projectile && e.live != null).Select(e => e.live).ToArray();
                ownedDefenseGraph.DisposeAfterCasts(casts, () => Finish(false));
                return;
            }
            if (terminal) return; terminal = true; sources?.Dispose(); sources = null; ReleaseInput(); Application.logMessageReceived -= OnLog; Sweep();
            DisposeOwnedDefenseGraphs();
            Dictionary<string, string> personalAfter = null;
            try { personalAfter = Fingerprints(personalRoot); } catch (Exception error) { errors.Add("Final personal fingerprint failed: " + error); }
            bool personalUnchanged = personalBefore != null && personalAfter != null && personalBefore.Count == personalAfter.Count && personalBefore.All(p => personalAfter.TryGetValue(p.Key, out var value) && value == p.Value);
            bool hostStopped = (network == null || !network.Session.IsActive) && (GameBootstrap.Instance == null || !GameBootstrap.Instance.Session.IsActive);
            bool ownedGraphsPassed = ownedDefenseGraph != null && ownedDefenseGraph.CleanupComplete && !ownedDefenseGraph.CleanupConflict && ownedDefenseGraph.ObservedCasts == config.warmupCycles + config.cycles && ownedDefenseGraph.EveryObservedCastHadActiveBatch && ownedDefenseGraph.SettledNativeZeroObservations == snapshots.Count;
            bool liveImagesReady = liveImages.Count == 1 && liveImages.All(i => File.Exists(i.path) && new FileInfo(i.path).Length > 0);
            bool passed = !failed && errors.Count == 0 && cleanupPassed && inputRestored && personalUnchanged && hostStopped && CompletedProtocol() && ownedGraphsPassed && liveImagesReady;
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed, cleanupPassed, personalUnchanged, personalBefore, personalAfter,
                processId = System.Diagnostics.Process.GetCurrentProcess().Id, buildGuid = Application.buildGUID, development = Debug.isDebugBuild, inputRestored, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, config, snapshotControl = config.snapshotControl, measuredCycles, completedCycles = measuredCycles, cycles, snapshots,
                controlBaselineEffectCount, controlObservedEffectCount = effectCount, controlRootEffectJournalUnchanged = config.snapshotControl && controlBaselineEffectCount >= 0 && controlBaselineEffectCount == effectCount,
                ownedGraphsPassed, ownedDefenseGraph = ownedDefenseGraph?.Report,
                liveImages, liveImagesReady, liveImageBoundary = "Unity ScreenCapture warmup checkpoint, requested frame/time only; no calibrated exact present frame, deterministic pixel parity, external window inspection or timing claim.",
                effects = effects.Take(effectCount).ToArray(), loadedSkillCatalog, restoredSkillSlots, sourceFault, overflow, projectileRequired, projectileCount, liveEffectsAfterCleanup = Alive(), errors, scene = SceneManager.GetActiveScene().name, ownedHostStopped = hostStopped, saveRoot = request.saveRoot,
                readyForTiming = false, populationVerified = false, retentionAccepted = false,
                boundary = "Actual production skill calls/synchronous root differences and exact observed FireballShooter callbacks. Normal protocols capture baseline/after10 or baseline/after10/after20. Explicit snapshot-only control performs the same two warm-up cycles, then three snapshots separated by3seconds with zero measured activations and an unchanged effect journal. Native snapshots and ownership analysis are separate from timing/global retention acceptance. Impact/secondary effects without an explicit creator callback remain native snapshot attribution work; QA owns preallocated numeric history and temporary root references." };
            try { using (var stream = new FileStream(outputPath, FileMode.CreateNew)) using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented)); }
            catch (Exception error) { passed = false; Debug.LogError(error); }
            finally { Application.Quit(passed ? 0 : 2); }
        }
        static Dictionary<string, string> Fingerprints(string root)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!Directory.Exists(root)) return result;
            using (var sha = SHA256.Create()) foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) using (var stream = File.OpenRead(path)) result[path.Substring(root.Length)] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            return result;
        }
        void OnLog(string message, string stack, LogType type) { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 256) errors.Add(message + "\n" + stack); }
        void OnDisable() { if (!initialized || terminal) return; failed = true; errors.Add("Owned skill retention process disabled prematurely."); sources?.Dispose(); if (ownsHost && network != null && network.Session.IsActive) _ = network.Session.ReturnToMenuAsync(); Finish(false); }
        void OnDestroy()
        {
            ownedDefenseGraph?.RestoreBinding();
            if (ownedDefenseGraph != null && !ownedDefenseGraph.CleanupComplete && !abortGraphCleanupStarted)
            {
                abortGraphCleanupStarted = true;
                var casts = effects.Take(effectCount).Where(e => e.slot == 1 && !e.projectile && e.live != null).Select(e => e.live).ToArray();
                ownedDefenseGraph.DisposeAfterCasts(casts, null);
            }
            sources?.Dispose(); ReleaseInput(); Application.logMessageReceived -= OnLog;
        }
    }
#endif
}
