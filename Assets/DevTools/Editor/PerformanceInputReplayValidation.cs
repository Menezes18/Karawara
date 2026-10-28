using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceInputReplayValidation
    {
        [Serializable] sealed class Case { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public string utc;
            public bool completed, passed, cleanedUp;
            public List<Case> cases = new List<Case>();
            public List<string> limits = new List<string>();
        }
        [MenuItem("Karawara/Validation/TOBE/Validate Performance Input Replay")]
        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            var baseline = InputSystem.devices.ToArray();
            var enabled = baseline.Select(d => d.enabled).ToArray();
            Test(report, "No explicit replay flag creates no virtual devices or changes controls", () =>
            {
                Require(!Application.isPlaying, "This bounded fixture requires EditMode and never drives live production controls.");
                using (var fixture = new FixtureControls())
                {
                    var controls = fixture.controls;
                    var before = controls.asset.devices;
                    var mask = controls.asset.bindingMask;
                    var session = Create(Array.Empty<string>(), controls, Timeline());
                    Require(session == null && InputSystem.devices.Count == baseline.Length && SameDevices(controls.asset.devices, before) &&
                        controls.asset.bindingMask.Equals(mask) && !controls.asset.enabled, "No activation is a no-op.");
                }
            });
            foreach (string invalid in new[] { "duplicate-flag", "backward-time", "nonfinite-time", "unknown-key", "ui-key" })
                Test(report, "Reject invalid replay before device creation: " + invalid, () =>
                {
                    Harness();
                    using (var fixture = new FixtureControls())
                    {
                        var controls = fixture.controls;
                        string json = Timeline();
                        string[] args = new[] { "-karawaraPerformanceReplay" };
                        if (invalid == "duplicate-flag") args = new[] { "-karawaraPerformanceReplay", "-karawaraPerformanceReplay" };
                        if (invalid == "backward-time") json = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":1,\"keys\":[]},{\"timeSeconds\":0,\"keys\":[]}]}";
                        if (invalid == "nonfinite-time") json = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":\"NaN\",\"keys\":[]}]}";
                        if (invalid == "unknown-key") json = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"Invented\"]}]}";
                        if (invalid == "ui-key") json = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"Escape\"]}]}";
                        bool rejected = false;
                        object session = null;
                        try { session = Create(args, controls, json); }
                        catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
                        finally { (session as IDisposable)?.Dispose(); }
                        Require(rejected && InputSystem.devices.Count == baseline.Length && !controls.asset.enabled, "Invalid immutable timeline/activation must fail before adding devices or enabling actions.");
                    }
                });
            Test(report, "Actual existing movement camera and button callbacks plus releases", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    var controls = fixture.controls;
                    Vector2 movement = default, camera = default;
                    int dodge = 0, jump = 0, attack = 0, lockOn = 0, attackCanceled = 0;
                    controls.PlayerMovement.Movement.performed += c => movement = c.ReadValue<Vector2>();
                    controls.PlayerCamera.Movement.performed += c => camera = c.ReadValue<Vector2>();
                    controls.PlayerActions.Dodge.performed += c => dodge++;
                    controls.PlayerActions.Jump.performed += c => jump++;
                    controls.PlayerActions.RB.performed += c => attack++;
                    controls.PlayerActions.RB.canceled += c => attackCanceled++;
                    controls.PlayerActions.LockOn.performed += c => lockOn++;
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, controls, Timeline());
                        Require(session is IDisposable && InputSystem.devices.Count == baseline.Length + 2, "Own exactly one virtual Keyboard and one virtual Mouse.");
                        Advance(session, 0);
                        UpdateInput();
                        Require(movement.y > .9f && camera.x > 0 && dodge == 1 && jump == 1 && attack == 1 && lockOn == 1, "Queued virtual device state must trigger the generated existing action bindings.");
                        Advance(session, .1);
                        UpdateInput();
                        Require(movement == Vector2.zero && attackCanceled == 1, "Release events must deliver zero movement through performed callback and cancel the attack.");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            Test(report, "NPC dialogue Enter reaches actual current Keyboard and releases", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    var originalKeyboard = Keyboard.current;
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls,
                            "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"Enter\"]},{\"timeSeconds\":0.1,\"keys\":[]}]}");
                        Advance(session, 0); UpdateInput();
                        Require(Keyboard.current != originalKeyboard && Keyboard.current.enterKey.isPressed && Keyboard.current.enterKey.wasPressedThisFrame,
                            "NPCDialogue reads Keyboard.current.enterKey directly; the owned keyboard must receive a real press.");
                        Advance(session, .1); UpdateInput();
                        Require(!Keyboard.current.enterKey.isPressed && Keyboard.current.enterKey.wasReleasedThisFrame, "Actual Enter release must be visible.");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                    Require(Keyboard.current == originalKeyboard, "Original current keyboard must be restored.");
                }
            });
            Test(report, "Positioned UI mouse press and release reach the owned current Mouse", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    var originalMouse = Mouse.current;
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls,
                            "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[],\"mousePositionX\":643,\"mousePositionY\":207,\"leftButton\":true},{\"timeSeconds\":0.1,\"keys\":[],\"mousePositionX\":643,\"mousePositionY\":207}]}");
                        Advance(session, 0); UpdateInput();
                        Require(Mouse.current != originalMouse && Mouse.current.position.ReadValue() == new Vector2(643, 207) && Mouse.current.leftButton.wasPressedThisFrame,
                            "A real positioned pointer press must reach the owned device for UI raycasting.");
                        Advance(session, .1); UpdateInput();
                        Require(Mouse.current.position.ReadValue() == new Vector2(643, 207) && !Mouse.current.leftButton.isPressed && Mouse.current.leftButton.wasReleasedThisFrame,
                            "Pointer release must retain the requested button position.");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                    Require(Mouse.current == originalMouse, "Original current mouse must be restored.");
                }
            });
            foreach (string position in new[] { "\"mousePositionX\":1", "\"mousePositionY\":1", "\"mousePositionX\":\"NaN\",\"mousePositionY\":2", "\"mousePositionX\":1,\"mousePositionY\":\"Infinity\"" })
                Test(report, "Reject malformed pointer pair before device creation: " + position, () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        object session = null; bool rejected = false;
                        try { session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls,
                            "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]," + position + "},{\"timeSeconds\":0.1,\"keys\":[]}]}"); }
                        catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
                        finally { (session as IDisposable)?.Dispose(); }
                        Require(rejected && InputSystem.devices.Count == baseline.Length && !fixture.controls.asset.enabled,
                            "Partial/nonfinite pointer coordinates must fail before acquiring devices or controls.");
                    }
                });
            Test(report, "Actual sprint Hold and release callbacks", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    var controls = fixture.controls;
                    int sprint = 0, released = 0;
                    controls.PlayerActions.Sprint.performed += c => sprint++;
                    controls.PlayerActions.Sprint.canceled += c => released++;
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, controls,
                            "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"LeftShift\"]},{\"timeSeconds\":0.3,\"keys\":[]}]}");
                        Advance(session, 0); UpdateInput();
                        Thread.Sleep(250); UpdateInput();
                        Require(sprint > 0, "Existing Hold(duration=0.2) must produce an actual performed callback.");
                        Advance(session, .3); UpdateInput();
                        Require(released > 0, "Actual release cancels existing Sprint interaction.");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            Test(report, "Dispose restores controls masks devices and enabled states without disabling real devices", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    var controls = fixture.controls;
                    controls.asset.devices = baseline;
                    controls.asset.bindingMask = new InputBinding { action = "Movement" };
                    controls.PlayerMovement.Enable();
                    string bindings = controls.asset.ToJson();
                    var originalDevices = SnapshotDevices(controls.asset.devices);
                    var originalMask = controls.asset.bindingMask;
                    bool[] originalEnabled = controls.asset.Select(a => a.enabled).ToArray();
                    object session = null;
                    try { session = Create(new[] { "-karawaraPerformanceReplay" }, controls, Timeline()); }
                    finally { (session as IDisposable)?.Dispose(); (session as IDisposable)?.Dispose(); }
                    Require(InputSystem.devices.Count == baseline.Length && SameDevices(controls.asset.devices, originalDevices) &&
                        controls.asset.bindingMask.Equals(originalMask) && controls.asset.Select(a => a.enabled).SequenceEqual(originalEnabled),
                        "Dispose is idempotent and restores exact supplied controls state.");
                    Require(controls.asset.ToJson() == bindings, "No binding override or generated asset mutation is retained.");
                    for (int i = 0; i < baseline.Length; i++) Require(baseline[i].added && baseline[i].enabled == enabled[i], "Real device state was not changed.");
                    controls.Disable();
                }
            });
            Test(report, "Backward clock fails closed and removes both owned devices", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    var controls = fixture.controls;
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, controls, Timeline());
                        Advance(session, .05);
                        bool rejected = false;
                        try { Advance(session, .01); }
                        catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                        Require(rejected && InputSystem.devices.Count == baseline.Length && !controls.asset.enabled, "A non-monotonic scheduler must terminate and restore the session.");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            foreach (bool disable in new[] { false, true })
                Test(report, "Production consumer clears movement and camera on " + (disable ? "controls disable" : "neutral release"), () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        var originalInstance = PlayerInputManager.instance;
                        var owned = new GameObject("Owned inactive production input consumer");
                        owned.SetActive(false);
                        var consumer = owned.AddComponent<PlayerInputManager>();
                        object session = null;
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        try
                        {
                            typeof(PlayerInputManager).GetMethod("OnEnable", flags).Invoke(consumer, null);
                            Require(PlayerInputManager.instance == originalInstance, "Inactive owned consumer must not replace the live singleton.");
                            session = Create(new[] { "-karawaraPerformanceReplay" }, consumer.playerControls, Timeline());
                            Advance(session, 0); UpdateInput();
                            var movement = typeof(PlayerInputManager).GetField("movementInput", flags);
                            var camera = typeof(PlayerInputManager).GetField("camera_Input", flags);
                            Require((Vector2)movement.GetValue(consumer) != Vector2.zero && (Vector2)camera.GetValue(consumer) != Vector2.zero,
                                "Real generated action events must first populate actual consumer fields.");
                            if (disable) consumer.playerControls.Disable();
                            else { Advance(session, .1); UpdateInput(); }
                            typeof(PlayerInputManager).GetMethod("HandlePlayerMovementInput", flags).Invoke(consumer, null);
                            typeof(PlayerInputManager).GetMethod("HandleCameraMovementInput", flags).Invoke(consumer, null);
                            Require((Vector2)movement.GetValue(consumer) == Vector2.zero && (Vector2)camera.GetValue(consumer) == Vector2.zero &&
                                consumer.moveAmount == 0 && consumer.cameraHorizontal_Input == 0 && consumer.cameraVertical_Input == 0,
                                "Canceled actions left stale movement/camera values in the actual production consumer.");
                        }
                        finally
                        {
                            (session as IDisposable)?.Dispose();
                            typeof(PlayerInputManager).GetMethod("OnDestroy", flags).Invoke(consumer, null);
                            UnityEngine.Object.DestroyImmediate(owned);
                        }
                    }
                });
            foreach (string scenario in new[] { "unknown-mode", "mixed-manual-look" })
                Test(report, "Heading camera rejects ambiguous timeline: " + scenario, () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        string json = "{\"schemaVersion\":1,\"cameraMode\":\"" + (scenario == "unknown-mode" ? "invented" : "movement-heading-v1") +
                            "\",\"frames\":[{\"timeSeconds\":0,\"keys\":[\"D\"],\"mouseDeltaX\":1},{\"timeSeconds\":1,\"keys\":[]}]}";
                        object session = null; bool rejected = false;
                        try { session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, json); }
                        catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                        finally { (session as IDisposable)?.Dispose(); }
                        Require(rejected && InputSystem.devices.Count == baseline.Length, "Invalid camera mode created owned devices");
                    }
                });
            foreach (float yaw in new[] { 0f, 90f, 180f, -90f })
                Test(report, "Actual heading camera remaps world-east movement at yaw " + yaw, () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        object session = null;
                        try
                        {
                            session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls,
                                "{\"schemaVersion\":1,\"cameraMode\":\"movement-heading-v1\",\"frames\":[{\"timeSeconds\":0,\"keys\":[\"D\"]},{\"timeSeconds\":1,\"keys\":[]}]}" );
                            var advance = Harness().GetMethod("AdvanceWithHeading", BindingFlags.Public | BindingFlags.Instance);
                            Require(advance != null, "Actual heading replay entry missing");
                            advance.Invoke(session, new object[] { 0.0, yaw, 220f }); UpdateInput();
                            var movement = fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>();
                            var world = Quaternion.Euler(0, yaw, 0) * new Vector3(movement.x, 0, movement.y);
                            Require(Vector3.Dot(world.normalized, Vector3.right) > .999f, "Camera rotation changed the scripted world movement direction");
                            var camera = fixture.controls.PlayerCamera.Movement.ReadValue<Vector2>();
                            if (yaw == 0) Require(camera.x > 0 && camera.x * 220 <= 90.01f, "Normal mouse action did not turn toward motion at bounded speed");
                            if (yaw == 90) Require(camera == Vector2.zero, "Aligned camera should not drift");
                            advance.Invoke(session, new object[] { 1.0, yaw, 220f }); UpdateInput();
                            Require(fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>() == Vector2.zero && fixture.controls.PlayerCamera.Movement.ReadValue<Vector2>() == Vector2.zero, "Heading replay did not release movement/look");
                        }
                        finally { (session as IDisposable)?.Dispose(); }
                    }
                    Require(InputSystem.devices.Count == baseline.Length, "Heading replay leaked virtual input devices");
                });
            Test(report, "Heading replay rejects nonfinite camera samples and releases ownership", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls,
                            "{\"schemaVersion\":1,\"cameraMode\":\"movement-heading-v1\",\"frames\":[{\"timeSeconds\":0,\"keys\":[\"W\"]},{\"timeSeconds\":1,\"keys\":[]}]}" );
                        var advance = Harness().GetMethod("AdvanceWithHeading", BindingFlags.Public | BindingFlags.Instance);
                        Require(advance != null, "Heading replay validation entry missing"); bool rejected = false;
                        try { advance.Invoke(session, new object[] { 0.0, float.NaN, 220f }); }
                        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                        Require(rejected && InputSystem.devices.Count == baseline.Length, "Invalid heading sample retained replay ownership");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            foreach (string scenario in new[] { "missing-mode", "unknown-group", "mixed-movement", "invalid-radius", "nonfinite-point", "final-group" })
                Test(report, "World waypoint discovery rejects invalid ownership: " + scenario, () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        var root = Newtonsoft.Json.Linq.JObject.Parse(WaypointTimeline());
                        if (scenario == "missing-mode") root.Remove("cameraMode");
                        if (scenario == "unknown-group") root["frames"][0]["waypointGroup"] = "missing";
                        if (scenario == "mixed-movement") root["frames"][0]["keys"] = new Newtonsoft.Json.Linq.JArray("W");
                        if (scenario == "invalid-radius") root["waypointGroups"]["path"]["arrivalRadius"] = 0;
                        if (scenario == "nonfinite-point") root["waypointGroups"]["path"]["points"][0][0] = "NaN";
                        if (scenario == "final-group") root["frames"][1]["waypointGroup"] = "path";
                        object session = null; bool rejected = false;
                        try { session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, root.ToString()); }
                        catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                        finally { (session as IDisposable)?.Dispose(); }
                        Require(rejected && InputSystem.devices.Count == baseline.Length, "Malformed waypoint plan acquired input ownership");
                    }
                });
            Test(report, "Waypoint feedback follows actual position through normal movement and releases at goal", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, WaypointTimeline());
                        var advance = Harness().GetMethod("AdvanceWithWorldTargets");
                        Require(advance != null, "World feedback entry missing");
                        advance.Invoke(session, new object[] { 0.0, 0f, 220f, Vector3.zero }); UpdateInput();
                        Require(fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>().x > .9f, "First target must drive east via existing movement binding");
                        advance.Invoke(session, new object[] { .1, 90f, 220f, new Vector3(5, 0, 0) }); UpdateInput();
                        Require(fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>().x < -.9f, "Second target must drive north relative to east-facing camera");
                        advance.Invoke(session, new object[] { .2, 0f, 220f, new Vector3(5, 0, 5) }); UpdateInput();
                        Require(fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>() == Vector2.zero && fixture.controls.PlayerCamera.Movement.ReadValue<Vector2>() == Vector2.zero, "Completed group must release movement/look");
                        advance.Invoke(session, new object[] { 1.0, 0f, 220f, new Vector3(5, 0, 5) }); UpdateInput();
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            Test(report, "Completed waypoint group settles camera along final segment without moving", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, WaypointTimeline());
                        var advance = Harness().GetMethod("AdvanceWithWorldTargets");
                        advance.Invoke(session, new object[] { 0d, 0f, 220f, Vector3.zero }); UpdateInput();
                        advance.Invoke(session, new object[] { .1, 90f, 220f, new Vector3(5, 0, 0) }); UpdateInput();
                        advance.Invoke(session, new object[] { .2, 90f, 220f, new Vector3(5, 0, 5) }); UpdateInput();
                        Require(fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>() == Vector2.zero && fixture.controls.PlayerCamera.Movement.ReadValue<Vector2>().x < 0, "Follower stopped mid-turn or moved after arrival");
                        advance.Invoke(session, new object[] { 1d, 90f, 220f, new Vector3(5, 0, 5) }); UpdateInput();
                        Require(fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>() == Vector2.zero && fixture.controls.PlayerCamera.Movement.ReadValue<Vector2>() == Vector2.zero, "Explicit final release retained follower input");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            foreach (bool invalidPosition in new[] { false, true })
                Test(report, "Waypoint feedback fails closed on " + (invalidPosition ? "missing actual position" : "unfinished deadline"), () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        object session = null;
                        try
                        {
                            session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, WaypointTimeline());
                            var advance = Harness().GetMethod("AdvanceWithWorldTargets"); Require(advance != null, "World feedback entry missing");
                            advance.Invoke(session, new object[] { 0.0, 0f, 220f, Vector3.zero });
                            bool rejected = false;
                            try
                            {
                                if (invalidPosition) Harness().GetMethod("AdvanceWithHeading").Invoke(session, new object[] { .1, 0f, 220f });
                                else advance.Invoke(session, new object[] { 1.0, 0f, 220f, Vector3.zero });
                            }
                            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                            Require(rejected && InputSystem.devices.Count == baseline.Length, "Invalid feedback did not release input ownership");
                        }
                        finally { (session as IDisposable)?.Dispose(); }
                    }
                });
            Test(report, "Waypoint arrival on the actual release dispatch satisfies the outgoing group", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    object session = null;
                    try
                    {
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, WaypointTimeline());
                        var advance = Harness().GetMethod("AdvanceWithWorldTargets");
                        advance.Invoke(session, new object[] { 0.0, 0f, 220f, Vector3.zero });
                        advance.Invoke(session, new object[] { .5, 0f, 220f, new Vector3(5, 0, 0) });
                        advance.Invoke(session, new object[] { 1.0, 0f, 220f, new Vector3(5, 0, 5) }); UpdateInput();
                        var arrivals = (double[][])Harness().GetMethod("WaypointArrivals").Invoke(session, null);
                        Require(arrivals.Length == 2 && arrivals[1][2] == 1 && fixture.controls.PlayerMovement.Movement.ReadValue<Vector2>() == Vector2.zero,
                            "Actual boundary position was lost before checking the outgoing group");
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            foreach (string scenario in new[] { "0", "91", "\"NaN\"", "\"Infinity\"", "no-heading" })
                Test(report, "Reject invalid authored camera turn bound: " + scenario, () =>
                {
                    using (var fixture = new FixtureControls())
                    {
                        var root = Newtonsoft.Json.Linq.JObject.Parse(WaypointTimeline());
                        root["cameraTurnDegreesPerSecond"] = scenario == "no-heading" ? 30 : Newtonsoft.Json.Linq.JToken.Parse(scenario);
                        if (scenario == "no-heading") { root.Remove("cameraMode"); root.Remove("waypointGroups"); ((Newtonsoft.Json.Linq.JObject)root["frames"][0]).Remove("waypointGroup"); }
                        object session = null; bool rejected = false;
                        try { session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, root.ToString()); }
                        catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                        finally { (session as IDisposable)?.Dispose(); }
                        Require(rejected && InputSystem.devices.Count == baseline.Length, "Invalid heading turn bound acquired input ownership");
                    }
                });
            Test(report, "Authored camera turn rate reaches existing mouse action at the requested bound", () =>
            {
                using (var fixture = new FixtureControls())
                {
                    object session = null;
                    try
                    {
                        var root = Newtonsoft.Json.Linq.JObject.Parse(WaypointTimeline()); root["cameraTurnDegreesPerSecond"] = 30;
                        session = Create(new[] { "-karawaraPerformanceReplay" }, fixture.controls, root.ToString());
                        Harness().GetMethod("AdvanceWithWorldTargets").Invoke(session, new object[] { 0.0, 0f, 220f, Vector3.zero }); UpdateInput();
                        float turn = fixture.controls.PlayerCamera.Movement.ReadValue<Vector2>().x * 220;
                        Require(Mathf.Abs(turn - 30) < .01f, "Authored30deg/s was not respected through actual existing camera input: " + turn);
                    }
                    finally { (session as IDisposable)?.Dispose(); }
                }
            });
            report.cleanedUp = InputSystem.devices.Count == baseline.Length;
            for (int i = 0; i < baseline.Length; i++) report.cleanedUp &= baseline[i].added && baseline[i].enabled == enabled[i];
            report.completed = true;
            report.passed = report.cleanedUp && report.cases.All(c => c.passed);
            report.limits.Add("EditMode actual InputSystem.Update/action callbacks and owned production consumer checks are complementary; this does not replay a naturally active host/player lifecycle or complete five routes.");
            report.limits.Add("Existing source has Keyboard/Mouse bindings only. Replay uses two private virtual devices and no binding overrides or global real-device disable.");
            report.limits.Add("UI pause/skills scheduling is separate; Escape/Tab are rejected here. Runtime monotonic dispatch must contain no device enumeration or ordinary-path managed allocation; inspect source and measure separately from this reflective fixture.");
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-input-replay-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Debug.Log("Performance input replay fixture result: " + path);
            if (!report.passed) Debug.LogError("Performance input replay fixture FAILED; " + path);
        }
        sealed class FixtureControls : IDisposable
        {
            public readonly PlayerControls controls = new PlayerControls();
            readonly object manager;
            readonly PropertyInfo runPlayerUpdates;
            readonly bool previousRunPlayerUpdates;
            public FixtureControls()
            {
                Require(!Application.isPlaying, "Replay fixture owns an EditMode scope and never drives production Play controls.");
                manager = typeof(InputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                runPlayerUpdates = manager.GetType().GetProperty("runPlayerUpdatesInEditMode", BindingFlags.Instance | BindingFlags.Public);
                previousRunPlayerUpdates = (bool)runPlayerUpdates.GetValue(manager);
                // Installed package explicitly provides this mode for real player-state updates in EditMode tests.
                runPlayerUpdates.SetValue(manager, true);
            }
            public void Dispose()
            {
                try { controls.Disable(); UnityEngine.Object.DestroyImmediate(controls.asset); }
                finally { runPlayerUpdates.SetValue(manager, previousRunPlayerUpdates); }
            }
        }
        static void UpdateInput() => typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null,
            new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });
        static UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? SnapshotDevices(UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices)
            => devices.HasValue ? new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(devices.Value.ToArray()) : (UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>?)null;
        static bool SameDevices(UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? first, UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? second)
            => first.HasValue == second.HasValue && (!first.HasValue || first.Value.SequenceEqual(second.Value));
        static string Timeline() => "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"W\",\"LeftCtrl\",\"Space\"],\"mouseDeltaX\":3,\"mouseDeltaY\":2,\"leftButton\":true,\"middleButton\":true},{\"timeSeconds\":0.1,\"keys\":[]}]}";
        static string WaypointTimeline() => "{\"schemaVersion\":1,\"cameraMode\":\"movement-heading-v1\",\"waypointGroups\":{\"path\":{\"arrivalRadius\":0.5,\"points\":[[5,0,0],[5,0,5]]}},\"frames\":[{\"timeSeconds\":0,\"keys\":[],\"waypointGroup\":\"path\"},{\"timeSeconds\":1,\"keys\":[]}]}";
        static Type Harness()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceInputReplay")).FirstOrDefault(t => t != null);
            Require(type != null, "PerformanceInputReplay implementation missing.");
            return type;
        }
        static object Create(string[] args, PlayerControls controls, string json) => Harness().GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { args, controls, json });
        static void Advance(object session, double elapsed) => Harness().GetMethod("Advance", BindingFlags.Public | BindingFlags.Instance).Invoke(session, new object[] { elapsed });
        static void Test(Report report, string name, Action test) { var result = new Case { name = name }; try { test(); result.passed = true; result.detail = "passed"; } catch (Exception error) { result.detail = error.ToString(); } report.cases.Add(result); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
