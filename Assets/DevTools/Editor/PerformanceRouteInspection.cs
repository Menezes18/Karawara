using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RPGKarawara;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Authoring inventory only. No physics simulation, spawns, input replay or scene saves.
public static class PerformanceRouteInspection
{
    [Serializable] public sealed class Reference
    {
        public string field, objectName, assetPath, guid, sha256, hierarchy;
        public Vector3 authoredWorldPosition;
        public List<string> componentTypes = new List<string>();
        public List<string> serializedHints = new List<string>();
    }
    [Serializable] public sealed class Item
    {
        public string category, hierarchy, type, prefabPath, details;
        public bool activeSelf, activeInHierarchy, enabled;
        public Vector3 authoredWorldPosition, authoredLocalPosition;
        public Quaternion authoredWorldRotation;
        public List<Reference> references = new List<Reference>();
    }
    [Serializable] public sealed class SceneInventory
    {
        public string scene, path, sha256, error;
        public int buildIndex, roots, colliders, triggers, cameras, aiComponents, missingScripts;
        public bool previewClosed;
        public List<Item> items = new List<Item>();
    }
    [Serializable] public sealed class Route
    {
        public string routeId, scene;
        public bool runtimeRehearsed, readyForTiming;
        public List<string> preconditions = new List<string>(), limits = new List<string>();
    }
    [Serializable] public sealed class Report
    {
        public string utc, sourceRoutes, sourceRoutesHash, sourcePlan, sourcePlanHash, originalSetup, finalSetup, error;
        public string boundary = "Read-only authored preview-scene inventory. Coordinates are authoring transforms; colliders are not measured ground; AI components are not a runtime living roster. No timing route, gameplay checkpoint, random seed or performance baseline is certified.";
        public bool completed, passed, originalSetupPreserved, allPreviewsClosed;
        public List<SceneInventory> scenes = new List<SceneInventory>();
        public List<Route> routes = new List<Route>();
    }
    [MenuItem("Karawara/Validation/TOBE/Inspect Performance Route Authoring")]
    public static void Run()
    {
        var report = new Report { utc = DateTime.UtcNow.ToString("O"),
            sourceRoutes = "Docs/Validation/TOBE/performance-routes.md", sourcePlan = "Docs/superpowers/plans/2026-10-01-01b-performance-baseline.md" };
        report.originalSetup = Setup();
        try
        {
            Require(!EditorApplication.isPlaying, "Authoring inspection requires EditMode.");
            report.sourceRoutesHash = Hash(report.sourceRoutes); report.sourcePlanHash = Hash(report.sourcePlan);
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            foreach (string name in new[] { "Tutorial_Final", "MapaEsboco", "t", "CucaLoad", "SceneMenu" })
            {
                int index = Array.FindIndex(enabled, s => Path.GetFileNameWithoutExtension(s.path) == name);
                Require(index >= 0, "Enabled production scene missing: " + name);
                var inventory = new SceneInventory { scene = name, path = enabled[index].path, buildIndex = index, sha256 = Hash(enabled[index].path) };
                report.scenes.Add(inventory);
                Scene preview = default;
                try
                {
                    preview = EditorSceneManager.OpenPreviewScene(inventory.path);
                    var roots = preview.GetRootGameObjects(); inventory.roots = roots.Length;
                    foreach (var transform in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                    {
                        if (transform.CompareTag("Spawn")) inventory.items.Add(Describe(transform, "Spawn-tag authored candidate"));
                        foreach (var component in transform.GetComponents<Component>())
                        {
                            if (component == null) { inventory.missingScripts++; continue; }
                            string type = component.GetType().Name;
                            string category = null;
                            if (component is Collider collider)
                            { inventory.colliders++; if (collider.isTrigger) inventory.triggers++; category = collider.isTrigger ? "Trigger" : "Physical collider candidate; ground not proven"; }
                            else if (component is Camera) { inventory.cameras++; category = "Authored camera; runtime selection not proven"; }
                            else if (type.StartsWith("AI", StringComparison.Ordinal) || type == "CucaBoss")
                            { inventory.aiComponents++; category = "Authored AI component; living roster not proven"; }
                            else if (Relevant(type)) category = "Route fixture/control/context";
                            if (category != null) inventory.items.Add(Describe(component, category));
                        }
                    }
                }
                catch (Exception ex) { inventory.error = ex.GetBaseException().Message; }
                finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); inventory.previewClosed = !preview.IsValid() || !preview.isLoaded; }
                Require(Hash(inventory.path) == inventory.sha256, "Inspection changed scene file: " + name);
            }
            report.routes.Add(RouteFor("tutorial-exploration-v1", "Tutorial_Final", "Rehearse four walkable 30-second legs and freeze actual input/camera/checkpoint poses.", "Dismiss authored visible tutorials through their real UI action; record any pause and missing video content."));
            report.routes.Add(RouteFor("mapa-exploration-v1", "MapaEsboco", "Bind runtime AssetData prefab/Resources texture and record actual instantiated world hashes/terrain/start/loop endpoints.", "MapaEsboco authoring may contain only painter settings; SceneMenu WorldSave references are context hints, not instantiated runtime geometry."));
            report.routes.Add(RouteFor("t-saci-combat-v1", "t", "Bind actual BossSpawnerInput prefab/spawnPoint/id, prepare its one Saci before timing, verify host and stable living roster.", "Freeze equipment/stats/seed policy and real approach/attack/dodge inputs; reject boss death or roster divergence."));
            report.routes.Add(RouteFor("cuca-encounter-v1", "CucaLoad", "Bind the authored actual Cuca encounter instance, phase/health, quest/boss state and measured grounded entry.", "BossSpawnerInput in CucaLoad references Saci, so its debug spawn is not a Cuca encounter. Authored Cuca components are candidates only."));
            report.routes.Add(RouteFor("tutorial-ui-minimap-v1", "Tutorial_Final", "Bind actual HUD/skill/minimap and fixed runtime camera/roster; rehearse real ActivatePause(1/2) transitions.", "Use monotonic scheduling for planned timeScale-zero UI periods; verify closing UI restores gameplay inputs/time scale."));
            report.completed = report.scenes.Count == 5 && report.routes.Count == 5;
        }
        catch (Exception ex) { report.error = ex.GetBaseException().Message; }
        finally
        {
            report.finalSetup = Setup(); report.originalSetupPreserved = report.originalSetup == report.finalSetup;
            report.allPreviewsClosed = report.scenes.All(s => s.previewClosed);
            report.passed = report.completed && string.IsNullOrEmpty(report.error) && report.scenes.All(s => string.IsNullOrEmpty(s.error)) && report.originalSetupPreserved && report.allPreviewsClosed;
            string path = "Docs/Validation/TOBE/Execution/performance-route-inspection-" + Guid.NewGuid().ToString("N") + ".json";
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Debug.Log("Performance route authoring inspection: passed=" + report.passed + ", report=" + path);
        }
    }
    static Route RouteFor(string id, string scene, string prerequisite, string limit)
    {
        var route = new Route { routeId = id, scene = scene };
        route.preconditions.Add("Fresh one-player host with copied immutable hashed QA save, distinct absolute save root and observed 1920x1080/renderScale1/HighFidelity/VSync0/cap-1.");
        route.preconditions.Add(prerequisite);
        route.preconditions.Add("Freeze actual grounded player and selected camera poses, FOV, verified living roster, route revision, inputs, checkpoints, tolerances, screenshots and seed policy before 30s warm-up + 120s capture.");
        route.limits.Add(limit);
        route.limits.Add("Preview inspection supplies no physics, natural-input journey, runtime camera/AI selection, timing, repeatability or standalone proof.");
        return route;
    }
    static bool Relevant(string type) => type.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("MiniMap", StringComparison.OrdinalIgnoreCase) >= 0 ||
        type.IndexOf("SkillTree", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("CutScene", StringComparison.OrdinalIgnoreCase) >= 0 ||
        type == "PopUpTutorial" || type == "PlayerUIHudManager" || type == "PlayerCamera" || type == "PlayerManager" ||
        type == "WorldSaveGameManager" || type == "GateActivator" || type == "AnimationTrigger" || type == "BossFogWall";
    static Item Describe(Component component, string category)
    {
        var transform = component.transform;
        var item = new Item { category = category, hierarchy = Hierarchy(transform), type = component.GetType().FullName,
            activeSelf = component.gameObject.activeSelf, activeInHierarchy = component.gameObject.activeInHierarchy,
            enabled = component is Behaviour behaviour ? behaviour.enabled : component is Collider collider ? collider.enabled : true,
            authoredWorldPosition = transform.position, authoredLocalPosition = transform.localPosition, authoredWorldRotation = transform.rotation,
            prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject) };
        if (component is Camera camera) item.details = "fieldOfView=" + camera.fieldOfView + "; targetTexture=" + (camera.targetTexture != null ? AssetDatabase.GetAssetPath(camera.targetTexture) : "none") + "; targetDisplay=" + camera.targetDisplay;
        if (component is BossSpawnerInput spawner) item.details = "bossID=" + spawner.id + "; prefab=" + AssetDatabase.GetAssetPath(spawner.bossPrefab) + "; SpawnBoss not invoked";
        if (component is Transform || component is Collider) return item;
        var serialized = new SerializedObject(component);
        var property = serialized.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script" || property.objectReferenceValue == null) continue;
            item.references.Add(DescribeReference(property.propertyPath, property.objectReferenceValue));
        }
        return item;
    }
    static Reference DescribeReference(string field, UnityEngine.Object value)
    {
        string asset = AssetDatabase.GetAssetPath(value);
        var reference = new Reference { field = field, objectName = value.name, assetPath = asset, guid = AssetDatabase.AssetPathToGUID(asset), sha256 = File.Exists(asset) ? Hash(asset) : null };
        var gameObject = value as GameObject ?? (value as Component)?.gameObject;
        if (gameObject != null)
        {
            reference.hierarchy = Hierarchy(gameObject.transform); reference.authoredWorldPosition = gameObject.transform.position;
            reference.componentTypes = gameObject.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null).Select(c => c.GetType().FullName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        if (value is ScriptableObject && (value.GetType().Name == "AssetData" || value is tutorialInfo))
        {
            var serialized = new SerializedObject(value);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.String) reference.serializedHints.Add(property.propertyPath + "=" + property.stringValue);
                else if (property.propertyType == SerializedPropertyType.Vector3) reference.serializedHints.Add(property.propertyPath + "=" + property.vector3Value.ToString("R"));
                else if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                    reference.serializedHints.Add(property.propertyPath + "=" + (property.objectReferenceValue != null ? AssetDatabase.GetAssetPath(property.objectReferenceValue) : "unassigned"));
            }
        }
        return reference;
    }
    static string Setup() => string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive));
    static string Hierarchy(Transform transform) { string result = transform.name; while (transform.parent != null) { transform = transform.parent; result = transform.name + "/" + result; } return result; }
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
