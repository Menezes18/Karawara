#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    public static class ArchitectureReferenceAudit
    {
        [Serializable] private sealed class Script { public string guid, path, type, assembly; }
        [Serializable] private sealed class Scene { public string path; public int missingScripts; }
        [Serializable] private sealed class Event { public string asset, targetType, method; public bool resolved; }
        [Serializable] private sealed class Report
        {
            public string phase;
            public List<Script> scripts = new List<Script>();
            public List<Scene> scenes = new List<Scene>();
            public List<Event> events = new List<Event>();
        }
        public static void Run(string phase)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Requires idle Edit mode.");
            var report = new Report { phase = phase };
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path); var type = script != null ? script.GetClass() : null;
                report.scripts.Add(new Script { guid = guid, path = path, type = type?.FullName, assembly = type?.Assembly.GetName().Name });
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Concat(new[] { "Assets/DevTools/Scenes/DevPlayground.unity", "Assets/Scenes/MapaEsboco 1.unity" }).Where(File.Exists).Distinct();
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var row = new Scene { path = path }; report.scenes.Add(row);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        row.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                        foreach (var component in transform.GetComponents<MonoBehaviour>())
                        {
                            if (component == null) continue;
                            var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                            while (property.Next(true))
                            {
                                if (property.propertyType != SerializedPropertyType.String || property.name != "m_MethodName" || string.IsNullOrEmpty(property.stringValue)) continue;
                                var targetPath = property.propertyPath.Substring(0, property.propertyPath.Length - "m_MethodName".Length) + "m_Target";
                                var target = serialized.FindProperty(targetPath)?.objectReferenceValue;
                                var type = target != null ? target.GetType() : null;
                                bool found = type != null && type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Any(m => m.Name == property.stringValue);
                                report.events.Add(new Event { asset = path, targetType = type?.FullName, method = property.stringValue, resolved = found });
                            }
                        }
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            var output = "Docs/Validation/TOBE/Execution/g3a-references-" + phase + ".json";
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("G3A reference audit: " + output + ", scripts=" + report.scripts.Count + ", scenes=" + report.scenes.Count + ", events=" + report.events.Count);
        }
    }
}
#endif
