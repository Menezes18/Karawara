using System;
using System.Collections.Generic;
using System.Linq;
using RPGKarawara;
using RPGKarawara.DevTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGKarawara.DevTools.Editor
{
    /// <summary>Creates development assets without changing production scenes or prefabs.</summary>
    public static class DevPlaygroundBuilder
    {
        public const string ScenePath = "Assets/DevTools/Scenes/DevPlayground.unity";
        public const string CatalogPath = "Assets/DevTools/Data/DevPlaygroundCatalog.asset";
        private const string MaterialDirectory = "Assets/DevTools/Data/Materials";

        [MenuItem("Karawara/Dev Playground/Generate Scene and Catalog")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before generating DevPlayground.");
            EnsureFolder("Assets/DevTools/Scenes");
            EnsureFolder("Assets/DevTools/Data");
            EnsureFolder(MaterialDirectory);

            // Never replace or close an open scene while it may contain user changes.
            var existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded)
                throw new InvalidOperationException("Close DevPlayground before regenerating it; the open scene is preserved.");

            var previousActive = SceneManager.GetActiveScene();
            // Extract scene actors before creating an untitled scene (Unity disallows nested creation).
            var catalog = GenerateCatalog();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                BuildScene(catalog);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Could not save " + ScenePath);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DevPlayground generated at " + ScenePath + ". Existing open scenes were preserved. " +
                      "Use Karawara/Dev Playground/Open Scene to inspect or enter Play Mode.");
        }

        [MenuItem("Karawara/Dev Playground/Open Scene")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("Generate DevPlayground first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Homol's Scene Selector may pin SceneMenu as the Play start scene.
            // Opening the playground explicitly means Play should use this scene.
            EditorSceneManager.playModeStartScene = null;
        }

        [MenuItem("Karawara/Dev Playground/Refresh Catalog")]
        public static void RefreshCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before refreshing the scene-derived catalog.");
            EnsureFolder("Assets/DevTools/Data");
            GenerateCatalog();
            AssetDatabase.SaveAssets();
        }

        private static DevPlaygroundCatalog GenerateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DevPlaygroundCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DevPlaygroundCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.networkManagerPrefab = RequiredPrefab("Assets/Data/Prefabs/World Managers/World NetworkManager.prefab");
            catalog.playerPrefab = RequiredPrefab("Assets/Data/Prefabs/Player.prefab");
            catalog.cameraPrefab = RequiredPrefab("Assets/Data/Prefabs/PlayerCamera (Left & Right).prefab");
            catalog.inputPrefab = RequiredPrefab("Assets/Data/Prefabs/PlayerInputManager.prefab");
            catalog.hudPrefab = RequiredPrefab("Assets/Data/Prefabs/Player  Ui Manager.prefab");
            catalog.managerPrefabs = new[]
            {
                "Assets/Data/Prefabs/World Managers/World  Utility Manager.prefab",
                "Assets/Data/Prefabs/World Managers/World Item Database.prefab",
                "Assets/Data/Prefabs/World Managers/World Action Manager.prefab",
                "Assets/Data/Prefabs/World Managers/World Character Effects Manager.prefab",
                "Assets/Data/Prefabs/World Managers/World Sound FX Manager.prefab",
                "Assets/Data/Prefabs/World Managers/World A.I Manager.prefab"
            }.Select(RequiredPrefab).ToArray();
            catalog.skills = FindAssets<Skill>("t:Skill");
            // Homol has two unrelated WeaponItem classes in different namespaces.
            // A name-only type search can select the wrong class; filter by the real CLR type.
            catalog.weapons = FindAssets<WeaponItem>("t:ScriptableObject");
            catalog.spells = FindAssets<SpellItem>("t:ScriptableObject");
            catalog.animations = FindSubAssets<AnimationClip>("t:AnimationClip");
            catalog.materials = FindSubAssets<Material>("t:Material");

            var cuca = ExtractSceneCuca();

            var enemies = new List<DevPlaygroundAsset>();
            var showcase = new List<DevPlaygroundAsset>();
            var effects = new List<DevPlaygroundAsset>();
            var interactables = new List<DevPlaygroundAsset>();
            var cucaEntry = new DevPlaygroundAsset
            {
                label = "Cuca - Production Scene Boss", path = "Assets/DevTools/Data/Generated/CucaDev.prefab", prefab = cuca
            };
            enemies.Add(cucaEntry);
            showcase.Add(cucaEntry);
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith("Assets/DevTools/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var entry = new DevPlaygroundAsset { label = prefab.name, path = path, prefab = prefab };
                var components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                bool enemy = components.Any(c => c != null &&
                    (c is AICharacterManager || c.GetType().Name == "BossAISystem" ||
                     c is CucaBoss || c.GetType().Name == "BossControl"));
                if (enemy) enemies.Add(entry);
                if (components.Any(c => c is Interactable)) interactables.Add(entry);
                bool effect = prefab.GetComponentInChildren<ParticleSystem>(true) != null ||
                              prefab.GetComponentInChildren<UnityEngine.VFX.VisualEffect>(true) != null;
                if (effect) effects.Add(entry);
                // Include enemy meshes, characters, props, important gameplay prefabs and imported models.
                // Runtime previews disable scripts before activation. Keep manager/UI/editor tool assets out.
                // A character may include a floating HP canvas; only exclude UI roots.
                bool uiOnly = prefab.GetComponent<Canvas>() != null || prefab.GetComponent<RectTransform>() != null;
                bool editorTool = path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  path.IndexOf("/nTools/", StringComparison.OrdinalIgnoreCase) >= 0;
                bool visible = prefab.GetComponentInChildren<Renderer>(true) != null ||
                               prefab.GetComponentInChildren<Animator>(true) != null || effect;
                if (visible && !uiOnly && !editorTool)
                    showcase.Add(entry);
            }
            catalog.enemies = enemies.ToArray();
            catalog.showcase = showcase.ToArray();
            catalog.effects = effects.ToArray();
            catalog.interactables = interactables.ToArray();
            EditorUtility.SetDirty(catalog);
            Debug.Log($"DevPlayground catalog: {catalog.enemies.Length} enemy/boss prefabs, " +
                      $"{catalog.skills.Length} skills, {catalog.weapons.Length} weapons, {catalog.spells.Length} spells, " +
                      $"{catalog.showcase.Length} models, {catalog.effects.Length} effects, " +
                      $"{catalog.interactables.Length} interaction prefabs, {catalog.animations.Length} animation clips, " +
                      $"{catalog.materials.Length} materials. Catalog entries are asset discovery, not runtime certification.");
            return catalog;
        }

        private static GameObject ExtractSceneCuca()
        {
            const string sourcePath = "Assets/Scenes/CucaLoad.unity";
            const string targetPath = "Assets/DevTools/Data/Generated/CucaDev.prefab";
            EnsureFolder("Assets/DevTools/Data/Generated");
            var previous = SceneManager.GetActiveScene();
            var sourceScene = SceneManager.GetSceneByPath(sourcePath);
            bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            Scene scratch = default;
            try
            {
                if (openedSource) sourceScene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
                var candidates = sourceScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CucaBoss>(true))
                    .Where(boss => boss.GetComponent<HealthManager>() != null).ToArray();
                if (candidates.Length != 1)
                    throw new InvalidOperationException("Expected one fully configured production Cuca in " + sourcePath + ", found " + candidates.Length);

                // All editing occurs in a disposable scene; the source scene is never saved or modified.
                scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scratch);
                var container = new GameObject("Inactive Cuca Extraction");
                container.SetActive(false);
                var clone = UnityEngine.Object.Instantiate(candidates[0].gameObject, container.transform, true);
                clone.name = "CucaDev";
                if (PrefabUtility.IsPartOfPrefabInstance(clone))
                    PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                clone.transform.SetParent(null, true);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                foreach (var component in clone.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                        throw new InvalidOperationException("Production Cuca has a missing script; extraction stopped.");
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        if (reference == null || AssetDatabase.Contains(reference)) continue;
                        var referencedTransform = reference is Component referencedComponent ? referencedComponent.transform :
                            reference is GameObject referencedObject ? referencedObject.transform : null;
                        if (referencedTransform != null && !referencedTransform.IsChildOf(clone.transform))
                            throw new InvalidOperationException("Production Cuca reference escapes its hierarchy: " +
                                component.GetType().Name + "." + property.propertyPath + " -> " + reference.name +
                                ". Extraction stopped; no dependency was stripped.");
                    }
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, targetPath, out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("Could not save generated Cuca prefab.");
                Debug.Log("Extracted Homol Cuca hierarchy with CucaBoss and HealthManager; source scene preserved.");
                return prefab;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true);
                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
            }
        }

        private static void BuildScene(DevPlaygroundCatalog catalog)
        {
            var root = new GameObject("DevPlayground");
            var controller = root.AddComponent<DevPlaygroundController>();
            controller.catalog = catalog;
            var staticRoot = Child("Arena Static Geometry", root.transform);
            var geometry = new List<Collider>();
            var ground = Material("Floor", new Color(0.17f, 0.22f, 0.25f));
            var wall = Material("Walls", new Color(0.28f, 0.36f, 0.39f));
            var blue = Material("Movement", new Color(0.12f, 0.42f, 0.7f));
            var orange = Material("Combat", new Color(0.7f, 0.3f, 0.12f));
            var green = Material("Interaction", new Color(0.15f, 0.48f, 0.32f));
            var purple = Material("Showcase", new Color(0.4f, 0.22f, 0.6f));
            geometry.Add(Box("Arena Floor", staticRoot, new Vector3(0, -0.5f, 0), new Vector3(72, 1, 72), ground).GetComponent<Collider>());
            geometry.Add(Box("North Boundary", staticRoot, new Vector3(0, 2, 36), new Vector3(72, 4, 1), wall).GetComponent<Collider>());
            geometry.Add(Box("South Boundary", staticRoot, new Vector3(0, 2, -36), new Vector3(72, 4, 1), wall).GetComponent<Collider>());
            geometry.Add(Box("East Boundary", staticRoot, new Vector3(36, 2, 0), new Vector3(1, 4, 72), wall).GetComponent<Collider>());
            geometry.Add(Box("West Boundary", staticRoot, new Vector3(-36, 2, 0), new Vector3(1, 4, 72), wall).GetComponent<Collider>());

            controller.playerSpawn = Marker("Player Spawn", root.transform, new Vector3(0, 0.15f, -12));
            controller.enemySpawn = Marker("Enemy Spawn - Combat Arena", root.transform, new Vector3(0, 0.2f, 20));
            controller.showcaseAnchor = Marker("Showcase Anchor", root.transform, new Vector3(22, 0.2f, 0));
            var stations = Child("Stations and Teleport Destinations", root.transform);
            controller.teleportPoints = new[]
            {
                Marker("Hub - Player Spawn", stations, new Vector3(0, 0.15f, -12)),
                Marker("Combat - Enemy Arena", stations, new Vector3(0, 0.15f, 10)),
                Marker("Movement - Steps and Slopes", stations, new Vector3(-23, 0.15f, -13)),
                Marker("Physics - Dynamic Crates", stations, new Vector3(-23, 0.15f, 11)),
                Marker("Skills - Effects Lane", stations, new Vector3(23, 0.15f, -19)),
                Marker("Showcase - Asset Preview", stations, new Vector3(23, 0.15f, 4)),
                Marker("Interactions - E Key", stations, new Vector3(1, 0.15f, -24))
            };
            foreach (var station in controller.teleportPoints)
                Label(station.name, station.position + new Vector3(0, 0.04f, -3), stations);
            Box("Combat Zone", root.transform, new Vector3(0, 0.012f, 19), new Vector3(22, 0.025f, 25), orange, false);
            Box("Movement Zone", root.transform, new Vector3(-23, 0.012f, -15), new Vector3(19, 0.025f, 23), blue, false);
            Box("Physics Zone", root.transform, new Vector3(-23, 0.012f, 15), new Vector3(19, 0.025f, 20), green, false);
            Box("Showcase Zone", root.transform, new Vector3(23, 0.012f, 9), new Vector3(20, 0.025f, 20), purple, false);
            Box("Skills Zone", root.transform, new Vector3(23, 0.012f, -19), new Vector3(20, 0.025f, 20), orange, false);

            var movement = Child("Movement - Collision Course", staticRoot);
            for (int i = 0; i < 5; i++)
            {
                var height = (i + 1) * 0.25f;
                geometry.Add(Box("Step " + (i + 1), movement, new Vector3(-29 + i * 1.5f, height / 2, -18),
                    new Vector3(1.5f, height, 5), blue).GetComponent<Collider>());
            }
            var slope = Box("Walkable Slope 20 Degrees", movement, new Vector3(-24, 1.2f, -7), new Vector3(8, 0.6f, 5), blue);
            slope.transform.rotation = Quaternion.Euler(0, 0, 20);
            geometry.Add(slope.GetComponent<Collider>());
            var steep = Box("Steep Slope 55 Degrees", movement, new Vector3(-30, 2.6f, -7), new Vector3(6, 0.6f, 5), wall);
            steep.transform.rotation = Quaternion.Euler(0, 0, 55);
            geometry.Add(steep.GetComponent<Collider>());
            geometry.Add(Box("Low Ceiling", movement, new Vector3(-18, 2.8f, -21), new Vector3(5, 0.5f, 7), wall).GetComponent<Collider>());
            geometry.Add(Box("Low Ceiling Support", movement, new Vector3(-15.5f, 1.4f, -21), new Vector3(0.5f, 2.8f, 7), wall).GetComponent<Collider>());

            var physics = Child("Physics - Resettable Objects", root.transform);
            for (int i = 0; i < 6; i++)
            {
                var crate = Box("Dynamic Crate " + (i + 1), physics,
                    new Vector3(-28 + (i % 3) * 2.2f, 0.7f + (i / 3) * 1.5f, 15), Vector3.one * 1.4f, green);
                var rigidbody = crate.AddComponent<Rigidbody>();
                rigidbody.mass = 3;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Physics Sphere";
            ball.layer = LayerMask.NameToLayer("Environment");
            ball.transform.SetParent(physics);
            ball.transform.position = new Vector3(-21, 1, 18);
            ball.GetComponent<Renderer>().sharedMaterial = green;
            ball.AddComponent<Rigidbody>().mass = 2;
            geometry.Add(Box("Navigation Obstacle A", staticRoot, new Vector3(-5, 1.3f, 19), new Vector3(2, 2.6f, 5), wall).GetComponent<Collider>());
            geometry.Add(Box("Navigation Obstacle B", staticRoot, new Vector3(5, 1.3f, 24), new Vector3(2, 2.6f, 5), wall).GetComponent<Collider>());
            controller.navigationGeometry = geometry.ToArray();

            // These examples use the actual production interaction component and do not save or change scenes.
            var interactions = Child("Production Interactable Examples", root.transform);
            Interaction("Read Playground Instructions - E", new Vector3(-3, 0.7f, -26), interactions,
                Interactable.InteractionType.ShowText, null, green);
            Interaction("Teleport To Movement Course - E", new Vector3(2, 0.7f, -26), interactions,
                Interactable.InteractionType.Teleport, controller.teleportPoints[2], green);
            Interaction("Teleport Back To Hub - E", new Vector3(-18, 0.7f, -13), interactions,
                Interactable.InteractionType.Teleport, controller.playerSpawn, green);
            BuildLeverStation(interactions, wall, orange, green);
            BuildCrumblingPlatform(root.transform, blue);

            var sunlight = Child("Directional Light", root.transform).gameObject.AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.intensity = 1.4f;
            sunlight.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.fog = false;
            Label("KARAWARA DEV PLAYGROUND\nF1: panel | Host local | No production save writes", new Vector3(0, 0.05f, -4), root.transform, 1.3f);

            // Disabled in Play Mode: the runtime installs the real production player camera.
            var preview = Child("Editor Preview Camera", root.transform).gameObject.AddComponent<Camera>();
            preview.transform.position = new Vector3(48, 48, -48);
            preview.transform.LookAt(Vector3.zero);
            preview.enabled = false;
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 3), Quaternion.Euler(50, -35, 0), 65);
        }

        private static Interactable Interaction(string title, Vector3 position, Transform parent,
            Interactable.InteractionType kind, Transform destination, Material material)
        {
            var obj = Box(title, parent, position, new Vector3(1.5f, 1.4f, 1.5f), material);
            obj.GetComponent<BoxCollider>().isTrigger = true;
            var interaction = obj.AddComponent<Interactable>();
            interaction.interactionType = kind;
            interaction.interactableText = title;
            interaction.teleportTarget = destination;
            Label(title, position + new Vector3(0, 1, 0), parent, 0.55f, false);
            return interaction;
        }

        private static void BuildLeverStation(Transform parent, Material wall, Material orange, Material green)
        {
            // Exercise Homol's real animation events without moving a production scene camera.
            const string animationPath = "Assets/DevTools/Data/Generated/LeverCameraDev.anim";
            const string controllerPath = "Assets/DevTools/Data/Generated/LeverCameraDev.controller";
            EnsureFolder("Assets/DevTools/Data/Generated");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "LeverCameraDev" };
                clip.SetCurve("", typeof(Transform), "localScale.x", AnimationCurve.Constant(0, 1, 1));
                AnimationUtility.SetAnimationEvents(clip, new[]
                {
                    new AnimationEvent { time = 0.5f, functionName = "AtivarCollider" },
                    new AnimationEvent { time = 0.95f, functionName = "DeativarCameraCutScene" }
                });
                AssetDatabase.CreateAsset(clip, animationPath);
            }
            var animationController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (animationController == null)
            {
                animationController = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                animationController.AddParameter("AtivarCamera", AnimatorControllerParameterType.Trigger);
                var machine = animationController.layers[0].stateMachine;
                var idle = machine.AddState("Idle");
                var activated = machine.AddState("Lever Camera");
                activated.motion = clip;
                machine.defaultState = idle;
                var begin = idle.AddTransition(activated);
                begin.hasExitTime = false;
                begin.duration = 0;
                begin.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "AtivarCamera");
                var end = activated.AddTransition(idle);
                end.hasExitTime = true;
                end.exitTime = 1;
                end.duration = 0;
            }
            var station = Child("Production Lever and Gate - Complete Dependencies", parent);
            var gate = Box("Gate - Opens When Both Levers Active", station, new Vector3(9, 1.5f, -25),
                new Vector3(1, 3, 6), wall);
            var sequence = station.gameObject.AddComponent<CutScenePortao>();
            var view = Child("Lever Sequence Camera", station).gameObject;
            var camera = view.AddComponent<Camera>();
            camera.depth = 10;
            camera.transform.position = new Vector3(14, 5, -32);
            camera.transform.LookAt(new Vector3(7, 1, -25));
            camera.enabled = false;
            sequence.cameraObject = view;

            var canvasRoot = new GameObject("Gate Fade Overlay", typeof(RectTransform), typeof(Canvas));
            canvasRoot.transform.SetParent(station, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var imageRoot = new GameObject("Fade Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageRoot.transform.SetParent(canvasRoot.transform, false);
            var rect = imageRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = imageRoot.GetComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = false;
            var fade = canvasRoot.AddComponent<FadeImage>();
            fade.image = image;
            fade.fadeDuration = 0.5f;
            sequence.fadeImage = fade;

            sequence.stone = new GameObject[2];
            var levers = new Lever[2];
            for (int i = 0; i < 2; i++)
            {
                var indicator = Box("Lever " + (i + 1) + " Active Indicator", station,
                    new Vector3(8, 3.4f, -27 + i * 4), Vector3.one * 0.6f, orange, false);
                indicator.SetActive(false);
                sequence.stone[i] = indicator;
                var interaction = Interaction("Activate Gate Lever " + (i + 1) + " - E",
                    new Vector3(5, 0.7f, -27 + i * 4), station,
                    Interactable.InteractionType.ActivateLever, null, green);
                var lever = interaction.gameObject.AddComponent<Lever>();
                lever.num = i;
                lever.cutScenePortao = sequence;
                var leverView = i == 0 ? view : Child("Lever Sequence Camera", station).gameObject;
                if (i != 0)
                {
                    leverView.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
                    var secondCamera = leverView.AddComponent<Camera>();
                    secondCamera.depth = camera.depth;
                    secondCamera.enabled = false;
                }
                leverView.name = "Homol Lever Camera " + (i + 1);
                var leverAnimator = leverView.AddComponent<Animator>();
                leverAnimator.runtimeAnimatorController = animationController;
                leverAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var animationTrigger = leverView.AddComponent<global::AnimationTrigger>();
                animationTrigger.animator = leverAnimator;
                animationTrigger.cam = leverView.GetComponent<Camera>();
                animationTrigger.target = indicator;
                lever.animationTrigger = animationTrigger;
                interaction.lever = lever;
                levers[i] = lever;
            }
            var activator = station.gameObject.AddComponent<GateActivator>();
            activator.levers = levers;
            activator.gate = gate;
            activator.trueorfalse = false;
            activator.cameraAtivar = view;
            activator.cutScenePortao = sequence;
            Label("GATE SYSTEM: activate both levers with E\nHomol camera animation toggles indicator and restores time", new Vector3(6, 0.06f, -31), parent, 0.6f);
        }

        private static void BuildCrumblingPlatform(Transform parent, Material material)
        {
            var station = Child("Production Plataforma - Crumbles After 3 Seconds", parent);
            station.position = new Vector3(-22, 0, -26);
            var platform = Box("Crumbling Platform", station, station.position + new Vector3(0, 0.45f, 0),
                new Vector3(5, 0.9f, 3), material);
            platform.AddComponent<Plataforma>().timeToDestruct = 3;
            // Keep the solid top and trigger on the same GameObject so production callbacks receive the player.
            var trigger = platform.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 1, 0);
            trigger.size = new Vector3(1, 1.2f, 1);
            Label("PLATAFORMA: step on top; disappears in 3s\nRegenerate/reopen scene to restore", station.position + new Vector3(0, 0.07f, -3), parent, 0.55f);
        }

        private static Transform Child(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.transform;
        }

        private static Transform Marker(string name, Transform parent, Vector3 position)
        {
            var marker = Child(name, parent);
            marker.position = position;
            return marker;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = LayerMask.NameToLayer("Environment");
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        private static void Label(string value, Vector3 position, Transform parent, float scale = 0.8f, bool onGround = true)
        {
            var label = Child("Label - " + value.Split('\n')[0], parent);
            label.position = position;
            if (onGround) label.rotation = Quaternion.Euler(90, 0, 0);
            var text = label.gameObject.AddComponent<TextMesh>();
            text.text = value;
            text.fontSize = 48;
            text.characterSize = 0.1f * scale;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
        }

        private static Material Material(string name, Color color)
        {
            var path = MaterialDirectory + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No Lit material shader found.");
            material = new Material(shader) { name = "DevPlayground " + name };
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T[] FindAssets<T>(string filter) where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets(filter, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith("Assets/DevTools/", StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null).ToArray();
        }

        private static T[] FindSubAssets<T>(string filter) where T : UnityEngine.Object
        {
            // Imported FBX clips/materials are subassets, so LoadAssetAtPath alone would omit them.
            return AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct(StringComparer.Ordinal)
                .Where(p => !p.StartsWith("Assets/DevTools/", StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<T>()
                .Where(a => !a.name.StartsWith("__preview__", StringComparison.Ordinal))
                .Distinct().ToArray();
        }

        private static GameObject RequiredPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Required production prefab missing: " + path);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var index = path.LastIndexOf('/');
            var parent = path.Substring(0, index);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(index + 1));
        }
    }
}
