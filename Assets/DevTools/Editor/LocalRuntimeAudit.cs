#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Karawara.DevTools.Editor
{
    /// <summary>Read-only dependency inventory. Does not migrate assets or start gameplay.</summary>
    public static class LocalRuntimeAudit
    {
        private static readonly Regex Tokens = new Regex(
            @"\b(?:Unity\.Netcode|NetworkVariable|NetworkBehaviour|NetworkObject|NetworkManager|NetworkTransform|IsOwner|IsServer|IsHost|ServerRpc|ClientRpc|OnNetworkSpawn|OnNetworkDespawn|StartHost|StartClient|SpawnedObjects)\b");
        // Preserve strings (including reflection names) and newlines; erase comments only.
        private static readonly Regex CommentsAndLiterals = new Regex(
            "@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/");

        [Serializable] public sealed class SourceEntry
        {
            public string path, sha256;
            public string[] tokens;
        }
        [Serializable] public sealed class TypeEntry
        {
            public string path, guid, type;
            public string[] networkFields, rpcMethods;
        }
        [Serializable] public sealed class AssetEntry
        {
            public string path, sha256;
            public string[] networkScriptGuids;
        }
        [Serializable] public sealed class SceneEntry
        {
            public string path;
            public int dependencyCount;
            public string[] networkAssets;
        }
        [Serializable] public sealed class Report
        {
            public string schema = "karawara.local-runtime-audit.v1";
            public string generatedUtc;
            public bool closurePassed;
            public SourceEntry[] sourceDependencies;
            public TypeEntry[] networkTypes;
            public AssetEntry[] serializedReferences;
            public SceneEntry[] sceneClosure;
            public string[] networkPackages;
            public string[] runtimeSourceDependencies, compiledNetworkReferences;
            public string[] limitations;
            public string manifestSha256, lockSha256;
        }

        public static string[] FindNetworkTokens(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string code = CommentsAndLiterals.Replace(source, match =>
                match.Value.StartsWith("//", StringComparison.Ordinal) || match.Value.StartsWith("/*", StringComparison.Ordinal)
                    ? Regex.Replace(match.Value, @"[^\r\n]", " ") : match.Value);
            var entries = new List<string>();
            string[] lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                foreach (Match match in Tokens.Matches(lines[i])) entries.Add((i + 1) + ":" + match.Value);
            return entries.ToArray();
        }

        [MenuItem("Karawara/Validation/TOBE/Audit Local Runtime")]
        public static void Run()
        {
            var report = Collect();
            string directory = "Docs/Validation/TOBE/Execution";
            Directory.CreateDirectory(directory);
            string path = directory + "/local-runtime-audit-" + Guid.NewGuid().ToString("N") + ".json";
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("Local Runtime audit: closure=" + report.closurePassed + "; sources=" + report.sourceDependencies.Length
                + "; types=" + report.networkTypes.Length + "; serialized assets=" + report.serializedReferences.Length + "; " + path);
        }

        public static Report Collect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Local runtime audit requires an idle Edit-mode Editor.");
            var sources = new List<SourceEntry>();
            var codeDependencies = new List<string>();
            foreach (string file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                string source = File.ReadAllText(file);
                string codeOnly = CommentsAndLiterals.Replace(source, match => Regex.Replace(match.Value, @"[^\r\n]", " "));
                if (Tokens.IsMatch(codeOnly)) codeDependencies.Add(file.Replace('\\', '/'));
                string[] tokens = FindNetworkTokens(source);
                if (tokens.Length > 0) sources.Add(new SourceEntry { path = file.Replace('\\', '/'), sha256 = Hash(file), tokens = tokens });
            }

            var types = new List<TypeEntry>();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript").OrderBy(g => g, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script == null ? null : script.GetClass();
                if (type == null || !IsNetworkType(type)) continue;
                types.Add(new TypeEntry
                {
                    path = path, guid = guid, type = type.FullName,
                    networkFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Where(f => IsNetworkType(f.FieldType)).Select(f => f.Name + ":" + f.FieldType).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                    rpcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Where(m => m.GetCustomAttributesData().Any(a => a.AttributeType.Namespace == "Unity.Netcode" && a.AttributeType.Name.EndsWith("RpcAttribute", StringComparison.Ordinal)))
                        .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray()
                });
            }
            var networkGuids = new HashSet<string>(types.Select(t => t.guid));
            networkGuids.UnionWith(LegacyNetworkingScripts.Guids);
            const string beforeMigration = "Docs/Validation/TOBE/Execution/local-runtime-audit-1fd6dabaa49b47bcace289d41e360115.json";
            if (File.Exists(beforeMigration))
                foreach (var oldType in JsonUtility.FromJson<Report>(File.ReadAllText(beforeMigration)).networkTypes)
                    if (oldType.path.StartsWith("Packages/", StringComparison.Ordinal)) networkGuids.Add(oldType.guid);
            var assets = new List<AssetEntry>();
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !p.StartsWith("Assets/_Recovery/", StringComparison.Ordinal)
                && (p.EndsWith(".prefab", StringComparison.Ordinal) || p.EndsWith(".unity", StringComparison.Ordinal) || p.EndsWith(".asset", StringComparison.Ordinal)))
                .OrderBy(p => p, StringComparer.Ordinal))
            {
                string text = File.ReadAllText(path);
                string[] matches = Regex.Matches(text, @"\bguid: ([0-9a-f]{32})\b").Cast<Match>()
                    .Select(m => m.Groups[1].Value).Where(networkGuids.Contains).Distinct().OrderBy(g => g, StringComparer.Ordinal).ToArray();
                if (matches.Length > 0) assets.Add(new AssetEntry { path = path, sha256 = Hash(path), networkScriptGuids = matches });
            }
            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/DevTools" }).Select(AssetDatabase.GUIDToAssetPath)).Distinct();
            var scenes = new List<SceneEntry>();
            var networkAssets = new HashSet<string>(assets.Select(a => a.path));
            foreach (string root in roots)
            {
                string[] dependencies = AssetDatabase.GetDependencies(root, true);
                scenes.Add(new SceneEntry { path = root, dependencyCount = dependencies.Length,
                    networkAssets = dependencies.Where(networkAssets.Contains).OrderBy(p => p, StringComparer.Ordinal).ToArray() });
            }
            string manifest = File.ReadAllText("Packages/manifest.json");
            string[] packages = Regex.Matches(manifest, "\"(com\\.unity\\.(?:netcode[^\"]*|transport|multiplayer[^\"]*))\"\\s*:\\s*\"([^\"]+)\"")
                .Cast<Match>().Select(m => m.Groups[1].Value + "@" + m.Groups[2].Value).ToArray();
            string[] compiledReferences = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("Assembly-CSharp", StringComparison.Ordinal) || a.GetName().Name.StartsWith("Karawara", StringComparison.Ordinal))
                .SelectMany(a => a.GetReferencedAssemblies().Where(r => r.Name.StartsWith("Unity.Netcode", StringComparison.Ordinal) || r.Name.StartsWith("Unity.Networking.Transport", StringComparison.Ordinal))
                    .Select(r => a.GetName().Name + " -> " + r.Name)).Distinct().ToArray();
            return new Report
            {
                generatedUtc = DateTime.UtcNow.ToString("O"), closurePassed = codeDependencies.Count == 0 && types.Count == 0 && assets.Count == 0 && packages.Length == 0 && compiledReferences.Length == 0,
                runtimeSourceDependencies = codeDependencies.ToArray(), compiledNetworkReferences = compiledReferences,
                sourceDependencies = sources.ToArray(), networkTypes = types.ToArray(), serializedReferences = assets.ToArray(),
                sceneClosure = scenes.ToArray(), manifestSha256 = Hash("Packages/manifest.json"), lockSha256 = Hash("Packages/packages-lock.json"),
                networkPackages = packages,
                limitations = new[] {
                    "Conservative lexical source inventory: strings retained; preprocessor branches not evaluated; vendor matches are not edit authorization.",
                    "Compiled inheritance and RPC/field metadata only for MonoScript.GetClass-resolvable types; helper classes require source review.",
                    "Serialized GUID occurrences are references, not live component counts; binary assets, effective prefab overrides and UnityEvent/animation-event targets require separate resolution.",
                    "Field writers/subscribers still require semantic receiver review against the migration map before each family is migrated.",
                    "closurePassed covers code symbols, compiled references, packages and current/historical package script GUIDs in owned serialized assets. Recovery scenes preserve historical evidence and are excluded. Operational validation strings are reported separately from code dependencies; runtime gameplay is a separate gate. No scene, prefab, save or package is modified." }
            };
        }

        private static bool IsNetworkType(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
                if (current.Namespace != null && (current.Namespace == "Unity.Netcode" || current.Namespace.StartsWith("Unity.Netcode.", StringComparison.Ordinal))) return true;
            return false;
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }
    }
}
#endif
