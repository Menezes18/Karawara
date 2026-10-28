using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using RPGKarawara;
using RPGKarawara.SkillTree;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Verification only. Coordinator enters/exits Play; this runner never changes production code.
public static class LegacyProductionSceneValidation
{
    const string PlanPath = "Docs/Validation/TOBE/Execution/legacy-production-spawn-plan.json";
    const string ReportPath = "Docs/Validation/TOBE/Execution/legacy-production-scene-validation.json";
    static readonly string[] Targets = { "Tutorial_Final", "t", "CucaLoad" };
    [Serializable] public sealed class Spawn
    {
        public string scene, path, hierarchy, sceneHash;
        public int buildIndex, authoredCandidates;
        public Vector3 position;
    }
    [Serializable] public sealed class SpawnPlan { public bool completed; public string utc, error; public List<Spawn> scenes = new List<Spawn>(); }
    [Serializable] public sealed class GroundSample
    {
        public int frame;
        public float gameTime, timeScale, footGap;
        public Vector3 position, foot, ground, normal;
        public bool hit, grounded;
        public string collider;
    }
    [Serializable] public sealed class SceneResult
    {
        public Spawn authored;
        public bool completed, passed, host, owner, spawned, hudReady, cameraReady, skillsCorrect, exactRestoreObserved;
        public bool authoredRestoreObserved, naturallyGroundedSaveCaptured, restoringSavedPose;
        public string error, phase, positionEvidence;
        public int health, vitality, healthRestores, observedFrames, initialHealthRestores, settlingFrames;
        public float hudHealth, elapsedGameSeconds, settlingGameSeconds, timeScale, gameTime;
        public Vector3 actualRestore, finalPosition, actualAuthoredRestore, savedPosition;
        public List<GroundSample> initialSettling = new List<GroundSample>();
        public List<GroundSample> ground = new List<GroundSample>();
        public List<string> errors = new List<string>();
        public List<TutorialRouteAction> tutorialActions = new List<TutorialRouteAction>();
    }
    [Serializable] public sealed class TutorialRouteAction
    {
        public string scene, context, popup, ui, outcome;
        public int frame;
        public float gameTime, timeScaleBefore, timeScaleAfter;
    }
    [Serializable] public sealed class Fingerprint { public string path, hash; public long writeTicks, length; }
    [Serializable] public sealed class Report
    {
        public string startedUtc, finishedUtc, result, saveRoot, originalScene, cleanupError;
        public string boundary = "Authored entry is a discovery/settling stage, not safe-spawn certification. Only actual naturally grounded positions are captured via TrySaveGame and reloaded through WorldSave.LoadGame for restore acceptance. Same-process skills only; no fresh-process or natural gameplay certification.";
        public bool completed, passed, cleanupCompleted, personalFilesUnchanged;
        public List<SceneResult> scenes = new List<SceneResult>();
        public List<string> errors = new List<string>();
        public List<TutorialRouteAction> tutorialActions = new List<TutorialRouteAction>();
        public List<Fingerprint> personalBefore = new List<Fingerprint>(), personalAfter = new List<Fingerprint>();
    }
    static Report report;
    static SpawnPlan plan;
    static WorldSaveGameManager manager;
    static PlayerManager player;
    static SceneResult current;
    static int stage, target, lastFrame;
    static double deadline;
    static float observationStart, nextSample;
    static int observationFrame;
    static AsyncOperation returning;
    static string originalRoot;
    static bool originalIsolation, originalManagerEnabled, originalInputEnabled, originalControlsEnabled, originalCameraFollow, startedHost;
    static bool capturedState;
    static float originalTimeScale;
    static bool originalCursor;
    static CursorLockMode originalLock;
    static CharacterSaveData originalContext, originalPlayer;
    static CharacterSlot originalSlot, originalSelected;
    static long originalGeneration;
    static int originalWorldIndex;
    static Dictionary<PlayerManager, long> originalApplied;
    static PlayerManager originalCameraPlayer;
    static Quaternion originalRotation;
    static bool originalInvulnerable;
    static Skill qaSkill;
    static readonly Dictionary<Skill, string> skillSnapshots = new Dictionary<Skill, string>();
    static NetworkVariable<int>.OnValueChangedDelegate onHealth;
    static NetworkVariable<Vector3>.OnValueChangedDelegate onPosition;

    [MenuItem("Karawara/Validation/TOBE/Prepare Legacy Production Spawn Plan")]
    public static void Prepare()
    {
        var preparation = new SpawnPlan { utc = DateTime.UtcNow.ToString("O") };
        string sceneSetup = string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive));
        try
        {
            Require(!EditorApplication.isPlaying, "Prepare authored spawn coordinates in EditMode before entering SceneMenu Play.");
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            foreach (string name in Targets)
            {
                int index = Array.FindIndex(enabled, s => Path.GetFileNameWithoutExtension(s.path) == name);
                Require(index >= 0, "Required enabled scene missing: " + name);
                Scene preview = EditorSceneManager.OpenPreviewScene(enabled[index].path);
                try
                {
                    var points = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Where(t => t.gameObject.activeSelf && t.CompareTag("Spawn"))
                        .OrderBy(t => t.name == "Spawn" ? 0 : 1).ThenBy(Hierarchy, StringComparer.Ordinal).ToArray();
                    Require(points.Length > 0, "No authored Spawn-tag transform in " + name + "; no coordinate invented.");
                    preparation.scenes.Add(new Spawn { scene = name, path = enabled[index].path, buildIndex = index,
                        hierarchy = Hierarchy(points[0]), position = points[0].position, authoredCandidates = points.Length,
                        sceneHash = Hash(enabled[index].path) });
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            preparation.completed = preparation.scenes.Count == Targets.Length;
            Require(sceneSetup == string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive)), "Preview preparation changed the original open-scene setup.");
        }
        catch (Exception ex) { preparation.completed = false; preparation.error = ex.GetBaseException().Message; }
        Directory.CreateDirectory(Path.GetDirectoryName(PlanPath));
        File.WriteAllText(PlanPath, JsonUtility.ToJson(preparation, true));
        Debug.Log("Legacy production spawn plan: completed=" + preparation.completed + ", " + preparation.error);
    }

    [MenuItem("Karawara/Validation/TOBE/Run Legacy Production Scene Validation")]
    public static void Run()
    {
        if (report != null) { Debug.LogWarning("Legacy production validation already running."); return; }
        manager = null; player = null; current = null; originalApplied = null; originalPlayer = null;
        returning = null; capturedState = false; startedHost = false; stage = -1; onHealth = null; onPosition = null;
        report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), result = "running" };
        try
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().name == "SceneMenu", "Run requires a fresh SceneMenu Play session; it never starts PlayMode.");
            Require(Time.timeScale > 0, "Fresh SceneMenu must be unpaused; the QA fixture will not force time scale.");
            plan = JsonUtility.FromJson<SpawnPlan>(File.ReadAllText(PlanPath));
            Require(plan != null && plan.completed && plan.scenes.Count == Targets.Length, "Prepare a completed authored spawn plan first.");
            foreach (var spawn in plan.scenes)
                Require(SceneUtility.GetScenePathByBuildIndex(spawn.buildIndex) == spawn.path && Hash(spawn.path) == spawn.sceneHash, "Authored scene/build index changed after preparation: " + spawn.scene);
            manager = WorldSaveGameManager.instance;
            Require(manager != null && TitleScreenManager.Instance != null && NetworkManager.Singleton != null, "Actual SceneMenu save/title/network managers missing.");
            Require(!WorldSaveGameManager.DevPlaygroundIsolationActive && Object.FindAnyObjectByType<RPGKarawara.DevTools.DevPlaygroundController>() == null, "Fresh production SceneMenu required; playground lifecycle must not participate.");
            report.originalScene = SceneManager.GetActiveScene().path;
            report.personalBefore = PersonalFiles();
            originalRoot = manager.SaveRootOverride; originalIsolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
            originalManagerEnabled = manager.enabled; originalContext = manager.currentCharacterData; originalSlot = manager.currentCharacterSlotBeingUsed;
            originalSelected = TitleScreenManager.Instance.currentSelectedSlot;
            originalGeneration = (long)Field("loadGeneration").GetValue(manager); originalWorldIndex = manager.GetWorldSceneIndex();
            originalApplied = new Dictionary<PlayerManager, long>((Dictionary<PlayerManager, long>)Field("appliedPlayers").GetValue(manager));
            originalTimeScale = Time.timeScale; originalCursor = Cursor.visible; originalLock = Cursor.lockState;
            originalCameraPlayer = PlayerCamera.instance != null ? PlayerCamera.instance.player : null;
            originalCameraFollow = PlayerCamera.instance != null && PlayerCamera.instance.canFollow;
            originalInputEnabled = PlayerInputManager.instance != null && PlayerInputManager.instance.enabled;
            originalControlsEnabled = PlayerInputManager.instance != null && PlayerInputManager.instance.playerControls != null && PlayerInputManager.instance.playerControls.asset.enabled;
            capturedState = true;
            report.saveRoot = Path.Combine(Path.GetTempPath(), "Karawara-ProductionRestore-" + Guid.NewGuid().ToString("N"));
            manager.SaveRootOverride = report.saveRoot;
            Require(manager.SaveRoot == report.saveRoot, "QA root was not accepted; no host/load command issued.");
            Directory.CreateDirectory(report.saveRoot);
            manager.enabled = true;
            Application.logMessageReceived += Capture;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += OnReload;
            startedHost = !NetworkManager.Singleton.IsListening;
            Require(TitleScreenManager.Instance.TryStartNetworkAsHost(), "Production title host failed: " + TitleScreenManager.Instance.HostStatus);
            stage = 0; target = 0; lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 30;
            Flush();
        }
        catch (Exception ex) { Abort(ex.GetBaseException().Message); }
    }

    static void Tick()
    {
        if (report == null) return;
        if (!EditorApplication.isPlaying) { Finish(false, "Play stopped before completion", false); return; }
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (stage == 1 || stage == 2 || stage == 5 || stage == 6)
                ContinueTutorialRoute("stage " + stage);
            if (stage == 0)
            {
                player = LocalPlayer();
                if (Ready(false))
                {
                    originalPlayer = new CharacterSaveData(); player.SaveGameDataToCurrentCharacterData(ref originalPlayer);
                    originalRotation = player.transform.rotation; originalInvulnerable = player.characterNetworkManager.isInvulnerable.Value;
                    player.characterNetworkManager.isInvulnerable.Value = true;
                    foreach (string guid in AssetDatabase.FindAssets("t:Skill"))
                    {
                        var skill = AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(guid));
                        if (skill != null) skillSnapshots[skill] = EditorJsonUtility.ToJson(skill);
                    }
                    Require(skillSnapshots.Count > 0, "Actual skill catalog has no assets for the QA selected slot.");
                    qaSkill = Object.Instantiate(skillSnapshots.Keys.OrderBy(s => AssetDatabase.GetAssetPath(s), StringComparer.Ordinal).First());
                    onHealth = (before, health) => { if (current != null && health == 90) { if (current.restoringSavedPose) current.healthRestores++; else current.initialHealthRestores++; } };
                    onPosition = (before, position) => {
                        if (current == null || SceneManager.GetActiveScene().buildIndex != current.authored.buildIndex || player.playerNetworkManager.currentHealth.Value != 90) return;
                        Vector3 expected = current.restoringSavedPose ? current.savedPosition : current.authored.position;
                        if (Vector3.Distance(position, expected) >= .001f || Vector3.Distance(player.transform.position, expected) >= .001f) return;
                        if (current.restoringSavedPose && !current.exactRestoreObserved)
                        { current.actualRestore = player.transform.position; current.exactRestoreObserved = true; current.positionEvidence = "Actual network-position event and exact Transform during saved-pose restore"; current.ground.Add(SampleGround()); }
                        else if (!current.restoringSavedPose && !current.authoredRestoreObserved)
                        { current.actualAuthoredRestore = player.transform.position; current.authoredRestoreObserved = true; current.initialSettling.Add(SampleGround()); }
                    };
                    player.playerNetworkManager.currentHealth.OnValueChanged += onHealth;
                    player.playerNetworkManager.networkPosition.OnValueChanged += onPosition;
                    BeginTarget();
                }
                else if (now >= deadline) Abort("Actual SceneMenu host/player/HUD/skill readiness timeout.");
            }
            else if (stage == 1)
            {
                BlockInput();
                if (SceneManager.GetActiveScene().buildIndex == current.authored.buildIndex && Ready(true) && current.authoredRestoreObserved)
                {
                    Snapshot();
                    var authored = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => Hierarchy(t) == current.authored.hierarchy);
                    Require(authored != null && Vector3.Distance(authored.position, current.authored.position) < .001f, "Runtime authored spawn differs from prepared source.");
                    Require(current.health == 90 && current.vitality == 20 && current.hudHealth == 90 && current.skillsCorrect, "Selected QA stats/HUD/skills were not applied.");
                    stage = 2; observationStart = Time.time; observationFrame = Time.frameCount; nextSample = Time.time;
                    current.phase = "Naturally settling authored entry; no projected spawn or save acceptance yet";
                    deadline = now + 60; Flush();
                }
                else if (now >= deadline) Abort("Real LoadWorldScene/readiness/exact saved position timeout in " + current.authored.scene + ". " + manager.SaveStatus);
            }
            else if (stage == 2)
            {
                BlockInput();
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (Time.time >= nextSample) { current.initialSettling.Add(SampleGround()); nextSample = Time.time + .1f; }
                current.settlingFrames = Time.frameCount - observationFrame;
                current.settlingGameSeconds = Time.time - observationStart;
                if (!Finite(player.transform.position) || player.transform.position.y < current.authored.position.y - 300)
                { EndTarget(false, "Authored-entry discovery never found actual ground; no safe pose captured or invented."); return; }
                var recent = current.initialSettling.Skip(Math.Max(0, current.initialSettling.Count - 5)).ToArray();
                if (current.settlingFrames >= 30 && recent.Length == 5 && recent.All(s => SafeGround(s) && s.grounded && s.footGap <= .25f) &&
                    recent.All(s => Vector3.Distance(s.position, recent[0].position) < .05f))
                {
                    CaptureNaturallyGroundedSave();
                }
                else if (now >= deadline) EndTarget(false, "Authored entry did not naturally settle to a stable grounded pose before deadline; no save/fallback invented.");
            }
            else if (stage == 5)
            {
                BlockInput();
                // A stationary same-scene reload may legitimately emit no position delta.
                if (SceneManager.GetActiveScene().buildIndex == current.authored.buildIndex && Ready(true) && ApplicationRecordedForPlayer())
                {
                    if (!current.exactRestoreObserved && current.healthRestores == 1 &&
                        Vector3.Distance(player.playerNetworkManager.networkPosition.Value, current.savedPosition) < .001f &&
                        Vector3.Distance(player.transform.position, current.savedPosition) < .05f)
                    { current.exactRestoreObserved = true; current.actualRestore = player.playerNetworkManager.networkPosition.Value; current.positionEvidence = "Unchanged actual network position, matching Transform, one health restore and completed generation"; current.ground.Add(SampleGround()); }
                    if (current.exactRestoreObserved)
                    {
                        Snapshot();
                        Require(current.health == 90 && current.vitality == 20 && current.hudHealth == 90 && current.skillsCorrect, "Captured grounded save did not restore its stats/HUD/skills.");
                        stage = 6; observationStart = Time.time; observationFrame = Time.frameCount; nextSample = Time.time;
                        current.phase = "Observing real restored naturally-grounded save";
                        deadline = now + 45; Flush();
                    }
                }
                if (stage == 5 && now >= deadline) Abort("Real saved-grounded-pose reload/readiness/exact position timeout in " + current.authored.scene + ". " + manager.SaveStatus);
            }
            else if (stage == 6)
            {
                BlockInput();
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (Time.time >= nextSample) { current.ground.Add(SampleGround()); nextSample = Time.time + .1f; }
                current.observedFrames = Time.frameCount - observationFrame;
                current.elapsedGameSeconds = Time.time - observationStart;
                if (player.transform.position.y < current.savedPosition.y - 1.5f || player.transform.position.y > current.savedPosition.y + 1.5f)
                { EndTarget(false, "Reloaded grounded save fell or jumped height outside its measured saved-position envelope."); return; }
                if (current.elapsedGameSeconds >= 3 && current.observedFrames >= 30)
                {
                    Snapshot();
                    bool safe = current.ground.Count >= 3 && current.ground.Skip(Math.Max(0, current.ground.Count - 3)).All(SafeGround) &&
                        player.characterLocomotionManager.isGrounded && current.healthRestores == 1 && current.health == 90 && current.vitality == 20 && current.hudHealth == 90;
                    EndTarget(safe, safe ? "" : "Restored saved-pose grounding, HP/HUD or one-application requirement failed after advancing frames.");
                }
                else if (now >= deadline) Abort("Restored saved-pose observation did not reach three gameplay seconds and thirty frames.");
            }
            else if (stage == 3)
            {
                if (returning == null || !returning.isDone) { if (now >= deadline) Finish(false, "Original SceneMenu cleanup load timeout", false); return; }
                RestoreRuntime();
                if (startedHost && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
                stage = 4; deadline = now + 20;
            }
            else if (stage == 4)
            {
                if (!startedHost || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                    Finish(report.scenes.Count == Targets.Length && report.scenes.All(s => s.completed && s.passed), "Finished production scene restore validation", true);
                else if (now >= deadline) Finish(false, "QA-started host shutdown timeout", false);
            }
        }
        catch (Exception ex) { Abort(ex.GetBaseException().Message); }
    }

    static void BeginTarget()
    {
        if (target >= plan.scenes.Count) { BeginCleanup(); return; }
        ContinueTutorialRoute("before next production load");
        current = new SceneResult { authored = plan.scenes[target++], phase = "Loading authored entry for grounded-pose discovery" }; report.scenes.Add(current);
        BlockInput();
        player.playerNetworkManager.currentHealth.Value = 80;
        var data = new CharacterSaveData { characterName = "QA production " + current.authored.scene, sceneIndex = current.authored.buildIndex,
            vitality = 20, endurance = 10, currentHealth = 90, xPosition = current.authored.position.x, yPosition = current.authored.position.y,
            zPosition = current.authored.position.z, skillsSlot = new[] { qaSkill }, SkillAttack = new[] { qaSkill } };
        var written = new SaveFileDataWriter { saveDataDirectoryPath = manager.SaveRoot, saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03) }.TrySave(data);
        Require(written.Success, "QA selected-slot seed failed: " + written.Message);
        if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.currentSelectedSlot = CharacterSlot.CharacterSlot_03;
        manager.currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_03;
        manager.LoadGame(); // Production command owns scene validation, generation and asynchronous load.
        Require(manager.currentCharacterData != null && manager.currentCharacterData.sceneIndex == current.authored.buildIndex && string.IsNullOrEmpty(manager.SaveStatus), "Production load command rejected QA slot: " + manager.SaveStatus);
        stage = 1; deadline = EditorApplication.timeSinceStartup + 120; Flush();
    }
    static void CaptureNaturallyGroundedSave()
    {
        Require(Ready(true) && player.characterLocomotionManager.isGrounded, "Actual player lost grounded readiness before capture.");
        Vector3 actualPosition = player.transform.position;
        var saved = manager.TrySaveGame(); // Actual gameplay snapshot and atomic writer, always in QA root.
        Require(saved.Success, "Naturally grounded QA TrySaveGame failed: " + saved.Message);
        var loaded = manager.TryLoadSelectedSlot(CharacterSlot.CharacterSlot_03);
        Require(loaded.Success && loaded.Value.sceneIndex == current.authored.buildIndex && loaded.Value.currentHealth == 90 && loaded.Value.vitality == 20,
            "Grounded QA save did not round-trip actual scene/stats.");
        current.savedPosition = new Vector3(loaded.Value.xPosition, loaded.Value.yPosition, loaded.Value.zPosition);
        Require(Vector3.Distance(current.savedPosition, actualPosition) < .001f, "QA save coordinates differ from the actually grounded player.");
        current.naturallyGroundedSaveCaptured = true;
        player.playerNetworkManager.currentHealth.Value = 80;
        current.restoringSavedPose = true; current.healthRestores = 0; current.exactRestoreObserved = false;
        current.phase = "Reloading actual naturally-grounded saved pose through production load";
        manager.LoadGame();
        Require(string.IsNullOrEmpty(manager.SaveStatus), "Real grounded-save reload was rejected: " + manager.SaveStatus);
        stage = 5; deadline = EditorApplication.timeSinceStartup + 120; Flush();
    }
    static void EndTarget(bool passed, string error) { Snapshot(); current.completed = true; current.passed = passed && current.errors.Count == 0; current.error = error; Flush(); BeginTarget(); }
    static void BeginCleanup()
    {
        ContinueTutorialRoute("before original SceneMenu cleanup");
        stage = 3; current = null;
        manager.StopAllCoroutines();
        returning = SceneManager.LoadSceneAsync(report.originalScene);
        deadline = EditorApplication.timeSinceStartup + 120; Flush();
    }
    static void Abort(string error)
    {
        if (report == null) return;
        report.errors.Add(error); report.result = error;
        if (current != null) { current.error = error; current.completed = true; current.passed = false; }
        if (stage == 3 || stage == 4 || manager == null || string.IsNullOrEmpty(report.originalScene) || !EditorApplication.isPlaying) { Finish(false, error, false); return; }
        try { BeginCleanup(); } catch (Exception cleanup) { report.cleanupError = cleanup.GetBaseException().Message; Finish(false, error, false); }
    }
    static bool Ready(bool world)
    {
        var hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsListening && player != null && player.IsSpawned && player.IsOwner &&
            player.playerNetworkManager != null && player.playerStatsManager != null && player.GetComponent<PlayerSkillManager>() != null &&
            Object.FindAnyObjectByType<SkillTreeUiManager>(FindObjectsInactive.Include) != null && hud != null && hud.IsHealthBarReady &&
            PlayerCamera.instance != null && PlayerCamera.instance.player == player && PlayerCamera.instance.cameraObject != null &&
            (!world || (PlayerCamera.instance.canFollow && PlayerCamera.instance.cameraObject.isActiveAndEnabled && SceneManager.GetActiveScene().isLoaded));
    }
    static void Snapshot()
    {
        if (current == null || player == null) return;
        current.host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        current.owner = player.IsOwner; current.spawned = player.IsSpawned;
        current.health = player.playerNetworkManager.currentHealth.Value; current.vitality = player.playerNetworkManager.vitality.Value;
        var hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
        current.hudReady = hud != null && hud.IsHealthBarReady;
        if (current.hudReady) current.hudHealth = ((UI_StatBar)typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud)).GetComponent<Slider>().value;
        current.cameraReady = PlayerCamera.instance != null && PlayerCamera.instance.player == player && PlayerCamera.instance.canFollow;
        var skills = player.GetComponent<PlayerSkillManager>();
        var tree = Object.FindAnyObjectByType<SkillTreeUiManager>(FindObjectsInactive.Include);
        current.skillsCorrect = skills != null && skills.slot != null && skills.slot.Length > 0 && skills.slot[0].skillSlot == qaSkill &&
            tree != null && tree.attackSkills != null && tree.attackSkills.Length == 1 && tree.attackSkills[0] == qaSkill;
        current.finalPosition = player.transform.position;
        current.timeScale = Time.timeScale; current.gameTime = Time.time;
    }
    static GroundSample SampleGround()
    {
        var controller = player.characterController;
        Vector3 foot = player.transform.TransformPoint(controller.center) - Vector3.up * (controller.height * player.transform.lossyScale.y * .5f);
        var sample = new GroundSample { frame = Time.frameCount, gameTime = Time.time, timeScale = Time.timeScale, position = player.transform.position, foot = foot, grounded = player.characterLocomotionManager.isGrounded };
        var hits = Physics.RaycastAll(foot + Vector3.up * 2, Vector3.down, 8, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.gameObject.scene.buildIndex == current.authored.buildIndex && h.collider.GetComponentInParent<CharacterManager>() == null)
            .OrderBy(h => h.distance).ToArray();
        if (hits.Length > 0) { var hit = hits[0]; sample.hit = true; sample.ground = hit.point; sample.normal = hit.normal; sample.collider = Hierarchy(hit.collider.transform); sample.footGap = foot.y - hit.point.y; }
        return sample;
    }
    static bool SafeGround(GroundSample sample) => sample.hit && sample.footGap >= -.25f && sample.footGap <= 1.5f && Vector3.Dot(sample.normal, Vector3.up) > .3f;
    static bool Finite(Vector3 position) => !float.IsNaN(position.x) && !float.IsInfinity(position.x) && !float.IsNaN(position.y) && !float.IsInfinity(position.y) && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
    static bool ApplicationRecordedForPlayer() => ((Dictionary<PlayerManager, long>)Field("appliedPlayers").GetValue(manager)).TryGetValue(player, out long generation) && generation == (long)Field("loadGeneration").GetValue(manager);
    // Scripted route action, equivalent to dismissing the authored visible tutorial.
    // Never disable a popup, project a spawn, resume an unrelated pause or assign Time.timeScale.
    static void ContinueTutorialRoute(string context)
    {
        var scene = SceneManager.GetActiveScene();
        var popups = Object.FindObjectsByType<PopUpTutorial>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(p => p.isActiveAndEnabled && p.gameObject.scene == scene && !p.passou &&
                p.UI != null && p.UI.activeSelf && p.UI.activeInHierarchy).ToArray();
        if (popups.Length == 0 && Time.timeScale > 0) return;
        var hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
        var pause = hud != null ? typeof(PlayerUIHudManager).GetField("pauseMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud) as GameObject : null;
        bool hudPaused = (pause != null && pause.activeSelf) || (hud != null && hud.pauseSkillMenu != null && hud.pauseSkillMenu.activeSelf);
        bool cutscene = Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Any(d => d.isActiveAndEnabled && d.state == UnityEngine.Playables.PlayState.Playing) ||
            Object.FindObjectsByType<AnimationTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Any(a => a.isActiveAndEnabled && a.cam != null && a.cam.isActiveAndEnabled) ||
            Object.FindObjectsByType<GateActivator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Any(g => g.isActiveAndEnabled && g.cameraAtivar != null && g.cameraAtivar.activeInHierarchy);
        if (popups.Length != 1 || hudPaused || cutscene)
        {
            string diagnostic = "Unsupported pause/route context in " + scene.name + " (" + context +
                "): timeScale=" + Time.timeScale + ", visible active tutorials=" + popups.Length +
                ", HUD pause=" + hudPaused + ", active cutscene=" + cutscene + "; no forced resume or unrelated dialog dismissal.";
            RecordTutorialAction(new TutorialRouteAction { scene = scene.name, context = context, frame = Time.frameCount,
                gameTime = Time.time, timeScaleBefore = Time.timeScale, timeScaleAfter = Time.timeScale, outcome = diagnostic });
            throw new InvalidOperationException(diagnostic);
        }
        var popup = popups[0];
        var action = new TutorialRouteAction { scene = scene.name, context = context, popup = Hierarchy(popup.transform),
            ui = Hierarchy(popup.UI.transform), frame = Time.frameCount, gameTime = Time.time, timeScaleBefore = Time.timeScale,
            outcome = "Calling authored PopUpTutorial.close() for the visible tutorial as a scripted route action" };
        RecordTutorialAction(action);
        try
        {
            popup.close();
            action.timeScaleAfter = Time.timeScale;
            Require(popup.passou && !popup.UI.activeSelf && Time.timeScale > 0,
                "Authored tutorial close did not dismiss its UI and resume gameplay; no forced correction.");
            action.outcome = "Authored close dismissed the visible tutorial, marked passou and resumed gameplay";
        }
        catch (Exception ex) { action.timeScaleAfter = Time.timeScale; action.outcome = "Authored close failed: " + ex.GetBaseException().Message; throw; }
        finally { Flush(); }
    }
    static void RecordTutorialAction(TutorialRouteAction action)
    {
        report.tutorialActions.Add(action);
        if (current != null) current.tutorialActions.Add(action);
        Flush();
    }
    static void BlockInput() { if (PlayerInputManager.instance != null) { PlayerInputManager.instance.enabled = false; PlayerInputManager.instance.playerControls?.Disable(); } }
    static void RestoreRuntime()
    {
        if (player != null && player.IsSpawned && originalPlayer != null) { Require(player.ApplyLegacySave(originalPlayer), "Original runtime player state could not restore."); player.transform.rotation = originalRotation; player.characterNetworkManager.isInvulnerable.Value = originalInvulnerable; }
        if (manager != null)
        {
            manager.currentCharacterSlotBeingUsed = originalSlot; manager.currentCharacterData = originalContext;
            Field("loadGeneration").SetValue(manager, originalGeneration); Field("worldSceneIndex").SetValue(manager, originalWorldIndex);
            var applied = (Dictionary<PlayerManager, long>)Field("appliedPlayers").GetValue(manager); applied.Clear(); foreach (var pair in originalApplied) applied.Add(pair.Key, pair.Value);
        }
        if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.currentSelectedSlot = originalSelected;
    }
    static void Finish(bool passed, string result, bool restoredScene)
    {
        if (report == null) return;
        var finished = report;
        Action<Action> cleanup = action => { try { action(); } catch (Exception ex) { finished.cleanupError += ex.GetBaseException().Message + "\n"; } };
        cleanup(() => { if (player != null) { player.playerNetworkManager.currentHealth.OnValueChanged -= onHealth; player.playerNetworkManager.networkPosition.OnValueChanged -= onPosition; } });
        cleanup(() => { foreach (var pair in skillSnapshots) if (pair.Key != null) EditorJsonUtility.FromJsonOverwrite(pair.Value, pair.Key); skillSnapshots.Clear(); if (qaSkill != null) Object.DestroyImmediate(qaSkill); });
        cleanup(() => { if (manager != null && originalApplied != null) RestoreRuntime(); });
        cleanup(() => { finished.personalAfter = PersonalFiles(); finished.personalFilesUnchanged = JsonUtility.ToJson(new FingerprintList { files = finished.personalBefore }) == JsonUtility.ToJson(new FingerprintList { files = finished.personalAfter }); });
        cleanup(() => { if (capturedState && manager != null) { manager.SaveRootOverride = originalRoot; manager.enabled = originalManagerEnabled; manager.RefreshCharacterProfiles(); } if (capturedState) WorldSaveGameManager.DevPlaygroundIsolationActive = originalIsolation; });
        cleanup(() => { if (!capturedState) return; if (PlayerCamera.instance != null) { PlayerCamera.instance.player = originalCameraPlayer; PlayerCamera.instance.canFollow = originalCameraFollow; } if (PlayerInputManager.instance != null) { PlayerInputManager.instance.enabled = originalInputEnabled; if (originalControlsEnabled) PlayerInputManager.instance.playerControls?.Enable(); else PlayerInputManager.instance.playerControls?.Disable(); } });
        if (capturedState) { Time.timeScale = originalTimeScale; Cursor.visible = originalCursor; Cursor.lockState = originalLock; }
        cleanup(() => { if (!string.IsNullOrEmpty(finished.saveRoot) && Directory.Exists(finished.saveRoot)) Directory.Delete(finished.saveRoot, true); });
        Application.logMessageReceived -= Capture; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= OnPlayState; AssemblyReloadEvents.beforeAssemblyReload -= OnReload;
        finished.finishedUtc = DateTime.UtcNow.ToString("O"); finished.result = result; finished.completed = finished.scenes.Count == Targets.Length && finished.scenes.All(s => s.completed);
        finished.cleanupCompleted = restoredScene && string.IsNullOrEmpty(finished.cleanupError); finished.passed = passed && finished.completed && finished.cleanupCompleted && finished.personalFilesUnchanged && finished.errors.Count == 0;
        report = null; Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(finished, true));
        Debug.Log("Legacy production scene validation: passed=" + finished.passed + ", completed=" + finished.completed + ", " + result);
    }
    [Serializable] sealed class FingerprintList { public List<Fingerprint> files; }
    static List<Fingerprint> PersonalFiles()
    {
        var files = new List<Fingerprint>();
        foreach (string slot in new[] { "characterSlot_01", "characterSlot_02", "characterSlot_03" }) foreach (string suffix in new[] { "", ".bak", ".tmp" })
        { string path = Path.Combine(Application.persistentDataPath, slot + suffix); if (File.Exists(path)) { var info = new FileInfo(path); files.Add(new Fingerprint { path = path, length = info.Length, writeTicks = info.LastWriteTimeUtc.Ticks, hash = Hash(path) }); } }
        return files;
    }
    static void Capture(string message, string stack, LogType type) { if (report == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return; string error = type + ": " + message + "\n" + stack; if (current != null) current.errors.Add(error); else report.errors.Add(error); Flush(); }
    static void OnPlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode && report != null) Finish(false, "Coordinator stopped Play before runner completion", false); }
    static void OnReload() { if (report != null) Finish(false, "Assembly reload interrupted validation", false); }
    static void Flush() { if (report != null) { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); } }
    static FieldInfo Field(string name) => typeof(WorldSaveGameManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
    static PlayerManager LocalPlayer() => NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null ? NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerManager>() : null;
    static string Hierarchy(Transform transform) { string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
