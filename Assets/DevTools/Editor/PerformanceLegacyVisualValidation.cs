using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceLegacyVisualValidation
    {
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required"); var cases = new List<object>();
            foreach (string change in new[] { "baseline", "inactive-root", "root-roundtrip", "camera-object-roundtrip", "data-enabled", "camera-enabled", "listener-enabled", "animation-enabled", "animation-clip", "destroy-clip", "automatic-play", "destroy-camera-object", "destroy-listener", "destroy-animation", "destroy-data", "declared-reference", "reject-foreign", "reject-duplicate", "reject-destroyed-camera-object", "reject-missing-listener", "reject-missing-animation" })
            {
                var root = new GameObject("Owned retired legacy visual"); root.SetActive(false); GameObject foreign = null; AnimationClip clip = null; AnimationClip other = null;
                PerformanceActorRosterContext context = null; PerformanceActorLifecycleScope scope = null;
                try
                {
                    var source = root.AddComponent<PerformanceAuthoredVisual>(); source.visual = root.AddComponent<Animator>(); root.AddComponent<SkinnedMeshRenderer>();
                    var child = new GameObject("Preserved legacy camera"); child.SetActive(false); child.transform.SetParent(root.transform); var camera = child.AddComponent<Camera>(); var listener = child.AddComponent<AudioListener>(); var animation = child.AddComponent<Animation>(); var data = child.AddComponent<UniversalAdditionalCameraData>();
                    clip = new AnimationClip { legacy = true }; animation.AddClip(clip, "Owned legacy clip"); animation.clip = clip; source.reviewedComponents = new MonoBehaviour[] { data };
                    root.SetActive(true);
                    if (change.StartsWith("reject-"))
                    {
                        if (change == "reject-foreign") { foreign = new GameObject("Foreign camera data"); foreign.SetActive(false); source.reviewedComponents = new MonoBehaviour[] { foreign.AddComponent<UniversalAdditionalCameraData>() }; }
                        if (change == "reject-duplicate") source.reviewedComponents = new MonoBehaviour[] { data, data };
                        if (change == "reject-destroyed-camera-object") Object.DestroyImmediate(child);
                        if (change == "reject-missing-listener") Object.DestroyImmediate(listener);
                        if (change == "reject-missing-animation") Object.DestroyImmediate(animation);
                        bool rejected = false; try { using (PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false)) { } } catch (ArgumentException) { rejected = true; }
                        Require(rejected, "Malformed legacy camera declaration admitted");
                    }
                    else
                    {
                        if (change == "inactive-root") root.SetActive(false);
                        var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }); Require(census.report.actors.Any(a => a.objectId == root.GetInstanceID() && a.role == "world-visual" && a.supportedBinding), "Reviewed retired visual not bound");
                        context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false); scope = PerformanceActorLifecycleScope.Begin(new[] { "-karawaraPerformanceRoster" }, context); Require(context.ReadLivingHostileCount() == 0, "Visual counted as hostile before mutation");
                        if (change == "root-roundtrip") { root.SetActive(false); root.SetActive(true); }
                        if (change == "camera-object-roundtrip") { child.SetActive(true); child.SetActive(false); }
                        if (change == "data-enabled") data.enabled = !data.enabled;
                        if (change == "camera-enabled") camera.enabled = !camera.enabled;
                        if (change == "listener-enabled") listener.enabled = !listener.enabled;
                        if (change == "animation-enabled") animation.enabled = !animation.enabled;
                        if (change == "animation-clip") { other = new AnimationClip { legacy = true }; animation.AddClip(other, "Different clip"); animation.clip = other; }
                        if (change == "destroy-clip") Object.DestroyImmediate(clip);
                        if (change == "automatic-play") animation.playAutomatically = !animation.playAutomatically;
                        if (change == "destroy-camera-object") Object.DestroyImmediate(child);
                        if (change == "destroy-listener") Object.DestroyImmediate(listener);
                        if (change == "destroy-animation") Object.DestroyImmediate(animation);
                        if (change == "destroy-data") Object.DestroyImmediate(data);
                        if (change == "declared-reference") source.reviewedComponents = Array.Empty<MonoBehaviour>();
                        var result = new CaptureResult { conditions = new CaptureConfig { enemyCount = 17 } }; context.Validate(result);
                        bool expected = change != "baseline" && change != "inactive-root";
                        Require(result.conditions.enemyCount == 17 && !result.populationVerified && !result.comparisonValid, "Visual changed enemies or granted certification");
                        Require(result.conditionsChanged == expected && !scope.NotificationFaulted, "Lost legacy camera source change or internal callback fault");
                        if (!expected) { for (int i = 0; i < 64; i++) context.ReadLivingHostileCount(); long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount(); Require(before == GC.GetAllocatedBytesForCurrentThread(), "Warm native camera polling allocated"); }
                    }
                    cases.Add(new { change, passed = true });
                }
                catch (Exception error) { cases.Add(new { change, passed = false, detail = error.ToString() }); }
                finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); if (foreign != null) Object.DestroyImmediate(foreign); if (clip != null) Object.DestroyImmediate(clip); if (other != null) Object.DestroyImmediate(other); }
            }
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-legacy-visual-fixture-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases }, Formatting.Indented));
        }
        static void Require(bool yes, string message) { if (!yes) throw new InvalidOperationException(message); }
    }
}
