using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceGcCollectionValidation
    {
        public static string Run()
        {
            var checks = new JArray();
            Action<string, Action> test = (name, action) =>
            {
                try { action(); checks.Add(new JObject { ["name"] = name, ["passed"] = true }); }
                catch (Exception error) { checks.Add(new JObject { ["name"] = name, ["passed"] = false, ["error"] = error.ToString() }); }
            };
            Func<int, Func<int, int>, object> create = (max, read) =>
            {
                var type = typeof(RPGKarawara.DevTools.Performance.CaptureResult).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceGcCollectionCounter", true);
                return Activator.CreateInstance(type, new object[] { max, read });
            };
            Func<object, string, double, JObject> invoke = (counter, method, elapsed) =>
            {
                counter.GetType().GetMethod(method).Invoke(counter, new object[] { elapsed });
                return JObject.FromObject(counter.GetType().GetProperty("Report").GetValue(counter, null));
            };
            Action<bool, string> require = (condition, message) => { if (!condition) throw new InvalidOperationException(message); };
            test("warmup is excluded and reads are bounded", () =>
            {
                int calls = 0, value = 10; var c = create(0, _ => { calls++; return value; });
                invoke(c, "Observe", 29.99); require(calls == 0, "Warmup touched GC count");
                invoke(c, "Observe", 30.01); invoke(c, "Observe", 40); require(calls == 1, "Repeated baseline sampling");
                value = 13; var r = invoke(c, "Finish", 150.01);
                require((bool)r["available"] && (int)r["deltas"][0] == 3 && calls == 2, "Wrong bounded window/delta");
            });
            test("generation counts are never summed as distinct collections", () =>
            {
                var values = new[] { 10, 4, 1 }; var c = create(2, i => values[i]); invoke(c, "Observe", 30);
                values = new[] { 15, 6, 2 }; var r = invoke(c, "Finish", 150);
                require((int)r["deltas"][0] == 5 && (int)r["deltas"][1] == 2 && (int)r["deltas"][2] == 1 && r["totalCollections"] == null, "Generations collapsed/double counted");
            });
            test("unsupported API is unavailable, never zero", () =>
            {
                var c = create(0, _ => { throw new NotSupportedException("test"); }); invoke(c, "Observe", 30);
                var r = invoke(c, "Finish", 150); require(!(bool)r["available"] && r["deltas"].Type == JTokenType.Null, "Unsupported count invented");
            });
            test("decreasing runtime count rejects delta", () =>
            {
                int value = 7; var c = create(0, _ => value); invoke(c, "Observe", 30); value = 6;
                var r = invoke(c, "Finish", 150); require(!(bool)r["available"] && r["deltas"].Type == JTokenType.Null, "Counter reset accepted");
            });
            test("finish before warmup is not a measured zero", () =>
            {
                var c = create(0, _ => 7); var r = invoke(c, "Finish", 20);
                require(!(bool)r["available"] && r["deltas"].Type == JTokenType.Null, "Unmeasured interval accepted");
            });
            test("finish is idempotent and excludes later collections", () =>
            {
                int calls = 0, value = 1; var c = create(0, _ => { calls++; return value; }); invoke(c, "Observe", 30);
                value = 4; invoke(c, "Finish", 150); value = 99; var r = invoke(c, "Finish", 160);
                require((int)r["deltas"][0] == 3 && (double)r["endElapsedSeconds"] == 150 && calls == 2, "Final value mutated");
            });
            test("invalid generation bounds reject", () =>
            {
                foreach (int max in new[] { -1, 33 })
                {
                    bool rejected = false; try { create(max, _ => 0); } catch (Exception) { rejected = true; }
                    require(rejected, "Invalid generation accepted");
                }
            });
            bool passed = true; foreach (var c in checks) passed &= (bool)c["passed"];
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/gc-collection-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(file))
                writer.Write(new JObject { ["passed"] = passed, ["checks"] = checks, ["g1bAccepted"] = false }.ToString());
            return path;
        }
    }
}
