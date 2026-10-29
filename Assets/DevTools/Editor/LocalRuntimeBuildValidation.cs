#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace RPGKarawara.DevTools
{
    public static class LocalRuntimeBuildValidation
    {
        [Serializable] private sealed class Report
        {
            public string startedUtc, finishedUtc, kind, result, executable, buildGuid, assemblySha256, error;
            public int errors, warnings;
            public string[] scenes, extraDefines;
        }
        [MenuItem("Karawara/Validation/TOBE/Build G2 Development")]
        public static void BuildDevelopment() => Build("Development", BuildOptions.Development, Array.Empty<string>());
        [MenuItem("Karawara/Validation/TOBE/Build G2 QA Release")]
        public static void BuildQaRelease() => Build("QaRelease", BuildOptions.None, new[] { "KARAWARA_QA" });
        [MenuItem("Karawara/Validation/TOBE/Build G2 Ordinary Release")]
        public static void BuildOrdinaryRelease() => Build("Release", BuildOptions.None, Array.Empty<string>());
        private static void Build(string kind, BuildOptions options, string[] defines)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling)
                throw new InvalidOperationException("Build requires idle Edit mode and no active build.");
            string reportPath = "Docs/Validation/TOBE/Execution/g2-build-" + kind + ".json";
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), kind = kind, result = "running", extraDefines = defines,
                executable = Path.GetFullPath("Builds/TOBE-G2/Windows" + kind + "/Karawara.exe"),
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray() };
            Directory.CreateDirectory(Path.GetDirectoryName(report.executable));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            try
            {
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = report.scenes, locationPathName = report.executable,
                    target = BuildTarget.StandaloneWindows64, options = options, extraScriptingDefines = defines });
                if (build == null) throw new InvalidOperationException("Build report unavailable; inspect Editor.log before retry.");
                report.result = build.summary.result.ToString(); report.errors = build.summary.totalErrors; report.warnings = build.summary.totalWarnings;
                report.buildGuid = build.summary.guid.ToString();
                report.error = string.Join("\n", build.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content));
                string assembly = Path.Combine(Path.GetDirectoryName(report.executable), "Karawara_Data/Managed/Karawara.Gameplay.dll");
                if (File.Exists(assembly)) using (var hash = SHA256.Create()) report.assemblySha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(assembly))).Replace("-", "");
            }
            catch (Exception e) { report.result = "Exception"; report.error = e.ToString(); }
            finally { report.finishedUtc = DateTime.UtcNow.ToString("O"); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); }
            Debug.Log("G2 " + kind + " build " + report.result);
        }
    }
}
#endif
