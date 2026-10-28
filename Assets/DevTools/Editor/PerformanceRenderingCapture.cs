using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    // Editor-only remote Frame Debugger reader. Never starts a timing capture.
    // Native remote IDs must never be resolved against the Editor object registry.
    public static class PerformanceRenderingCapture
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly HashSet<string> DrawTypes = new HashSet<string>(new[] { "StaticBatch", "DynamicBatch", "Mesh", "DynamicGeometry", "GLDraw", "SkinOnGPU", "DrawProcedural", "DrawProceduralIndirect", "DrawProceduralIndexed", "DrawProceduralIndexedIndirect", "InstancedMesh", "SRPBatch", "HybridBatch" });
        static readonly HashSet<string> NonDrawTypes = new HashSet<string>(new[] { "ClearNone", "ClearColor", "ClearDepth", "ClearColorDepth", "ClearStencil", "ClearColorStencil", "ClearDepthStencil", "ClearAll", "SetRenderTarget", "ResolveRT", "ResolveDepth", "GrabIntoRT", "ComputeDispatch", "RayTracingDispatch", "PluginEvent", "BeginSubpass", "HierarchyLevelBreak", "ConfigureFoveatedRendering" });
        static Type utility => typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility", true);
        static object Call(string name, params object[] values) => utility.GetMethod(name, Static).Invoke(null, values);
        static T Property<T>(string name) => (T)utility.GetProperty(name, Static).GetValue(null);
        static Session active;
        sealed class Session
        {
            public int guid, count, hash, next, maxDetails;
            public string identifier, path, label;
            public DateTime utc;
            public double started, requested;
            public bool initialized, released;
            public JArray events;
            public readonly List<string> errors = new List<string>();
        }
        // Newer EntityIds are opaque Player values. Never call EditorUtility.InstanceIDToObject
        // or serialize a UnityEngine.Object while reading remote event metadata.
        public static JObject SerializeNativeIdentityData(object data, int maximumEntries = 4096)
        {
            if (data == null || data.GetType().FullName != "UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData" || maximumEntries < 1 || maximumEntries > 4096)
                throw new ArgumentException("Installed native event data and bounded 1..4096 identity entry budget required.");
            int budget = maximumEntries;
            var result = new JObject { ["schemaVersion"] = 1, ["ownershipResolved"] = false,
                ["boundary"] = "Opaque remote Player EntityId integers and shader parameters only; zero is retained as supplied. No Editor object resolution, material/renderer ownership or GPU cost is inferred." };
            var meshField = data.GetType().GetField("m_MeshEntityIds", Instance);
            var mesh = meshField?.GetValue(data) as Array;
            var meshValues = new JArray();
            if (mesh != null)
            {
                Charge(mesh.Length, ref budget);
                var element = meshField.FieldType.GetElementType();
                var raw = element.GetField("m_Data", Instance);
                if (element.FullName != "UnityEngine.EntityId" || raw?.FieldType != typeof(int)) throw new ArgumentException("Installed EntityId representation changed.");
                foreach (var id in mesh) meshValues.Add((int)raw.GetValue(id));
            }
            result["meshEntityIds"] = ArrayEvidence(mesh, meshValues, meshField == null ? "Installed event field absent." : "Native array unavailable.");
            var shaderField = data.GetType().GetField("m_ShaderInfo", Instance);
            var shader = shaderField?.GetValue(data);
            var shaderFields = new JObject();
            result["shaderInfo"] = new JObject { ["available"] = shader != null,
                ["unavailableReason"] = shader == null ? "Native ShaderInfo unavailable." : null, ["fields"] = shaderFields };
            if (shader != null)
                foreach (string name in new[] { "m_Keywords", "m_Floats", "m_Ints", "m_Vectors", "m_Matrices", "m_Textures", "m_Buffers", "m_CBuffers" })
                {
                    var field = shader.GetType().GetField(name, Instance);
                    var rows = field?.GetValue(shader) as Array; var values = new JArray();
                    if (rows != null)
                    {
                        Charge(rows.Length, ref budget);
                        foreach (var row in rows)
                        {
                            if (row == null) throw new ArgumentException("Null native shader parameter row.");
                            var value = new JObject();
                            foreach (var member in row.GetType().GetFields(Instance)) value[member.Name] = ShaderValue(member.GetValue(row), member.FieldType);
                            values.Add(value);
                        }
                    }
                    shaderFields[name] = ArrayEvidence(rows, values, field == null ? "Installed shader field absent." : "Native parameter array unavailable.");
                }
            return result;
        }
        static void Charge(int count, ref int budget)
        {
            if (count > budget) throw new ArgumentException("Remote identity/shader entry budget exceeded; export is incomplete.");
            budget -= count;
        }
        static JObject ArrayEvidence(Array array, JArray values, string reason) => new JObject {
            ["available"] = array != null, ["count"] = array == null ? JValue.CreateNull() : new JValue(array.Length),
            ["values"] = array == null ? JValue.CreateNull() : values, ["unavailableReason"] = array == null ? reason : null };
        static JToken ShaderValue(object value, Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return new JObject { ["available"] = false, ["value"] = JValue.CreateNull(),
                ["unavailableReason"] = "Remote Unity object reference is deliberately unresolved; Editor identity cannot establish Player ownership." };
            if (value == null) return JValue.CreateNull();
            if (type == typeof(string))
            { if (((string)value).Length > 4096) throw new ArgumentException("Native shader text bound exceeded."); return new JValue((string)value); }
            if (type.IsPrimitive || type.IsEnum)
            {
                if (value is float f && (float.IsNaN(f) || float.IsInfinity(f)) || value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                    return new JObject { ["available"] = false, ["value"] = JValue.CreateNull(),
                        ["rawNonFinite"] = value is float nf ? float.IsNaN(nf) ? "NaN" : float.IsPositiveInfinity(nf) ? "PositiveInfinity" : "NegativeInfinity" : double.IsNaN((double)value) ? "NaN" : double.IsPositiveInfinity((double)value) ? "PositiveInfinity" : "NegativeInfinity",
                        ["unavailableReason"] = "Native shader parameter is nonfinite; raw classification retained as metadata, never a numeric cost measurement." };
                return JToken.FromObject(value);
            }
            if (type == typeof(Vector4) || type == typeof(Matrix4x4))
            {
                var fields = new JObject();
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) fields[field.Name] = ShaderValue(field.GetValue(value), field.FieldType);
                return fields;
            }
            return new JObject { ["available"] = false, ["value"] = JValue.CreateNull(), ["unavailableReason"] = "Unsupported native shader field type: " + type.FullName };
        }
        public static void ValidateTarget(int requested, int connected, string identifier, bool available, bool local, bool editor, bool debuggerEnabled)
        {
            if (requested == -1 || requested != connected || !available || !local || editor || debuggerEnabled || string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("An exact available local standalone connection and an unowned Frame Debugger are required; Editor fallback is forbidden.");
        }
        public static object Summarize(string eventsJson)
        {
            var events = JArray.Parse(eventsJson);
            if (events.Count == 0 || events.Count > 20000) throw new ArgumentException("Bounded nonempty native event stream required.");
            long draws = 0; int unknown = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var row = events[i]; string type = (string)row["type"];
                if ((int?)row["index"] != i || string.IsNullOrWhiteSpace(type) || (!DrawTypes.Contains(type) && !NonDrawTypes.Contains(type))) throw new ArgumentException("Incomplete or unknown native event sequence.");
                if (row["drawCalls"] != null && row["drawCalls"].Type != JTokenType.Null && (long)row["drawCalls"] < 0) throw new ArgumentException("Negative native draw count.");
                if (!DrawTypes.Contains(type)) continue;
                if (row["drawCalls"] == null || row["drawCalls"].Type == JTokenType.Null) unknown++;
                else draws += (long)row["drawCalls"];
            }
            return new { eventCount = events.Count, drawCalls = unknown == 0 ? (long?)draws : null, knownDrawCalls = draws, unknownDrawEvents = unknown,
                groups = events.GroupBy(r => (string)r["type"]).Select(g => new { type = g.Key, events = g.Count() }).ToArray() };
        }
        public static bool IsComplete(bool initialized, int count, int exported, bool released, int errorCount)
            => initialized && count > 0 && exported == count && released && errorCount == 0;
        public static string Begin(int guid, string expectedIdentifier, string outputRoot, string label, int maxDetailEvents = 20000)
        {
            if (active != null || Application.isPlaying || string.IsNullOrWhiteSpace(label) || label.Length > 256 || maxDetailEvents < 1 || maxDetailEvents > 20000)
                throw new ArgumentException("One bounded, labeled Editor-owned rendering attribution session at a time.");
            string identifier = ProfilerDriver.GetConnectionIdentifier(guid);
            ValidateTarget(guid, ProfilerDriver.connectedProfiler, identifier, ProfilerDriver.GetAvailableProfilers().Contains(guid), (bool)typeof(ProfilerDriver).GetMethod("IsIdentifierOnLocalhost", Static).Invoke(null, new object[] { guid }), (bool)typeof(ProfilerDriver).GetMethod("IsConnectionEditor", Static).Invoke(null, null), FrameDebugger.enabled);
            if (identifier != expectedIdentifier) throw new ArgumentException("Standalone identifier changed before capture.");
            if (!Path.IsPathRooted(outputRoot) || !Directory.Exists(outputRoot)) throw new ArgumentException("Existing absolute QA output root required.");
            var session = new Session { guid = guid, identifier = identifier, label = label, utc = DateTime.UtcNow, started = EditorApplication.timeSinceStartup, maxDetails = maxDetailEvents,
                path = Path.Combine(outputRoot, "frame-debugger-" + Guid.NewGuid().ToString("N") + ".json") };
            using (new FileStream(session.path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            active = session;
            try { Call("SetEnabled", true, guid); EditorApplication.update += Tick; AssemblyReloadEvents.beforeAssemblyReload += Cancel; }
            catch { Finish("Remote debugger startup failed."); throw; }
            return session.path;
        }
        public static string Status() => active == null ? "idle" : active.path + " : events=" + active.count + ", details=" + active.next;
        public static void Cancel() { if (active != null) Finish("Capture explicitly canceled or assembly reloaded."); }
        static bool Owns(Session session) => FrameDebugger.enabled && (int)Call("GetRemotePlayerGUID") == session.guid;
        static void Tick()
        {
            var session = active; if (session == null) return;
            try
            {
                if (ProfilerDriver.connectedProfiler != session.guid || ProfilerDriver.GetConnectionIdentifier(session.guid) != session.identifier || !ProfilerDriver.GetAvailableProfilers().Contains(session.guid) || !Owns(session))
                    throw new InvalidOperationException("Remote target/debugger ownership changed.");
                double now = EditorApplication.timeSinceStartup;
                if (now - session.started > 120) throw new TimeoutException("Bounded remote event export timed out; partial metadata remains unqualified.");
                if (Property<bool>("receivingRemoteFrameEventData")) return;
                if (!session.initialized)
                {
                    session.count = Property<int>("count"); if (session.count == 0) return;
                    if (session.count > 20000) throw new InvalidOperationException("Native event capacity exceeded.");
                    session.hash = Property<int>("eventsHash");
                    var native = (Array)Call("GetFrameEvents");
                    if (native.Length != session.count) return;
                    session.events = new JArray();
                    for (int i = 0; i < native.Length; i++)
                    {
                        var value = native.GetValue(i);
                        session.events.Add(new JObject { ["index"] = i, ["type"] = value.GetType().GetField("m_Type", Instance).GetValue(value).ToString(),
                            ["name"] = (string)Call("GetFrameEventInfoName", i), ["drawCalls"] = JValue.CreateNull() });
                    }
                    session.initialized = true; session.requested = now;
                }
                if (session.count != Property<int>("count") || session.hash != Property<int>("eventsHash")) throw new InvalidOperationException("Frozen native event stream changed during export.");
                for (int budget = 0; budget < 16 && session.next < session.count && session.next < session.maxDetails; budget++)
                {
                    var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData", true);
                    var data = Activator.CreateInstance(type);
                    utility.GetProperty("limit", Static).SetValue(null, session.next + 1);
                    bool ready = (bool)Call("GetFrameEventData", session.next, data);
                    if (!ready || (int)type.GetField("m_FrameEventIndex", Instance).GetValue(data) != session.next)
                    {
                        if (now - session.requested > 5) throw new TimeoutException("Remote native event detail unavailable at " + session.next);
                        return;
                    }
                    var row = (JObject)session.events[session.next]; var details = new JObject();
                    foreach (var field in type.GetFields(Instance))
                    {
                        var value = field.GetValue(data);
                        if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum) details[field.Name] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                        else if (field.Name.EndsWith("State", StringComparison.Ordinal))
                        {
                            var state = new JObject();
                            foreach (var member in field.FieldType.GetFields(Instance))
                                if (member.FieldType.IsPrimitive || member.FieldType.IsEnum) state[member.Name] = JToken.FromObject(member.GetValue(value));
                            details[field.Name] = state;
                        }
                    }
                    details["remoteIdentityData"] = SerializeNativeIdentityData(data);
                    row["native"] = details;
                    if (DrawTypes.Contains((string)row["type"])) row["drawCalls"] = details["m_DrawCallCount"]?.DeepClone() ?? JValue.CreateNull();
                    session.next++; session.requested = now;
                }
                if (session.next == session.count || session.next == session.maxDetails) Finish(null);
            }
            catch (Exception error) { Finish(error.ToString()); }
        }
        static void Finish(string error)
        {
            var session = active; if (session == null) return;
            active = null; EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            if (error != null) session.errors.Add(error);
            try { if (Owns(session)) { Call("SetEnabled", false, session.guid); session.released = !FrameDebugger.enabled; } }
            catch (Exception cleanup) { session.errors.Add(cleanup.ToString()); }
            object summary = null;
            try { if (session.events != null) summary = Summarize(session.events.ToString(Formatting.None)); }
            catch (Exception failure) { session.errors.Add(failure.ToString()); }
            var report = new { schemaVersion = 1, scope = "local-standalone-frame-debugger-attribution", timingEligible = false, completed = IsComplete(session.initialized, session.count, session.next, session.released, session.errors.Count),
                utc = session.utc.ToString("O"), finishedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, session.label,
                targetGuid = session.guid, targetIdentifier = session.identifier, nativeEventsHash = session.hash, detailsComplete = session.next == session.count && session.initialized,
                detailedEvents = session.next, releasedOwnedDebugger = session.released, summary, events = session.events, errors = session.errors,
                limitations = new[] { "Attribution only; debugger interrupts timing and input replay.", "Remote native IDs are not Editor object IDs. Material/renderer provenance requires a separately bound Player observation.", "Native event counts are not draw-call counts; unsupported/incomplete values remain null.", "No GPU pass timing is inferred from event/draw counts.", "Exclusive debugger operation required: the SDK exposes no session token to distinguish a same-target disable/re-enable by another operator." } };
            File.WriteAllText(session.path, JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Standalone rendering attribution " + report.completed + ": " + session.path);
        }
    }
}



