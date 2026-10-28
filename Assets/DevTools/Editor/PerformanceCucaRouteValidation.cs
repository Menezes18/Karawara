using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEditor;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCucaRouteValidation
    {
        const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
        static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        static bool Mode(string method, string value) => (bool)typeof(PerformanceProcessHarness).GetMethod(method, Private).Invoke(null, new object[] { value });
        public static void Run()
        {
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, error = "" }); } catch (Exception error) { cases.Add(new { name, passed = false, error = error.ToString() }); } };
            foreach (string mode in new[] { "cuca-path-inspection", "cuca-route-rehearsal" })
            {
                test(mode+" recognized", () => Require(Mode("IsRehearsalMode", mode), "Cuca discovery mode missing"));
                test(mode+" uses isolated seed", () => Require(Mode("IsSeededMode", mode), "Cuca must use explicit scene3 seed"));
                test(mode+" is neither Video1 nor certified", () => Require(!Mode("IsVideoMode", mode) && !Mode("IsCertifiedRouteMode", mode), "Cuca discovery promoted/misclassified"));
            }
            test("Scene3 seed parsed without weakening Tutorial scene1", () =>
            {
                var fields = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/tutorial-progression-seed-v1.json"));
                fields["sceneIndex"] = 3;
                var parse = typeof(PerformanceProcessHarness).GetMethod("ParseCucaSeed", Private);
                Require(parse != null, "Cuca seed parser missing");
                parse.Invoke(null, new object[] { fields.ToString(Formatting.None) });
                fields["sceneIndex"] = 1;
                bool rejected = false;
                try { parse.Invoke(null, new object[] { fields.ToString(Formatting.None) }); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                Require(rejected, "Wrong seed scene accepted");
            });
            test("Exact Cuca scene mapping", () =>
            {
                var method = typeof(PerformanceProcessHarness).GetMethod("RehearsalScene", Private);
                Require(method != null && (string)method.Invoke(null, new object[] { "cuca-path-inspection" }) == "CucaLoad", "Cuca scene mapping missing");
                Require((string)method.Invoke(null, new object[] { "tutorial-certified-route" }) == "Tutorial_Final" && (string)method.Invoke(null, new object[] { "video1-certified-route" }) == "Video1", "Existing scene mapping regressed");
            });
            test("Actual request parser accepts isolated Cuca inspection", () => Require(Process(3).success, "Cuca request was rejected"));
            test("Actual request parser rejects wrong Cuca seed scene", () => Require(!Process(1).success, "Tutorial seed accepted for Cuca"));
            test("Certified Cuca resolves its own coordinator mode", () => Require(Mode("IsCucaCertifiedRouteMode", "cuca-certified-route") && Mode("IsCucaMode", "cuca-certified-route") && Mode("IsRehearsalMode", "cuca-certified-route"), "Dedicated Cuca certified mode missing"));
            test("Certified Cuca uses isolated scene3 seed", () => Require(Mode("IsSeededMode", "cuca-certified-route") && (string)typeof(PerformanceProcessHarness).GetMethod("RehearsalScene", Private).Invoke(null, new object[]{"cuca-certified-route"}) == "CucaLoad", "Certified Cuca seed/scene mapping missing"));
            test("Certified Cuca is distinct from generic Video1 Tutorial coordinator", () => Require(!Mode("IsCertifiedRouteMode", "cuca-certified-route") && !Mode("IsVideoMode", "cuca-certified-route"), "Cuca routed to generic mission coordinator"));
            test("Certified Cuca requires no discovery screenshots", () => Require(!(bool)typeof(PerformanceTutorialProcess).GetMethod("RequiresDiscoveryScreenshots", Private).Invoke(null, new object[]{"cuca-certified-route"}), "Certified Cuca still requires diagnostic screenshots"));
            test("Actual parser accepts dedicated certified Cuca manifest", () => {
                var fixture=typeof(PerformanceCucaCertifiedRouteValidation);
                var manifest=(JObject)fixture.GetMethod("Manifest", Private).Invoke(null,null);
                var seed=(string)fixture.GetField("Seed", Private).GetRawConstantValue();
                var timeline=(string)fixture.GetField("Timeline", Private).GetRawConstantValue();
                var root=Path.GetFullPath("Builds/TOBE-QA/CucaParserFixtures/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
                var config=new PerformanceProcessConfig {mode="cuca-certified-route",width=1920,height=1080,quality="High Fidelity",vsync=0,targetFrameRate=-1,certifiedManifestJson=manifest.ToString(Formatting.None),rehearsal=new TutorialRehearsalConfig {routeId="cuca-encounter-v1",outputRoot=Path.Combine(root,"output"),durationSeconds=150,maxLatenessSeconds=.25,seedSaveJson=seed,timelineJson=timeline,requireObservedJump=true,checkpoints=Array.Empty<RehearsalPoseCheckpoint>()}};
                var path=Path.Combine(root,"config.json");File.WriteAllText(path,JsonConvert.SerializeObject(config));
                Require(PerformanceProcessHarness.ParseRequest(new[]{"-karawaraPerformanceConfig",path,"-karawaraSaveRoot",Path.Combine(root,"saves")},Path.Combine(root,"personal")).success,"Dedicated certified Cuca request rejected");
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-cuca-route-fixture-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Cuca discovery mode/scene seed contract only; no actual encounter or timing acceptance" }, Formatting.Indented));
            UnityEngine.Debug.Log("Cuca route fixture: "+path);
        }
        static PerformanceProcessResponse Process(int scene)
        {
            string root = Path.GetFullPath("Builds/TOBE-QA/CucaParserFixtures/"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var seed = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/tutorial-progression-seed-v1.json")); seed["sceneIndex"] = scene;
            var config = new PerformanceProcessConfig { mode = "cuca-path-inspection", width = 1920, height = 1080, quality = "High Fidelity", vsync = 0, targetFrameRate = -1,
                rehearsal = new TutorialRehearsalConfig { routeId = "cuca-path-inspection-v1", outputRoot = Path.Combine(root,"output"), durationSeconds = 20, maxLatenessSeconds = .25,
                    seedSaveJson = seed.ToString(Formatting.None), timelineJson = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":19,\"keys\":[]}]}", checkpoints = Array.Empty<RehearsalPoseCheckpoint>() } };
            string path = Path.Combine(root,"config.json"); File.WriteAllText(path,JsonConvert.SerializeObject(config));
            return PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig",path,"-karawaraSaveRoot",Path.Combine(root,"saves") },Path.Combine(root,"personal"));
        }
    }
}
