using System;
using System.Collections.Generic;
using System.IO;
using Karawara.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Karawara.Core.Characters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Performance
{
    // An explicitly attached functional rehearsal, never a timing capture or host/scene bootstrap.
    [DefaultExecutionOrder(32000)]
    public sealed class PerformanceRouteRehearsal : MonoBehaviour, IDisposable
    {
        public sealed class Checkpoint
        {
            public readonly double elapsed;
            public readonly Vector3 position;
            public readonly Quaternion cameraRotation;
            public readonly float positionTolerance, cameraAngleTolerance;
            public Checkpoint(double elapsed, Vector3 position, Quaternion cameraRotation, float positionTolerance, float cameraAngleTolerance)
            { this.elapsed = elapsed; this.position = position; this.cameraRotation = cameraRotation; this.positionTolerance = positionTolerance; this.cameraAngleTolerance = cameraAngleTolerance; }
        }
        public sealed class Config
        {
            public readonly string routeId, timelineJson, outputRoot;
            public readonly double durationSeconds, maxLatenessSeconds;
            internal readonly PopUpTutorial[] tutorials;
            internal readonly Camera[] minimaps;
            internal readonly Checkpoint[] checkpoints;
            public Config(string routeId, string timelineJson, string outputRoot, double durationSeconds, double maxLatenessSeconds,
                PopUpTutorial[] knownTutorialPopups = null, Camera[] minimapCameras = null, Checkpoint[] checkpoints = null)
            {
                this.routeId = routeId; this.timelineJson = timelineJson; this.outputRoot = outputRoot;
                this.durationSeconds = durationSeconds; this.maxLatenessSeconds = maxLatenessSeconds;
                tutorials = knownTutorialPopups == null ? Array.Empty<PopUpTutorial>() : (PopUpTutorial[])knownTutorialPopups.Clone();
                minimaps = minimapCameras == null ? Array.Empty<Camera>() : (Camera[])minimapCameras.Clone();
                this.checkpoints = checkpoints == null ? Array.Empty<Checkpoint>() : (Checkpoint[])checkpoints.Clone();
            }
        }
        public sealed class Result
        {
            public string cameraMode;
            public double[][] waypointArrivals;
            public readonly bool readyForTiming = false;
            public bool completed, passed, controlsRestored, observedMovement, observedJump, observedCameraMovement, observedFinalRelease;
            public readonly bool populationVerified = false;
            public int suppliedCheckpointCount, observedCheckpointCount;
            public string timelineJson;
            public string[] actionNames;
            public string routeId, runId, utc, endReason, saveRoot, scene, outputPath, exportError;
            public double elapsed;
            public double[][] dispatch;
            public Frame[] frames;
            public ActionEvent[] actionEvents;
            public CameraContext[] cameras;
            public PauseEvent[] pauses;
            public string boundary = "Functional production-owner rehearsal only; no frozen five-route manifests, roster coverage or timing acceptance";
        }
        public struct Frame
        {
            public double elapsed;
            public int frame, sceneHandle, health;
            public int localPlayerId, localCharacterId;
            public float timeScale, moveAmount;
            public bool focused, grounded, jumping, sprinting, attacking, performingAction, dead;
            public bool canMove, canRotate, interacting;
            public CollisionFlags controllerCollisions;
            public Vector3 controllerVelocity;
            public Vector3 playerPosition;
            public Quaternion playerRotation;
            public Vector2 movementAction, cameraAction, movementConsumer, cameraConsumer;
            public CameraPose[] cameras;
        }
        public struct CameraPose { public int instanceId; public Vector3 position; public Quaternion rotation; public float fieldOfView; }
        public struct ActionEvent { public double elapsed, inputEventTime; public int actionIndex, packetIndex, frame; public InputActionPhase phase; }
        public struct PauseEvent { public double elapsed; public int instanceId, frame; public string action; }
        public sealed class CameraContext
        {
            public int instanceId, sceneHandle, targetDisplay, cullingMask;
            public string path, role;
            public float depth;
            public Rect viewport;
            public int targetTextureId;
        }
        public bool IsRunning { get; private set; }
        public Result LastResult { get; private set; }

        // Pure scheduling contract also used by the bounded EditMode fixture.
        public static double[] ValidateSchedule(string timeline, double duration, double maxLateness)
        {
            if (!Finite(duration) || duration <= 0 || duration > 150 || !Finite(maxLateness) || maxLateness <= 0 || maxLateness > .5 || maxLateness >= duration)
                throw new ArgumentException("Bound duration to (0,150] and lateness to (0,min(duration,.5)].");
            if (string.IsNullOrWhiteSpace(timeline) || timeline.Length > 1024 * 1024) throw new ArgumentException("Bounded immutable timeline required.");
            var root = JObject.Parse(timeline, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (root["cameraMode"] != null && (string)root["cameraMode"] != "movement-heading-v1") throw new ArgumentException("Unsupported route camera mode.");
            bool movementHeading = (string)root["cameraMode"] == "movement-heading-v1";
            PerformanceInputReplay.ValidateHeadingTurn(root, movementHeading);
            var frames = root["frames"] as JArray;
            if ((int?)root["schemaVersion"] != 1 || frames == null || frames.Count == 0 || frames.Count > 18000) throw new ArgumentException("Schema1 frames required.");
            var times = new double[frames.Count];
            double previous = -1;
            for (int i = 0; i < times.Length; i++)
            {
                if (!(frames[i] is JObject frame) || frame["timeSeconds"] == null || !(frame["keys"] is JArray keys)) throw new ArgumentException("Explicit timestamp/keys required.");
                times[i] = (double)frame["timeSeconds"];
                if (!Finite(times[i]) || times[i] < 0 || times[i] <= previous || times[i] > duration) throw new ArgumentException("Ordered finite timeline must finish within duration.");
                previous = times[i];
                if (movementHeading && (((double?)frame["mouseDeltaX"] ?? 0) != 0 || ((double?)frame["mouseDeltaY"] ?? 0) != 0)) throw new ArgumentException("Heading camera cannot mix manual look deltas.");
                if (i == times.Length - 1 && (keys.Count != 0 || ((bool?)frame["leftButton"] ?? false) || ((bool?)frame["rightButton"] ?? false) || ((bool?)frame["middleButton"] ?? false) ||
                    ((double?)frame["mouseDeltaX"] ?? 0) != 0 || ((double?)frame["mouseDeltaY"] ?? 0) != 0)) throw new ArgumentException("Explicit release tail required.");
            }
            if (duration - times[times.Length - 1] < maxLateness) throw new ArgumentException("Allow a bounded release observation tail before ending.");
            return times;
        }
        public static int DuePacketCount(double[] times, int next, double elapsed, double maxLateness)
        {
            if (times == null || next < 0 || next > times.Length || !Finite(elapsed) || elapsed < 0 || !Finite(maxLateness) || maxLateness <= 0)
                throw new ArgumentException("Finite clock and valid packet cursor required.");
            if (next == times.Length || times[next] > elapsed) return 0;
            if (elapsed - times[next] > maxLateness) throw new InvalidOperationException("Input packet exceeded bounded dispatch lateness.");
            if (next + 1 < times.Length && times[next + 1] <= elapsed) throw new InvalidOperationException("Multiple packets due: refuse press/release collapse in one Dynamic update.");
            return 1;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static void ValidateWorldReadiness(bool sceneMatchesWorld, bool healthUiReady)
        {
            if (!sceneMatchesWorld || !healthUiReady) throw new InvalidOperationException("Actual world scene and production health UI must be ready before Begin.");
        }
        static void ValidateRoots(string personalRoot, string saves, string output)
        {
            if (!Path.IsPathFullyQualified(personalRoot)) throw new ArgumentException("Absolute personal root required for isolation validation.");
            string personal = Path.GetFullPath(personalRoot);
            var savePolicy = SaveRootPolicy.Resolve(personal, new[] { "-karawaraSaveRoot", saves }, true);
            var outputPolicy = SaveRootPolicy.Resolve(personal, new[] { "-karawaraSaveRoot", output }, true);
            if (!savePolicy.Success || !outputPolicy.Success) throw new ArgumentException("Absolute distinct QA and output roots required.");
            if (Overlap(personal, savePolicy.Value) || Overlap(personal, outputPolicy.Value) || Overlap(savePolicy.Value, outputPolicy.Value))
                throw new ArgumentException("Personal, QA save and output roots must be disjoint; ancestors/descendants are rejected.");
            RejectReparseAncestors(personal); RejectReparseAncestors(savePolicy.Value); RejectReparseAncestors(outputPolicy.Value);
        }
        static bool Overlap(string a, string b)
        {
            a = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            b = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        static void RejectReparseAncestors(string root)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(root));
                while (directory != null)
                {
                    if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new ArgumentException("Existing reparse/junction/symlink ancestors are unsupported for rehearsal isolation.");
                    directory = directory.Parent;
                }
            }
            catch (IOException error) { throw new ArgumentException("Cannot verify root ancestry before rehearsal IO.", error); }
            catch (UnauthorizedAccessException error) { throw new ArgumentException("Cannot verify root ancestry before rehearsal IO.", error); }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        static readonly HashSet<PlayerManager> owners = new HashSet<PlayerManager>();
        Config config;
        PlayerManager player;
        PlayerInputManager input;
        PlayerCamera cameraOwner;
        float cameraYawSpeed;
        WorldSaveGameManager world;
        PlayerControls controls;
        PerformanceInputReplay replay;
        Camera[] boundCameras, cameraScratch;
        CameraContext[] cameraContexts;
        Frame[] samples;
        CameraPose[,] cameraSamples;
        ActionEvent[] events;
        PauseEvent[] pauses;
        InputAction[] actions;
        bool[] originalActionEnabled;
        InputDevice[] originalAssetDevices, originalAllDevices;
        bool originalDeviceRestriction;
        InputBinding? originalMask;
        double[] schedule;
        double start, previousElapsed;
        int sceneHandle, sampleCount, eventCount, pauseCount, next, checkpointIndex;
        string sceneName, saveRoot, saveRootOverride, runId, pendingFailure;
        bool replayFinished, moved, jumped, cameraMoved, finalRelease, ownerRegistered;
        Vector3 initialPosition;
        Quaternion initialCameraRotation;
        const int MaxSamples = 60000, MaxEvents = 240000, MaxPauses = 256;
#endif
        public static PerformanceRouteRehearsal Begin(string[] args, Config config, PlayerManager player, PlayerInputManager input, PlayerCamera cameraOwner)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int flags = 0;
            if (args != null) for (int i = 0; i < args.Length; i++) if (args[i] == "-karawaraPerformanceRehearsal") flags++;
            if (flags == 0) return null;
            if (flags != 1 || config == null || string.IsNullOrWhiteSpace(config.routeId) || config.routeId.Length > 128) throw new ArgumentException("Exactly one explicit rehearsal flag and bounded route ID required.");
            var times = ValidateSchedule(config.timelineJson, config.durationSeconds, config.maxLatenessSeconds);
            var world = WorldSaveGameManager.instance;
            var network = GameBootstrap.Instance;
            ValidateWorldReadiness(world != null && SceneManager.GetActiveScene().buildIndex == world.GetWorldSceneIndex(),
                PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHudManager != null && PlayerUIManager.instance.playerUIHudManager.IsHealthBarReady);
            if (!Application.isPlaying || !Application.isFocused || Time.timeScale != 1 || world == null || network == null || !network.Session.IsActive || !network.Session.IsActive ||
                player == null || !player.IsInitialized || !player.IsInitialized || network.Session == null || network.Session.Player != player || world.player != player ||
                input == null || input != PlayerInputManager.instance || input.player != player || !input.isActiveAndEnabled || input.playerControls == null || !input.playerControls.asset.enabled ||
                player.playerNetworkManager == null || player.characterLocomotionManager == null || player.isDead.Value || player.playerNetworkManager.currentHealth.Value <= 0 ||
                cameraOwner == null || cameraOwner != PlayerCamera.instance || cameraOwner.player != player || !cameraOwner.canFollow || cameraOwner.cameraObject == null || !cameraOwner.cameraObject.isActiveAndEnabled)
                throw new InvalidOperationException("Caller must prepare the actual healthy spawned local host owner, focused production controls and PlayerCamera before Begin.");
            string saveRoot = world.SaveRoot;
            ValidateRoots(Application.persistentDataPath, saveRoot, config.outputRoot);
            var cameras = Camera.allCameras;
            if (cameras.Length == 0 || cameras.Length > 16 || Array.IndexOf(cameras, cameraOwner.cameraObject) < 0) throw new InvalidOperationException("Bound actual active camera contexts required (1..16).");
            foreach (var minimap in config.minimaps) if (minimap == null || minimap.targetTexture == null || Array.IndexOf(cameras, minimap) < 0) throw new ArgumentException("Minimap roles must bind actual active texture cameras.");
            double priorCheckpoint = -1;
            foreach (var checkpoint in config.checkpoints)
            {
                if (checkpoint == null || !Finite(checkpoint.elapsed) || checkpoint.elapsed <= priorCheckpoint || checkpoint.elapsed > config.durationSeconds || checkpoint.elapsed < 0 ||
                    !Finite(checkpoint.positionTolerance) || checkpoint.positionTolerance <= 0 || !Finite(checkpoint.cameraAngleTolerance) || checkpoint.cameraAngleTolerance <= 0 ||
                    !Finite(checkpoint.position.x) || !Finite(checkpoint.position.y) || !Finite(checkpoint.position.z) || !Finite(checkpoint.cameraRotation.x) || !Finite(checkpoint.cameraRotation.y) || !Finite(checkpoint.cameraRotation.z) || !Finite(checkpoint.cameraRotation.w))
                    throw new ArgumentException("Checkpoints require ordered bounded timestamps and finite explicit pose tolerances.");
                priorCheckpoint = checkpoint.elapsed;
            }
            if (!owners.Add(player)) throw new InvalidOperationException("Player already has a rehearsal owner.");
            var owned = new GameObject("Explicit QA Route Rehearsal");
            PerformanceRouteRehearsal session = null;
            try
            {
                session = owned.AddComponent<PerformanceRouteRehearsal>();
                session.config = config; session.player = player; session.ownerRegistered = true; session.input = input; session.cameraOwner = cameraOwner; session.world = world; session.controls = input.playerControls;
                session.schedule = times; session.sceneHandle = SceneManager.GetActiveScene().handle; session.sceneName = SceneManager.GetActiveScene().name;
                session.saveRoot = saveRoot; session.saveRootOverride = world.SaveRootOverride; session.runId = Guid.NewGuid().ToString("N"); session.boundCameras = cameras; session.cameraScratch = new Camera[cameras.Length];
                session.samples = new Frame[MaxSamples]; session.cameraSamples = new CameraPose[MaxSamples, cameras.Length]; session.events = new ActionEvent[MaxEvents]; session.pauses = new PauseEvent[MaxPauses];
                session.cameraContexts = new CameraContext[cameras.Length];
                for (int i = 0; i < cameras.Length; i++)
                {
                    var camera = cameras[i];
                    session.cameraContexts[i] = new CameraContext { instanceId = camera.GetInstanceID(), sceneHandle = camera.gameObject.scene.handle, path = TransformPath(camera.transform),
                        role = camera == cameraOwner.cameraObject ? "production-player-camera" : Array.IndexOf(config.minimaps, camera) >= 0 ? "caller-bound-minimap-texture" : camera.targetTexture == null ? "active-display-output" : "active-texture-context-unclassified",
                        depth = camera.depth, targetDisplay = camera.targetDisplay, viewport = camera.rect, cullingMask = camera.cullingMask, targetTextureId = camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0 };
                }
                var actionList = new List<InputAction>(); foreach (var action in session.controls.asset) actionList.Add(action);
                session.actions = actionList.ToArray(); session.originalActionEnabled = new bool[session.actions.Length];
                for (int i = 0; i < session.actions.Length; i++) session.originalActionEnabled[i] = session.actions[i].enabled;
                var devices = session.controls.asset.devices; session.originalDeviceRestriction = devices.HasValue;
                if (devices.HasValue) { session.originalAssetDevices = new InputDevice[devices.Value.Count]; for (int i = 0; i < devices.Value.Count; i++) session.originalAssetDevices[i] = devices.Value[i]; }
                session.originalAllDevices = new InputDevice[InputSystem.devices.Count]; for (int i = 0; i < session.originalAllDevices.Length; i++) session.originalAllDevices[i] = InputSystem.devices[i];
                session.originalMask = session.controls.asset.bindingMask;
                // Replay owns its virtual devices; this owner never overrides bindings or real device state.
                session.replay = PerformanceInputReplay.Create(new[] { "-karawaraPerformanceReplay" }, session.controls, config.timelineJson);
                if (session.replay.CameraMode == "movement-heading-v1")
                    session.cameraYawSpeed = (float)typeof(PlayerCamera).GetField("leftAndRightRotationSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cameraOwner);
                session.initialPosition = player.transform.position; session.initialCameraRotation = cameraOwner.cameraObject.transform.rotation;
                session.start = Time.realtimeSinceStartupAsDouble; session.IsRunning = true;
                foreach (var action in session.actions) { action.performed += session.ObserveAction; action.canceled += session.ObserveAction; }
                InputSystem.onBeforeUpdate += session.BeforeInput;
                return session;
            }
            catch
            {
                if (session != null) session.Dispose();
                owners.Remove(player); Destroy(owned); throw;
            }
#else
            return null;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        static string TransformPath(Transform transform) { string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }
        double Clock()
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            if (!Finite(elapsed) || elapsed < previousElapsed) throw new InvalidOperationException("Owned monotonic clock diverged.");
            previousElapsed = elapsed; return elapsed;
        }
        void Guard()
        {
            var network = GameBootstrap.Instance;
            if (!Application.isFocused) throw new InvalidOperationException("Focus lost.");
            // SaveRoot resolution allocates: resolve once before Begin, then monitor its mutable override.
            if (world == null || world != WorldSaveGameManager.instance || world.player != player || !string.Equals(world.SaveRootOverride, saveRootOverride, StringComparison.Ordinal) ||
                player == null || !player.IsInitialized || !player.IsInitialized || network == null || !network.Session.IsActive || !network.Session.IsActive || network.Session == null || network.Session.Player != player ||
                input == null || input != PlayerInputManager.instance || input.player != player || input.playerControls != controls || !input.isActiveAndEnabled || !controls.asset.enabled ||
                cameraOwner == null || cameraOwner != PlayerCamera.instance || cameraOwner.player != player || !cameraOwner.canFollow || cameraOwner.cameraObject == null ||
                SceneManager.GetActiveScene().handle != sceneHandle) throw new InvalidOperationException("Production owner, scene, controls, camera or QA root changed.");
            if (player.isDead.Value || player.playerNetworkManager.currentHealth.Value <= 0) throw new InvalidOperationException("Player died.");
            if (Camera.allCamerasCount != boundCameras.Length || Camera.GetAllCameras(cameraScratch) != boundCameras.Length) throw new InvalidOperationException("Active camera set changed.");
            for (int i = 0; i < boundCameras.Length; i++)
            {
                var camera = boundCameras[i]; var expected = cameraContexts[i];
                if (camera == null || !camera.isActiveAndEnabled || Array.IndexOf(cameraScratch, camera) < 0 || camera.depth != expected.depth || camera.targetDisplay != expected.targetDisplay ||
                    camera.rect != expected.viewport || camera.cullingMask != expected.cullingMask || (camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0) != expected.targetTextureId)
                    throw new InvalidOperationException("Bound active output/texture camera context diverged.");
            }
            if (Array.IndexOf(boundCameras, cameraOwner.cameraObject) < 0) throw new InvalidOperationException("Production camera binding changed.");
        }
        void BeforeInput()
        {
            if (!IsRunning || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            try
            {
                Guard(); double elapsed = Clock();
                if (elapsed > config.durationSeconds + config.maxLatenessSeconds) throw new InvalidOperationException("Owned rehearsal clock timeout before orderly End.");
                // A triggered tutorial is handled through its authored close in LateUpdate, outside timing.
                if (Time.timeScale == 0) return;
                if (Time.timeScale != 1) throw new InvalidOperationException("Unknown timeScale transition.");
                int due = DuePacketCount(schedule, next, elapsed, config.maxLatenessSeconds);
                replayFinished = replay.AdvanceWithWorldTargets(elapsed, cameraOwner.transform.eulerAngles.y, cameraYawSpeed, player.transform.position); next += due;
            }
            catch (Exception error) { EndWithReason(error.Message); }
        }
        void ObserveAction(InputAction.CallbackContext context)
        {
            if (!IsRunning) return;
            if (eventCount == events.Length) { pendingFailure = "Action telemetry buffer exhausted."; return; }
            events[eventCount++] = new ActionEvent { elapsed = Time.realtimeSinceStartupAsDouble - start, inputEventTime = context.time, actionIndex = Array.IndexOf(actions, context.action),
                packetIndex = next - 1, frame = Time.frameCount, phase = context.phase };
        }
        void CloseKnownTutorial(double elapsed)
        {
            var active = UnityEngine.Object.FindObjectsByType<PopUpTutorial>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            PopUpTutorial triggered = null; int count = 0;
            foreach (var popup in active)
            {
                if (popup.passou || popup.UI == null || !popup.UI.activeInHierarchy) continue;
                var collider = popup.GetComponent<BoxCollider>();
                if (collider == null || collider.enabled) continue;
                count++; triggered = popup;
            }
            if (count != 1 || Array.IndexOf(config.tutorials, triggered) < 0 || pauseCount == pauses.Length)
                throw new InvalidOperationException("Unknown/ambiguous authored pause; explicit single tutorial binding required.");
            pauses[pauseCount++] = new PauseEvent { elapsed = elapsed, instanceId = triggered.GetInstanceID(), frame = Time.frameCount, action = "Observed triggered tutorial; invoked authored close outside timing" };
            triggered.close();
            if (Time.timeScale != 1 || triggered.UI.activeInHierarchy || !triggered.passou) throw new InvalidOperationException("Authored tutorial close failed to restore gameplay.");
        }
        void LateUpdate()
        {
            if (!IsRunning) return;
            try
            {
                Guard(); double elapsed = Clock();
                if (pendingFailure != null) throw new InvalidOperationException(pendingFailure);
                if (elapsed > config.durationSeconds + config.maxLatenessSeconds) throw new InvalidOperationException("Owned observation clock timeout.");
                if (Time.timeScale == 0) CloseKnownTutorial(elapsed);
                if (Time.timeScale != 1) throw new InvalidOperationException("Unplanned pause/timeScale.");
                if (sampleCount == samples.Length) throw new InvalidOperationException("Observation buffer exhausted.");
                var movement = controls.PlayerMovement.Movement.ReadValue<Vector2>(); var cameraInput = controls.PlayerCamera.Movement.ReadValue<Vector2>();
                var net = player.playerNetworkManager;
                samples[sampleCount] = new Frame { elapsed = elapsed, frame = Time.frameCount, sceneHandle = sceneHandle, localPlayerId = player.LocalId, localCharacterId = player.LocalId,
                    health = net.currentHealth.Value, timeScale = Time.timeScale, focused = Application.isFocused, playerPosition = player.transform.position, playerRotation = player.transform.rotation,
                    movementAction = movement, cameraAction = cameraInput, movementConsumer = new Vector2(input.horizontal_Input, input.vertical_Input), cameraConsumer = new Vector2(input.cameraHorizontal_Input, input.cameraVertical_Input),
                    moveAmount = input.moveAmount, grounded = player.characterLocomotionManager.isGrounded, jumping = net.isJumping.Value, sprinting = net.isSprinting.Value, attacking = net.isAttacking.Value,
                    performingAction = player.isPerformingAction, dead = player.isDead.Value,
                    canMove = player.characterLocomotionManager.canMove, canRotate = player.characterLocomotionManager.canRotate,
                    interacting = player.playerLocomotionManager != null && player.playerLocomotionManager.canInteract,
                    controllerCollisions = player.characterController.collisionFlags, controllerVelocity = player.characterController.velocity };
                for (int i = 0; i < boundCameras.Length; i++) { var camera = boundCameras[i]; cameraSamples[sampleCount, i] = new CameraPose { instanceId = camera.GetInstanceID(), position = camera.transform.position, rotation = camera.transform.rotation, fieldOfView = camera.fieldOfView }; }
                sampleCount++;
                moved |= Vector2.Distance(new Vector2(initialPosition.x, initialPosition.z), new Vector2(player.transform.position.x, player.transform.position.z)) > .05f; jumped |= net.isJumping.Value;
                cameraMoved |= Quaternion.Angle(initialCameraRotation, cameraOwner.cameraObject.transform.rotation) > .1f;
                finalRelease = replayFinished && movement == Vector2.zero && cameraInput == Vector2.zero && input.moveAmount == 0 && input.horizontal_Input == 0 && input.vertical_Input == 0 && input.cameraHorizontal_Input == 0 && input.cameraVertical_Input == 0;
                if (finalRelease) for (int i = 0; i < actions.Length; i++) if (actions[i].type == InputActionType.Button && actions[i].IsPressed()) { finalRelease = false; break; }
                while (checkpointIndex < config.checkpoints.Length && elapsed >= config.checkpoints[checkpointIndex].elapsed)
                {
                    var checkpoint = config.checkpoints[checkpointIndex++];
                    if (elapsed - checkpoint.elapsed > config.maxLatenessSeconds || Vector3.Distance(player.transform.position, checkpoint.position) > checkpoint.positionTolerance ||
                        Quaternion.Angle(cameraOwner.cameraObject.transform.rotation, checkpoint.cameraRotation) > checkpoint.cameraAngleTolerance) throw new InvalidOperationException("Supplied rehearsal checkpoint diverged.");
                }
                if (elapsed >= config.durationSeconds) EndWithReason(replayFinished && finalRelease && checkpointIndex == config.checkpoints.Length ? "completed" : "Timeline, release or checkpoint observation incomplete.");
            }
            catch (Exception error) { EndWithReason(error.Message); }
        }
        bool Restored()
        {
            var devices = controls.asset.devices;
            if (devices.HasValue != originalDeviceRestriction || !controls.asset.bindingMask.Equals(originalMask) || InputSystem.devices.Count != originalAllDevices.Length) return false;
            if (devices.HasValue) { if (devices.Value.Count != originalAssetDevices.Length) return false; for (int i = 0; i < originalAssetDevices.Length; i++) if (devices.Value[i] != originalAssetDevices[i]) return false; }
            for (int i = 0; i < originalAllDevices.Length; i++) if (InputSystem.devices[i] != originalAllDevices[i]) return false;
            for (int i = 0; i < actions.Length; i++) if (actions[i].enabled != originalActionEnabled[i]) return false;
            return true;
        }
        sealed class PoseContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
            {
                var properties = base.CreateProperties(type, serialization);
                string[] components = type == typeof(Vector2) ? new[] { "x", "y" } :
                    type == typeof(Vector3) ? new[] { "x", "y", "z" } :
                    type == typeof(Quaternion) ? new[] { "x", "y", "z", "w" } :
                    type == typeof(Rect) ? new[] { "x", "y", "width", "height" } : null;
                if (components == null) return properties;
                var result = new List<JsonProperty>();
                foreach (var property in properties) if (Array.IndexOf(components, property.PropertyName) >= 0) result.Add(property);
                return result;
            }
        }
        static string SerializeResult(Result result) => JsonConvert.SerializeObject(result, Formatting.Indented,
            new JsonSerializerSettings { ContractResolver = new PoseContractResolver() });
        Result EndWithReason(string reason)
        {
            if (!IsRunning) return LastResult;
            IsRunning = false;
            InputSystem.onBeforeUpdate -= BeforeInput;
            foreach (var action in actions) { action.performed -= ObserveAction; action.canceled -= ObserveAction; }
            var ownedReplay = replay; replay = null;
            string cleanupFailure = null;
            try { ownedReplay?.Dispose(); } catch (Exception error) { cleanupFailure = error.ToString(); }
            if (ownerRegistered) { owners.Remove(player); ownerRegistered = false; }
            // Only after callbacks/device ownership end do we materialize report DTOs or touch the filesystem.
            var result = new Result { routeId = config.routeId, runId = runId, utc = DateTime.UtcNow.ToString("O"), endReason = cleanupFailure ?? reason, saveRoot = saveRoot, scene = sceneName,
                elapsed = Time.realtimeSinceStartupAsDouble - start, completed = reason == "completed", controlsRestored = cleanupFailure == null && Restored(),
                observedMovement = moved, observedJump = jumped, observedCameraMovement = cameraMoved, observedFinalRelease = finalRelease, cameras = cameraContexts,
                suppliedCheckpointCount = config.checkpoints.Length, observedCheckpointCount = checkpointIndex, timelineJson = config.timelineJson, cameraMode = ownedReplay?.CameraMode, waypointArrivals = ownedReplay?.WaypointArrivals(), actionNames = new string[actions.Length],
                dispatch = ownedReplay?.DispatchTimeline(), frames = new Frame[sampleCount], actionEvents = new ActionEvent[eventCount], pauses = new PauseEvent[pauseCount] };
            result.passed = result.completed && result.controlsRestored;
            for (int i = 0; i < actions.Length; i++) result.actionNames[i] = actions[i].actionMap.name + "/" + actions[i].name;
            Array.Copy(events, result.actionEvents, eventCount); Array.Copy(pauses, result.pauses, pauseCount);
            for (int frame = 0; frame < sampleCount; frame++) { result.frames[frame] = samples[frame]; result.frames[frame].cameras = new CameraPose[boundCameras.Length]; for (int c = 0; c < boundCameras.Length; c++) result.frames[frame].cameras[c] = cameraSamples[frame, c]; }
            try
            {
                // Recheck the immutable paths immediately before export; another process may have changed ancestry.
                ValidateRoots(Application.persistentDataPath, saveRoot, config.outputRoot);
                Directory.CreateDirectory(config.outputRoot);
                result.outputPath = Path.Combine(config.outputRoot, "route-rehearsal-" + runId + ".json");
                string json = SerializeResult(result);
                using (var stream = new FileStream(result.outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) using (var writer = new StreamWriter(stream)) writer.Write(json);
            }
            catch (Exception error) { result.passed = false; result.exportError = error.ToString(); }
            samples = null; cameraSamples = null; events = null; pauses = null; cameraScratch = null;
            LastResult = result;
            return result;
        }
#endif
        public Result End()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            return EndWithReason("Caller ended rehearsal before owned completion.");
#else
            return null;
#endif
        }
        public void Dispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (IsRunning) { End(); return; }
            // Begin can fail after replay ownership is acquired but before the running flag/clock.
            // Such a partial scope has no report, but must release its actual devices and callbacks.
            InputSystem.onBeforeUpdate -= BeforeInput;
            if (actions != null) foreach (var action in actions) { action.performed -= ObserveAction; action.canceled -= ObserveAction; }
            var ownedReplay = replay; replay = null;
            try { ownedReplay?.Dispose(); }
            finally
            {
                if (ownerRegistered) { owners.Remove(player); ownerRegistered = false; }
                samples = null; cameraSamples = null; events = null; pauses = null; cameraScratch = null;
            }
#endif
        }
        void OnDisable() { Dispose(); }
        void OnDestroy() { Dispose(); }
        void OnApplicationQuit() { Dispose(); }
    }
}
