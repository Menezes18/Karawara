using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RPGKarawara;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Editor
{
    /// <summary>Runtime scene transitions only. Never creates, loads or deletes character saves.</summary>
    public static class ProductionSceneSmokeTest
    {
        const string ReportPath = "Docs/HomolProductionSceneSmokeTest.json";
        static Report report;
        static Phase current;
        static AsyncOperation loading;
        static int stage, sceneIndex;
        static string[] enabledScenePaths;
        static double deadline;
        static float previousTimeScale;
        static bool previousCursor;
        static CursorLockMode previousLock;
        static HashSet<int> previousEnemyIds = new HashSet<int>();
        static float sceneObservationStart;
        static float sceneObservationUnscaledStart;
        static int sceneObservationFrame;

        [Serializable] public class Report
        {
            public string utc, unityVersion, persistentDataPath, result;
            public bool completed, saveFilesUnchanged, sceneLifecyclePassed, initialNewGamePlacementApplied;
            public List<Phase> phases = new List<Phase>();
            public List<Fingerprint> savesBefore = new List<Fingerprint>(), savesAfter = new List<Fingerprint>();
            public string limitation = "Loads scenes directly without invoking NewGame or saving; reproduces NewGame's initial player placement before Tutorial_Final. Exercises host spawn and scene initialization only; no player-input, combat, visuals or multiplayer parity certification.";
        }
        [Serializable] public class Phase
        {
            public string requestedScene, activeScene, result, playerPosition;
            public int buildIndex, playerCount, enemyCount, sceneCheckerCount;
            public int observedFrames;
            public int enabledAudioListenerCount;
            public float observedGameSeconds;
            public float observedUnscaledSeconds, timeScale;
            public bool paused;
            public bool sceneLoaded, hostListening, playerPresent, playerOwner, playerSpawned, playerRequired;
            public bool inputPresent, inputEnabled, cameraPresent, cameraFollow, hudPresent, hudActive, spawnPresent;
            public string[] enemyNames, enemiesSurvivingPreviousScene;
            public List<LogEntry> errors = new List<LogEntry>();
        }
        [Serializable] public class LogEntry { public string type, message, stack; }
        [Serializable] public class Fingerprint { public string path, hash, error; public long bytes, lastWriteUtcTicks; }

        [MenuItem("Karawara/Validation/Run Production Scene Smoke Test")]
        public static void Run()
        {
            if (report != null) { Debug.LogWarning("Production smoke test is already running."); return; }
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "SceneMenu")
            {
                Debug.LogError("Enter Play Mode in SceneMenu before running the production scene smoke test.");
                return;
            }
            report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                persistentDataPath = Application.persistentDataPath, result = "running"
            };
            report.savesBefore = Fingerprints();
            previousEnemyIds.Clear();
            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.visible;
            previousLock = Cursor.lockState;
            enabledScenePaths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            int menuIndex = Array.FindIndex(enabledScenePaths, path => Path.GetFileNameWithoutExtension(path) == "SceneMenu");
            if (menuIndex < 0)
            {
                report = null;
                Debug.LogError("SceneMenu must be enabled in Build Settings.");
                return;
            }
            current = NewPhase(menuIndex);
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
            try
            {
                // Keep isolation enabled until Play Mode exits; production UI may still be used after the test.
                WorldSaveGameManager.DevPlaygroundIsolationActive = true;
                var save = WorldSaveGameManager.instance;
                if (save == null) throw new InvalidOperationException("SceneMenu has no WorldSaveGameManager singleton.");
                save.enabled = false;
                save.currentCharacterSlotBeingUsed = CharacterSlot.NO_SLOT;
                save.currentCharacterData = new CharacterSaveData
                {
                    characterName = "Production Smoke Test", currentHealth = 400,
                    vitality = 1, endurance = 1, sceneIndex = 1
                };
                var network = NetworkManager.Singleton;
                if (network == null) throw new InvalidOperationException("SceneMenu has no NetworkManager singleton.");
                if (!network.IsListening)
                {
                    // Keep validation local and avoid a socket left by an interrupted Editor reload.
                    var transport = network.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
                    if (transport != null) transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
                    if (TitleScreenManager.Instance == null) throw new InvalidOperationException("No TitleScreenManager singleton.");
                    TitleScreenManager.Instance.StartNetworkAsHost();
                }
                else if (!network.IsHost) throw new InvalidOperationException("Existing network session is not a host.");
                stage = 0;
                sceneIndex = 0;
                deadline = EditorApplication.timeSinceStartup + 20;
                Flush();
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void Tick()
        {
            if (report == null) return;
            if (!EditorApplication.isPlaying) { Finish(false, "Play Mode stopped before completion"); return; }
            try
            {
                var now = EditorApplication.timeSinceStartup;
                if (stage == 0)
                {
                    var network = NetworkManager.Singleton;
                    var player = LocalPlayer();
                    if (network != null && network.IsHost && player != null && player.IsSpawned && player.IsOwner)
                    {
                        stage = 1;
                        deadline = now + 3;
                    }
                    else if (now >= deadline)
                    {
                        Snapshot(current);
                        current.result = "Host/player spawn timeout";
                        Finish(false, current.result);
                    }
                }
                else if (stage == 1)
                {
                    if (now < deadline) return;
                    Snapshot(current);
                    current.result = "SceneMenu host/player initialized";
                    BeginScene();
                }
                else if (stage == 2)
                {
                    if (loading != null && loading.isDone)
                    {
                        stage = 3;
                        sceneObservationStart = Time.time;
                        sceneObservationUnscaledStart = Time.unscaledTime;
                        sceneObservationFrame = Time.frameCount;
                        deadline = now + 60;
                    }
                    else if (now >= deadline)
                    {
                        Snapshot(current);
                        current.result = "Scene load timeout";
                        Finish(false, current.result);
                    }
                }
                else if (stage == 3)
                {
                    if (Time.unscaledTime - sceneObservationUnscaledStart < 5 || Time.frameCount - sceneObservationFrame < 30)
                    {
                        if (now >= deadline)
                        {
                            Snapshot(current);
                            current.result = "Scene observation timeout before five unscaled seconds and thirty frames";
                            Finish(false, current.result);
                        }
                        return;
                    }
                    Snapshot(current);
                    current.result = current.sceneLoaded && (!current.playerRequired || (current.playerOwner && current.playerSpawned))
                        ? (current.errors.Count == 0 ? "Initialized without captured errors" : "Initialized with captured errors")
                        : "Loaded with missing scene/player state";
                    if (current.enemiesSurvivingPreviousScene.Length > 0)
                        current.result = "Scene lifecycle failed: enemies survived previous scene";
                    Flush();
                    sceneIndex++;
                    BeginScene();
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void BeginScene()
        {
            while (sceneIndex < enabledScenePaths.Length && Path.GetFileNameWithoutExtension(enabledScenePaths[sceneIndex]) == "SceneMenu")
                sceneIndex++;
            if (sceneIndex >= enabledScenePaths.Length)
            {
                Finish(true, "All " + enabledScenePaths.Length + " enabled production scenes observed (including SceneMenu initialization)");
                return;
            }
            if (SceneManager.sceneCountInBuildSettings <= sceneIndex)
                throw new InvalidOperationException("Missing enabled build scene index " + sceneIndex);
            current = NewPhase(sceneIndex);
            previousEnemyIds = new HashSet<int>(UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsSortMode.None)
                .Select(enemy => enemy.GetInstanceID()));
            WorldSaveGameManager.DevPlaygroundIsolationActive = true;
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.enabled = false;
            if (!report.initialNewGamePlacementApplied && Path.GetFileNameWithoutExtension(enabledScenePaths[sceneIndex]) == "Tutorial_Final")
            {
                var player = LocalPlayer();
                if (player == null) throw new InvalidOperationException("Missing local player for initial NewGame placement.");
                var controller = player.characterController;
                bool controllerEnabled = controller != null && controller.enabled;
                if (controllerEnabled) controller.enabled = false;
                // Same position as WorldSaveGameManager.NewGame; no save operation is invoked.
                player.transform.position = new Vector3(-319.92f, 175.81f, 595.46f);
                if (controllerEnabled) controller.enabled = true;
                report.initialNewGamePlacementApplied = true;
            }
            loading = SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Single);
            stage = 2;
            deadline = EditorApplication.timeSinceStartup + 60;
            Flush();
        }

        static Phase NewPhase(int index)
        {
            var name = Path.GetFileNameWithoutExtension(enabledScenePaths[index]);
            var phase = new Phase
            {
                buildIndex = index, requestedScene = enabledScenePaths[index], result = "waiting",
                playerRequired = name != "Creditos" && name != "Video1"
            };
            report.phases.Add(phase);
            return phase;
        }

        static PlayerManager LocalPlayer()
        {
            var network = NetworkManager.Singleton;
            if (network != null && network.LocalClient != null && network.LocalClient.PlayerObject != null)
                return network.LocalClient.PlayerObject.GetComponent<PlayerManager>();
            return UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOwner);
        }

        static void Snapshot(Phase phase)
        {
            if (stage == 3)
            {
                phase.observedFrames = Time.frameCount - sceneObservationFrame;
                phase.observedGameSeconds = Time.time - sceneObservationStart;
                phase.observedUnscaledSeconds = Time.unscaledTime - sceneObservationUnscaledStart;
            }
            phase.timeScale = Time.timeScale;
            phase.paused = Mathf.Approximately(Time.timeScale, 0);
            phase.enabledAudioListenerCount = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Count(listener => listener.enabled);
            var scene = SceneManager.GetActiveScene();
            phase.activeScene = scene.path;
            phase.sceneLoaded = scene.isLoaded && scene.buildIndex == phase.buildIndex;
            phase.hostListening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsListening;
            var player = LocalPlayer();
            phase.playerPresent = player != null;
            phase.playerOwner = player != null && player.IsOwner;
            phase.playerSpawned = player != null && player.IsSpawned;
            phase.playerPosition = player != null ? player.transform.position.ToString("F3") : "absent";
            phase.playerCount = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Length;
            var enemies = UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsSortMode.None);
            phase.enemyCount = enemies.Length;
            phase.enemyNames = enemies.Select(e => e.name).OrderBy(n => n).ToArray();
            phase.enemiesSurvivingPreviousScene = enemies.Where(e => previousEnemyIds.Contains(e.GetInstanceID()))
                .Select(e => e.name).OrderBy(n => n).ToArray();
            phase.sceneCheckerCount = UnityEngine.Object.FindObjectsByType<SceneChecker>(FindObjectsSortMode.None).Length;
            phase.spawnPresent = GameObject.FindGameObjectWithTag("Spawn") != null;
            phase.inputPresent = PlayerInputManager.instance != null;
            phase.inputEnabled = phase.inputPresent && PlayerInputManager.instance.enabled;
            phase.cameraPresent = PlayerCamera.instance != null;
            phase.cameraFollow = phase.cameraPresent && PlayerCamera.instance.canFollow;
            phase.hudPresent = PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHudManager != null;
            phase.hudActive = phase.hudPresent && PlayerUIManager.instance.playerUIHudManager.gameObject.activeInHierarchy;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (current == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            if (current.errors.Count < 300) current.errors.Add(new LogEntry { type = type.ToString(), message = message, stack = stack });
        }
        static void Fail(Exception ex)
        {
            current.errors.Add(new LogEntry { type = "HarnessException", message = ex.Message, stack = ex.ToString() });
            try { Snapshot(current); } catch { /* Preserve original failure. */ }
            Finish(false, ex.Message);
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && report != null)
                Finish(false, "Play Mode stopped before completion");
        }
        static void Finish(bool completed, string result)
        {
            if (report == null) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            Application.logMessageReceived -= OnLog;
            report.completed = completed;
            report.sceneLifecyclePassed = completed && report.phases.All(phase => phase.sceneLoaded &&
                phase.hostListening && (!phase.playerRequired || (phase.playerOwner && phase.playerSpawned)) &&
                phase.errors.Count == 0 && (phase.enemiesSurvivingPreviousScene == null || phase.enemiesSurvivingPreviousScene.Length == 0));
            report.result = result;
            if (completed && !report.sceneLifecyclePassed)
                report.result = "All enabled production scenes observed; initialization or scene lifecycle validation failed";
            report.savesAfter = Fingerprints();
            report.saveFilesUnchanged = report.savesBefore.All(f => string.IsNullOrEmpty(f.error)) &&
                                        report.savesAfter.All(f => string.IsNullOrEmpty(f.error)) &&
                                        JsonUtility.ToJson(new FingerprintList { values = report.savesBefore }) ==
                                        JsonUtility.ToJson(new FingerprintList { values = report.savesAfter });
            Time.timeScale = previousTimeScale;
            Cursor.visible = previousCursor;
            Cursor.lockState = previousLock;
            Flush();
            var summary = report.result + "; save files unchanged=" + report.saveFilesUnchanged;
            report = null;
            current = null;
            loading = null;
            Debug.Log("Production smoke test: " + summary + ". Report: " + ReportPath);
        }
        [Serializable] class FingerprintList { public List<Fingerprint> values; }
        static List<Fingerprint> Fingerprints()
        {
            var result = new List<Fingerprint>();
            var path = Application.persistentDataPath;
            if (!Directory.Exists(path)) return result;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                var value = new Fingerprint { path = file };
                try
                {
                    var info = new FileInfo(file);
                    value.bytes = info.Length;
                    value.lastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sha = SHA256.Create()) value.hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                }
                catch (Exception ex) { value.error = ex.Message; }
                result.Add(value);
            }
            return result;
        }
        static void Flush()
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
