using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using RPGKarawara.SkillTree;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools
{
    public static class DevPlaygroundSmokeTest
    {
        [Serializable] public class CaseResult
        {
            public string name;
            public bool passed;
            public string details;
            public List<string> errors = new List<string>();
        }
        [Serializable] public class Snapshot
        {
            public string path;
            public string json;
            public string sha256;
            public long lastWriteUtcTicks;
            [NonSerialized] public Object asset;
        }
        [Serializable] public class Report
        {
            public string startedUtc, finishedUtc, scene, persistentDataPath;
            public string boundary = "Baseline captured when runner invoked in an already active Play session; does not cover scene startup before invocation.";
            public bool completed, passed;
            public List<CaseResult> cases = new List<CaseResult>();
            public List<Snapshot> originalAssets = new List<Snapshot>();
            public List<Snapshot> saveFilesBefore = new List<Snapshot>();
            public List<Snapshot> saveFilesAfter = new List<Snapshot>();
        }
        private sealed class Step
        {
            public string name;
            public Action begin;
            public Func<string> verify;
            public double wait;
        }
        private static DevPlaygroundController controller;
        private static Report report;
        private static readonly Queue<Step> steps = new Queue<Step>();
        private static Step current;
        private static CaseResult result;
        private static double deadline;
        private static bool running;
        private static bool includePreviews;
        private static string reportPath;
        private static int healthBefore;
        private static Vector3 originalPosition;
        private static Quaternion originalRotation;
        private static bool originalGodMode;
        private static readonly Dictionary<Type, string[]> singletonIdentities = new Dictionary<Type, string[]>();

        [MenuItem("Karawara/Validation/Run Playground Smoke Test")]
        public static void Run() => Run(true);

        [MenuItem("Karawara/Validation/Run Playground Gameplay Smoke Test")]
        public static void RunGameplay() => Run(false);

        private static void Run(bool previews)
        {
            if (running) { Debug.LogWarning("Playground smoke test already running."); return; }
            controller = Object.FindAnyObjectByType<DevPlaygroundController>();
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "DevPlayground"
                || controller == null || !controller.Ready)
            {
                Debug.LogError("Smoke test requires an already running, ready DevPlayground Play session. It never starts or stops PlayMode.");
                return;
            }
            report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), scene = SceneManager.GetActiveScene().path,
                persistentDataPath = Application.persistentDataPath };
            includePreviews = previews;
            reportPath = previews ? "Docs/HomolDevPlaygroundSmokeTest.json" : "Docs/HomolDevPlaygroundGameplaySmokeTest.json";
            steps.Clear(); singletonIdentities.Clear(); current = null;
            originalPosition = controller.Player.transform.position;
            originalRotation = controller.Player.transform.rotation;
            originalGodMode = controller.Player.characterNetworkManager.isInvulnerable.Value;
            foreach (var asset in controller.catalog.skills.Cast<ScriptableObject>().Concat(controller.catalog.weapons).Concat(controller.catalog.spells))
                if (asset != null) report.originalAssets.Add(new Snapshot { path = AssetDatabase.GetAssetPath(asset),
                    json = EditorJsonUtility.ToJson(asset), asset = asset, sha256 = HashAsset(asset) });
            report.saveFilesBefore = SaveFiles();
            foreach (Type type in new[] { typeof(Unity.Netcode.NetworkManager), typeof(WorldItemDatabase), typeof(WorldUtilityManager),
                typeof(WorldSaveGameManager), typeof(PlayerInputManager), typeof(PlayerUIManager), typeof(PlayerCamera) })
                singletonIdentities[type] = Identities(type);
            Add("Host / owner / save isolation / navigation", null, () =>
            {
                Require(controller.Network.IsHost && controller.Network.IsServer && controller.Network.IsListening, "Local NGO host not listening.");
                var transport = controller.Network.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
                Require(transport != null && transport.GetLocalEndpoint().Port != 0, "Host has no bound UDP port.");
                Require(controller.Player.IsOwner && controller.Player.NetworkObject.IsSpawned, "Production player not owned/spawned.");
                var initialSpell = controller.Player.playerInventoryManager.currentSpell;
                Require(initialSpell != null && controller.Player.playerNetworkManager.currentSpellID.Value == initialSpell.itemID,
                    "Default equipped spell was not published through the network variable.");
                Require(WorldSaveGameManager.DevPlaygroundIsolationActive, "Save isolation guard inactive.");
                Require(Time.timeScale > 0, "Time scale is paused; timed effect validation requires advancing gameplay.");
                Require(NavMesh.CalculateTriangulation().vertices.Length > 0, "No runtime navigation triangles.");
                Require(NavMesh.SamplePosition(controller.enemySpawn.position, out _, 5, NavMesh.AllAreas), "Enemy station not on navigation mesh.");
                return "Production host and owner present; isolation enabled; navigable enemy station.";
            }, .1);
            Add("TakeDamageEffect decreases health", () =>
            {
                controller.SetGodMode(false); controller.Heal(); healthBefore = controller.Player.playerNetworkManager.currentHealth.Value;
                Damage(7);
            }, () =>
            {
                int health = controller.Player.playerNetworkManager.currentHealth.Value;
                Require(health == healthBefore - 7, "Expected damage 7, before=" + healthBefore + " after=" + health);
                return "Actual effects manager processed TakeDamageEffect: " + healthBefore + " -> " + health;
            }, .25);
            Add("God mode blocks actual damage", () =>
            {
                controller.SetGodMode(true); healthBefore = controller.Player.playerNetworkManager.currentHealth.Value; Damage(7);
            }, () =>
            {
                Require(controller.Player.playerNetworkManager.currentHealth.Value == healthBefore, "Invulnerable player health changed.");
                return "TakeDamageEffect rejected by production invulnerability guard.";
            }, .2);
            Add("Heal / revive API", () => controller.Heal(), () =>
            {
                Require(controller.Player.playerNetworkManager.currentHealth.Value == controller.Player.playerNetworkManager.maxHealth.Value,
                    "Heal did not restore max health.");
                return "Health restored to maximum (living player; death/revive path not exercised).";
            }, .1);
            Add("Lethal TakeDamageEffect / production death", () =>
            {
                controller.SetGodMode(false); controller.Heal();
                Damage(controller.Player.playerNetworkManager.maxHealth.Value + 1);
            }, () =>
            {
                Require(controller.Player.playerNetworkManager.currentHealth.Value <= 0 && controller.Player.isDead.Value,
                    "Lethal damage failed to enter production death state.");
                return "Actual lethal effect triggered health depletion and isDead.";
            }, 1);
            Add("Heal revives dead player", () => controller.Heal(), () =>
            {
                Require(!controller.Player.isDead.Value, "Heal left player dead.");
                Require(controller.Player.playerNetworkManager.currentHealth.Value == controller.Player.playerNetworkManager.maxHealth.Value,
                    "Revive did not restore max health.");
                controller.SetGodMode(true);
                return "Dead player revived and health restored through public Heal API.";
            }, .25);
            Add("Direct production SaveGame isolation", () => WorldSaveGameManager.instance.SaveGame(), () =>
            {
                Require(Fingerprint(report.saveFilesBefore) == Fingerprint(SaveFiles()), "SaveGame wrote a persistent character slot despite isolation.");
                return "Direct SaveGame call left persistent slot set, hashes and timestamps unchanged.";
            }, .25);
            foreach (var point in controller.teleportPoints)
            {
                var target = point;
                if (target == null) continue;
                Add("Teleport / " + target.name, () => controller.Teleport(target), () =>
                {
                    Require(Vector3.Distance(controller.Player.transform.position, target.position) < 1, "Player missed teleport station.");
                    return "Actual position=" + controller.Player.transform.position + " target=" + target.position;
                }, .1);
            }
            Add("Player grounded on production environment floor", () => controller.Teleport(controller.playerSpawn), () =>
            {
                Require(controller.Player.characterLocomotionManager.isGrounded, "Player grounding check did not detect playground floor.");
                Require(controller.Player.characterController != null && controller.Player.characterController.enabled, "Player controller disabled.");
                return "Production locomotion reports grounded after 1s at spawn.";
            }, 1);
            var gateStation = Object.FindAnyObjectByType<GateActivator>();
            bool[] leverStates = null;
            bool gateActive = false, gateFlag = false;
            for (int i = 0; i < 2; i++)
            {
                int leverIndex = i;
                Add("Wired lever interaction / " + (i + 1), () =>
                {
                    Require(gateStation != null && gateStation.levers.Length == 2 && gateStation.gate != null, "Complete lever/gate station missing.");
                    if (leverIndex == 0)
                    {
                        leverStates = gateStation.levers.Select(l => l.isActive).ToArray();
                        gateActive = gateStation.gate.activeSelf; gateFlag = gateStation.PortaoAtivo;
                        foreach (var lever in gateStation.levers) lever.isActive = false;
                        gateStation.gate.SetActive(true); gateStation.PortaoAtivo = false;
                    }
                    var target = gateStation.levers[leverIndex].GetComponent<Interactable>();
                    Require(target != null && target.lever == gateStation.levers[leverIndex], "Lever Interactable is not wired.");
                    var animation = gateStation.levers[leverIndex].animationTrigger;
                    Require(animation != null && animation.animator != null && animation.cam != null && animation.target != null,
                        "Homol AnimationTrigger camera, animator or target is missing.");
                    target.Interact(controller.Player);
                    Require(Time.timeScale == 0 && animation.cam.enabled && animation.animator.updateMode == AnimatorUpdateMode.UnscaledTime,
                        "Homol lever did not begin its camera animation with unscaled time.");
                }, () =>
                {
                    Require(gateStation.levers[leverIndex].isActive, "Interaction did not activate lever.");
                    var animation = gateStation.levers[leverIndex].animationTrigger;
                    Require(Time.timeScale == 1 && !animation.cam.enabled, "Homol animation event did not restore gameplay.");
                    Require(animation.target.activeSelf, "Homol AtivarCollider event did not toggle the lever indicator.");
                    if (leverIndex == 1) Require(!gateStation.gate.activeSelf && gateStation.PortaoAtivo, "Both levers failed to open gate.");
                    return "Production Interactable -> Lever -> AnimationTrigger events completed; gate active=" + gateStation.gate.activeSelf;
                }, 2.3);
            }
            Add("Restore lever station after smoke interaction", () =>
            {
                if (gateStation == null || leverStates == null) return;
                for (int i = 0; i < 2; i++)
                {
                    gateStation.levers[i].isActive = leverStates[i];
                    gateStation.cutScenePortao.stone[i].SetActive(leverStates[i]);
                }
                gateStation.gate.SetActive(gateActive); gateStation.PortaoAtivo = gateFlag;
            }, () => "Only runtime test station state restored; scene asset untouched.", .1);
            var platform = Object.FindAnyObjectByType<Plataforma>();
            bool platformActive = false, platformOccupied = false, platformFlag = false;
            float platformTimer = 0;
            Add("Production crumbling platform trigger / timer", () =>
            {
                Require(platform != null, "Crumbling platform station missing.");
                platformActive = platform.gameObject.activeSelf; platformOccupied = platform.isPlayerOnPlatform;
                platformFlag = platform.isPlatformActive; platformTimer = platform.timer;
                platform.gameObject.SetActive(true); platform.timer = 0; platform.isPlatformActive = true; platform.isPlayerOnPlatform = false;
                platform.OnTriggerEnter(controller.Player.GetComponent<Collider>());
                Require(platform.isPlayerOnPlatform, "Actual player collider did not satisfy production trigger.");
            }, () =>
            {
                Require(!platform.gameObject.activeSelf && !platform.isPlatformActive, "Platform failed to crumble after 3s.");
                return "Production OnTriggerEnter accepted real player collider and Update deactivated platform.";
            }, platform != null ? platform.timeToDestruct + .5 : 3.5);
            Add("Restore platform runtime test station", () =>
            {
                if (platform == null) return;
                platform.gameObject.SetActive(platformActive); platform.timer = platformTimer;
                platform.isPlayerOnPlatform = platformOccupied; platform.isPlatformActive = platformFlag;
            }, () => "Original runtime platform flags, timer and active state restored.", .1);
            for (int i = 0; i < controller.SkillCount; i++)
            {
                int index = i;
                Skill equipped = null;
                GameObject[] createdEffects = Array.Empty<GameObject>();
                Add("Skill activate / " + index, () =>
                {
                    controller.ResetSkills(); controller.EquipSkill(index);
                    var manager = controller.Player.GetComponent<PlayerSkillManager>();
                    var source = controller.catalog.skills.Where(s => s != null).ElementAt(index);
                    equipped = manager.slot[(int)source.skillType].skillSlot;
                    Require(equipped != null && equipped != source && equipped.isUnlocked, "Skill is not an unlocked runtime clone.");
                    var existing = new HashSet<string>(Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Select(o => o.GetEntityId().ToString()));
                    controller.ActivateSkill(index);
                    createdEffects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                        .Where(o => !existing.Contains(o.GetEntityId().ToString()) && (o.transform.parent == null || existing.Contains(o.transform.parent.gameObject.GetEntityId().ToString()))).ToArray();
                    Require(createdEffects.Length > 0, "Skill Activate produced no effect objects: " + equipped.name);
                    Require(equipped.cooldownDuration <= 0 || equipped.IsOnCooldown, "Activation did not start runtime cooldown.");
                }, () =>
                {
                    Require(equipped != null, "Skill missing after activation.");
                    // Cooldown may expire during the observation interval for short skills.
                    return equipped.name + "; type=" + equipped.GetType().Name + "; duration=" + equipped.cooldownDuration
                        + "; cooldown=" + equipped.IsOnCooldown + "; active=" + equipped.active + "; created effects="
                        + string.Join(", ", createdEffects.Where(o => o != null).Select(o => o.name)) + "; activation observed for 1s; effect cleanup observed next.";
                }, 1);
                Add("Skill reset / " + index, () => controller.ResetSkills(), () =>
                {
                    Require(!equipped.IsOnCooldown, "Runtime skill cooldown not reset.");
                    Require(controller.Player.GetComponent<PlayerSkillManager>().slot.All(s => s.canUse), "Slot cooldown not reset.");
                    var lingering = createdEffects.Where(o => o != null && o.GetComponent<DurationSkill>() != null).ToArray();
                    Require(lingering.Length == 0, "Timed effect remained beyond duration + 3s: " + string.Join(", ", lingering.Select(o => o.name)));
                    return "Cooldown and all slot gates reset; DurationSkill effects expired after observation window.";
                }, Mathf.Max(2, controller.catalog.skills.Where(s => s != null).ElementAt(index).cooldownDuration + 2));
            }
            for (int i = 0; i < controller.catalog.weapons.Length; i++)
            {
                var weapon = controller.catalog.weapons[i];
                if (weapon == null) continue;
                Add("Weapon equip / " + weapon.name, () => controller.EquipWeapon(weapon), () =>
                {
                    var equipped = controller.Player.playerInventoryManager.currentRightHandWeapon;
                    Require(equipped != null && equipped != weapon, "Equipped weapon absent or original asset used.");
                    var template = controller.Player.playerInventoryManager.weaponsInRightHandSlots[0];
                    Require(template != null && template != weapon && template.itemID == equipped.itemID && template.GetType() == equipped.GetType(),
                        "Weapon template/equipped clones have different itemID/type or use original asset.");
                    return "Equipped runtime clone " + equipped.name + ", ID=" + equipped.itemID;
                }, .3);
            }
            foreach (var spell in controller.catalog.spells)
            {
                if (spell == null) continue;
                Add("Spell equip / " + spell.name, () => controller.EquipSpell(spell), () =>
                {
                    var equipped = controller.Player.playerInventoryManager.currentSpell;
                    Require(equipped != null && equipped != spell, "Spell absent or original asset used.");
                    Require(equipped.GetType() == spell.GetType(), "Wrong spell type selected.");
                    Require(controller.Player.playerNetworkManager.currentSpellID.Value == equipped.itemID,
                        "Spell network ID does not match equipped clone.");
                    return "Production spell selected through network callback: " + equipped.name;
                }, .3);
            }
            for (int i = 0; i < controller.catalog.enemies.Length; i++)
            {
                int index = i;
                var entry = controller.catalog.enemies[i];
                GameObject[] enemyObjects = Array.Empty<GameObject>();
                UI_Boss_HP_Bar[] enemyBars = Array.Empty<UI_Boss_HP_Bar>();
                Add("Enemy spawn / " + entry.label, () =>
                {
                    controller.ClearEnemies(); controller.Heal(); controller.Teleport(controller.playerSpawn);
                    controller.SetEnemySelection(index, 1); controller.SpawnEnemies();
                    enemyObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude)
                        .Where(o => o.transform.parent == null && o.name == entry.prefab.name + "(Clone)").ToArray();
                }, () =>
                {
                    Require(controller.SpawnedEnemyCount == 1, "Expected one enemy; actual=" + controller.SpawnedEnemyCount + "; " + controller.Status);
                    Require(enemyObjects.Length == 1 && enemyObjects[0] != null, "Expected one living root matching the enemy prefab.");
                    var boss = enemyObjects[0].GetComponent<AIBossCharacterManager>();
                    if (boss != null)
                        enemyBars = Object.FindObjectsByType<UI_Boss_HP_Bar>(FindObjectsInactive.Include)
                            .Where(bar => typeof(UI_Boss_HP_Bar).GetField("bossCharacter", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(bar) as AIBossCharacterManager == boss).ToArray();
                    var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude)
                        .Where(a => !a.transform.IsChildOf(controller.Player.transform)).ToArray();
                    foreach (var agent in agents.Where(a => a.enabled)) Require(agent.isOnNavMesh, "Enemy agent off NavMesh: " + agent.name);
                    return entry.path + "; one tracked enemy; agents=" + agents.Length + "; AI ran for 1s.";
                }, 1);
                Add("Enemy clear / " + entry.label, () => controller.ClearEnemies(), () =>
                {
                    Require(controller.SpawnedEnemyCount == 0, "Clear left tracked enemies.");
                    Require(enemyObjects.All(o => o == null), "Clear left a spawned enemy GameObject alive.");
                    Require(enemyBars.All(bar => bar == null), "Clear left a boss health bar alive.");
                    return "Tracked count zero after destruction frame.";
                }, .2);
                Add("Enemy respawn / " + entry.label, () => controller.SpawnEnemies(), () =>
                {
                    Require(controller.SpawnedEnemyCount == 1, "Respawn failed: " + controller.Status);
                    controller.ClearEnemies(); return "Selection respawned, then cleared.";
                }, 1);
            }
            int cucaIndex = Array.FindIndex(controller.catalog.enemies,
                e => e.prefab != null && e.prefab.GetComponent<CucaBoss>() != null);
            HealthManager[] cucas = Array.Empty<HealthManager>();
            float firstHealth = 0, secondHealth = 0;
            var healthField = typeof(CucaBoss).GetField("healthManager", BindingFlags.Instance | BindingFlags.NonPublic);
            Add("Homol Cuca / two instances bind their own health", () =>
            {
                Require(cucaIndex >= 0, "Catalog has no Homol Cuca entry (generate CucaDev and refresh catalog).");
                controller.ClearEnemies(); controller.Heal(); controller.SetGodMode(true); controller.Teleport(controller.playerSpawn);
                var spear = controller.catalog.weapons.FirstOrDefault(w => w != null && w.weaponModel != null &&
                    w.weaponModel.GetComponentsInChildren<MeleeWeaponDamageCollider>(true).Any(m => m.CompareTag("Lanca")));
                Require(spear != null, "Catalog lacks a production spear with the Lanca damage collider.");
                controller.EquipWeapon(spear);
                controller.SetEnemySelection(cucaIndex, 2); controller.SpawnEnemies();
            }, () =>
            {
                cucas = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Exclude)
                    .OrderBy(c => c.GetEntityId().ToString()).ToArray();
                Require(controller.SpawnedEnemyCount == 2 && cucas.Length == 2, "Expected exactly two live Homol Cuca instances.");
                Require(healthField != null, "Production Cuca private fields changed; update smoke runner.");
                foreach (var cuca in cucas)
                {
                    var ai = cuca.GetComponent<CucaBoss>();
                    Require(ai != null && healthField.GetValue(ai) as HealthManager == cuca,
                        "CucaBoss did not bind its own HealthManager on " + cuca.name);
                }
                return "Two Homol Cuca roots spawned; each CucaBoss uses its own HealthManager after Start.";
            }, 1);
            Add("Homol Cuca / weapon trigger damages only contacted boss", () =>
            {
                Require(cucas.Length == 2 && cucas.All(c => c != null), "Two Cuca instances unavailable for damage case.");
                var melee = controller.Player.GetComponentsInChildren<MeleeWeaponDamageCollider>()
                    .FirstOrDefault(m => m.enabled && m.gameObject.activeInHierarchy && m.CompareTag("Lanca"));
                Require(melee != null, "Equipped player weapon damage component missing.");
                var trigger = melee.GetComponent<Collider>();
                Require(trigger != null && trigger.CompareTag("Lanca"), "Homol weapon trigger must have the production Lanca tag.");
                Require(melee.characterCausingDamage == controller.Player, "Equipped melee collider is not assigned to the production player.");
                firstHealth = cucas[0].currentHealth; secondHealth = cucas[1].currentHealth;
                var method = typeof(HealthManager).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(method != null, "Production melee trigger method unavailable.");
                float oldDamage = melee.physicalDamage;
                try { melee.physicalDamage = 7; method.Invoke(cucas[0], new object[] { trigger }); }
                finally { melee.physicalDamage = oldDamage; }
            }, () =>
            {
                float firstAfter = cucas[0].currentHealth;
                float secondAfter = cucas[1].currentHealth;
                Require(Mathf.Approximately(firstAfter, firstHealth - 7), "Contacted Cuca did not lose exactly seven health.");
                Require(Mathf.Approximately(secondAfter, secondHealth), "Uncontacted Cuca health changed (singleton damage regression).");
                return "Homol HealthManager.OnTriggerEnter invoked with the equipped Lanca collider against first Cuca; health "
                    + firstHealth + " -> " + firstAfter + "; second boss unchanged at " + secondAfter + ".";
            }, .2);
            Add("Homol Cuca / 15s projectile and laser observation", null, () =>
            {
                Require(cucas.Length == 2 && cucas.All(c => c != null), "Cuca disappeared during AI observation.");
                foreach (var cuca in cucas)
                {
                    var agent = cuca.GetComponent<NavMeshAgent>();
                    Require(agent != null && agent.enabled && agent.isOnNavMesh, "Cuca navigation unavailable during AI observation.");
                }
                return "Two live Cuca instances observed for 15s with their production AI; errors/exceptions captured for this phase."
                    + " Observation alone does not prove every random projectile/dash branch executed.";
            }, 15);
            Add("Legacy Cuca / clear both instances", () => controller.ClearEnemies(), () =>
            {
                Require(controller.SpawnedEnemyCount == 0 && cucas.All(c => c == null), "Cuca cleanup retained roots or tracked enemies.");
                return "Both legacy bosses destroyed; tracked count zero.";
            }, .2);
            for (int i = 0; includePreviews && i < controller.catalog.showcase.Length; i++)
            {
                int index = i;
                var entry = controller.catalog.showcase[i];
                if (entry.prefab == null) continue;
                Add("Safe preview / " + entry.label, () => controller.PreviewAsset(index), () =>
                {
                    var preview = controller.CurrentPreview;
                    Require(preview != null, "Preview was not created.");
                    Require(preview.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Preview retained scripts/network behaviour.");
                    Require(preview.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Preview retained enabled colliders.");
                    Require(preview.GetComponentsInChildren<Rigidbody>(true).All(b => b.isKinematic && !b.detectCollisions), "Preview retained physical bodies.");
                    foreach (var pair in singletonIdentities) Require(pair.Value.SequenceEqual(Identities(pair.Key)), "Preview changed manager instances: " + pair.Key.Name);
                    Require(controller.Network.IsHost && controller.Player.IsOwner, "Preview disturbed host/player ownership.");
                    return entry.path + "; scripts removed; collisions disabled; singleton IDs unchanged.";
                }, .25);
            }
            Add("Clear tool-activated skills and preview", () =>
            {
                controller.ClearActiveSkills(); controller.ClearPreview();
            }, () =>
            {
                Require(controller.ActiveToolSkillObjects == 0 && controller.CurrentPreview == null, "Tool effects or preview remained tracked.");
                Require(!controller.Player.playerNetworkManager.isPowerPet.Value, "Spirit power remained active after clearing tool effects.");
                return "Tool-created skill objects and preview cleared; spirit power reset.";
            }, .25);
            Add("Original catalog assets unchanged", null, () =>
            {
                foreach (var snapshot in report.originalAssets)
                {
                    Require(snapshot.asset != null && EditorJsonUtility.ToJson(snapshot.asset) == snapshot.json, "Original in-memory asset mutated: " + snapshot.path);
                    Require(HashAsset(snapshot.asset) == snapshot.sha256, "Original asset file modified: " + snapshot.path);
                }
                return report.originalAssets.Count + " original skill/item JSON snapshots and disk hashes unchanged.";
            }, .1);
            Add("Persistent character slots untouched", null, () =>
            {
                report.saveFilesAfter = SaveFiles();
                Require(Fingerprint(report.saveFilesBefore) == Fingerprint(report.saveFilesAfter), "Persistent characterSlot file set, content or modification timestamp changed.");
                return "Compared " + report.saveFilesBefore.Count + " files; path/hash/length-independent UTC timestamp unchanged. Empty set is explicitly reported.";
            }, .1);
            running = true;
            Flush();
            Application.logMessageReceived += CaptureLog;
            EditorApplication.update += Update;
            Debug.Log("[DevPlayground smoke] Started " + steps.Count + " cases; report will be written to Docs/HomolDevPlaygroundSmokeTest.json.");
        }

        private static void Add(string name, Action begin, Func<string> verify, double wait)
            => steps.Enqueue(new Step { name = name, begin = begin, verify = verify, wait = wait });
        private static void Update()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying || controller == null) { Finish(false); return; }
            if (current == null)
            {
                if (steps.Count == 0) { Finish(true); return; }
                current = steps.Dequeue();
                result = new CaseResult { name = current.name, passed = true };
                report.cases.Add(result);
                try { current.begin?.Invoke(); }
                catch (Exception e) { result.passed = false; result.errors.Add(e.ToString()); }
                deadline = EditorApplication.timeSinceStartup + current.wait;
            }
            if (EditorApplication.timeSinceStartup < deadline) return;
            try { result.details = current.verify?.Invoke(); }
            catch (Exception e) { result.passed = false; result.errors.Add(e.ToString()); }
            if (result.errors.Count > 0) result.passed = false;
            Flush();
            current = null;
        }
        private static void CaptureLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (result != null) { result.errors.Add(message + "\n" + stack); result.passed = false; }
        }
        private static void Flush()
        {
            Directory.CreateDirectory("Docs");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }
        private static void Finish(bool completed)
        {
            EditorApplication.update -= Update; running = false;
            result = new CaseResult { name = "Final runtime cleanup and isolation recheck", passed = true };
            report.cases.Add(result);
            try
            {
                if (controller != null && controller.Ready)
                {
                    controller.ClearEnemies(); controller.ClearActiveSkills(); controller.ClearPreview();
                    controller.ResetSkills(); controller.Heal(); controller.SetGodMode(originalGodMode);
                    controller.Player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                }
                report.saveFilesAfter = SaveFiles();
                Require(Fingerprint(report.saveFilesBefore) == Fingerprint(report.saveFilesAfter), "Save file set/content/timestamps changed during final cleanup.");
                foreach (var snapshot in report.originalAssets)
                    Require(snapshot.asset != null && EditorJsonUtility.ToJson(snapshot.asset) == snapshot.json && HashAsset(snapshot.asset) == snapshot.sha256,
                        "Original skill/item changed by final cleanup: " + snapshot.path);
                result.details = "Enemies cleared; skills reset; player healed; original position/god mode restored; original assets and saves unchanged.";
            }
            catch (Exception e) { result.passed = false; result.errors.Add(e.ToString()); }
            Application.logMessageReceived -= CaptureLog;
            report.completed = completed; report.passed = completed && report.cases.All(c => c.passed);
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Docs");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("[DevPlayground smoke] " + (report.passed ? "PASS" : "FAIL") + "; completed=" + completed
                + "; cases=" + report.cases.Count + "; failures=" + report.cases.Count(c => !c.passed));
        }
        private static void Damage(float damage)
        {
            var effect = ScriptableObject.CreateInstance<TakeDamageEffect>();
            effect.physicalDamage = damage; effect.contactPoint = controller.Player.transform.position;
            try { controller.Player.characterEffectsManager.ProcessInstantEffect(effect); }
            finally { Object.Destroy(effect); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string[] Identities(Type type) => Object.FindObjectsByType(type, FindObjectsInactive.Include)
            .Select(o => o.GetEntityId().ToString()).OrderBy(id => id).ToArray();
        private static string HashAsset(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "NO_DISK_FILE";
        }
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
        private static List<Snapshot> SaveFiles()
        {
            var found = new List<Snapshot>();
            if (!Directory.Exists(Application.persistentDataPath)) return found;
            foreach (var path in Directory.GetFiles(Application.persistentDataPath, "characterSlot*", SearchOption.AllDirectories).OrderBy(p => p))
                found.Add(new Snapshot { path = path, sha256 = Hash(File.ReadAllBytes(path)), lastWriteUtcTicks = File.GetLastWriteTimeUtc(path).Ticks });
            return found;
        }
        private static string Fingerprint(List<Snapshot> snapshots) => string.Join("\n", snapshots.Select(s => s.path + "|" + s.sha256 + "|" + s.lastWriteUtcTicks));
    }
}
