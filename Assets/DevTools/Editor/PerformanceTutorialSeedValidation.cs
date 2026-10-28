using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceTutorialSeedValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate Tutorial QA Seed")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode seed fixture required.");
            string json = File.ReadAllText("Docs/Validation/TOBE/Execution/tutorial-progression-seed-v1.json");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true }); } catch (Exception error) { cases.Add(new { name, passed = false, error = error.ToString() }); } };
            Func<string, CharacterSaveData> parse = input => (CharacterSaveData)Method("ParseTutorialSeed").Invoke(null, new object[] { input });
            Func<string, Skill[], CharacterSaveData> bind = (input, catalog) => (CharacterSaveData)Method("BindTutorialSeed").Invoke(null, new object[] { input, catalog });
            var defense = AssetDatabase.LoadAssetAtPath<Skill>("Assets/Scripts/SkillTree/Data/Escudo.asset");
            var support = AssetDatabase.LoadAssetAtPath<Skill>("Assets/Scripts/SkillTree/Data/SkillJacare.asset");
            var catalog = new[] { defense, support };
            test("Parse actual copied scene1 QA seed", () => Require(parse(json).sceneIndex == 1, "Wrong scene"));
            test("Reject Video1 scene in Tutorial seed", () => { var data = JObject.Parse(json); data["sceneIndex"] = 2; Reject(() => parse(data.ToString())); });
            test("Existing Video1 parser still rejects scene1", () => Reject(() => Method("ParseVideoSeed").Invoke(null, new object[] { json })));
            foreach (string field in new[] { "skillsSlot", "SkillDefense", "SkillAttack", "SkillSuport" })
                test("Reject foreign native ID: " + field, () => { var data = JObject.Parse(json); data[field] = JArray.Parse("[{instanceID:12345}]"); Reject(() => parse(data.ToString())); });
            test("Rebind actual assets while preserving authored category", () =>
            {
                var data = bind(json, catalog);
                Require(data.skillsSlot.Length == 3 && data.skillsSlot.All(s => s == null) && data.SkillDefense.Length == 2 &&
                    ReferenceEquals(data.SkillDefense[0], defense) && ReferenceEquals(data.SkillDefense[1], support) && data.SkillAttack.Length == 0 && data.SkillSuport.Length == 0,
                    "Lost copied slot/null/category or exact authored asset identity");
            });
            test("Reject missing authored binding", () => Reject(() => bind(json, new[] { defense })));
            test("Reject ambiguous authored binding", () =>
            {
                var clone = Object.Instantiate(defense); ((Object)clone).name = ((Object)defense).name;
                try { Reject(() => bind(json, new[] { defense, support, clone })); } finally { Object.DestroyImmediate(clone); }
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-tutorial-seed-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                boundary = "Seed parsing and actual asset identity only; not process loading, gameplay, population or timing acceptance." }, Formatting.Indented));
            Debug.Log("Tutorial seed fixture: " + path);
        }
        static MethodInfo Method(string name) => typeof(PerformanceProcessHarness).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing seed helper: " + name);
        static void Reject(Action body)
        {
            try { body(); } catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { return; }
            throw new InvalidOperationException("Malformed/foreign seed was accepted");
        }
        static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
