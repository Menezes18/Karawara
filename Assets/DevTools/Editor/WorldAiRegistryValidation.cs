using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class WorldAiRegistryValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate World AI Registry (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned registry fixture requires PlayMode.");
            var cases = new List<object>();
            Action<string, Action<WorldAIManager, List<AIBossCharacterManager>, List<GameObject>>> test = (name, body) =>
            {
                var roots = new List<GameObject>();
                try
                {
                    var root = new GameObject("Owned inactive WorldAI registry"); root.SetActive(false); roots.Add(root);
                    var manager = root.AddComponent<WorldAIManager>();
                    var bosses = new List<AIBossCharacterManager>();
                    Set(manager, "spawnedInCharacters", new List<AICharacterManager>());
                    Set(manager, "spawnedInBosses", bosses);
                    Set(manager, "aiCharacterSpawners", new List<AICharacterSpawner>());
                    body(manager, bosses, roots);
                    cases.Add(new { name, passed = true, detail = "passed" });
                }
                catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); }
                finally { foreach (var root in roots) if (root != null) Object.DestroyImmediate(root); }
            };
            test("Successful despawn releases the previous boss registry", (manager, bosses, roots) =>
            {
                bosses.Add(Boss(roots, 7));
                manager.DespawnAllCharacters();
                Require(bosses.Count == 0, "Successful despawn retained the old boss reference.");
            });
            test("Ten reset boundaries do not accumulate stale boss references", (manager, bosses, roots) =>
            {
                for (int cycle = 0; cycle < 10; cycle++)
                {
                    var boss = Boss(roots, 7);
                    manager.AddCharacterToSpawnedCharactersList(boss);
                    // Model the real post-despawn boundary; networking itself is covered separately in standalone.
                    ((List<AICharacterManager>)typeof(WorldAIManager).GetField("spawnedInCharacters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager)).Clear();
                    Object.DestroyImmediate(boss.gameObject);
                    manager.ResetAllCharacters();
                    Require(bosses.Count == 0, "Reset retained a destroyed boss at cycle " + cycle);
                }
            });
            test("Boss lookup skips destroyed entries and returns the replacement", (manager, bosses, roots) =>
            {
                var old = Boss(roots, 7); bosses.Add(old); Object.DestroyImmediate(old.gameObject);
                var current = Boss(roots, 7); bosses.Add(current);
                Require(ReferenceEquals(manager.GetBossCharacterByID(7), current), "Lookup returned a destroyed generation instead of the current boss.");
            });
            test("Boss lookup skips null entries and preserves missing-ID behavior", (manager, bosses, roots) =>
            {
                bosses.Add(null); var current = Boss(roots, 7); bosses.Add(current);
                Require(ReferenceEquals(manager.GetBossCharacterByID(7), current), "Null entry prevented lookup.");
                Require(manager.GetBossCharacterByID(8) == null, "Missing ID did not return null.");
            });
            test("Duplicate registration still retains one character and boss", (manager, bosses, roots) =>
            {
                var current = Boss(roots, 7);
                manager.AddCharacterToSpawnedCharactersList(current); manager.AddCharacterToSpawnedCharactersList(current);
                Require(bosses.Count == 1 && ReferenceEquals(manager.GetBossCharacterByID(7), current), "Duplicate registration changed registry semantics.");
            });
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true,
                passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                boundary = "Owned inactive registry boundaries only; no actual NetworkObject despawn, scene cycle or memory snapshot proof" };
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/world-ai-registry-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented)); Debug.Log("World AI registry fixture: " + path);
        }
        static void Set(WorldAIManager manager, string name, object value) => typeof(WorldAIManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, value);
        static AIBossCharacterManager Boss(List<GameObject> roots, int id)
        {
            var root = new GameObject("Owned inactive boss generation"); root.SetActive(false); roots.Add(root);
            var boss = root.AddComponent<AIBossCharacterManager>(); boss.bossID = id; return boss;
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
