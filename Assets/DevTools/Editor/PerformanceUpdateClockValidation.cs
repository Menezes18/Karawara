using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceUpdateClockValidation
    {
        public static string Run()
        {
            var cases = new JArray();
            Action<string, Action> test = (name, action) => { try { action(); cases.Add(new JObject { ["name"] = name, ["passed"] = true }); } catch (Exception e) { cases.Add(new JObject { ["name"] = name, ["passed"] = false, ["detail"] = e.ToString() }); } };
            var type = typeof(PerformanceExternalClock).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceUpdateClock");
            Func<int, object> create = capacity => { Need(type != null, "PerformanceUpdateClock is not implemented"); return type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { capacity, Clock() }); };
            Action<object, long> record = (sampler, tick) => type.GetMethod("Record", new[] { typeof(long) }).Invoke(sampler, new object[] { tick });
            Func<object, JObject> export = sampler => JObject.Parse(JsonConvert.SerializeObject(type.GetMethod("Snapshot").Invoke(sampler, null)));
            test("Bounded ordered endpoints and explicit Update boundary", () => { var s = create(3); record(s, 1001); record(s, 1002); var r = export(s); Need((int)r["ticks"].Count() == 2 && (long)r["ticks"][0] == 1001 && !(bool)r["faulted"] && (string)r["clockSource"] == "Stopwatch.GetTimestamp / Update endpoints", "Wrong endpoint snapshot"); Need(!(bool)r["timingEligible"] && !(bool)r["g1bAccepted"], "Sampler granted acceptance"); });
            test("Overflow latches failure and preserves existing data", () => { var s = create(1); record(s, 1001); record(s, 1002); var r = export(s); Need((bool)r["overflow"] && (bool)r["faulted"] && r["ticks"].Count() == 1, "Overflow not rejected"); });
            foreach (long invalid in new long[] { 1001, 1000, -1 }) test("Reject nonmonotonic endpoint " + invalid, () => { var s = create(4); record(s, 1001); record(s, invalid); record(s, 1003); var r = export(s); Need((bool)r["faulted"] && r["ticks"].Count() == 1, "Invalid endpoint accepted or fault did not latch"); });
            test("Snapshot is independent of subsequent records", () => { var s = create(3); record(s, 1001); var first = export(s); record(s, 1002); Need(first["ticks"].Count() == 1 && export(s)["ticks"].Count() == 2, "Snapshot aliases live buffer"); });
            test("Snapshot deep copies shared anchors", () => { Need(type != null, "PerformanceUpdateClock is not implemented"); var clock = Clock(); var s = type.GetMethod("Create").Invoke(null, new object[] { 3, clock }); record(s, 1001); object snapshot = type.GetMethod("Snapshot").Invoke(s, null); clock.started.qpcBefore = 999; clock.End(() => 151); var first = JObject.Parse(JsonConvert.SerializeObject(snapshot)); Need((long)first["clock"]["started"]["qpcBefore"] == 1000 && first["clock"]["ended"].Type == JTokenType.Null, "Snapshot aliases mutable shared clock"); });
            test("Reject invalid capacity", () => { try { create(0); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { return; } throw new Exception("Zero capacity accepted"); });
            test("Record path has no managed allocation after warmup", () => { Need(type != null, "PerformanceUpdateClock is not implemented"); var s = create(4096); var call = (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), s, type.GetMethod("Record", new[] { typeof(long) })); call(1001); long before = GC.GetAllocatedBytesForCurrentThread(); for (long tick = 1002; tick < 3000; tick++) call(tick); long allocated = GC.GetAllocatedBytesForCurrentThread() - before; Need(allocated == 0, "Record allocated " + allocated + " bytes"); });
            var report = new JObject { ["cases"] = cases, ["passed"] = true, ["g1bAccepted"] = false, ["boundary"] = "Synthetic bounded sampler contract only; no Player timing or route acceptance" };
            foreach (JObject row in cases) if (!(bool)row["passed"]) report["passed"] = false;
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/update-clock-contract-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, report.ToString()); return path;
        }
        static PerformanceExternalClock Clock() => new PerformanceExternalClock { processId = 123, frequency = 1000, isHighResolution = true, started = new PerformanceExternalClock.Observation { qpcBefore = 1000, qpcAfter = 1000, unitySeconds = 0 } };
        static void Need(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    }
}
