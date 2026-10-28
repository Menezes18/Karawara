using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    // Local Editor isolation experiment, never a standalone retention certificate.
    public static class PerformanceVfxBatchProbe
    {
        static VisualEffectAsset asset;
        static GameObject clone, cameraRoot;
        static readonly List<object> rows = new List<object>();
        static double next, started;
        static int cycle;
        static bool destroying;
        static bool coldIntervals;
        static bool requestedInstancing;
        static int targetCycles;
        static string output, assetPath;
        public static string Begin(bool cold = false, bool allowInstancing = true)
        {
            if (!Application.isPlaying || output != null || Object.FindObjectsByType<RPGKarawara.PlayerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0)
                throw new InvalidOperationException("Exclusive empty Play scene required.");
            assetPath = AssetDatabase.GUIDToAssetPath("db3b70c1d988d02418aec685d4507a91");
            asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(assetPath);
            if (asset == null || asset.name != "ShieldTurtle") throw new InvalidOperationException("Exact authored ShieldTurtle graph required.");
            output = Path.GetFullPath("Docs/Validation/TOBE/Execution/vfx-batch-isolation-" + Guid.NewGuid().ToString("N") + ".json");
            cameraRoot = new GameObject("Owned VFX isolation camera");
            var camera = cameraRoot.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 1, -4); camera.transform.LookAt(Vector3.zero);
            rows.Clear(); cycle = 0; destroying = false; coldIntervals = cold; requestedInstancing = allowInstancing; targetCycles = cold ? 12 : 82; started = EditorApplication.timeSinceStartup;
            Observe("before-any-activation"); next = started;
            EditorApplication.update += Tick;
            return output;
        }
        static void Observe(string phase)
        {
            var info = VFXManager.GetBatchedEffectInfo(asset);
            int liveGraphComponents = 0;
            foreach (var effect in Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (effect.visualEffectAsset == asset) liveGraphComponents++;
            rows.Add(new { phase, cycle, frame = Time.frameCount, elapsed = EditorApplication.timeSinceStartup - started,
                assetId = asset.GetInstanceID(), reportedNativeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(asset),
                batchAssetMatched = info.vfxAsset == asset, returnedAssetId = info.vfxAsset == null ? 0 : info.vfxAsset.GetInstanceID(),
                info.activeBatchCount, info.inactiveBatchCount, info.activeInstanceCount, info.unbatchedInstanceCount,
                info.totalInstanceCapacity, info.maxInstancePerBatchCapacity, info.totalGPUSizeInBytes, info.totalCPUSizeInBytes,
                cloneAlive = clone != null, liveGraphComponents, requestedInstancing,
                actualCloneInstancing = clone == null ? (bool?)null : ReadInstancing(clone.GetComponent<VisualEffect>()) });
        }
        static void Tick()
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup - started > 120) throw new TimeoutException("VFX isolation stopped or exceeded bound.");
                if (EditorApplication.timeSinceStartup < next) return;
                if (cycle >= targetCycles && !destroying) { Observe("final-idle"); Finish(null); return; }
                if (!destroying)
                {
                    Observe(cycle == 2 ? "baseline-after-two-warmup" : "idle-before-next-activation");
                    clone = new GameObject("Owned graph-only instance " + cycle);
                    clone.SetActive(false);
                    var effect = clone.AddComponent<VisualEffect>();
                    using (var serialized = new SerializedObject(effect))
                    {
                        var flag = serialized.FindProperty("m_AllowInstancing");
                        if (flag == null || flag.propertyType != SerializedPropertyType.Boolean)
                            throw new InvalidOperationException("Installed VFX instancing flag unavailable.");
                        flag.boolValue = requestedInstancing; serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (ReadInstancing(effect) != requestedInstancing) throw new InvalidOperationException("Instancing readback mismatch.");
                    effect.visualEffectAsset = asset;
                    clone.SetActive(true);
                    destroying = true; next = EditorApplication.timeSinceStartup + .25;
                }
                else
                {
                    Observe("live-before-destroy"); Object.Destroy(clone); clone = null;
                    cycle++; destroying = false; next = EditorApplication.timeSinceStartup + (coldIntervals ? 6 : cycle == targetCycles ? 5 : .25);
                }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }
        static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            var path = output; output = null;
            if (clone != null) Object.Destroy(clone);
            if (cameraRoot != null) Object.Destroy(cameraRoot);
            using (var stream = new FileStream(path, FileMode.CreateNew))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(new {
                utc = DateTime.UtcNow.ToString("O"), completed = error == null, error, cycles = cycle, warmupCycles = 2, measuredCycles = Math.Max(0, cycle - 2),
                coldIntervals, requestedInstancing, liveSeconds = .25, idleSeconds = coldIntervals ? 6 : .25, finalIdleSeconds = coldIntervals ? 6 : 5,
                unityVersion = Application.unityVersion, api = SystemInfo.graphicsDeviceType.ToString(), assetPath,
                editorProcessId = System.Diagnostics.Process.GetCurrentProcess().Id, gpu = SystemInfo.graphicsDeviceName,
                probeSourceSha256 = Sha("Assets/DevTools/Editor/PerformanceVfxBatchProbe.cs"), probeAssemblySha256 = Sha(typeof(PerformanceVfxBatchProbe).Assembly.Location),
                assetGuid = "db3b70c1d988d02418aec685d4507a91", assetSha256 = Sha(assetPath), rows,
                timingEligible = false, retentionAccepted = false, g1bAccepted = false,
                boundary = "Editor Play graph-only serial creation/destruction with structured cycle/live/idle settings. No game scripts, actual skills, cooldowns, scene host, standalone native snapshots or G1B acceptance. Native batch API reports are distinct from memory-profiler object-size estimates; unmatched native asset responses remain explicit. Deferred-destroy observation is a later Editor update, not proof of GPU completion."
            }, Formatting.Indented));
            Debug.Log("VFX graph-only batch isolation: " + path);
        }
        static bool ReadInstancing(VisualEffect effect)
        {
            using (var serialized = new SerializedObject(effect))
            {
                serialized.Update(); var flag = serialized.FindProperty("m_AllowInstancing");
                if (flag == null || flag.propertyType != SerializedPropertyType.Boolean)
                    throw new InvalidOperationException("Installed VFX instancing flag unavailable.");
                return flag.boolValue;
            }
        }
        static string Sha(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
    }
}
