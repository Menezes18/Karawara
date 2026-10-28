using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Karawara.Core.Characters;

namespace RPGKarawara.DevTools.Performance
{
    [Serializable]
    public sealed class PerformanceProcessConfig
    {
        public string mode, quality;
        public int width, height, vsync, targetFrameRate;
        public CaptureConfig capture;
        public TutorialRehearsalConfig rehearsal;
        public string certifiedManifestJson;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string measurementArm;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool checkpointImages;
        public ProfilerAttributionConfig profilerAttribution;
        public WorldRetentionConfig retention;
        public SkillRetentionConfig skillRetention;
        public CucaProjectileRetentionConfig cucaProjectileRetention;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public RenderingInspectionConfig renderingInspection;
    }
    [Serializable] public sealed class CucaProjectileRetentionConfig
    {
        public string routeId, outputRoot, seedSaveJson;
        public int bursts, warmupSeconds, settleSeconds, observationTimeoutSeconds;
    }
    [Serializable] public sealed class ProfilerAttributionConfig
    {
        public double startSeconds, durationSeconds;
        public bool allocationCallstacks;
    }

    [Serializable] public sealed class WorldRetentionConfig
    {
        public string routeId, outputRoot, seedSaveJson;
        public int cycles, warmupSeconds, settleSeconds;
    }
    [Serializable] public sealed class SkillRetentionConfig
    {
        public string routeId, outputRoot, seedSaveJson;
        public string[] skillNames, skillTypes;
        public int cycles, warmupCycles, warmupSeconds, settleSeconds, cycleTimeoutSeconds;
        public bool snapshotControl, ownedDefenseGraph;
        public double controlIntervalSeconds = 3;
    }

    [Serializable] public sealed class TutorialRehearsalConfig
    {
        public string routeId, timelineJson, outputRoot, seedSaveJson;
        public double durationSeconds, maxLatenessSeconds;
        public bool requireObservedJump;
        public bool auditAiSpawnerReset;
        public int aiSpawnerResetCycles = 1;
        public bool auditMemorySnapshots;
        public RehearsalPoseCheckpoint[] checkpoints;
    }
    [Serializable] public sealed class RehearsalPoseCheckpoint
    {
        public double elapsed;
        public float[] position, cameraRotation;
        public float positionTolerance, cameraAngleTolerance;
    }

    [Serializable]
    public sealed class PerformanceProcessResponse
    {
        public bool handled, success;
        public string mode, saveRoot;
        public PerformanceProcessConfig config;
        public CaptureResult result;
        public List<string> errors = new List<string>();
    }

    // Explicit process entry only. Ordinary Release keeps the public no-op contract.
    public static class PerformanceProcessHarness
    {
        internal static bool IsCertifiedRouteMode(string mode) => mode == "video1-certified-route" || mode == "video1-route-attribution" || mode == "tutorial-certified-route";
        internal static bool IsCucaCertifiedRouteMode(string mode) => mode == "cuca-certified-route";
        internal static bool IsCucaMode(string mode) => IsCucaCertifiedRouteMode(mode) || mode == "cuca-path-inspection" || mode == "cuca-route-rehearsal";
        internal static string RehearsalScene(string mode) => IsCucaMode(mode) ? "CucaLoad" : IsVideoMode(mode) ? "Video1" : "Tutorial_Final";
        internal static bool IsUiRouteMode(string mode) => mode == "tutorial-ui-certified-route";
        internal static bool IsRehearsalMode(string mode) => IsUiRouteMode(mode) || IsCucaMode(mode) || mode == "tutorial-rehearsal" || mode == "tutorial-route-rehearsal" || mode == "video1-route-rehearsal" || IsCertifiedRouteMode(mode) || IsPathInspectionMode(mode);
        internal static bool IsPathInspectionMode(string mode) => mode == "cuca-path-inspection" || mode == "tutorial-path-inspection" || mode == "video1-path-inspection";
        internal static bool IsVideoMode(string mode) => mode == "video1-path-inspection" || mode == "video1-route-rehearsal" || mode == "video1-certified-route" || mode == "video1-route-attribution";
        internal static bool IsSeededMode(string mode) => IsUiRouteMode(mode) || IsCucaMode(mode) || IsVideoMode(mode) || mode == "tutorial-certified-route";
        internal static PerformanceRouteRehearsal.Checkpoint[] BuildRehearsalCheckpoints(TutorialRehearsalConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var declared = config.checkpoints ?? Array.Empty<RehearsalPoseCheckpoint>();
            if (declared.Length > 128) throw new ArgumentException("Bound route checkpoints to 128.");
            var result = new PerformanceRouteRehearsal.Checkpoint[declared.Length];
            double previous = -1;
            for (int i = 0; i < declared.Length; i++)
            {
                var checkpoint = declared[i];
                if (checkpoint == null || double.IsNaN(checkpoint.elapsed) || double.IsInfinity(checkpoint.elapsed) || checkpoint.elapsed < 0 ||
                    checkpoint.elapsed <= previous || checkpoint.elapsed > config.durationSeconds || checkpoint.position == null || checkpoint.position.Length != 3 ||
                    checkpoint.cameraRotation == null || checkpoint.cameraRotation.Length != 4 ||
                    !Finite(checkpoint.positionTolerance) || checkpoint.positionTolerance <= 0 || !Finite(checkpoint.cameraAngleTolerance) || checkpoint.cameraAngleTolerance <= 0)
                    throw new ArgumentException("Route checkpoints need ordered bounded time, scalar pose arrays and positive finite tolerances.");
                foreach (float value in checkpoint.position) if (!Finite(value)) throw new ArgumentException("Finite checkpoint position required.");
                double squared = 0;
                foreach (float value in checkpoint.cameraRotation) { if (!Finite(value)) throw new ArgumentException("Finite checkpoint camera rotation required."); squared += (double)value * value; }
                if (Math.Abs(squared - 1) > .01) throw new ArgumentException("Normalized checkpoint camera quaternion required.");
                result[i] = new PerformanceRouteRehearsal.Checkpoint(checkpoint.elapsed,
                    new Vector3(checkpoint.position[0], checkpoint.position[1], checkpoint.position[2]),
                    new Quaternion(checkpoint.cameraRotation[0], checkpoint.cameraRotation[1], checkpoint.cameraRotation[2], checkpoint.cameraRotation[3]),
                    checkpoint.positionTolerance, checkpoint.cameraAngleTolerance);
                previous = checkpoint.elapsed;
            }
            return result;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static void ValidateAiResetAudit(PerformanceProcessConfig config)
        {
            var audit = config.rehearsal;
            if (audit == null) return;
            if (!audit.auditAiSpawnerReset && (audit.aiSpawnerResetCycles != 1 || audit.auditMemorySnapshots))
                throw new ArgumentException("Extra AI reset cycles or snapshots require explicit reset audit activation.");
            if (audit.auditAiSpawnerReset && (config.mode != "video1-route-rehearsal" || audit.aiSpawnerResetCycles < 1 || audit.aiSpawnerResetCycles > 10))
                throw new ArgumentException("AI reset audit requires full Video1 rehearsal and one through ten cycles.");
        }
        internal static CharacterSaveData ParseVideoSeed(string json) => ParseSceneSeed(json, 2);
        internal static CharacterSaveData ParseTutorialSeed(string json) => ParseSceneSeed(json, 1);
        internal static CharacterSaveData ParseCucaSeed(string json) => ParseSceneSeed(json, 3);
        static CharacterSaveData ParseSceneSeed(string json, int expectedScene)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) throw new ArgumentException("Bounded explicit QA seed required.");
            var fields = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (string key in new[] { "sceneIndex", "currentHealth", "vitality", "endurance" })
                if (fields[key]?.Type != JTokenType.Integer) throw new ArgumentException("Integer QA seed field required: " + key);
            foreach (string key in new[] { "xPosition", "yPosition", "zPosition" })
                if (fields[key] == null || (fields[key].Type != JTokenType.Integer && fields[key].Type != JTokenType.Float) ||
                    float.IsNaN(fields[key].Value<float>()) || float.IsInfinity(fields[key].Value<float>())) throw new ArgumentException("Finite QA seed pose required.");
            if (fields["characterName"]?.Type != JTokenType.String) throw new ArgumentException("Explicit seed name required.");
            // Unity instance IDs are valid only in the process which produced them. Never let
            // JsonUtility turn a frozen foreign ID into an unrelated native Skill/Sprite.
            foreach (string key in SeedSkillFields)
            {
                if (fields[key] == null) continue;
                if (!(fields[key] is JArray entries) || entries.Count > 64) throw new ArgumentException("Bounded seed skill array required: " + key);
                foreach (var entry in entries)
                    if (entry.Type != JTokenType.Null && (!(entry is JObject reference) || reference.Count != 1 || reference["instanceID"]?.Type != JTokenType.Integer || reference["instanceID"].Value<long>() != 0))
                        throw new ArgumentException("Frozen QA seeds cannot contain process-local Unity skill references: " + key);
            }
            ReadSeedSkillBindings(fields);
            var data = JsonUtility.FromJson<CharacterSaveData>(json);
            if (data == null || data.sceneIndex != expectedScene || data.currentHealth <= 0 || data.vitality <= 0 || data.endurance <= 0 || string.IsNullOrWhiteSpace(data.characterName))
                throw new ArgumentException("Healthy scene" + expectedScene + " QA seed required.");
            _ = Convert.ToString(data.characterName);
            return data;
        }
        static readonly string[] SeedSkillFields = { "skillsSlot", "SkillAttack", "SkillDefense", "SkillSuport" };
        static JObject ReadSeedSkillBindings(JObject fields)
        {
            if (fields["qaSkillBindings"] == null) return null;
            if (!(fields["qaSkillBindings"] is JObject bindings)) throw new ArgumentException("Explicit QA skill bindings must be an object.");
            foreach (var property in bindings.Properties())
            {
                if (Array.IndexOf(SeedSkillFields, property.Name) < 0 || !(property.Value is JArray entries) || entries.Count > 64)
                    throw new ArgumentException("Unknown or unbounded QA skill binding: " + property.Name);
                foreach (var entry in entries)
                    if (entry.Type != JTokenType.Null && (!(entry is JObject asset) || asset.Count != 2 || asset["assetName"]?.Type != JTokenType.String || asset["type"]?.Type != JTokenType.String ||
                        string.IsNullOrWhiteSpace((string)asset["assetName"]) || string.IsNullOrWhiteSpace((string)asset["type"]) || ((string)asset["assetName"]).Length > 256 || ((string)asset["type"]).Length > 256))
                        throw new ArgumentException("QA skill binding requires an explicit asset name and concrete type.");
            }
            return bindings;
        }
        // Only explicit QA seed preparation uses these bindings. Legacy saves and their
        // unresolved cross-process persistence contract are deliberately untouched.
        internal static CharacterSaveData BindVideoSeed(string json, Skill[] authoredCatalog) => BindSceneSeed(json, authoredCatalog, 2);
        internal static CharacterSaveData BindTutorialSeed(string json, Skill[] authoredCatalog) => BindSceneSeed(json, authoredCatalog, 1);
        internal static CharacterSaveData BindCucaSeed(string json, Skill[] authoredCatalog) => BindSceneSeed(json, authoredCatalog, 3);
        static CharacterSaveData BindSceneSeed(string json, Skill[] authoredCatalog, int expectedScene)
        {
            var data = ParseSceneSeed(json, expectedScene);
            var bindings = ReadSeedSkillBindings(JObject.Parse(json));
            if (bindings == null) return data;
            foreach (var property in bindings.Properties())
            {
                var entries = (JArray)property.Value;
                var skills = new Skill[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Type == JTokenType.Null) continue;
                    Skill match = null;
                    foreach (var candidate in authoredCatalog ?? Array.Empty<Skill>())
                    {
                        if (candidate == null || ((UnityEngine.Object)candidate).name != (string)entries[i]["assetName"] || candidate.GetType().FullName != (string)entries[i]["type"]) continue;
                        if (match != null && !ReferenceEquals(match, candidate)) throw new ArgumentException("Ambiguous authored QA skill: " + entries[i]);
                        match = candidate;
                    }
                    if (match == null) throw new ArgumentException("Missing authored QA skill: " + entries[i]);
                    skills[i] = match;
                }
                switch (property.Name)
                {
                    case "skillsSlot": data.skillsSlot = skills; break;
                    case "SkillAttack": data.SkillAttack = skills; break;
                    case "SkillDefense": data.SkillDefense = skills; break;
                    case "SkillSuport": data.SkillSuport = skills; break;
                }
            }
            return data;
        }
        public static PerformanceProcessResponse ParseRequest(string[] args, string personalRoot)
        {
            var response = new PerformanceProcessResponse();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            args = args ?? Array.Empty<string>();
            int activation = Count(args, "-karawaraPerformanceConfig");
            if (activation == 0) return response;
            response.handled = true;
            try
            {
                if (activation != 1 || Count(args, "-karawaraSaveRoot") != 1)
                    throw new ArgumentException("Exactly one performance config and isolated save root are required.");
                if (Count(args, "-karawaraHostSaveRegression") != 0 || Count(args, "-karawaraSaveRegression") != 0)
                    throw new ArgumentException("Do not combine independent QA bootstraps.");
                string configPath = Absolute(Value(args, "-karawaraPerformanceConfig"));
                string save = Absolute(Value(args, "-karawaraSaveRoot"));
                string personal = Absolute(personalRoot);
                Separate(save, personal);
                RejectLinks(save);
                RejectLinks(configPath);
                if (!File.Exists(configPath) || new FileInfo(configPath).Length > 1024 * 1024)
                    throw new ArgumentException("Config file is missing or exceeds 1 MiB.");
                var json = JObject.Parse(File.ReadAllText(configPath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var config = json.ToObject<PerformanceProcessConfig>();
                if (config == null) throw new ArgumentException("Performance config is required.");
                config.checkpointImages = PerformanceCheckpointImages.ValidateRequest(config.mode, config.measurementArm, json["checkpointImages"], config.profilerAttribution != null);
                if (config.measurementArm != null && config.measurementArm != "full" && config.measurementArm != "light-control")
                    throw new ArgumentException("Unknown measurement arm.");
                if (config.measurementArm == "light-control" && config.mode != "tutorial-certified-route" && config.mode != "tutorial-ui-certified-route" && config.mode != "cuca-certified-route")
                    throw new ArgumentException("Light measurement control currently supports only explicit Tutorial exploration, UI or Cuca workloads.");
                if (config.renderingInspection != null) PerformanceRenderingInventory.ValidateRequest(config, json["renderingInspection"].ToString(Formatting.None));
                ValidateAiResetAudit(config);
                if (config.mode != "inventory" && config.mode != "stationary-scene-menu" && config.mode != "world-retention" && config.mode != "skill-retention" && config.mode != "cuca-projectile-retention" && !IsRehearsalMode(config.mode))
                    throw new ArgumentException("Unsupported performance process mode.");
                if (config.width != 1920 || config.height != 1080 || config.quality != "High Fidelity" ||
                    Array.IndexOf(QualitySettings.names, config.quality) < 0 || config.vsync != 0 || config.targetFrameRate != -1)
                    throw new ArgumentException("Require 1920x1080, High Fidelity, VSync 0 and uncapped target frame rate.");
                if (config.mode == "cuca-projectile-retention")
                {
                    string[] allowed = { "mode", "quality", "width", "height", "vsync", "targetFrameRate", "cucaProjectileRetention" };
                    var protocol = config.cucaProjectileRetention; var fields = json["cucaProjectileRetention"] as JObject;
                    if (json.Properties().Any(p => Array.IndexOf(allowed, p.Name) < 0) || protocol == null || fields == null ||
                        fields.Properties().Any(p => typeof(CucaProjectileRetentionConfig).GetField(p.Name) == null) || typeof(CucaProjectileRetentionConfig).GetFields().Any(f => fields[f.Name] == null || fields[f.Name].Type == JTokenType.Null) ||
                        new[] { "bursts", "warmupSeconds", "settleSeconds", "observationTimeoutSeconds" }.Any(n => fields[n]?.Type != JTokenType.Integer) ||
                        protocol.routeId != "cuca-natural-projectile-retention-v1" || protocol.bursts != 10 || protocol.warmupSeconds != 30 || protocol.settleSeconds != 5 || protocol.observationTimeoutSeconds != 360)
                        throw new ArgumentException("Explicit natural ten-burst Cuca diagnostic with 30-second warmup, 5-second observation and bounded360-second window required.");
                    ParseCucaSeed(protocol.seedSaveJson);
                    string projectileOutput = Absolute(protocol.outputRoot); Separate(projectileOutput, personal); Separate(projectileOutput, save); RejectLinks(projectileOutput);
                    if (File.Exists(save) || File.Exists(projectileOutput) || (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0) || (Directory.Exists(projectileOutput) && Directory.GetFileSystemEntries(projectileOutput).Length != 0))
                        throw new ArgumentException("Projectile diagnostic requires fresh empty isolated save/output roots.");
                    protocol.outputRoot = projectileOutput; response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true; return response;
                }
                if (config.cucaProjectileRetention != null) throw new ArgumentException("Cuca projectile retention metadata requires its explicit diagnostic mode.");
                if (config.mode == "skill-retention")
                {
                    string[] allowed = { "mode", "quality", "width", "height", "vsync", "targetFrameRate", "skillRetention" };
                    var protocol = config.skillRetention; var fields = json["skillRetention"] as JObject;
                    if (json.Properties().Any(p => Array.IndexOf(allowed, p.Name) < 0) || protocol == null || fields == null ||
                        fields.Properties().Any(p => typeof(SkillRetentionConfig).GetField(p.Name) == null) || typeof(SkillRetentionConfig).GetFields().Where(f => f.Name != "snapshotControl" && f.Name != "controlIntervalSeconds" && f.Name != "ownedDefenseGraph").Any(f => fields[f.Name] == null || fields[f.Name].Type == JTokenType.Null) ||
                        (fields["snapshotControl"] != null && fields["snapshotControl"].Type != JTokenType.Boolean) ||
                        (fields["ownedDefenseGraph"] != null && fields["ownedDefenseGraph"].Type != JTokenType.Boolean) || (protocol.ownedDefenseGraph && protocol.snapshotControl) ||
                        (fields["controlIntervalSeconds"] != null && (!protocol.snapshotControl || (fields["controlIntervalSeconds"].Type != JTokenType.Integer && fields["controlIntervalSeconds"].Type != JTokenType.Float))) ||
                        double.IsNaN(protocol.controlIntervalSeconds) || double.IsInfinity(protocol.controlIntervalSeconds) || protocol.controlIntervalSeconds < 3 || protocol.controlIntervalSeconds > 600 ||
                        (protocol.snapshotControl ? protocol.routeId != "video1-skill-snapshot-control-v1" || protocol.cycles != 0 : protocol.routeId != (protocol.ownedDefenseGraph ? "video1-skill-owned-graph-retention-v2" : "video1-skill-retention-v1") || (protocol.cycles != 10 && protocol.cycles != 20)) || protocol.warmupCycles != 2 || protocol.warmupSeconds != 30 || protocol.settleSeconds != 3 || protocol.cycleTimeoutSeconds != 90)
                        throw new ArgumentException("Explicit isolated ten- or twenty-cycle skill protocol with two warm-up cycles, 30-second initial warm-up and bounded90-second cycles required.");
                    string[] types = { "RPGKarawara.SkillTree.AttackSkill", "RPGKarawara.SkillTree.DefenseSkill", "RPGKarawara.SkillTree.SupportSkill" };
                    if (protocol.skillNames == null || protocol.skillTypes == null || protocol.skillNames.Length != 3 || !protocol.skillTypes.SequenceEqual(types) ||
                        protocol.skillNames.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 256) || protocol.skillNames.Distinct(StringComparer.Ordinal).Count() != 3)
                        throw new ArgumentException("Three unique exact authored attack/defense/support references required.");
                    ParseVideoSeed(protocol.seedSaveJson);
                    var bindings = JObject.Parse(protocol.seedSaveJson)["qaSkillBindings"]?["skillsSlot"] as JArray;
                    if (bindings == null || bindings.Count != 3 || bindings.Where((b, i) => (string)b?["assetName"] != protocol.skillNames[i] || (string)b?["type"] != protocol.skillTypes[i]).Any())
                        throw new ArgumentException("Seed slots must bind the exact declared authored workload.");
                    string skillOutput = Absolute(protocol.outputRoot); Separate(skillOutput, personal); Separate(skillOutput, save); RejectLinks(skillOutput);
                    if (File.Exists(save) || File.Exists(skillOutput) || (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0) || (Directory.Exists(skillOutput) && Directory.GetFileSystemEntries(skillOutput).Length != 0))
                        throw new ArgumentException("Skill retention requires fresh empty isolated save/output roots.");
                    protocol.outputRoot = skillOutput; response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true; return response;
                }
                if (config.skillRetention != null) throw new ArgumentException("Skill retention metadata requires its explicit diagnostic mode.");
                if (config.mode == "world-retention")
                {
                    var retention = config.retention;
                    if (retention == null || config.capture != null || config.rehearsal != null || retention.routeId != "video1-menu-world-retention-v1" ||
                        retention.cycles != 10 || retention.warmupSeconds != 30 || retention.settleSeconds != 3)
                        throw new ArgumentException("Explicit ten-cycle world retention with 30-second warm-up and 3-second settling required, without capture/rehearsal.");
                    ParseVideoSeed(retention.seedSaveJson);
                    if (JObject.Parse(retention.seedSaveJson)["qaSkillBindings"] != null)
                        throw new ArgumentException("World-retention does not yet bind authored QA skills; use a supported seed, never silently omit declared skills.");
                    string retentionOutput = Absolute(retention.outputRoot);
                    Separate(retentionOutput, personal); Separate(retentionOutput, save); RejectLinks(retentionOutput);
                    if (File.Exists(save) || File.Exists(retentionOutput) ||
                        (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0) ||
                        (Directory.Exists(retentionOutput) && Directory.GetFileSystemEntries(retentionOutput).Length != 0))
                        throw new ArgumentException("Retention requires fresh empty isolated roots.");
                    retention.outputRoot = retentionOutput;
                    response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true; return response;
                }
                if (config.retention != null) throw new ArgumentException("Retention config is only valid in world-retention mode.");
                if (config.mode != "video1-route-attribution" && config.profilerAttribution != null) throw new ArgumentException("Profiler attribution requires its explicit diagnostic mode; timing mode cannot consume it.");
                if (IsCucaCertifiedRouteMode(config.mode))
                {
                    PerformanceCucaCertifiedRouteProcess.ValidateRequest(config);
                    string cucaOutput = Absolute(config.rehearsal.outputRoot);
                    Separate(cucaOutput, personal); Separate(cucaOutput, save); RejectLinks(cucaOutput);
                    if (File.Exists(save) || File.Exists(cucaOutput) ||
                        (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0) ||
                        (Directory.Exists(cucaOutput) && Directory.GetFileSystemEntries(cucaOutput).Length != 0))
                        throw new ArgumentException("Cuca route requires fresh isolated save/output roots.");
                    config.rehearsal.outputRoot = cucaOutput;
                    response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true;
                    return response;
                }
                if (IsUiRouteMode(config.mode))
                {
                    PerformanceUiRouteProcess.ValidateRequest(config);
                    string uiOutput = Absolute(config.rehearsal.outputRoot);
                    Separate(uiOutput, personal); Separate(uiOutput, save); RejectLinks(uiOutput);
                    if (File.Exists(save) || File.Exists(uiOutput) ||
                        (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0) ||
                        (Directory.Exists(uiOutput) && Directory.GetFileSystemEntries(uiOutput).Length != 0))
                        throw new ArgumentException("UI route requires fresh isolated save/output roots.");
                    config.rehearsal.outputRoot = uiOutput;
                    response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true;
                    return response;
                }
                if (IsCertifiedRouteMode(config.mode))
                {
                    if (config.mode == "video1-route-attribution")
                    {
                        if (!(json["profilerAttribution"] is JObject attribution) || attribution.Count != 3 || attribution["startSeconds"] == null || attribution["durationSeconds"] == null || attribution["allocationCallstacks"]?.Type != JTokenType.Boolean)
                            throw new ArgumentException("Explicit bounded profiler window and callstack setting required.");
                        PerformanceProfilerAttribution.ValidateWindow(config.profilerAttribution.startSeconds, config.profilerAttribution.durationSeconds);
                    }
                    var route = config.rehearsal;
                    string expectedRoute = config.mode == "tutorial-certified-route" ? "tutorial-exploration-v1" : "video1-exploration-baseline-v1";
                    if (route == null || config.capture != null || route.routeId != expectedRoute || route.durationSeconds != 150 || route.auditAiSpawnerReset || route.auditMemorySnapshots ||
                        (route.checkpoints != null && route.checkpoints.Length != 0)) throw new ArgumentException("Certified full route requires only explicit manifest, replay/seed and spatial waypoints, without diagnostic audits/checkpoints.");
                    // Shape/identity preparation only. Begin compares the actual loaded Player assembly.
                    var declaration = JObject.Parse(config.certifiedManifestJson ?? "{}");
                    var manifest = PerformanceCertifiedRouteProcess.ParseManifest(config.certifiedManifestJson, route.timelineJson, route.seedSaveJson, (string)declaration["buildAssemblySha256"], (string)declaration["playerBuildGuid"], (string)declaration["distributionSha256"]).Snapshot;
                    if (manifest.routeId != route.routeId || manifest.scene != (IsVideoMode(config.mode) ? "Video1" : "Tutorial_Final"))
                        throw new ArgumentException("Certified mode, route and scene must match exactly.");
                    if (route.maxLatenessSeconds != manifest.maxLatenessSeconds) throw new ArgumentException("Input lateness differs from frozen manifest.");
                    string routeOutput = Absolute(route.outputRoot);
                    Separate(routeOutput, personal); Separate(routeOutput, save); RejectLinks(routeOutput);
                    if (File.Exists(save) || File.Exists(routeOutput) || (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0) || (Directory.Exists(routeOutput) && Directory.GetFileSystemEntries(routeOutput).Length != 0))
                        throw new ArgumentException("Certified route requires fresh isolated save/output roots.");
                    route.outputRoot = routeOutput;
                    response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true; return response;
                }
                if (config.certifiedManifestJson != null) throw new ArgumentException("Certification manifest is only valid in explicit certified-route mode.");
                if (IsRehearsalMode(config.mode))
                {
                    var rehearsal = config.rehearsal;
                    bool fullRoute = config.mode == "tutorial-route-rehearsal" || config.mode == "video1-route-rehearsal" || config.mode == "cuca-route-rehearsal";
                    bool inspection = IsPathInspectionMode(config.mode);
                    bool video = IsVideoMode(config.mode);
                    string routeId = IsCucaMode(config.mode) ? (fullRoute ? "cuca-encounter-discovery-v1" : "cuca-path-inspection-v1") : video ? (fullRoute ? "video1-exploration-discovery-v1" : "video1-path-inspection-v1") : fullRoute ? "tutorial-exploration-discovery-v1" : inspection ? "tutorial-path-inspection-v1" : "tutorial-controls-functional-v1";
                    if (rehearsal == null || config.capture != null || rehearsal.routeId != routeId)
                        throw new ArgumentException("Explicit Tutorial rehearsal required, without a timing capture config.");
                    PerformanceRouteRehearsal.ValidateSchedule(rehearsal.timelineJson, rehearsal.durationSeconds, rehearsal.maxLatenessSeconds);
                    BuildRehearsalCheckpoints(rehearsal);
                    if (IsCucaMode(config.mode)) ParseCucaSeed(rehearsal.seedSaveJson);
                    else if (video) ParseVideoSeed(rehearsal.seedSaveJson);
                    else if (!string.IsNullOrEmpty(rehearsal.seedSaveJson)) throw new ArgumentException("Tutorial new-game discovery does not accept a seed.");
                    if (rehearsal.durationSeconds != (fullRoute ? 150 : inspection ? 20 : 7))
                        throw new ArgumentException("Control/path/full-route rehearsal requires 7/20/150 seconds respectively.");
                    string rehearsalOutput = Absolute(rehearsal.outputRoot);
                    Separate(rehearsalOutput, personal); Separate(rehearsalOutput, save); RejectLinks(rehearsalOutput);
                    if (File.Exists(save) || File.Exists(rehearsalOutput) ||
                        (Directory.Exists(save) && Directory.GetFileSystemEntries(save).Length != 0))
                        throw new ArgumentException("Fresh empty QA save root and directory output required.");
                    rehearsal.outputRoot = rehearsalOutput;
                    response.config = config; response.mode = config.mode; response.saveRoot = save; response.success = true;
                    return response;
                }
                if (config.capture == null || config.rehearsal != null) throw new ArgumentException("Capture config required without a rehearsal config.");
                var capture = config.capture;
                var captureJson = json["capture"] as JObject;
                if (captureJson == null || captureJson["warmupSeconds"] == null || captureJson["captureSeconds"] == null ||
                    captureJson["renderScale"] == null || captureJson["qaEnabled"] == null)
                    throw new ArgumentException("Explicit timing, render scale and QA fields are required.");
                if (!capture.qaEnabled || capture.warmupSeconds != 30 || capture.captureSeconds != 120 || capture.renderScale != 1 ||
                    capture.enemyCount < 0 || string.IsNullOrWhiteSpace(capture.commit) || string.IsNullOrWhiteSpace(capture.buildId) ||
                    string.IsNullOrWhiteSpace(capture.camera) || capture.routeId != "scene-menu-idle-v1")
                    throw new ArgumentException("Require explicit QA identities, scene-menu-idle-v1, render scale 1 and fixed 30+120 seconds.");
                string output = Absolute(capture.outputRoot);
                Separate(output, personal);
                Separate(output, save);
                RejectLinks(output);
                capture.outputRoot = output;
                response.config = config;
                response.mode = config.mode;
                response.saveRoot = save;
                response.success = true;
            }
            catch (Exception error) { response.errors.Add(error.GetType().Name + ": " + error.Message); }
#endif
            return response;
        }

        public static PerformanceProcessResponse ExecuteInventory(string[] args, string personalRoot)
        {
            var response = ParseRequest(args, personalRoot);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (!response.handled || !response.success) return response;
            response.success = false;
            if (response.mode != "inventory") { response.errors.Add("ExecuteInventory requires inventory mode."); return response; }
            GameObject owned = null;
            PerformanceCaptureController controller = null;
            try
            {
                owned = new GameObject("Karawara QA counter inventory") { hideFlags = HideFlags.HideAndDontSave };
                controller = owned.AddComponent<PerformanceCaptureController>();
                if (!controller.Begin(response.config.capture)) throw new InvalidOperationException("Inventory Begin rejected.");
                response.result = controller.End();
                response.success = response.result != null && response.result.errors.Count == 0 &&
                    File.Exists(response.result.inventoryPath) && File.Exists(response.result.jsonPath) && File.Exists(response.result.csvPath);
                if (!response.success) response.errors.Add("Inventory output failed.");
            }
            catch (Exception error) { response.errors.Add(error.GetType().Name + ": " + error.Message); }
            finally
            {
                if (controller != null) controller.End();
                if (owned != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(owned); else UnityEngine.Object.DestroyImmediate(owned); }
            }
#endif
            return response;
        }

        public static bool BindStationaryContext(PerformanceCaptureController controller)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (controller == null || controller.IsCapturing) return false;
            var previousContext = controller.GetComponent<PerformanceContextLifetime>();
            if (previousContext != null) previousContext.Release();
            controller.CameraIdProvider = null;
            controller.EnemyCountProvider = null;
            controller.FinalValidation = null;
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "SceneMenu") return false;
            var production = PlayerCamera.instance;
            if (production == null || production.cameraObject == null) return false;
            Camera sceneOutput = null;
            var all = Camera.allCameras;
            int outputCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var camera = all[i];
                if (!IsScreenOutput(camera)) continue;
                outputCount++;
                if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType != CameraRenderType.Base) return false;
                if (camera == production.cameraObject)
                {
                    if (!camera.transform.IsChildOf(production.transform)) return false;
                    continue;
                }
                if (camera.gameObject.scene != scene || sceneOutput != null) return false;
                sceneOutput = camera;
            }
            if (outputCount != 2 || sceneOutput == null || !IsScreenOutput(production.cameraObject)) return false;
            var outputs = new[] { production.cameraObject, sceneOutput };
            Array.Sort(outputs, (left, right) => {
                int display = left.targetDisplay.CompareTo(right.targetDisplay);
                return display != 0 ? display : left.depth.CompareTo(right.depth);
            });
            // Tied submissions cannot establish a reproducible order for this bounded diagnostic.
            if (outputs[0].targetDisplay == outputs[1].targetDisplay && outputs[0].depth == outputs[1].depth) return false;
            new FixedStationaryContext(controller, scene, outputs);
            return true;
#else
            return false;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        static bool IsScreenOutput(Camera candidate) => candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy && candidate.targetTexture == null;

        // Preflight serialization/discovery stays outside sampling. The provider only reads cached references/properties.
        sealed class CameraState
        {
            readonly Camera camera;
            readonly UniversalAdditionalCameraData data;
            readonly ScriptableRenderer renderer;
            readonly Camera[] stack;
            readonly Scene ownerScene;
            readonly Transform parent;
            readonly Rect viewport, pixelViewport;
            readonly float depth, fov, near, far, size;
            readonly int display, mask;
            readonly CameraClearFlags clear;
            readonly Color background;
            readonly bool orthographic;
            readonly Matrix4x4 projection;
            public readonly object descriptor;
            public CameraState(Camera camera, PlayerCamera production)
            {
                this.camera = camera;
                ownerScene = camera.gameObject.scene;
                parent = camera.transform.parent;
                if (!camera.TryGetComponent(out data) || data.renderType != CameraRenderType.Base)
                    throw new InvalidOperationException("Stationary camera requires authored URP Base data.");
                renderer = data.scriptableRenderer;
                var overlays = data.cameraStack;
                stack = overlays == null ? Array.Empty<Camera>() : overlays.ToArray();
                viewport = camera.rect; pixelViewport = camera.pixelRect;
                depth = camera.depth; fov = camera.fieldOfView; near = camera.nearClipPlane; far = camera.farClipPlane;
                size = camera.orthographicSize; display = camera.targetDisplay; mask = camera.cullingMask;
                clear = camera.clearFlags; background = camera.backgroundColor;
                orthographic = camera.orthographic; projection = camera.projectionMatrix;
                var stackIdentities = new string[stack.Length];
                for (int i = 0; i < stack.Length; i++) stackIdentities[i] = StableReference(stack[i], production);
                descriptor = new { role = production != null && production.cameraObject == camera ? "production-player-output" : "scene-output",
                    reference = StableReference(camera, production),
                    viewport = new[] { viewport.x, viewport.y, viewport.width, viewport.height },
                    pixelViewport = new[] { pixelViewport.x, pixelViewport.y, pixelViewport.width, pixelViewport.height }, depth, display, mask, clear,
                    targetTexture = "screen", fov, orthographic, size, near, far,
                    background = new[] { background.r, background.g, background.b, background.a },
                    projection = new[] { projection.m00, projection.m01, projection.m02, projection.m03,
                        projection.m10, projection.m11, projection.m12, projection.m13, projection.m20, projection.m21, projection.m22, projection.m23,
                        projection.m30, projection.m31, projection.m32, projection.m33 },
                    urpType = data.renderType.ToString(), renderer = renderer?.GetType().FullName,
                    pipelineAsset = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : null,
                    rendererIndex = JObject.Parse(JsonUtility.ToJson(data))["m_RendererIndex"]?.Value<int>(), orderedStack = stackIdentities };
            }
            public bool Matches()
            {
                if (!IsScreenOutput(camera) || camera.gameObject.scene != ownerScene || camera.transform.parent != parent || data == null ||
                    data.renderType != CameraRenderType.Base || data.scriptableRenderer != renderer ||
                    camera.rect != viewport || camera.pixelRect != pixelViewport || camera.depth != depth ||
                    camera.targetDisplay != display || camera.cullingMask != mask || camera.clearFlags != clear ||
                    camera.fieldOfView != fov || camera.orthographic != orthographic || camera.orthographicSize != size ||
                    camera.nearClipPlane != near || camera.farClipPlane != far || camera.backgroundColor != background ||
                    !camera.projectionMatrix.Equals(projection)) return false;
                var current = data.cameraStack;
                if ((current == null ? 0 : current.Count) != stack.Length) return false;
                for (int i = 0; i < stack.Length; i++) if (current[i] != stack[i]) return false;
                return true;
            }
        }
        static string StableReference(Camera camera, PlayerCamera production)
        {
            if (camera == null) return "missing";
            bool owned = production != null && camera.transform.IsChildOf(production.transform);
            string path = "";
            for (var current = camera.transform; current != null; current = current.parent)
            {
                path = current.name + (owned && current == production.transform ? "" : "[" + current.GetSiblingIndex() + "]") + "/" + path;
                if (owned && current == production.transform) break;
            }
            var components = camera.GetComponents<Camera>();
            return (owned ? "production-player-camera/" : camera.gameObject.scene.path + "/") + path + "Camera[" + Array.IndexOf(components, camera) + "]";
        }
        sealed class FixedStationaryContext : IDisposable
        {
            readonly Scene scene;
            readonly Camera[] outputs;
            readonly CameraState[] states;
            readonly PlayerCamera production;
            readonly Camera[] cameras;
            readonly bool[] initiallyRendering;
            readonly Camera productionOutput;
            readonly AICharacterManager[] enemies;
            readonly float timeScale;
            readonly int initialScreenCount;
            readonly string identity;
            bool cameraCreationObserved, disposed;

            public FixedStationaryContext(PerformanceCaptureController controller, Scene scene, Camera[] outputs)
            {
                this.scene = scene;
                this.outputs = (Camera[])outputs.Clone();
                states = new CameraState[outputs.Length];
                production = PlayerCamera.instance;
                productionOutput = production != null ? production.cameraObject : null;
                timeScale = Time.timeScale;
                cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                initiallyRendering = new bool[cameras.Length];
                for (int i = 0; i < cameras.Length; i++) initiallyRendering[i] = IsScreenOutput(cameras[i]);
                var found = UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var relevant = new List<AICharacterManager>();
                for (int i = 0; i < found.Length; i++) if (found[i].gameObject.scene == scene) relevant.Add(found[i]);
                enemies = relevant.ToArray();
                initialScreenCount = ScreenCount();
                var descriptors = new object[outputs.Length];
                for (int i = 0; i < outputs.Length; i++) { states[i] = new CameraState(outputs[i], production); descriptors[i] = states[i].descriptor; }
                identity = "scene-menu-idle-v1/camera-set-v1/" + JsonConvert.SerializeObject(descriptors);
                controller.CameraIdProvider = ReadCamera;
                controller.EnemyCountProvider = ReadPopulation;
                controller.FinalValidation = ValidateFinal;
                var lifetime = controller.GetComponent<PerformanceContextLifetime>();
                if (lifetime == null) lifetime = controller.gameObject.AddComponent<PerformanceContextLifetime>();
                lifetime.Initialize(Dispose);
                Camera.onPreCull += ObserveCamera;
                RenderPipelineManager.beginCameraRendering += ObserveSrpCamera;
            }
            bool Eligible(Camera candidate) => IsScreenOutput(candidate);
            bool OutputReferencesMatch()
            {
                if (PlayerCamera.instance != production || (production != null && production.cameraObject != productionOutput)) return false;
                for (int i = 0; i < outputs.Length; i++)
                {
                    if (!states[i].Matches()) return false;
                    if (outputs[i].gameObject.scene != scene && (production == null || production.cameraObject != outputs[i])) return false;
                }
                return true;
            }
            int ScreenCount()
            {
                int count = 0;
                for (int i = 0; i < cameras.Length; i++) if (Eligible(cameras[i])) count++;
                return count;
            }
            void ObserveSrpCamera(ScriptableRenderContext unused, Camera candidate) => ObserveCamera(candidate);
            void ObserveCamera(Camera candidate)
            {
                if (!Eligible(candidate)) return;
                for (int i = 0; i < outputs.Length; i++)
                    if (outputs[i] == candidate && !states[i].Matches()) cameraCreationObserved = true;
                for (int i = 0; i < cameras.Length; i++)
                    if (cameras[i] == candidate) { if (!initiallyRendering[i]) cameraCreationObserved = true; return; }
                cameraCreationObserved = true;
            }
            string ReadCamera()
            {
                if (disposed) throw new ObjectDisposedException("Stationary camera context");
                if (Time.timeScale != timeScale) throw new InvalidOperationException("Stationary timeScale changed.");
                if (cameraCreationObserved || ScreenCount() != initialScreenCount)
                    throw new InvalidOperationException("Stationary camera roster changed.");
                if (SceneManager.GetActiveScene() != scene || !OutputReferencesMatch())
                    throw new InvalidOperationException("Stationary camera set rendering properties/ownership changed.");
                return identity;
            }
            int ReadPopulation()
            {
                if (Time.timeScale != timeScale) throw new InvalidOperationException("Stationary timeScale changed.");
                int count = 0;
                for (int i = 0; i < enemies.Length; i++)
                    if (enemies[i] != null && enemies[i].isActiveAndEnabled && enemies[i].gameObject.scene == scene) count++;
                return count;
            }
            void ValidateFinal(CaptureResult result)
            {
                try
                {
                    bool cameraChanged = cameraCreationObserved || ScreenCount() != initialScreenCount || !OutputReferencesMatch();
                    var finalCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < finalCameras.Length; i++)
                    {
                        if (!Eligible(finalCameras[i])) continue;
                        bool known = false;
                        for (int j = 0; j < cameras.Length; j++) if (finalCameras[i] == cameras[j]) { known = true; break; }
                        cameraChanged |= !known;
                    }
                    if (cameraChanged)
                    {
                        result.cameraVerified = false;
                        result.conditionsChanged = true;
                        result.errors.Add("Stationary camera roster changed (render event/postflight).");
                    }
                    bool populationChanged = ReadPopulationWithoutPause() != result.conditions.enemyCount;
                    var finalEnemies = UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < finalEnemies.Length; i++)
                    {
                        var enemy = finalEnemies[i];
                        if (enemy.gameObject.scene != scene || !enemy.isActiveAndEnabled) continue;
                        bool known = false;
                        for (int j = 0; j < enemies.Length; j++) if (enemy == enemies[j]) { known = true; break; }
                        populationChanged |= !known;
                    }
                    // Endpoint scans cannot prove absence of transient unregistered AI creation.
                    result.populationVerified = false;
                    result.comparisonValid = false;
                    result.validationUnavailableReasons.Add("Stationary population verification unavailable: frozen roster/postflight cannot observe transient unregistered AI creation.");
                    if (populationChanged)
                    {
                        result.conditionsChanged = true;
                        result.errors.Add("Stationary population roster changed (postflight).");
                    }
                    if (Time.timeScale != timeScale)
                    {
                        result.conditionsChanged = true;
                        result.errors.Add("Stationary timeScale changed (postflight).");
                    }
                    if (SceneManager.GetActiveScene() != scene)
                    {
                        result.cameraVerified = false;
                        result.conditionsChanged = true;
                        result.errors.Add("Stationary scene changed (postflight).");
                    }
                }
                finally { Dispose(); }
            }
            int ReadPopulationWithoutPause()
            {
                int count = 0;
                for (int i = 0; i < enemies.Length; i++)
                    if (enemies[i] != null && enemies[i].isActiveAndEnabled && enemies[i].gameObject.scene == scene) count++;
                return count;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Camera.onPreCull -= ObserveCamera;
                RenderPipelineManager.beginCameraRendering -= ObserveSrpCamera;
            }
        }
        static int Count(string[] args, string flag) { int count = 0; for (int i = 0; i < args.Length; i++) if (args[i] == flag) count++; return count; }
        static string Value(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("-"))
                throw new ArgumentException("Missing value for " + flag);
            return args[index + 1];
        }
        static string Absolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new ArgumentException("Paths must be fully qualified.");
            string full = Path.GetFullPath(path);
            if (full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) == Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                throw new ArgumentException("Filesystem roots cannot be QA roots.");
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        static void Separate(string first, string second)
        {
            var comparison = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (first.Equals(second, comparison) || first.StartsWith(second + Path.DirectorySeparatorChar, comparison) || second.StartsWith(first + Path.DirectorySeparatorChar, comparison))
                throw new ArgumentException("Personal, QA save and output roots must not overlap.");
        }
        static void RejectLinks(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("QA paths cannot traverse symbolic links or junctions.");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareTutorialStartupDiagnostics()
        {
            var request = ParseRequest(Environment.GetCommandLineArgs(), Application.persistentDataPath);
            if (request.handled && request.success && (IsRehearsalMode(request.mode) || request.mode == "world-retention" || request.mode == "skill-retention" || request.mode == "cuca-projectile-retention"))
                PerformanceTutorialProcess.PrepareStartupDiagnostics();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            var request = ParseRequest(args, Application.persistentDataPath);
            if (!request.handled) return;
            if (!request.success) { Fail(string.Join("; ", request.errors)); return; }
            if (request.mode == "cuca-projectile-retention")
            {
                var retention = new GameObject("Karawara QA Cuca projectile retention process");
                UnityEngine.Object.DontDestroyOnLoad(retention);
                retention.AddComponent<PerformanceCucaProjectileRetentionProcess>().Initialize(request); return;
            }
            if (request.mode == "skill-retention")
            {
                var retention = new GameObject("Karawara QA skill retention process");
                UnityEngine.Object.DontDestroyOnLoad(retention);
                retention.AddComponent<PerformanceSkillRetentionProcess>().Initialize(request); return;
            }
            if (request.mode == "world-retention")
            {
                var retention = new GameObject("Karawara QA world retention process");
                UnityEngine.Object.DontDestroyOnLoad(retention);
                retention.AddComponent<PerformanceWorldRetentionProcess>().Initialize(request); return;
            }
            if (IsRehearsalMode(request.mode))
            {
                var rehearsal = new GameObject("Karawara QA Tutorial process");
                UnityEngine.Object.DontDestroyOnLoad(rehearsal);
                rehearsal.AddComponent<PerformanceTutorialProcess>().Initialize(request);
                return;
            }
            if (request.mode == "inventory")
            {
                var inventory = ExecuteInventory(args, Application.persistentDataPath);
                if (!inventory.success) Fail(string.Join("; ", inventory.errors));
                else { Debug.Log("QA inventory complete: " + inventory.result.jsonPath); if (!Application.isEditor) Application.Quit(0); }
                return;
            }
            var owned = new GameObject("Karawara QA stationary process") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(owned);
            owned.AddComponent<PerformanceStationaryProcess>().Initialize(request);
        }
        internal static void Fail(string reason) { Debug.LogError("QA performance process failed: " + reason); if (!Application.isEditor) Application.Quit(2); }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Ensures event subscriptions are released even if a bound context never begins capturing.
    public sealed class PerformanceContextLifetime : MonoBehaviour
    {
        Action release;
        public void Initialize(Action cleanup) { Release(); release = cleanup; }
        public void Release() { var cleanup = release; release = null; cleanup?.Invoke(); }
        void OnDisable() => Release();
        void OnDestroy() => Release();
    }
    // No scene attachment, host startup, input injection or character placement.
    public sealed class PerformanceStationaryProcess : MonoBehaviour
    {
        PerformanceProcessResponse request;
        PerformanceCaptureController controller;
        int previousQuality, previousVsync, previousFrameRate;
        bool configured;
        public void Initialize(PerformanceProcessResponse value) { request = value; StartCoroutine(Run()); }
        IEnumerator Run()
        {
            previousQuality = QualitySettings.GetQualityLevel();
            previousVsync = QualitySettings.vSyncCount;
            previousFrameRate = Application.targetFrameRate;
            configured = true;
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, request.config.quality), true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            if (!Application.isEditor) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            controller = gameObject.AddComponent<PerformanceCaptureController>();
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            bool ready = false;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsActive) { Abort("Stationary SceneMenu requires no active host/client."); yield break; }
                if (SettingsMatch() && PerformanceProcessHarness.BindStationaryContext(controller)) { ready = true; break; }
                yield return null;
            }
            if (!ready) { Abort("Settings/rendering camera readiness timed out (15 seconds)."); yield break; }
            request.config.capture.camera = controller.CameraIdProvider();
            request.config.capture.enemyCount = controller.EnemyCountProvider();
            if (!controller.Begin(request.config.capture)) { Abort("Stationary Begin rejected."); yield break; }
            double watchdog = Time.realtimeSinceStartupAsDouble + 165;
            while (controller.IsCapturing)
            {
                if (Time.realtimeSinceStartupAsDouble > watchdog || SceneManager.GetActiveScene().name != "SceneMenu" ||
                    (GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsActive))
                { Abort("Stationary scene/host/watchdog boundary failed."); yield break; }
                yield return null;
            }
            var result = controller.LastResult;
            bool success = result != null && result.fullWindowCompleted && !result.conditionsChanged && result.errors.Count == 0;
            if (success) Debug.Log("QA stationary SceneMenu diagnostic complete: " + result.jsonPath);
            else PerformanceProcessHarness.Fail("Stationary diagnostic did not complete with stable conditions.");
            Destroy(gameObject);
            if (success && !Application.isEditor) Application.Quit(0);
        }
        bool SettingsMatch() => Screen.width == 1920 && Screen.height == 1080 && QualitySettings.names[QualitySettings.GetQualityLevel()] == "High Fidelity" &&
            QualitySettings.vSyncCount == 0 && Application.targetFrameRate == -1 &&
            Math.Abs(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale - 1 : 0) < 0.0001;
        void Abort(string reason) { if (controller != null) controller.End(); PerformanceProcessHarness.Fail(reason); Destroy(gameObject); }
        void OnDestroy()
        {
            if (controller != null) controller.End();
            if (configured && Application.isEditor)
            {
                QualitySettings.SetQualityLevel(previousQuality, true);
                QualitySettings.vSyncCount = previousVsync;
                Application.targetFrameRate = previousFrameRate;
            }
        }
    }
#endif
}



