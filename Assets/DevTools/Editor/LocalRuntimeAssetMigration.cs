#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools
{
    public static class LocalRuntimeAssetMigration
    {
        [Serializable] private sealed class Entry { public string path; public int removed; public bool bootstrapConfigured; }
        [Serializable] private sealed class Report { public string utc; public List<Entry> changed = new List<Entry>(); }
        [Serializable] private sealed class Candidates { public Entry[] serializedReferences; }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Migration requires idle Edit mode.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes before migrating serialized components.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Data/Prefabs/Player.prefab").GetComponent<PlayerManager>();
            try
            {
                var candidates = JsonUtility.FromJson<Candidates>(File.ReadAllText("Docs/Validation/TOBE/Execution/local-runtime-audit-1fd6dabaa49b47bcace289d41e360115.json"));
                foreach (string path in candidates.serializedReferences.Select(e => e.path == "Assets/Data/Prefabs/World Managers/World NetworkManager.prefab"
                    ? AssetDatabase.GUIDToAssetPath("42d506ff94522a54fbec839cd81601e6") : e.path).Where(p => p.EndsWith(".prefab", StringComparison.Ordinal)))
                {
                    if (path.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal) || path.StartsWith("Assets/Plugins/", StringComparison.Ordinal)) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int removed = Strip(root);
                        bool bootstrap = AssetDatabase.AssetPathToGUID(path) == "42d506ff94522a54fbec839cd81601e6";
                        if (bootstrap)
                        {
                            var owner = root.GetComponent<GameBootstrap>() ?? root.AddComponent<GameBootstrap>();
                            owner.ConfigurePlayerPrefab(player);
                        }
                        if (removed > 0 || bootstrap)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            report.changed.Add(new Entry { path = path, removed = removed, bootstrapConfigured = bootstrap });
                        }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                    .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/DevTools" }).Select(AssetDatabase.GUIDToAssetPath))
                    .Concat(candidates.serializedReferences.Select(e => e.path).Where(p => p.EndsWith(".unity", StringComparison.Ordinal) && !p.StartsWith("Assets/_Recovery/", StringComparison.Ordinal)));
                foreach (string path in roots.Distinct())
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int removed = scene.GetRootGameObjects().Sum(Strip);
                    bool bootstrap = path.EndsWith("/SceneMenu.unity", StringComparison.Ordinal) && !scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<GameBootstrap>(true) != null);
                    if (bootstrap)
                    {
                        var owner = new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();
                        owner.ConfigurePlayerPrefab(player);
                    }
                    if (removed > 0 || bootstrap)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        report.changed.Add(new Entry { path = path, removed = removed, bootstrapConfigured = bootstrap });
                    }
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory("Docs/Validation/TOBE/Execution");
                File.WriteAllText("Docs/Validation/TOBE/Execution/g2-serialized-asset-migration.json", JsonUtility.ToJson(report, true));
            }
        }

        private static int Strip(GameObject root)
        {
            int removed = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (component == null) continue;
                string ns = component.GetType().Namespace ?? "";
                if (ns == "Unity.Netcode" || ns.StartsWith("Unity.Netcode.", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                    removed++;
                }
            }
            return removed;
        }
    }
}
#endif
