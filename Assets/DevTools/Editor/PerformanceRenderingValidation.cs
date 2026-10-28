using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceRenderingValidation
    {
        public static string Run()
        {
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, error = "" }); } catch (Exception e) { cases.Add(new { name, passed = false, error = e.ToString() }); } };
            test("Accept exact local standalone target", () => Target(42, 42, "WindowsPlayer(Karawara)", true, true, false, false));
            test("Accept actual signed standalone connection GUID", () => Target(-1735286911, -1735286911, "WindowsPlayer(2,DESKTOP-VIS1R15)", true, true, false, false));
            foreach (string scenario in new[] { "editor", "changed", "missing", "remote-host", "already-enabled", "empty-identity" })
                test("Reject " + scenario, () => Reject(() => Target(scenario == "editor" ? -1 : 42, scenario == "changed" ? 43 : scenario == "editor" ? -1 : 42,
                    scenario == "empty-identity" ? "" : "WindowsPlayer(Karawara)", scenario != "missing", scenario != "remote-host", scenario == "editor", scenario == "already-enabled")));
            test("Native events are not equivalent to draw calls", () =>
            {
                var result = Summary("[{\"index\":0,\"type\":\"ClearColor\",\"name\":\"Camera\"},{\"index\":1,\"type\":\"SRPBatch\",\"name\":\"Opaque\"}]");
                Require((int)result["eventCount"] == 2 && result["drawCalls"].Type == JTokenType.Null, "Unmeasured batch draw calls fabricated");
            });
            test("Actual native batch draw count preserved", () =>
            {
                var result = Summary("[{\"index\":0,\"type\":\"ClearColor\",\"name\":\"Camera\"},{\"index\":1,\"type\":\"SRPBatch\",\"name\":\"Opaque\",\"drawCalls\":7}]");
                Require((int)result["drawCalls"] == 7, "Native batch draw count lost");
            });
            foreach (string json in new[] { "[]", "[{\"index\":1,\"type\":\"Mesh\",\"name\":\"x\",\"drawCalls\":1}]", "[{\"index\":0,\"type\":\"Mesh\",\"name\":\"x\",\"drawCalls\":-1}]", "[{\"index\":0,\"type\":\"FutureDraw\",\"name\":\"x\",\"drawCalls\":1}]" })
                test("Reject incomplete or unknown event stream " + json, () => Reject(() => Summary(json)));
            foreach (string scenario in new[] { "complete", "truncated", "uninitialized", "cleanup-failed", "errors", "empty" })
                test("Completion eligibility " + scenario, () =>
                {
                    bool completed = (bool)Invoke("IsComplete", scenario != "uninitialized", scenario == "empty" ? 0 : 264,
                        scenario == "truncated" ? 1 : scenario == "empty" ? 0 : 264, scenario != "cleanup-failed", scenario == "errors" ? 1 : 0);
                    Require(completed == (scenario == "complete"), "Partial/failed remote capture reported complete");
                });
            test("Installed raw EntityId values preserve signed opaque values", () => {
                var data = NativeData(); var field = data.GetType().GetField("m_MeshEntityIds", NativeFlags); var element = field.FieldType.GetElementType();
                var array = Array.CreateInstance(element, 3); int[] values = { 123, -456, 0 };
                for (int i = 0; i < values.Length; i++) { var id = Activator.CreateInstance(element); element.GetField("m_Data", NativeFlags).SetValue(id, values[i]); array.SetValue(id, i); }
                field.SetValue(data, array); var output = Identity(data);
                Require((bool)output["meshEntityIds"]["available"] && output["meshEntityIds"]["values"].Values<int>().SequenceEqual(values), "Raw EntityId values lost or normalized");
                Require((bool)output["ownershipResolved"] == false, "Opaque native identity promoted to ownership");
            });
            test("Null mesh identities stay unavailable", () => { var output = Identity(NativeData()); Require(!(bool)output["meshEntityIds"]["available"] && output["meshEntityIds"]["values"].Type == JTokenType.Null, "Missing native IDs fabricated"); });
            test("Empty mesh array is distinct from unavailable", () => { var data = NativeData(); var field = data.GetType().GetField("m_MeshEntityIds", NativeFlags); field.SetValue(data, Array.CreateInstance(field.FieldType.GetElementType(), 0)); Require((bool)Identity(data)["meshEntityIds"]["available"], "Present empty array treated as absent"); });
            test("Oversized native arrays are rejected", () => { var data = NativeData(); var field = data.GetType().GetField("m_MeshEntityIds", NativeFlags); field.SetValue(data, Array.CreateInstance(field.FieldType.GetElementType(), 3)); Reject(() => Identity(data, 2)); });
            test("Installed ShaderInfo keyword primitives are exported", () => {
                var data = NativeData(); SetShaderArray(data, "m_Keywords", element => { element.GetType().GetField("m_Name", NativeFlags).SetValue(element, "_TEST_KEYWORD"); element.GetType().GetField("m_IsGlobal", NativeFlags).SetValue(element, true); });
                var row = Identity(data)["shaderInfo"]["fields"]["m_Keywords"]; Require((bool)row["available"] && (string)row["values"][0]["m_Name"] == "_TEST_KEYWORD" && (bool)row["values"][0]["m_IsGlobal"], "Installed shader primitives missing");
            });
            test("Shader texture Unity objects are explicitly unresolved", () => {
                var data = NativeData(); SetShaderArray(data, "m_Textures", element => element.GetType().GetField("m_TextureName", NativeFlags).SetValue(element, "opaque remote texture"));
                var value = Identity(data)["shaderInfo"]["fields"]["m_Textures"]["values"][0]; Require((string)value["m_TextureName"] == "opaque remote texture" && !(bool)value["m_Value"]["available"] && value["m_Value"]["value"].Type == JTokenType.Null, "Unity object resolved or silently omitted");
            });
            test("Total shader and mesh budget is enforced", () => { var data = NativeData(); SetShaderArray(data, "m_Keywords", element => {}); SetShaderArray(data, "m_Floats", element => {}); Reject(() => Identity(data, 1)); });
            foreach(string scenario in new[]{"float-NaN","float-positive-infinity","vector-negative-infinity","matrix-NaN"})
                test("Native nonfinite shader parameter remains explicit "+scenario,()=>{
                    var data=NativeData();string group=scenario.StartsWith("float",StringComparison.Ordinal)?"m_Floats":scenario.StartsWith("vector",StringComparison.Ordinal)?"m_Vectors":"m_Matrices";
                    string raw=scenario=="float-positive-infinity"?"PositiveInfinity":scenario=="vector-negative-infinity"?"NegativeInfinity":"NaN";
                    float nonfinite=raw=="PositiveInfinity"?float.PositiveInfinity:raw=="NegativeInfinity"?float.NegativeInfinity:float.NaN;
                    SetShaderArray(data,group,element=>{
                        var field=element.GetType().GetField("m_Value",NativeFlags);object value;
                        if(group=="m_Floats")value=nonfinite;
                        else if(group=="m_Vectors")value=new Vector4(1,nonfinite,3,4);
                        else{var matrix=Matrix4x4.identity;matrix.m12=nonfinite;value=matrix;}
                        field.SetValue(element,value);
                    });
                    var row=Identity(data)["shaderInfo"]["fields"][group]["values"][0]["m_Value"];
                    var observed=group=="m_Floats"?row:group=="m_Vectors"?row["y"]:row["m12"];
                    Require(!(bool)observed["available"]&&observed["value"].Type==JTokenType.Null&&(string)observed["rawNonFinite"]==raw&&!string.IsNullOrEmpty((string)observed["unavailableReason"]),"Native nonfinite shader value was dropped, fabricated or aborted");
                    if(group=="m_Vectors")Require((float)row["x"]==1&&(float)row["z"]==3&&(float)row["w"]==4,"Finite vector neighbours changed");
                    if(group=="m_Matrices")Require((float)row["m00"]==1&&(float)row["m11"]==1,"Finite matrix neighbours changed");
                });
            var report = new { utc = DateTime.UtcNow.ToString("O"), passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases };
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-rendering-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Rendering fixture " + report.passed + ": " + path);
            return path;
        }
        const BindingFlags NativeFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static object NativeData() => Activator.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData", true));
        static JObject Identity(object data, int maximumEntries = 4096) => (JObject)Invoke("SerializeNativeIdentityData", data, maximumEntries);
        static void SetShaderArray(object data, string name, Action<object> configure)
        {
            var shaderField = data.GetType().GetField("m_ShaderInfo", NativeFlags); var shader = shaderField.GetValue(data) ?? Activator.CreateInstance(shaderField.FieldType);
            var arrayField = shaderField.FieldType.GetField(name, NativeFlags); var element = Activator.CreateInstance(arrayField.FieldType.GetElementType()); configure(element);
            var array = Array.CreateInstance(element.GetType(), 1); array.SetValue(element, 0); arrayField.SetValue(shader, array); shaderField.SetValue(data, shader);
        }
        static object Invoke(string method, params object[] args)
        {
            var type = typeof(PerformanceRenderingValidation).Assembly.GetType("RPGKarawara.DevTools.Editor.PerformanceRenderingCapture");
            Require(type != null, "Remote standalone rendering capture missing");
            return type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }
        static void Target(int expected, int actual, string identifier, bool available, bool local, bool editor, bool enabled) => Invoke("ValidateTarget", expected, actual, identifier, available, local, editor, enabled);
        static JObject Summary(string json) => JObject.FromObject(Invoke("Summarize", json));
        static void Reject(Action body)
        {
            try { body(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { return; }
            throw new InvalidOperationException("Invalid rendering evidence accepted");
        }
        static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    }
}


