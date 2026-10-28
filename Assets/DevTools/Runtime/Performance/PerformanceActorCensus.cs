using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Performance
{
    // Discovery is performed outside sampling; it is never itself a creation-path certificate.
    public static class PerformanceActorCensus
    {
        [Serializable] public sealed class Actor
        {
            public int objectId, sceneHandle;
            public string scene, path, primaryType, role;
            public string[] components;
            public bool active, enabled, supportedBinding, spawned;
            public ulong? networkObjectId;
            public float? currentHealth, maximumHealth;
            public bool? dead;
            public float[] position;
        }
        [Serializable] public sealed class Workload
        {
            public int objectId, componentId;
            public string scene, path, type;
            public bool active, enabled;
        }
        [Serializable] public sealed class Report
        {
            public string utc, activeScene;
            public int frame;
            public bool creationPathsVerified = false;
            public Actor[] actors;
            public Workload[] unclassifiedAnimators, creationCandidates;
            public SpawnerAssociation[] spawnerAssociations;
            public UiIcon[] uiIcons;
            public PerformanceUiSourceInventory.Row[] uiSources;
            public PerformanceAuxiliaryContext.Row[] auxiliarySources;
        }
        // Current retained references only: this does not prove historical creation/despawn closure.
        [Serializable] public sealed class SpawnerAssociation
        {
            public int spawnerId, prefabId, createdObjectId, actorObjectId;
            public string scene, path, prefabName, createdPath, status;
            public bool spawnerActive, createdActive, supportedActor, uniqueOwner, spawned, linked;
            public ulong? networkObjectId;
        }
        [Serializable] public sealed class UiIcon
        {
            public int creatorId, iconId, witnessId, animatorId;
            public int targetId, graphicId, groupId, parentId, canvasId, controllerId, creatorSceneHandle, iconSceneHandle;
            public string scene, path;
            public bool supportedBinding, active;
            public bool creatorActive, creatorEnabled, iconEnabled, graphicEnabled, animatorEnabled, canvasEnabled, canvasActive;
        }
        public sealed class Snapshot
        {
            public Report report;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            [JsonIgnore] internal Component[] bindings;
            [JsonIgnore] internal string[] roles;
            [JsonIgnore] internal MiniMapEntity[] uiCreators;
            [JsonIgnore] internal MonoBehaviour[] uiSourceOwners;
            [JsonIgnore] internal PerformanceAuxiliarySource[] auxiliaryBindings;
            internal PerformanceAuxiliaryContext CreateAuxiliaryContext() => PerformanceAuxiliaryContext.Begin(new[] { "-karawaraPerformanceRoster" }, auxiliaryBindings);
            internal PerformanceUiSourceContext CreateUiSourceContext() => PerformanceUiSourceContext.Begin(new[] { "-karawaraPerformanceRoster" }, uiSourceOwners);
            internal PerformanceActorRosterContext CreateContext() => PerformanceActorRosterContext.Create(
                new[] { "-karawaraPerformanceRoster" }, bindings, roles, Array.Empty<GameObject>(), false);
            internal PerformanceActorRosterContext CreateEndpointContext() => PerformanceActorRosterContext.CreateEndpointOnly(
                new[] { "-karawaraPerformanceRoster" }, bindings, roles, Array.Empty<GameObject>(), false);
            internal PerformanceMinimapContext CreateUiContext() => PerformanceMinimapContext.Begin(new[] { "-karawaraPerformanceRoster" }, uiCreators);
#endif
        }
        public static Snapshot Capture(string[] args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            int count = 0;
            if (args != null) foreach (var arg in args) if (arg == "-karawaraPerformanceRoster") count++;
            if (count == 0) return null;
            if (count != 1 || !Application.isPlaying) throw new InvalidOperationException("One explicit roster flag and an actual loaded runtime required for census.");
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var groups = all.Where(c => c != null && c.gameObject.scene.IsValid() && Known(c)).GroupBy(c => c.gameObject).ToArray();
            var bindings = new List<Component>(); var roles = new List<string>(); var actors = new List<Actor>();
            var ownedRoots = new List<GameObject>();
            foreach (var group in groups)
            {
                var parts = group.ToArray();
                Component primary = SelectPrimary(parts);
                string role = primary is PlayerManager ? "local-player" : primary is AICharacterManager || primary is CucaBoss || primary is HealthManager ? "hostile" :
                    PerformanceActorRosterContext.AmbientNpcController(primary) ? "ambient-npc" : primary is AraraMovement ? "ambient-animal" : primary is PerformanceAuthoredVisual || primary is PerformanceSceneAnimatorSource || primary is PerformanceCucaSceneSource ? "world-visual" : primary is WolfAI || primary is PetAttackController ? "companion" : "unclassified-controller";
                bool supported = (primary is PlayerManager || primary is AICharacterManager || primary is CucaBoss || primary is HealthManager || primary is AraraMovement || (primary is PerformanceAuthoredVisual visual && visual.IsValidSource()) || (primary is PerformanceSceneAnimatorSource sceneVisual && sceneVisual.IsValidSource()) || (primary is PerformanceCucaSceneSource cucaVisual && cucaVisual.IsValidSource()) || PerformanceActorRosterContext.AmbientNpcController(primary)) &&
                    parts.All(p => p == primary || (primary is CucaBoss && p is HealthManager) ||
                        (PerformanceActorRosterContext.AmbientNpcController(primary) && PerformanceActorRosterContext.AmbientNpcController(p)));
                if (primary is CharacterManager character && character.characterNetworkManager == null) supported = false;
                if (supported) { bindings.Add(primary); roles.Add(role); ownedRoots.Add(group.Key); }
                var network = primary as Unity.Netcode.NetworkBehaviour;
                var characterSource = primary as CharacterManager;
                var healthSource = parts.OfType<HealthManager>().FirstOrDefault();
                var networkSource = characterSource == null ? null : characterSource.characterNetworkManager;
                var root = group.Key;
                actors.Add(new Actor { objectId = root.GetInstanceID(), sceneHandle = root.scene.handle, scene = root.scene.name, path = Path(root.transform),
                    primaryType = primary.GetType().FullName, role = role, components = parts.Select(p => p.GetType().FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                    active = root.activeInHierarchy, enabled = ((Behaviour)primary).enabled, supportedBinding = supported,
                    spawned = network != null && network.IsSpawned, networkObjectId = network != null && network.IsSpawned ? network.NetworkObjectId : (ulong?)null,
                    currentHealth = networkSource != null ? networkSource.currentHealth.Value : healthSource != null ? healthSource.currentHealth : (float?)null,
                    maximumHealth = networkSource != null ? networkSource.maxHealth.Value : healthSource != null ? healthSource.maxHealth : (float?)null,
                    dead = characterSource != null ? characterSource.isDead.Value : (bool?)null,
                    position = new[] { root.transform.position.x, root.transform.position.y, root.transform.position.z } });
            }
            // Use exact objects retained by the production creation owner, never prefab/path naming.
            foreach (var loader in all.OfType<WorldSaveGameManager>())
            {
                var root = loader.QaLoadedWorldObject;
                if (root == null || !root.scene.IsValid() || ownedRoots.Contains(root) || !PerformanceLoadedVisualProof.CanBind(loader)) continue;
                var animator = root.GetComponent<Animator>();
                // A visual exemption must not swallow another known actor/controller subtree.
                if (animator == null || root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && Known(c))) continue;
                ownedRoots.Add(root); bindings.Add(animator); roles.Add("world-visual");
                actors.Add(new Actor { objectId = root.GetInstanceID(), sceneHandle = root.scene.handle, scene = root.scene.name, path = Path(root.transform),
                    primaryType = animator.GetType().FullName, role = "world-visual", components = new[] { animator.GetType().FullName },
                    active = root.activeInHierarchy, enabled = animator.enabled, supportedBinding = true,
                    position = new[] { root.transform.position.x, root.transform.position.y, root.transform.position.z } });
            }
            var animated = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var uiOwners = all.OfType<MiniMapEntity>().Where(o => o != null && o.IconInstance != null && o.IconInstance is System.MiniMap.IconBaseMiniMapIcon).ToArray();
            var uiCreators = new List<MiniMapEntity>(); var uiRows = new List<UiIcon>(); var uiAnimatorIds = new HashSet<int>();
            foreach (var creator in uiOwners)
            {
                var icon = (System.MiniMap.IconBaseMiniMapIcon)creator.IconInstance;
                var animator = icon.GetComponent<Animator>(); var witness = icon.GetComponent<PerformanceMinimapIconWitness>();
                var graphic = icon.TargetGraphic; var canvas = graphic == null ? null : graphic.canvas;
                bool supported = uiOwners.Count(o => o.IconInstance == icon) == 1 && PerformanceMinimapContext.CanBind(creator);
                if (supported) { uiCreators.Add(creator); if (animator != null) uiAnimatorIds.Add(animator.GetInstanceID()); }
                uiRows.Add(new UiIcon { creatorId = creator.GetInstanceID(), iconId = icon.GetInstanceID(), witnessId = witness == null ? 0 : witness.GetInstanceID(),
                    targetId = creator.Target == null ? 0 : creator.Target.GetInstanceID(), graphicId = graphic == null ? 0 : graphic.GetInstanceID(),
                    groupId = icon.m_CanvasGroup == null ? 0 : icon.m_CanvasGroup.GetInstanceID(), parentId = icon.transform.parent == null ? 0 : icon.transform.parent.GetInstanceID(),
                    canvasId = canvas == null ? 0 : canvas.GetInstanceID(), controllerId = animator == null || animator.runtimeAnimatorController == null ? 0 : animator.runtimeAnimatorController.GetInstanceID(),
                    creatorSceneHandle = creator.gameObject.scene.handle, iconSceneHandle = icon.gameObject.scene.handle,
                    creatorActive = creator.gameObject.activeInHierarchy, creatorEnabled = creator.enabled, iconEnabled = icon.enabled,
                    graphicEnabled = graphic != null && graphic.enabled, animatorEnabled = animator != null && animator.enabled,
                    canvasEnabled = canvas != null && canvas.enabled, canvasActive = canvas != null && canvas.gameObject.activeInHierarchy,
                    animatorId = animator == null ? 0 : animator.GetInstanceID(), scene = icon.gameObject.scene.name, path = Path(icon.transform), supportedBinding = supported, active = icon.gameObject.activeInHierarchy });
            }
            var uiSources = PerformanceUiSourceInventory.Capture(args, all);
            var uiSourceOwners = new List<MonoBehaviour>();
            foreach (var row in uiSources)
            {
                var owner = all.FirstOrDefault(o => o != null && o.GetInstanceID() == row.ownerId);
                if (!PerformanceUiSourceContext.HasWitness(owner, row)) continue;
                uiAnimatorIds.Add(row.animatorId); if (!uiSourceOwners.Contains(owner)) uiSourceOwners.Add(owner);
            }
            var auxiliary = all.OfType<PerformanceAuxiliarySource>().ToArray();
            var auxiliaryBindings = auxiliary.Where(PerformanceAuxiliaryContext.CanBind).ToArray();
            foreach (var source in auxiliaryBindings) uiAnimatorIds.Add(source.visual.GetInstanceID());
            var unknown = new List<Workload>();
            foreach (var animator in animated)
            {
                if (uiAnimatorIds.Contains(animator.GetInstanceID())) continue;
                bool owned = false;
                foreach (var root in ownedRoots) if (animator.transform == root.transform || animator.transform.IsChildOf(root.transform)) { owned = true; break; }
                if (!owned) unknown.Add(Describe(animator));
            }
            var spawners = all.Where(c => c != null && (c.GetType().Name.Contains("Spawner") || c is WorldObjectManager)).Select(Describe).ToArray();
            var aiSpawners = all.Where(s => (s is AICharacterSpawner || s is BossSpawnerInput) && s.gameObject.scene.IsValid()).ToArray();
            Func<MonoBehaviour, GameObject> createdBy = s => s is AICharacterSpawner aiSource ? aiSource.QaSpawned : ((BossSpawnerInput)s).QaSpawned;
            Func<MonoBehaviour, GameObject> prefabOf = s => s is AICharacterSpawner aiSource ? aiSource.QaPrefab : ((BossSpawnerInput)s).bossPrefab;
            var associations = aiSpawners.Select(s =>
            {
                var created = createdBy(s); var prefab = prefabOf(s);
                var actor = created == null ? null : actors.FirstOrDefault(a => a.objectId == created.GetInstanceID());
                var ai = created == null ? null : created.GetComponent<AICharacterManager>();
                var networkObject = created == null ? null : created.GetComponent<Unity.Netcode.NetworkObject>();
                bool unique = created != null && aiSpawners.Count(other => createdBy(other) == created) == 1;
                bool supported = ai != null && actor != null && actor.supportedBinding && actor.role == "hostile";
                bool spawned = networkObject != null && networkObject.IsSpawned && ai != null && ai.IsSpawned && ai.NetworkObject == networkObject;
                string status = created == null ? "not-created" : !unique ? "ambiguous-owner" : !supported ? "unbound-actor" :
                    prefab == null ? "missing-prefab" : !spawned ? "not-network-spawned" : "linked";
                return new SpawnerAssociation { spawnerId = s.GetInstanceID(), scene = s.gameObject.scene.name, path = Path(s.transform),
                    prefabId = prefab == null ? 0 : prefab.GetInstanceID(), prefabName = prefab == null ? null : prefab.name,
                    createdObjectId = created == null ? 0 : created.GetInstanceID(), createdPath = created == null ? null : Path(created.transform),
                    actorObjectId = actor == null ? 0 : actor.objectId, spawnerActive = s.gameObject.activeInHierarchy,
                    createdActive = created != null && created.activeInHierarchy, supportedActor = supported, uniqueOwner = unique,
                    spawned = spawned, networkObjectId = spawned ? networkObject.NetworkObjectId : (ulong?)null, status = status, linked = status == "linked" };
            }).OrderBy(s => s.scene + "/" + s.path, StringComparer.Ordinal).ToArray();
            return new Snapshot { bindings = bindings.ToArray(), roles = roles.ToArray(), uiCreators = uiCreators.ToArray(), uiSourceOwners = uiSourceOwners.ToArray(), auxiliaryBindings = auxiliaryBindings, report = new Report {
                utc = DateTime.UtcNow.ToString("O"), activeScene = SceneManager.GetActiveScene().name, frame = Time.frameCount,
                actors = actors.OrderBy(a => a.scene + "/" + a.path, StringComparer.Ordinal).ToArray(), spawnerAssociations = associations, uiIcons = uiRows.ToArray(), uiSources = uiSources, auxiliarySources = PerformanceAuxiliaryContext.CaptureRows(auxiliary), unclassifiedAnimators = unknown.ToArray(), creationCandidates = spawners } };
#else
            return null;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        // FindObjectsSortMode.None has no stable component order between Player runs.
        // Preserve character/Cuca precedence, then choose a canonical controller type.
        static Component SelectPrimary(Component[] parts)
        {
            var ordered = parts.OrderBy(p => p.GetType().FullName, StringComparer.Ordinal).ToArray();
            return ordered.FirstOrDefault(p => p is CharacterManager) ?? ordered.FirstOrDefault(p => p is CucaBoss) ?? ordered[0];
        }
        static bool Known(Component actor) => actor is CharacterManager || actor is CucaBoss || actor is HealthManager || actor is NPCDialogue ||
            actor is BossAISystem || actor is BossControl || actor is WolfAI || actor is PetAttackController || actor is RandomNPCMovement ||
            actor is NPCMovement || actor is NPCStart || actor is NPCATIVAR || actor is NPC || actor is AraraMovement || actor is PerformanceAuthoredVisual || actor is PerformanceSceneAnimatorSource || actor is PerformanceCucaSceneSource;
        static Workload Describe(Component component) => new Workload { objectId = component.gameObject.GetInstanceID(), componentId = component.GetInstanceID(),
            scene = component.gameObject.scene.name, path = Path(component.transform), type = component.GetType().FullName,
            active = component.gameObject.activeInHierarchy, enabled = component is Behaviour behaviour && behaviour.enabled };
        static string Path(Transform transform) { string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }
#endif
    }
}
