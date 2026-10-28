using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using RPGKarawara.SkillTree;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // A passive same-process resource diagnostic. Death remains observable and never grants timing or population acceptance.
    public sealed class PerformanceCucaProjectileRetentionProcess : MonoBehaviour
    {
        enum Stage { Menu, Host, World, Warmup, SnapshotBefore, Observing, Settling, SnapshotAfter, Returning, Shutdown, MenuSettling, Done }
        struct Effect
        {
            public int ownerId, prefabId, instanceId, attackOrdinal, projectileOrdinal, createdFrame, destroyedFrame;
            public double createdTime, destroyedTime;
            public float[] createdPosition;
            [JsonIgnore] public GameObject live;
        }
        sealed class Snapshot
        {
            public string label, path, error;
            public bool completed, succeeded;
            public long bytes;
            public double requestedTime, completedTime;
            public int attacksBefore, attacksAfter, creationsBefore, creationsAfter, liveBefore, liveAfter;
        }
        sealed class Observation
        {
            public int frame, health, attackCount, createdCount, liveProjectiles;
            public double time;
            public bool dead, playerActive, bossActive;
        }
        const int Capacity = 1024;
        readonly Effect[] effects = new Effect[Capacity];
        readonly HashSet<int> cloneIds = new HashSet<int>();
        readonly List<Snapshot> snapshots = new List<Snapshot>(2);
        readonly List<Observation> observations = new List<Observation>(128);
        readonly List<string> errors = new List<string>();
        int effectCount, expectedAttackOrdinal, expectedProjectileOrdinal, baselineAttackOrdinal = -1, baselineCreatedCount, baselineEffectCount;
        int lastHealth = int.MinValue, lastAttack = -1, lastLive = -1, lastPlayerId, shutdownFrame = -1;
        int frozenBossId, frozenBossRootId, frozenPrefabId, frozenSpawnId, finalSnapshotObservedBursts, measuredLiveAfterSettling;
        bool lastDead, sourceFault, overflow, initialized, terminal, failed, ownsHost, inputRestored = true;
        bool workloadObserved, snapshotWorkloadObserved;
        int actualMeasuredBursts, actualMeasuredCreations, liveAfterSettling = -1, liveAfterReturn = -1;
        double stageAt, deadline, observationDeadline, firstDeathTime = -1;
        string personalRoot, outputPath;
        Dictionary<string, string> personalBefore;
        PerformanceProcessResponse request;
        CucaProjectileRetentionConfig config;
        WorldSaveGameManager world;
        NetworkManager network;
        PlayerManager player;
        CucaBoss boss;
        GameObject prefab, frozenLaserVfx;
        Transform frozenSpawn, frozenLaserOrigin;
        LineRenderer frozenLine;
        float[] frozenSettings;
        int boundAttackOrdinal, boundCreatedCount;
        PlayerControls controls;
        InputDevice[] previousDevices, previousRestriction, ownedDevices;
        InputBinding? previousMask;
        PerformanceInputReplay idleInput;
        PerformanceCucaProjectileRetentionSources sources;
        Stage stage;
        Snapshot pending;

        public void Initialize(PerformanceProcessResponse value)
        {
            if (Application.isEditor || initialized || value == null || !value.success || value.mode != "cuca-projectile-retention" || value.config?.cucaProjectileRetention == null)
                throw new InvalidOperationException("Explicit standalone Cuca projectile diagnostic required.");
            request = value; config = value.config.cucaProjectileRetention; initialized = true; personalRoot = Application.persistentDataPath;
            outputPath = Path.Combine(config.outputRoot, "process-" + Guid.NewGuid().ToString("N") + ".json");
            errors.AddRange(PerformanceTutorialProcess.TakeStartupDiagnostics()); Application.logMessageReceived += OnLog;
            try
            {
                personalBefore = Fingerprints(personalRoot); Directory.CreateDirectory(config.outputRoot);
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, request.config.quality), true);
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; SetStage(Stage.Menu, 60);
            }
            catch (Exception error) { failed = true; errors.Add(error.ToString()); Finish(false); }
        }
        void SetStage(Stage value, double timeout) { stage = value; stageAt = Time.realtimeSinceStartupAsDouble; deadline = stageAt + timeout; }
        bool SettingsMatch() => Screen.width == 1920 && Screen.height == 1080 && QualitySettings.names[QualitySettings.GetQualityLevel()] == request.config.quality && QualitySettings.vSyncCount == 0 && Application.targetFrameRate == -1 && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && Math.Abs(urp.renderScale - 1) < .0001;
        float[] BossSettings(CucaBoss value) => new[] { value.activationRange, value.detectionRange, value.stayCloseDuration, value.retreatDistance, value.attackCooldown, value.cooldownTime, value.laserDelay, value.laserSpeed, value.multiplierSpeed };
        void BindBoss(CucaBoss value)
        {
            if (value == null || value.magicProjectile == null || value.magicSpawnPoint == null || boss != null) throw new InvalidOperationException("One exact projectile producer and authored references required.");
            if (value.QaProjectileAttackCount < 0 || value.QaProjectileCreatedCount != (long)value.QaProjectileAttackCount * 3) throw new InvalidOperationException("Attach journal only at complete natural burst boundary.");
            boss = value; prefab = value.magicProjectile; frozenSpawn = value.magicSpawnPoint; frozenLaserVfx = value.VfxLaser; frozenLaserOrigin = value.laserOrigin; frozenLine = value.lineRenderer;
            frozenBossId = value.GetInstanceID(); frozenBossRootId = value.gameObject.GetInstanceID(); frozenPrefabId = prefab.GetInstanceID(); frozenSpawnId = frozenSpawn.GetInstanceID();
            frozenSettings = BossSettings(value); boundAttackOrdinal = value.QaProjectileAttackCount; boundCreatedCount = value.QaProjectileCreatedCount;
            expectedAttackOrdinal = boundAttackOrdinal + 1; expectedProjectileOrdinal = 0;
        }
        void ObserveProjectile(CucaBoss owner, GameObject declaredPrefab, GameObject instance, int attackOrdinal, int projectileOrdinal)
        {
            if (sourceFault || overflow) return;
            if (effectCount == Capacity) { overflow = true; return; }
            if (owner == null || !ReferenceEquals(owner, boss) || declaredPrefab == null || !ReferenceEquals(declaredPrefab, prefab) || !ReferenceEquals(owner.magicProjectile, prefab) || instance == null || ReferenceEquals(instance, prefab) ||
                attackOrdinal != expectedAttackOrdinal || projectileOrdinal != expectedProjectileOrdinal || !cloneIds.Add(instance.GetInstanceID())) { sourceFault = true; return; }
            var position = instance.transform.position;
            effects[effectCount++] = new Effect { ownerId = owner.GetInstanceID(), prefabId = declaredPrefab.GetInstanceID(), instanceId = instance.GetInstanceID(), attackOrdinal = attackOrdinal, projectileOrdinal = projectileOrdinal,
                createdFrame = Time.frameCount, createdTime = Time.realtimeSinceStartupAsDouble, createdPosition = new[] { position.x, position.y, position.z }, live = instance };
            if (++expectedProjectileOrdinal == 3) { expectedProjectileOrdinal = 0; expectedAttackOrdinal++; }
        }
        int CountCompleteBurstsAfter(int ordinal)
        {
            int count = 0;
            for (int i = 0; i + 2 < effectCount; i += 3)
                if (effects[i].attackOrdinal > ordinal && effects[i].projectileOrdinal == 0 && effects[i + 1].attackOrdinal == effects[i].attackOrdinal && effects[i + 1].projectileOrdinal == 1 && effects[i + 2].attackOrdinal == effects[i].attackOrdinal && effects[i + 2].projectileOrdinal == 2) count++;
            return count;
        }
        void Sweep()
        {
            for (int i = 0; i < effectCount; i++)
                if (!ReferenceEquals(effects[i].live, null) && effects[i].live == null)
                { effects[i].live = null; effects[i].destroyedFrame = Time.frameCount; effects[i].destroyedTime = Time.realtimeSinceStartupAsDouble; }
        }
        int Alive() { int count = 0; for (int i = 0; i < effectCount; i++) if (effects[i].live != null) count++; return count; }
        void RecordState(bool force = false)
        {
            if (player == null || boss == null) return;
            int health = player.characterNetworkManager.currentHealth.Value, attacks = boss.QaProjectileAttackCount, live = Alive(); bool dead = player.isDead.Value;
            if (!force && health == lastHealth && attacks == lastAttack && live == lastLive && dead == lastDead) return;
            if (observations.Count == 128) { overflow = true; return; }
            if (dead && firstDeathTime < 0) firstDeathTime = Time.realtimeSinceStartupAsDouble;
            observations.Add(new Observation { frame = Time.frameCount, time = Time.realtimeSinceStartupAsDouble, health = health, dead = dead, attackCount = attacks, createdCount = boss.QaProjectileCreatedCount, liveProjectiles = live, playerActive = player.gameObject.activeInHierarchy, bossActive = boss.gameObject.activeInHierarchy });
            lastHealth = health; lastDead = dead; lastAttack = attacks; lastLive = live;
        }
        void ValidateLiveContext()
        {
            if (player == null || player.GetInstanceID() != lastPlayerId || !player.IsSpawned || !player.IsOwner || !player.gameObject.activeInHierarchy || network == null || !network.IsHost || SceneManager.GetActiveScene().name != "CucaLoad" || Time.timeScale != 1 ||
                boss == null || !boss.gameObject.activeInHierarchy || boss.player != player.gameObject || !ReferenceEquals(boss.magicProjectile, prefab) || !ReferenceEquals(boss.magicSpawnPoint, frozenSpawn) || !ReferenceEquals(boss.VfxLaser, frozenLaserVfx) || !ReferenceEquals(boss.laserOrigin, frozenLaserOrigin) || !ReferenceEquals(boss.lineRenderer, frozenLine) || !BossSettings(boss).SequenceEqual(frozenSettings))
                throw new InvalidOperationException("Actual Cuca producer/player/host/scene/settings changed. Natural death alone is explicitly allowed.");
            if (boss.QaProjectileAttackCount < boundAttackOrdinal || boss.QaProjectileCreatedCount < boundCreatedCount || boss.QaProjectileCreatedCount - boundCreatedCount != effectCount ||
                (long)boss.QaProjectileCreatedCount > (long)boss.QaProjectileAttackCount * 3 || sources == null || sources.Faulted || sourceFault || overflow)
                throw new InvalidOperationException("Projectile source journal lost provenance, overflowed, or diverged from actual production counters.");
            if (PlayerInputManager.instance?.playerControls != controls || !controls.asset.devices.HasValue || ownedDevices == null || ownedDevices.Length != 2 || !controls.asset.devices.Value.SequenceEqual(ownedDevices) || ownedDevices.Any(d => !d.added)) throw new InvalidOperationException("Owned neutral input lease changed.");
        }
        void Update()
        {
            if (!initialized || terminal) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now > deadline) throw new TimeoutException("Cuca projectile retention stage " + stage);
                if (!failed && errors.Count != 0) throw new InvalidOperationException("Runtime errors in projectile diagnostic.");
                if (!failed && stage != Stage.Menu && stage != Stage.SnapshotBefore && stage != Stage.SnapshotAfter && (!Application.isFocused || !SettingsMatch())) throw new InvalidOperationException("Focus/render settings changed.");
                Sweep();
                if (!failed && stage >= Stage.Warmup && stage <= Stage.SnapshotAfter) { ValidateLiveContext(); idleInput.Advance(0); RecordState(); }
                switch (stage)
                {
                    case Stage.Menu:
                        world = WorldSaveGameManager.instance; network = NetworkManager.Singleton;
                        if (world == null || network == null || TitleScreenManager.Instance == null || PlayerInputManager.instance?.playerControls == null || !Application.isFocused || !SettingsMatch()) return;
                        if (SceneManager.GetActiveScene().name != "SceneMenu" || network.IsListening || Time.timeScale != 1) throw new InvalidOperationException("Fresh unpaused menu/stopped host required.");
                        world.SaveRootOverride = request.saveRoot; if (world.SaveRoot != request.saveRoot) throw new InvalidOperationException("Isolated save root rejected.");
                        ownsHost = TitleScreenManager.Instance.TryStartNetworkAsHost(); if (!ownsHost) throw new InvalidOperationException("Owned host start rejected.");
                        SetStage(Stage.Host, 60); break;
                    case Stage.Host:
                        player = network.LocalClient?.PlayerObject?.GetComponent<PlayerManager>();
                        var ui = PlayerUIManager.instance?.GetComponentInChildren<SkillTreeUiManager>(true);
                        if (player == null || !player.IsSpawned || !player.IsOwner || !world.CanCreateNewGame || ui == null) return;
                        var catalog = (ui.attackSkills ?? Array.Empty<Skill>()).Concat(ui.defenseSkills ?? Array.Empty<Skill>()).Concat(ui.supportSkills ?? Array.Empty<Skill>()).ToArray();
                        var seed = PerformanceProcessHarness.BindCucaSeed(config.seedSaveJson, catalog);
                        var writer = new SaveFileDataWriter { saveDataDirectoryPath = request.saveRoot, saveFileName = "characterSlot_01" };
                        var saved = writer.TrySave(seed); if (!saved.Success) throw new InvalidOperationException(saved.Message);
                        world.RefreshCharacterProfiles(); TitleScreenManager.Instance.SelectCharacterSlot(CharacterSlot.CharacterSlot_01); world.LoadGame(); SetStage(Stage.World, 90); break;
                    case Stage.World:
                        if (SceneManager.GetActiveScene().name != "CucaLoad" || player == null || !player.IsSpawned || !player.IsOwner || PlayerInputManager.instance?.player != player || PlayerUIManager.instance?.playerUIHudManager?.IsHealthBarReady != true || !player.characterLocomotionManager.isGrounded || Time.timeScale != 1) return;
                        if (player.isDead.Value) throw new InvalidOperationException("Initial loaded player must be healthy; death is allowed only during observed workload.");
                        var bosses = Object.FindObjectsByType<CucaBoss>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(b => b.gameObject.scene.name == "CucaLoad").ToArray();
                        if (bosses.Length != 1) throw new InvalidOperationException("One actual authored Cuca required.");
                        if (bosses[0].QaProjectileCreatedCount != (long)bosses[0].QaProjectileAttackCount * 3) return;
                        if (bosses[0].magicProjectile == null || bosses[0].magicProjectile.GetComponent<Rigidbody>() == null || bosses[0].magicProjectile.GetComponent<DamageCollider>() == null || bosses[0].magicSpawnPoint == null || bosses[0].lineRenderer == null) throw new InvalidOperationException("Actual authored physics projectile driver required.");
                        BindBoss(bosses[0]); lastPlayerId = player.GetInstanceID();
                        controls = PlayerInputManager.instance.playerControls; previousDevices = InputSystem.devices.ToArray(); previousMask = controls.asset.bindingMask; previousRestriction = controls.asset.devices.HasValue ? controls.asset.devices.Value.ToArray() : null;
                        inputRestored = false; idleInput = PerformanceInputReplay.Create(new[] { "-karawaraPerformanceReplay" }, controls, "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]}]}");
                        ownedDevices = controls.asset.devices.Value.ToArray();
                        sources = PerformanceCucaProjectileRetentionSources.Open(ObserveProjectile); RecordState(true); SetStage(Stage.Warmup, config.warmupSeconds + 30); break;
                    case Stage.Warmup:
                        if (now - stageAt >= config.warmupSeconds && expectedProjectileOrdinal == 0) TakeSnapshot("cuca-before-ten-natural-bursts", Stage.SnapshotBefore); break;
                    case Stage.SnapshotBefore:
                        if (!pending.completed) return;
                        if (!pending.succeeded) throw new InvalidOperationException("Baseline snapshot failed: " + pending.error);
                        if (expectedProjectileOrdinal != 0) return;
                        baselineAttackOrdinal = boss.QaProjectileAttackCount; baselineCreatedCount = boss.QaProjectileCreatedCount; baselineEffectCount = effectCount;
                        observationDeadline = now + config.observationTimeoutSeconds; RecordState(true); SetStage(Stage.Observing, config.observationTimeoutSeconds); break;
                    case Stage.Observing:
                        if (CountCompleteBurstsAfter(baselineAttackOrdinal) < config.bursts) return;
                        workloadObserved = true; SetStage(Stage.Settling, Math.Min(config.settleSeconds + 15, observationDeadline - now)); break;
                    case Stage.Settling:
                        if (now > observationDeadline) throw new TimeoutException("Natural burst observation deadline exceeded.");
                        if (now - stageAt < config.settleSeconds || expectedProjectileOrdinal != 0) return;
                        actualMeasuredBursts = CountCompleteBurstsAfter(baselineAttackOrdinal); actualMeasuredCreations = effectCount - baselineEffectCount; liveAfterSettling = Alive(); RecordState(true);
                        measuredLiveAfterSettling = effects.Take(effectCount).Count(e => e.attackOrdinal > baselineAttackOrdinal && e.live != null);
                        TakeSnapshot("cuca-after-at-least-ten-natural-bursts-and-observation", Stage.SnapshotAfter); break;
                    case Stage.SnapshotAfter:
                        if (!pending.completed) return;
                        if (!pending.succeeded) throw new InvalidOperationException("Final snapshot failed: " + pending.error);
                        finalSnapshotObservedBursts = CountCompleteBurstsAfter(baselineAttackOrdinal); snapshotWorkloadObserved = finalSnapshotObservedBursts >= config.bursts; BeginReturn(); break;
                    case Stage.Returning:
                        if (SceneManager.GetActiveScene().name != "SceneMenu") return;
                        if (ownsHost && network != null && network.IsListening) network.Shutdown(); shutdownFrame = -1; SetStage(Stage.Shutdown, 20); break;
                    case Stage.Shutdown:
                        if ((network != null && network.IsListening) || (player != null && player.IsSpawned)) return;
                        if (shutdownFrame < 0) shutdownFrame = Time.frameCount;
                        if (Time.frameCount < shutdownFrame + 2) return;
                        ownsHost = false; SetStage(Stage.MenuSettling, 15); break;
                    case Stage.MenuSettling:
                        if (SceneManager.GetActiveScene().name != "SceneMenu" || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) || Time.timeScale != 1) throw new InvalidOperationException("Unpaused menu and stopped host required after return.");
                        if (now - stageAt < config.settleSeconds) return;
                        liveAfterReturn = Alive(); Finish(true); break;
                }
            }
            catch (Exception error)
            {
                errors.Add(error.ToString());
                if (failed) { if (ownsHost && network != null && network.IsListening) network.Shutdown(); Finish(false); return; }
                failed = true;
                try { BeginReturn(); }
                catch (Exception cleanup) { errors.Add("Production return failed: " + cleanup); sources?.Dispose(); sources = null; ReleaseInput(); Time.timeScale = 1; SceneManager.LoadSceneAsync("SceneMenu"); SetStage(Stage.Returning, 30); }
            }
        }
        void TakeSnapshot(string label, Stage next)
        {
            pending = new Snapshot { label = label, path = Path.Combine(config.outputRoot, label + "-" + Guid.NewGuid().ToString("N") + ".snap"), requestedTime = Time.realtimeSinceStartupAsDouble, attacksBefore = boss.QaProjectileAttackCount, creationsBefore = boss.QaProjectileCreatedCount, liveBefore = Alive() };
            var result = pending; if (File.Exists(result.path)) throw new IOException("Exclusive snapshot path required.");
            snapshots.Add(result); SetStage(next, 90);
            Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(result.path, (path, success) =>
            {
                try
                {
                    Sweep(); result.attacksAfter = boss == null ? -1 : boss.QaProjectileAttackCount; result.creationsAfter = boss == null ? -1 : boss.QaProjectileCreatedCount; result.liveAfter = Alive(); result.completedTime = Time.realtimeSinceStartupAsDouble;
                    result.succeeded = success && string.Equals(Path.GetFullPath(path), Path.GetFullPath(result.path), StringComparison.OrdinalIgnoreCase) && File.Exists(result.path);
                    if (result.succeeded) result.bytes = new FileInfo(result.path).Length;
                    result.succeeded &= result.bytes > 0; if (!result.succeeded) result.error = "Requested nonempty snapshot missing.";
                }
                catch (Exception error) { result.error = error.ToString(); result.succeeded = false; }
                finally { result.completed = true; }
            }, Unity.Profiling.Memory.CaptureFlags.ManagedObjects | Unity.Profiling.Memory.CaptureFlags.NativeObjects | Unity.Profiling.Memory.CaptureFlags.NativeAllocations);
        }
        void BeginReturn()
        {
            sources?.Dispose(); sources = null; ReleaseInput(); SetStage(Stage.Returning, 30);
            if (SceneManager.GetActiveScene().name != "SceneMenu")
            {
                var hud = PlayerUIManager.instance?.playerUIHudManager;
                if (hud == null) throw new InvalidOperationException("Actual HUD return action unavailable.");
                hud.ChangeScene();
            }
        }
        void ReleaseInput()
        {
            if (idleInput == null) return;
            var owned = idleInput; idleInput = null;
            try { owned.Dispose(); inputRestored = InputSystem.devices.SequenceEqual(previousDevices) && controls.asset.bindingMask.Equals(previousMask) && controls.asset.devices.HasValue == (previousRestriction != null) && (previousRestriction == null || controls.asset.devices.Value.SequenceEqual(previousRestriction)); }
            catch (Exception error) { inputRestored = false; errors.Add("Owned input cleanup failed: " + error); }
        }
        void Finish(bool cleanupPassed)
        {
            if (terminal) return; terminal = true; sources?.Dispose(); sources = null; ReleaseInput(); Application.logMessageReceived -= OnLog; Sweep();
            Dictionary<string, string> personalAfter = null; try { personalAfter = Fingerprints(personalRoot); } catch (Exception error) { errors.Add("Final personal fingerprint failed: " + error); }
            bool personalUnchanged = personalBefore != null && personalAfter != null && personalBefore.Count == personalAfter.Count && personalBefore.All(p => personalAfter.TryGetValue(p.Key, out var value) && p.Value == value);
            bool hostStopped = (network == null || !network.IsListening) && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);
            bool passed = !failed && errors.Count == 0 && cleanupPassed && hostStopped && inputRestored && personalUnchanged && workloadObserved && snapshotWorkloadObserved && snapshots.Count == 2 && snapshots.All(s => s.completed && s.succeeded) && !sourceFault && !overflow;
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed, cleanupPassed, mode = request.mode, config, buildGuid = Application.buildGUID, development = Debug.isDebugBuild, processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                inputRestored, personalUnchanged, personalBefore, personalAfter, ownedHostStopped = hostStopped, saveRoot = request.saveRoot, scene = SceneManager.GetActiveScene().name,
                workloadObserved, snapshotWorkloadObserved, baselineAttackOrdinal, baselineCreatedCount, baselineEffectCount, actualMeasuredBursts, actualMeasuredCreations, boundAttackOrdinal, boundCreatedCount,
                frozenBossId, frozenBossRootId, frozenPrefabId, frozenSpawnId, playerId = lastPlayerId, frozenBossSettings = frozenSettings, firstDeathTime, deathAllowedOnlyForDiagnostic = true, sourceFault, overflow, liveAfterSettling, measuredLiveAfterSettling, finalSnapshotObservedBursts, liveAfterReturn,
                snapshots, observations, effects = effects.Take(effectCount).ToArray(), errors, readyForTiming = false, populationVerified = false, retentionAccepted = false,
                boundary = "Passive natural same-process Cuca bursts, including transparently recorded natural death. No forced attacks, stat changes, resurrection or QA projectile destruction. At least ten complete bursts; extra natural bursts and snapshot counter intervals remain explicit. Five seconds is an observation interval, not an authored projectile lifetime or globally idle combat. Snapshot/reference analysis and cleanup acceptance remain separate; post-menu release proves scene-unload cleanup only. Impact/secondary effects have no creator journal here." };
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
        void OnDisable() { if (!initialized || terminal) return; failed = true; errors.Add("Owned projectile diagnostic disabled prematurely."); sources?.Dispose(); if (ownsHost && network != null && network.IsListening) network.Shutdown(); Finish(false); }
        void OnDestroy() { sources?.Dispose(); ReleaseInput(); Application.logMessageReceived -= OnLog; }
    }
#endif
}
