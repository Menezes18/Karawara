using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    // Separate validation outputs. Defines/options apply only to this build invocation.
    public static class TobeBuildValidation
    {
        [Serializable] sealed class Report
        {
            public string startedUtc, finishedUtc, result, output, unityVersion, error, buildGuid;
            public bool development;
            public string[] scenes, projectDefines, extraScriptingDefines;
            public string[] skillRetentionAssets;
            public bool preloadedAssetsRestored;
            public int errors, warnings;
            public ulong bytes;
        }

        [MenuItem("Karawara/Validation/TOBE/Build QA Development")]
        public static void BuildDevelopment() => Build(true);

        [MenuItem("Karawara/Validation/TOBE/Build Ordinary Release Validation")]
        public static void BuildOrdinaryRelease() => Build(false);

        [MenuItem("Karawara/Validation/TOBE/Build QA Release Timing")]
        public static void BuildQaReleaseTiming() => Build(false, true);

        public static void BuildSkillRetentionDevelopment() => Build(true, false, true);

        static void Build(bool development, bool qaRelease = false, bool skillRetention = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building.");
            string[] defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (defines.Contains("KARAWARA_QA"))
                throw new InvalidOperationException("Remove the global KARAWARA_QA define before validation; QA defines must remain build-local.");
            if (development && qaRelease) throw new ArgumentException("QA Release must not use Development options.");
            if (skillRetention && (!development || qaRelease)) throw new ArgumentException("Skill diagnostic requires its separate Development build.");
            string variant = skillRetention ? "WindowsSkillRetentionDevelopment" : development ? "WindowsDevelopment" : qaRelease ? "WindowsQaReleaseTiming" : "WindowsOrdinaryRelease";
            string outputSession = Guid.NewGuid().ToString("N");
            var previousPreloaded = PlayerSettings.GetPreloadedAssets();
            var report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"), result = "running", development = development,
                output = Path.GetFullPath("Builds/TOBE-QA/BuildSessions/" + outputSession + "/" + variant + "/Karawara.exe"),
                unityVersion = Application.unityVersion, projectDefines = defines,
                extraScriptingDefines = qaRelease ? new[] { "KARAWARA_QA" } : Array.Empty<string>(),
                skillRetentionAssets = skillRetention ? new[] { "Assets/Scripts/SkillTree/Data/PetSpirit.asset", "Assets/Scripts/SkillTree/Data/AttackFogo.asset", "Assets/Scripts/SkillTree/Data/Escudo.asset", "Assets/Scripts/SkillTree/Data/SkillJacare.asset" } : Array.Empty<string>(),
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()
            };
            string reportPath = "Docs/Validation/TOBE/Execution/" + (skillRetention ? "qa-skill-retention-build" : development ? "qa-development-build" : qaRelease ? "qa-release-timing-build" : "ordinary-release-build") + ".json";
            Write(reportPath, report);
            try
            {
                if (report.scenes.Length == 0 || report.scenes.Any(s => !File.Exists(s)))
                    throw new InvalidOperationException("Enabled production scene list is empty or contains missing files.");
                Directory.CreateDirectory(Path.GetDirectoryName(report.output));
                if (skillRetention)
                {
                    var fixtures = report.skillRetentionAssets.Select(p => AssetDatabase.LoadAssetAtPath<RPGKarawara.Skill>(p)).ToArray();
                    if (fixtures.Any(s => s == null)) throw new InvalidOperationException("Missing authored diagnostic skill asset.");
                    PlayerSettings.SetPreloadedAssets(previousPreloaded.Concat(fixtures).Distinct().ToArray());
                }
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = report.scenes, locationPathName = report.output,
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    extraScriptingDefines = report.extraScriptingDefines
                });
                if (build == null) throw new InvalidOperationException("Unity returned no build report.");
                report.result = build.summary.result.ToString();
                report.errors = build.summary.totalErrors;
                report.warnings = build.summary.totalWarnings;
                report.bytes = build.summary.totalSize;
                report.buildGuid = build.summary.guid.ToString();
                report.error = string.Join("\n", build.steps.SelectMany(s => s.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content));
            }
            catch (Exception error) { report.result = "Exception"; report.error = error.ToString(); }
            finally
            {
                if (skillRetention) PlayerSettings.SetPreloadedAssets(previousPreloaded);
                report.preloadedAssetsRestored = PlayerSettings.GetPreloadedAssets().SequenceEqual(previousPreloaded);
                if (!report.preloadedAssetsRestored) { report.result = "Exception"; report.error += " Preloaded asset restoration failed."; }
                report.finishedUtc = DateTime.UtcNow.ToString("O"); Write(reportPath, report);
            }
            if (report.result == BuildResult.Succeeded.ToString()) Debug.Log(variant + " validation build succeeded: " + report.output);
            else Debug.LogError(variant + " validation build failed: " + report.error);
        }

        static void Write(string path, Report report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
