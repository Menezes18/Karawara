using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace RPGKarawara.DevTools.Performance
{
    // Explicit owned QA session only; never attaches to a scene or changes generated bindings.
    public sealed class PerformanceInputReplay : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        struct Packet
        {
            public double elapsed;
            public KeyboardState keyboard;
            public MouseState mouse;
            public Vector2 worldMovement;
            public int waypointGroup;
        }
        sealed class WaypointGroup
        {
            public string name;
            public Vector3[] points;
            public float radius;
            public int cursor;
        }
        readonly WaypointGroup[] waypointGroups;
        readonly double[][] waypointArrivals;
        int arrivalCount;
        static readonly HashSet<InputActionAsset> activeAssets = new HashSet<InputActionAsset>();
        readonly PlayerControls controls;
        readonly Packet[] packets;
        readonly double[] actualDispatchElapsed;
        readonly InputAction[] actions;
        readonly bool[] actionEnabled;
        readonly ReadOnlyArray<InputDevice>? previousDevices;
        readonly InputBinding? previousMask;
        readonly Keyboard previousKeyboard;
        readonly Mouse previousMouse;
        Keyboard keyboard;
        Mouse mouse;
        int next;
        double previousElapsed = -1;
        bool disposed;
        readonly bool movementHeading;
        readonly float cameraTurnDegreesPerSecond;
        Packet current;

        PerformanceInputReplay(PlayerControls controls, Packet[] packets, bool movementHeading, WaypointGroup[] groups, float cameraTurnDegreesPerSecond)
        {
            this.controls = controls;
            this.packets = packets;
            this.movementHeading = movementHeading;
            this.cameraTurnDegreesPerSecond = cameraTurnDegreesPerSecond;
            waypointGroups = groups;
            int pointCount = 0;
            foreach (var group in groups) pointCount += group.points.Length;
            waypointArrivals = new double[pointCount][];
            for (int i = 0; i < pointCount; i++) waypointArrivals[i] = new double[6];
            current.waypointGroup = -1;
            actualDispatchElapsed = new double[packets.Length];
            // Asset.devices is a view over a reused mutable buffer; snapshot the references independently.
            var originalDevices = controls.asset.devices;
            if (originalDevices.HasValue)
            {
                var copy = new InputDevice[originalDevices.Value.Count];
                for (int i = 0; i < copy.Length; i++) copy[i] = originalDevices.Value[i];
                previousDevices = new ReadOnlyArray<InputDevice>(copy);
            }
            else previousDevices = null;
            previousMask = controls.asset.bindingMask;
            previousKeyboard = Keyboard.current;
            previousMouse = Mouse.current;
            var collected = new List<InputAction>();
            foreach (var action in controls.asset) collected.Add(action);
            actions = collected.ToArray();
            actionEnabled = new bool[actions.Length];
            for (int i = 0; i < actions.Length; i++) actionEnabled[i] = actions[i].enabled;
            try
            {
                controls.Disable();
                keyboard = InputSystem.AddDevice<Keyboard>("Karawara QA Replay Keyboard");
                mouse = InputSystem.AddDevice<Mouse>("Karawara QA Replay Mouse");
                controls.asset.bindingMask = null;
                controls.asset.devices = new InputDevice[] { keyboard, mouse };
                controls.Enable();
            }
            catch { Dispose(); throw; }
        }
#endif
        public static PerformanceInputReplay Create(string[] args, PlayerControls controls, string timelineJson)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int flags = 0;
            if (args != null) for (int i = 0; i < args.Length; i++) if (args[i] == "-karawaraPerformanceReplay") flags++;
            if (flags == 0) return null;
            if (flags != 1) throw new ArgumentException("Exactly one explicit replay flag is required.");
            if (controls == null || controls.asset == null) throw new ArgumentException("Existing controls are required.");
            var packets = Parse(timelineJson, out bool movementHeading, out WaypointGroup[] groups, out float turnBound);
            if (!activeAssets.Add(controls.asset)) throw new InvalidOperationException("This controls asset already has a replay owner.");
            try { return new PerformanceInputReplay(controls, packets, movementHeading, groups, turnBound); }
            catch { activeAssets.Remove(controls.asset); throw; }
#else
            return null;
#endif
        }

        public string CameraMode
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                return movementHeading ? "movement-heading-v1" : "scheduled";
#else
                return "disabled";
#endif
            }
        }
        public bool Advance(double elapsedSeconds) => AdvanceWithHeading(elapsedSeconds, float.NaN, float.NaN);
        public bool AdvanceWithHeading(double elapsedSeconds, float cameraYaw, float cameraYawSpeed)
            => AdvanceWithWorldTargets(elapsedSeconds, cameraYaw, cameraYawSpeed, new Vector3(float.NaN, float.NaN, float.NaN));
        public bool AdvanceWithWorldTargets(double elapsedSeconds, float cameraYaw, float cameraYawSpeed, Vector3 playerPosition)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (disposed) throw new ObjectDisposedException(nameof(PerformanceInputReplay));
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0 || elapsedSeconds < previousElapsed ||
                keyboard == null || !keyboard.added || mouse == null || !mouse.added ||
                (waypointGroups.Length > 0 && !Finite(playerPosition)) ||
                (movementHeading && (float.IsNaN(cameraYaw) || float.IsInfinity(cameraYaw) || float.IsNaN(cameraYawSpeed) || float.IsInfinity(cameraYawSpeed) || cameraYawSpeed <= 0)))
            {
                Dispose();
                throw new InvalidOperationException("Replay clock must remain finite/monotonic and owned devices must remain present.");
            }
            previousElapsed = elapsedSeconds;
            try
            {
                // The outgoing group sees this actual position before its release packet.
                // The route caller separately bounds dispatch lateness against the authored schedule.
                if (current.waypointGroup >= 0) UpdateWaypointProgress(current.waypointGroup, playerPosition, elapsedSeconds);
                while (next < packets.Length && packets[next].elapsed <= elapsedSeconds)
                {
                    if (current.waypointGroup >= 0 && packets[next].waypointGroup != current.waypointGroup &&
                        waypointGroups[current.waypointGroup].cursor != waypointGroups[current.waypointGroup].points.Length)
                        throw new InvalidOperationException("Waypoint group deadline missed: " + waypointGroups[current.waypointGroup].name);
                    // Event timestamp is actual dispatch time; retain scheduled/observed elapsed separately.
                    InputSystem.QueueStateEvent(keyboard, packets[next].keyboard);
                    InputSystem.QueueStateEvent(mouse, packets[next].mouse);
                    current = packets[next];
                    actualDispatchElapsed[next] = elapsedSeconds;
                    next++;
                }
                if (movementHeading)
                {
                    Vector2 worldMovement = current.worldMovement;
                    Vector2 settledHeading = Vector2.zero;
                    if (current.waypointGroup >= 0)
                    {
                        var group = waypointGroups[current.waypointGroup];
                        UpdateWaypointProgress(current.waypointGroup, playerPosition, elapsedSeconds);
                        if (group.cursor < group.points.Length)
                        {
                            var difference = group.points[group.cursor] - playerPosition;
                            worldMovement = new Vector2(difference.x, difference.z).normalized;
                        }
                        else if (group.points.Length >= 2 && worldMovement.sqrMagnitude == 0)
                        {
                            // Finish following the final authored movement segment after arrival.
                            // Keep walking released; the explicit neutral packet also releases look.
                            var segment = group.points[group.points.Length - 1] - group.points[group.points.Length - 2];
                            settledHeading = new Vector2(segment.x, segment.z).normalized;
                        }
                    }
                    var keys = current.keyboard;
                    keys.Set(Key.W, false); keys.Set(Key.A, false); keys.Set(Key.S, false); keys.Set(Key.D, false);
                    float mouseX = 0;
                    if (worldMovement.sqrMagnitude > 0)
                    {
                        float desired = Mathf.Atan2(worldMovement.x, worldMovement.y) * Mathf.Rad2Deg;
                        float local = Mathf.DeltaAngle(cameraYaw, desired);
                        int octant = ((Mathf.RoundToInt(local / 45) % 8) + 8) % 8;
                        keys.Set(Key.W, octant == 0 || octant == 1 || octant == 7);
                        keys.Set(Key.D, octant == 1 || octant == 2 || octant == 3);
                        keys.Set(Key.S, octant == 3 || octant == 4 || octant == 5);
                        keys.Set(Key.A, octant == 5 || octant == 6 || octant == 7);
                    }
                    Vector2 lookHeading = worldMovement.sqrMagnitude > 0 ? worldMovement : settledHeading;
                    if (lookHeading.sqrMagnitude > 0)
                    {
                        float local = Mathf.DeltaAngle(cameraYaw, Mathf.Atan2(lookHeading.x, lookHeading.y) * Mathf.Rad2Deg);
                        if (Mathf.Abs(local) > .25f) mouseX = Mathf.Clamp(local * 4, -cameraTurnDegreesPerSecond, cameraTurnDegreesPerSecond) / cameraYawSpeed;
                    }
                    var look = current.mouse; look.delta = new Vector2(mouseX, 0);
                    InputSystem.QueueStateEvent(keyboard, keys); InputSystem.QueueStateEvent(mouse, look);
                }
                return next == packets.Length;
            }
            catch { Dispose(); throw; }
#else
            return false;
#endif
        }

        internal static float ValidateHeadingTurn(JObject root, bool movementHeading)
        {
            var token = root["cameraTurnDegreesPerSecond"];
            if (token == null) return 90;
            if (!movementHeading || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw new ArgumentException("Camera turn bound requires heading mode and a numeric rate.");
            double rate = (double)token;
            if (double.IsNaN(rate) || double.IsInfinity(rate) || rate < 10 || rate > 90)
                throw new ArgumentException("Camera turn bound must be finite and10..90degrees per second.");
            return (float)rate;
        }
        // Snapshot construction is explicit and outside timing; Advance does not allocate this DTO.
        public double[][] WaypointArrivals()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var result = new double[arrivalCount][];
            for (int i = 0; i < result.Length; i++) result[i] = (double[])waypointArrivals[i].Clone();
            return result;
#else
            return Array.Empty<double[]>();
#endif
        }
        public double[][] DispatchTimeline()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            var result = new double[next][];
            for (int i = 0; i < next; i++) result[i] = new[] { packets[i].elapsed, actualDispatchElapsed[i] };
            return result;
#else
            return Array.Empty<double[]>();
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (disposed) return;
            disposed = true;
            // Clear only owned state synchronously, without an extra global InputSystem.Update.
            try
            {
                if (keyboard != null && keyboard.added) InputState.Change(keyboard, default(KeyboardState));
                if (mouse != null && mouse.added) InputState.Change(mouse, default(MouseState));
            }
            finally
            {
                controls.Disable();
                try
                {
                    if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                }
                finally
                {
                    controls.asset.devices = previousDevices;
                    controls.asset.bindingMask = previousMask;
                    for (int i = 0; i < actions.Length; i++) if (actionEnabled[i]) actions[i].Enable();
                    if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                    if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                    activeAssets.Remove(controls.asset);
                }
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void UpdateWaypointProgress(int groupIndex, Vector3 position, double elapsed)
        {
            var group = waypointGroups[groupIndex];
            while (group.cursor < group.points.Length && Vector3.Distance(position, group.points[group.cursor]) <= group.radius)
            {
                var row = waypointArrivals[arrivalCount++];
                row[0] = groupIndex; row[1] = group.cursor++; row[2] = elapsed;
                row[3] = position.x; row[4] = position.y; row[5] = position.z;
            }
        }
        static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
        static Packet[] Parse(string json, out bool movementHeading, out WaypointGroup[] groups, out float turnBound)
        {
            movementHeading = false;
            groups = Array.Empty<WaypointGroup>();
            turnBound = 90;
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) throw new ArgumentException("Timeline is missing or exceeds 1 MiB.");
                var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (root["cameraMode"] != null && (string)root["cameraMode"] != "movement-heading-v1") throw new ArgumentException("Unsupported replay camera mode.");
                movementHeading = (string)root["cameraMode"] == "movement-heading-v1";
                turnBound = ValidateHeadingTurn(root, movementHeading);
                var groupNames = new Dictionary<string, int>(StringComparer.Ordinal);
                if (root["waypointGroups"] != null)
                {
                    if (!movementHeading || !(root["waypointGroups"] is JObject authoredGroups) || authoredGroups.Count == 0 || authoredGroups.Count > 8)
                        throw new ArgumentException("Waypoint discovery requires heading mode and 1..8 groups.");
                    var collected = new List<WaypointGroup>(); int total = 0;
                    foreach (var property in authoredGroups.Properties())
                    {
                        if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Length > 64 || !(property.Value is JObject group) ||
                            !(group["points"] is JArray points) || points.Count == 0 || (total += points.Count) > 128)
                            throw new ArgumentException("Waypoint discovery requires named groups and 1..128 total points.");
                        float radius = (float?)group["arrivalRadius"] ?? float.NaN;
                        if (float.IsNaN(radius) || radius < .2f || radius > 1) throw new ArgumentException("Waypoint arrival radius must be .2..1 metre.");
                        var positions = new Vector3[points.Count];
                        for (int i = 0; i < positions.Length; i++)
                        {
                            if (!(points[i] is JArray p) || p.Count != 3) throw new ArgumentException("Each waypoint requires XYZ.");
                            positions[i] = new Vector3((float)p[0], (float)p[1], (float)p[2]);
                            if (!Finite(positions[i])) throw new ArgumentException("Waypoints must be finite.");
                        }
                        groupNames.Add(property.Name, collected.Count);
                        collected.Add(new WaypointGroup { name = property.Name, points = positions, radius = radius });
                    }
                    groups = collected.ToArray();
                }
                if ((int?)root["schemaVersion"] != 1 || !(root["frames"] is JArray frames) || frames.Count == 0 || frames.Count > 18000)
                    throw new ArgumentException("Require schema1 and 1..18000 immutable frames.");
                var packets = new Packet[frames.Count];
                var usedGroups = new HashSet<int>(); int previousGroup = -1;
                double previous = -1;
                for (int i = 0; i < frames.Count; i++)
                {
                    var frame = frames[i] as JObject;
                    if (frame == null || frame["timeSeconds"] == null || !(frame["keys"] is JArray keys))
                        throw new ArgumentException("Each frame requires timeSeconds and explicit keys.");
                    double elapsed = (double)frame["timeSeconds"];
                    if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0 || elapsed > 150 || elapsed <= previous)
                        throw new ArgumentException("Frame times must be finite, strictly increasing and within 0..150 seconds.");
                    previous = elapsed;
                    var keyboard = default(KeyboardState);
                    var seen = new HashSet<Key>();
                    for (int j = 0; j < keys.Count; j++)
                    {
                        if (!Enum.TryParse((string)keys[j], false, out Key key) || !Allowed(key) || !seen.Add(key))
                            throw new ArgumentException("Unsupported or duplicate gameplay key.");
                        keyboard.Set(key, true);
                    }
                    float x = (float?)frame["mouseDeltaX"] ?? 0, y = (float?)frame["mouseDeltaY"] ?? 0;
                    if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                        throw new ArgumentException("Mouse deltas must be finite.");
                    if (movementHeading && (x != 0 || y != 0)) throw new ArgumentException("Heading camera cannot mix manual look deltas.");
                    bool hasPositionX = frame["mousePositionX"] != null, hasPositionY = frame["mousePositionY"] != null;
                    if (hasPositionX != hasPositionY) throw new ArgumentException("Mouse position requires both coordinates.");
                    float positionX = (float?)frame["mousePositionX"] ?? 0, positionY = (float?)frame["mousePositionY"] ?? 0;
                    if (float.IsNaN(positionX) || float.IsInfinity(positionX) || float.IsNaN(positionY) || float.IsInfinity(positionY))
                        throw new ArgumentException("Mouse position must be finite.");
                    var mouse = new MouseState { delta = new Vector2(x, y), position = new Vector2(positionX, positionY) };
                    int groupIndex = -1;
                    if (frame["waypointGroup"] != null)
                    {
                        if (!groupNames.TryGetValue((string)frame["waypointGroup"], out groupIndex) || seen.Contains(Key.W) || seen.Contains(Key.A) || seen.Contains(Key.S) || seen.Contains(Key.D) || i == frames.Count - 1)
                            throw new ArgumentException("Waypoint frames require a known group, no manual movement, and a later release.");
                        if (groupIndex != previousGroup && !usedGroups.Add(groupIndex)) throw new ArgumentException("Waypoint groups cannot be re-entered.");
                    }
                    previousGroup = groupIndex;
                    bool left = (bool?)frame["leftButton"] ?? false, right = (bool?)frame["rightButton"] ?? false, middle = (bool?)frame["middleButton"] ?? false;
                    mouse = mouse.WithButton(MouseButton.Left, left).WithButton(MouseButton.Right, right).WithButton(MouseButton.Middle, middle);
                    if (i == frames.Count - 1 && (keys.Count != 0 || left || right || middle || x != 0 || y != 0))
                        throw new ArgumentException("The final frame must explicitly release gameplay keys/buttons and mouse delta.");
                    packets[i] = new Packet { elapsed = elapsed, keyboard = keyboard, mouse = mouse, waypointGroup = groupIndex,
                        worldMovement = new Vector2((seen.Contains(Key.D) ? 1 : 0) - (seen.Contains(Key.A) ? 1 : 0),
                            (seen.Contains(Key.W) ? 1 : 0) - (seen.Contains(Key.S) ? 1 : 0)).normalized };
                }
                if (usedGroups.Count != groups.Length) throw new ArgumentException("Every waypoint group must be scheduled.");
                return packets;
            }
            catch (ArgumentException) { throw; }
            catch (Exception error) { throw new ArgumentException("Malformed replay timeline.", error); }
        }
        static bool Allowed(Key key) => key == Key.W || key == Key.A || key == Key.S || key == Key.D ||
            key == Key.LeftCtrl || key == Key.LeftShift || key == Key.Space || key == Key.E || key == Key.Enter ||
            key == Key.LeftArrow || key == Key.RightArrow || key == Key.Semicolon;
#endif
    }
}
