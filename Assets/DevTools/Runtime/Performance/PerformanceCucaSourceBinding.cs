using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Explicit QA setup for the exact authored Cuca graph inspected before capture.
    // Scene assets and production references remain untouched; each typed source
    // validates its complete owned graph before it can enter a census.
    internal static class PerformanceCucaSourceBinding
    {
        internal static PerformanceCucaSceneSource[] Bind(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded || scene.name != "CucaLoad")
                throw new InvalidOperationException("Actual loaded Cuca scene required for explicit QA binding");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var owned = new List<PerformanceCucaSceneSource>(8);
            try
            {
                Add(all, owned, "LevelCuca/Estantes/EstanteFull", PerformanceCucaSceneSource.Kind.NativeGeometry);
                foreach (string path in new[] { "LevelCuca/Armadilhas1/Espinhos1", "LevelCuca/Armadilhas1/Espinhos2", "LevelCuca/Armadilhas1/Espinhos3" })
                    Add(all, owned, path, PerformanceCucaSceneSource.Kind.DamageGeometry);
                foreach (string path in new[] { "Camera-1", "Camera-2", "Camera-3" }) Add(all, owned, path, PerformanceCucaSceneSource.Kind.LeverCamera);
                Add(all, owned, "CutScene2", PerformanceCucaSceneSource.Kind.DormantCutscene);
                foreach (var source in owned)
                {
                    try { PerformanceCucaSceneProof.Validate(source); }
                    catch (Exception error)
                    {
                        var collisions = source.GetComponentsInChildren<MeshCollider>(true).Select(c => new {
                            collider = Path(c.transform), collisionMesh = c.sharedMesh == null ? null : c.sharedMesh.name,
                            renderMesh = c.GetComponent<MeshFilter>()?.sharedMesh == null ? null : c.GetComponent<MeshFilter>().sharedMesh.name,
                            staticBatch = c.GetComponent<MeshRenderer>() != null && c.GetComponent<MeshRenderer>().isPartOfStaticBatch }).ToArray();
                        throw new InvalidOperationException("Exact authored Cuca source rejected: " + Path(source.transform) + "; " + error.Message + "; collisions=" + Newtonsoft.Json.JsonConvert.SerializeObject(collisions), error);
                    }
                }
                return owned.ToArray();
            }
            catch
            {
                foreach (var source in owned) if (source != null) UnityEngine.Object.Destroy(source);
                throw;
            }
        }
        static void Add(Transform[] all, List<PerformanceCucaSceneSource> owned, string path, PerformanceCucaSceneSource.Kind kind)
        {
            var candidates = all.Where(t => Path(t) == path).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Missing or ambiguous reviewed Cuca owner: " + path);
            var root = candidates[0].gameObject;
            if (root.GetComponent<PerformanceCucaSceneSource>() != null) throw new InvalidOperationException("Cuca QA source already bound: " + path);
            var source = root.AddComponent<PerformanceCucaSceneSource>(); owned.Add(source);
            source.kind = kind; source.animators = root.GetComponentsInChildren<Animator>(true);
            if (kind == PerformanceCucaSceneSource.Kind.NativeGeometry || kind == PerformanceCucaSceneSource.Kind.DamageGeometry)
            {
                source.shape = root.GetComponent<BoxCollider>();
                if (kind == PerformanceCucaSceneSource.Kind.DamageGeometry) source.damage = root.GetComponent<DamageCollider>();
            }
            else if (kind == PerformanceCucaSceneSource.Kind.LeverCamera)
            {
                source.lever = root.GetComponent<AnimationTrigger>(); source.camera = root.GetComponent<Camera>();
                source.cameraData = root.GetComponent<UniversalAdditionalCameraData>(); source.listener = root.GetComponent<AudioListener>();
                source.targetShape = source.lever?.target == null ? null : source.lever.target.GetComponent<BoxCollider>();
            }
            else
            {
                source.cameras = root.GetComponentsInChildren<Camera>(true); source.cutscenes = root.GetComponentsInChildren<cutSceneFight>(true);
                source.lifetimes = root.GetComponentsInChildren<DestroyAfterTime>(true); source.audio = root.GetComponent<AudioSource>(); source.listener = root.GetComponent<AudioListener>();
                source.trigger = all.Select(t => t.GetComponent<CutSceneTrigger>()).Single(t => t != null && t._cutScene == root);
            }
        }
        static string Path(Transform value) { string path = value.name; while (value.parent != null) { value = value.parent; path = value.name + "/" + path; } return path; }
    }
#endif
}
