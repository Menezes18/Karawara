using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace RPGKarawara.DevTools.Editor
{
    public static class UnityMigrationAudit
    {
        [Serializable] public class Report
        {
            public string utc;
            public string unityVersion;
            public string pipeline;
            public int prefabCount, materialCount, animatorControllerCount;
            public List<string> loadedScenes = new List<string>();
            public List<string> missingScripts = new List<string>();
            public List<string> shaderProblems = new List<string>();
            public List<string> productionShaderProblems = new List<string>();
            public List<string> showcaseShaderProblems = new List<string>();
            public List<string> loadErrors = new List<string>();
        }

        [MenuItem("Karawara/Validation/Audit Migration Assets and Scenes")]
        public static void Audit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before auditing scene assets.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in" };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { report.loadErrors.Add(path); continue; }
                report.prefabCount++;
                CheckHierarchy(prefab, path, report);
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            var productionDependencies = new HashSet<string>(AssetDatabase.GetDependencies(scenes.ToArray(), true));
            if (File.Exists(DevPlaygroundBuilder.ScenePath)) scenes.Add(DevPlaygroundBuilder.ScenePath);
            var previousActive = SceneManager.GetActiveScene();
            foreach (var path in scenes.Distinct())
            {
                var existing = SceneManager.GetSceneByPath(path);
                bool alreadyLoaded = existing.IsValid() && existing.isLoaded;
                var scene = existing;
                try
                {
                    if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    report.loadedScenes.Add(path);
                    foreach (var root in scene.GetRootGameObjects()) CheckHierarchy(root, path, report);
                }
                catch (Exception ex) { report.loadErrors.Add(path + ": " + ex.Message); }
                finally
                {
                    if (!alreadyLoaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            var dependencies = AssetDatabase.GetDependencies(scenes.ToArray(), true);
            foreach (var path in dependencies)
            {
                if (path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase)) report.animatorControllerCount++;
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                report.materialCount++;
                string issue = null;
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    issue = path + ": missing/error shader";
                else if (!material.shader.isSupported)
                    issue = path + ": unsupported shader " + material.shader.name;
                if (issue != null)
                {
                    report.shaderProblems.Add(issue);
                    if (productionDependencies.Contains(path)) report.productionShaderProblems.Add(issue);
                    else report.showcaseShaderProblems.Add(issue);
                }
            }
            WriteReport("Docs/HomolUnityMigrationEditorAudit.json", report);
            Debug.Log($"Migration audit: {report.loadedScenes.Count} scenes loaded, {report.prefabCount} prefabs, " +
                $"{report.materialCount} reachable materials; {report.missingScripts.Count} missing-script objects, " +
                $"{report.shaderProblems.Count} shader issues, {report.loadErrors.Count} load errors. See Docs/HomolUnityMigrationEditorAudit.json.");
        }

        static void CheckHierarchy(GameObject root, string assetPath, Report report)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing > 0) report.missingScripts.Add(assetPath + " :: " + HierarchyPath(transform) + " :: " + missing);
            }
        }

        static string HierarchyPath(Transform target)
        {
            string path = target.name;
            for (var parent = target.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        [Serializable] public class CompilationReport
        {
            public string utc, unityVersion, target;
            public bool development, success;
            public string[] assemblies;
            public string error;
        }

        [MenuItem("Karawara/Validation/Compile Release Player Scripts")]
        public static void CompileRelease() => Compile(false);

        [MenuItem("Karawara/Validation/Compile Development Player Scripts")]
        public static void CompileDevelopment() => Compile(true);

        static void Compile(bool development)
        {
            var report = new CompilationReport { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                target = EditorUserBuildSettings.activeBuildTarget.ToString(), development = development };
            try
            {
                var settings = new ScriptCompilationSettings { target = EditorUserBuildSettings.activeBuildTarget,
                    group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                    options = development ? ScriptCompilationOptions.DevelopmentBuild | ScriptCompilationOptions.Assertions : ScriptCompilationOptions.None };
                string output = "Library/DevToolsValidation/" + (development ? "Development" : "Release");
                Directory.CreateDirectory(output);
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
                report.assemblies = result.assemblies?.ToArray();
                report.success = report.assemblies != null && report.assemblies.Length > 0;
                if (!report.success) report.error = "Unity returned no compiled player assemblies. Inspect the Console.";
            }
            catch (Exception ex) { report.error = ex.ToString(); }
            WriteReport("Docs/HomolUnity" + (development ? "Development" : "Release") + "ScriptCompilation.json", report);
            if (report.success) Debug.Log("Player scripts compiled: " + (development ? "Development" : "Release"));
            else Debug.LogError("Player script compilation failed: " + report.error);
        }

        static void WriteReport(string path, object report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        [Serializable] public class StandaloneReport
        {
            public string utc, finishedUtc, result, output, error;
            public string[] scenes;
            public int errors, warnings;
            public ulong bytes;
        }

        [MenuItem("Karawara/Validation/Build Windows Release")]
        public static void BuildWindowsRelease()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
            var report = new StandaloneReport
            {
                utc = DateTime.UtcNow.ToString("O"), result = "running",
                output = Path.GetFullPath("Builds/DevToolsValidation/WindowsRelease/Karawara.exe"),
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray()
            };
            WriteReport("Docs/HomolUnityWindowsReleaseBuild.json", report);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(report.output));
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = report.scenes, locationPathName = report.output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
                if (build == null) throw new InvalidOperationException("Unity returned no build report; inspect Editor.log for its diagnostic.");
                report.result = build.summary.result.ToString();
                report.errors = build.summary.totalErrors;
                report.warnings = build.summary.totalWarnings;
                report.bytes = build.summary.totalSize;
                report.error = string.Join("\n", build.steps.SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                    .Select(message => message.content));
            }
            catch (Exception ex) { report.result = "Exception"; report.error = ex.ToString(); }
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            WriteReport("Docs/HomolUnityWindowsReleaseBuild.json", report);
            Debug.Log("Windows Release build: " + report.result + "; " + report.output);
        }
    }
}
