using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RPGKarawara.SkillTree;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

namespace RPGKarawara.DevTools
{
    // All operational code is excluded from ordinary players. Catalogs remain serializable.
    public class DevPlaygroundController : MonoBehaviour
    {
        public DevPlaygroundCatalog catalog;
        public Transform playerSpawn;
        public Transform enemySpawn;
        public Transform showcaseAnchor;
        public Transform[] teleportPoints = Array.Empty<Transform>();
        public Collider[] navigationGeometry = Array.Empty<Collider>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly List<GameObject> ownedObjects = new List<GameObject>();
        readonly List<GameObject> spawnedEnemies = new List<GameObject>();
        readonly List<GameObject> spawnedEffects = new List<GameObject>();
        readonly List<Skill> skills = new List<Skill>();
        readonly List<ScriptableObject> ownedAssets = new List<ScriptableObject>();
        readonly Dictionary<ScriptableObject, ScriptableObject> clones = new Dictionary<ScriptableObject, ScriptableObject>();
        NetworkManager network;
        PlayerManager player;
        PlayerSkillManager skillManager;
        GameObject showcase;
        GameObject staging;
        AnimationClip previewAnimation;
        Vector3 previewPosition;
        Quaternion previewRotation;
        int previewVersion;
        NavMeshDataInstance navMeshInstance;
        NavMeshData navMeshData;
        bool open = true;
        bool ready;
        bool godMode;
        bool infiniteHealth;
        bool noCooldowns;
        bool aiPaused;
        int tab;
        int enemyIndex;
        int spawnCount = 1;
        string search = "";
        string status = "Starting playground…";
        string vitality = "30";
        string endurance = "30";
        Vector2 scroll;
        Rect window = new Rect(12, 12, 520, 700);
        readonly string[] tabs = { "Player", "Enemies", "Assets", "Systems" };
        public bool Ready => ready;
        public PlayerManager Player => player;
        public NetworkManager Network => network;
        public int SpawnedEnemyCount => spawnedEnemies.Count;
        public int ActiveToolSkillObjects => spawnedEffects.FindAll(effect => effect != null).Count;
        public int SkillCount => skills.Count;
        public GameObject CurrentPreview => showcase;
        public string Status => status;
        public void SetEnemySelection(int index, int count = 1) { enemyIndex = index; spawnCount = count; }
        public void SetGodMode(bool value) { godMode = value; if (player != null) player.characterNetworkManager.isInvulnerable.Value = value; }
        public void SetNoCooldowns(bool value) { noCooldowns = value; ResetCooldowns(true); }
        public void ActivateSkill(int index)
        {
            if (player == null || index < 0 || index >= skills.Count) return;
            var before = new HashSet<GameObject>(FindObjectsByType<GameObject>(FindObjectsInactive.Include));
            skills[index].Activate(player.gameObject);
            foreach (var created in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                if (!before.Contains(created) && (created.transform.parent == null || before.Contains(created.transform.parent.gameObject)))
                    spawnedEffects.Add(created);
        }
        public void ClearActiveSkills()
        {
            foreach (var effect in spawnedEffects) if (effect != null) Destroy(effect);
            spawnedEffects.Clear();
            if (player != null)
            {
                player.playerNetworkManager.isPowerPet.Value = false;
                player.characterNetworkManager.isInvulnerable.Value = godMode;
            }
            if (skillManager != null) skillManager.damageSpirit = 0;
        }
        public void ClearPreview()
        {
            previewVersion++;
            if (showcase != null) Destroy(showcase);
            showcase = null;
            previewAnimation = null;
        }
        public void EquipSkill(int index) { if (skillManager != null && index >= 0 && index < skills.Count) skillManager.slot[(int)skills[index].skillType].skillSlot = skills[index]; }
        public void ResetSkills() => ResetCooldowns(true);
        public void SetAttributes(int vitalityLevel, int enduranceLevel)
        { vitality = vitalityLevel.ToString(); endurance = enduranceLevel.ToString(); ApplyAttributes(); }
        public void PreviewAsset(int index) { if (index >= 0 && index < catalog.showcase.Length) StartCoroutine(Preview(catalog.showcase[index])); }

        IEnumerator Start()
        {
            if (catalog == null) { Fail("Missing playground catalog."); yield break; }
            if (NetworkManager.Singleton != null || PlayerManagerExists())
            { Fail("Start this scene directly in a fresh Play session; another gameplay session is active."); yield break; }

            // A boss can call SaveGame even on a disabled manager. Require the production guard.
            WorldSaveGameManager.DevPlaygroundIsolationActive = true;
            foreach (var skill in catalog.skills) Clone(skill);
            staging = new GameObject("DevTools inactive staging");
            staging.SetActive(false);
            ownedObjects.Add(staging);

            foreach (var prefab in catalog.managerPrefabs)
                if (prefab != null) CreatePrepared(prefab);

            if (WorldGameSessionManager.instance == null)
                AddManager<WorldGameSessionManager>("Development session");
            if (WorldObjectManager.instance == null)
            {
                var objects = AddManager<WorldObjectManager>("Development world objects");
                InitializeLists(objects);
            }
            if (WorldSaveGameManager.instance == null)
            {
                var saveRoot = new GameObject("Development in-memory save context");
                ownedObjects.Add(saveRoot);
                var save = saveRoot.AddComponent<WorldSaveGameManager>();
                save.enabled = false;
                save.currentCharacterData = new CharacterSaveData();
            }
            else
            {
                WorldSaveGameManager.instance.enabled = false;
                WorldSaveGameManager.instance.currentCharacterData = new CharacterSaveData();
            }
            CreatePrepared(catalog.cameraPrefab);
            CreatePrepared(catalog.inputPrefab);
            CreatePrepared(catalog.hudPrefab);
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Development UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedObjects.Add(events);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (PlayerCamera.instance == null || PlayerInputManager.instance == null || PlayerUIManager.instance == null ||
                PlayerUIManager.instance.playerUIHudManager == null || WorldItemDatabase.Instance == null || WorldUtilityManager.Instance == null)
            { Fail("Production camera, input, HUD, item database or utility manager is missing."); yield break; }
            CreatePrepared(catalog.networkManagerPrefab);
            network = NetworkManager.Singleton;
            if (network == null || catalog.playerPrefab == null)
            { Fail("Production NetworkManager or player prefab is missing."); yield break; }
            network.NetworkConfig.PlayerPrefab = catalog.playerPrefab;
            network.NetworkConfig.EnableSceneManagement = false;
            // This sandbox hosts locally; an ephemeral port avoids production-session collisions.
            var transport = network.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
            if (transport != null) transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            // Register explicitly before hosting, preserving production prefab identity/hash.
            var registered = new HashSet<GameObject>();
            foreach (var entry in catalog.enemies)
                if (entry.prefab != null && entry.prefab.GetComponent<NetworkObject>() != null && registered.Add(entry.prefab))
                    RegisterPrefab(entry.prefab);
            BuildNavigation();
            yield return null; // Production InputManager.Start disables controls.
            if (!network.StartHost()) { Fail("Local host failed to start."); yield break; }
            float deadline = Time.realtimeSinceStartup + 15;
            while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (network.LocalClient == null || network.LocalClient.PlayerObject == null)
            { Fail("Host started but did not spawn the production player."); yield break; }
            player = network.LocalClient.PlayerObject.GetComponent<PlayerManager>();
            if (player == null) { Fail("Configured player has no PlayerManager."); yield break; }
            var inventory = player.playerInventoryManager;
            inventory.currentRightHandWeapon = Clone(inventory.currentRightHandWeapon);
            inventory.currentLeftHandWeapon = Clone(inventory.currentLeftHandWeapon);
            for (int i = 0; i < inventory.weaponsInRightHandSlots.Length; i++) inventory.weaponsInRightHandSlots[i] = Clone(inventory.weaponsInRightHandSlots[i]);
            for (int i = 0; i < inventory.weaponsInLeftHandSlots.Length; i++) inventory.weaponsInLeftHandSlots[i] = Clone(inventory.weaponsInLeftHandSlots[i]);
            yield return null; // Finish production Start methods before applying tools.
            PlayerCamera.instance.canFollow = true;
            WorldGameSessionManager.instance.AddPlayerToActivePlayersList(player);
            SetupSkills();
            SetupRespawn();
            Teleport(playerSpawn);
            ApplyAttributes();
            Heal();
            ready = true;
            SetOpen(open);
            status = "Production player and local NGO host ready. Saves isolated. F1 toggles tools.";
        }

        bool PlayerManagerExists() => FindAnyObjectByType<PlayerManager>() != null;
        void Fail(string message) { status = message; Debug.LogError("[DevPlayground] " + message, this); }

        T AddManager<T>(string name) where T : Component
        {
            var root = new GameObject(name);
            ownedObjects.Add(root);
            return root.AddComponent<T>();
        }

        GameObject CreatePrepared(GameObject prefab)
        {
            if (prefab == null) return null;
            var root = Instantiate(prefab, staging.transform);
            root.SetActive(false);
            // Production managers assign item/action IDs. Clone mutable assets before Awake/Start.
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.IsInitOnly) continue;
                    if (typeof(Skill).IsAssignableFrom(field.FieldType)) field.SetValue(component, Clone(field.GetValue(component) as Skill));
                    if (field.FieldType == typeof(Skill[]) && field.GetValue(component) is Skill[] sourceSkills)
                    {
                        var copies = new Skill[sourceSkills.Length];
                        for (int i = 0; i < copies.Length; i++) copies[i] = Clone(sourceSkills[i]);
                        field.SetValue(component, copies);
                    }
                }
                if (component is WorldItemDatabase database)
                {
                    var field = typeof(WorldItemDatabase).GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic);
                    var originals = field.GetValue(database) as List<WeaponItem>;
                    var copies = new List<WeaponItem>();
                    if (originals != null) foreach (var weapon in originals) copies.Add(Clone(weapon));
                    foreach (var weapon in catalog.weapons) if (weapon != null && !copies.Contains(Clone(weapon))) copies.Add(Clone(weapon));
                    field.SetValue(database, copies);
                    database.unarmedWeapon = Clone(database.unarmedWeapon);
                    if (database.unarmedWeapon != null && !copies.Contains(database.unarmedWeapon)) copies.Add(database.unarmedWeapon);
                }
                if (component is WorldActionManager actions)
                    for (int i = 0; i < actions.weaponItemActions.Length; i++) actions.weaponItemActions[i] = Clone(actions.weaponItemActions[i]);
                if (component is WorldCharacterEffectsManager effects)
                {
                    var field = typeof(WorldCharacterEffectsManager).GetField("instantEffects", BindingFlags.Instance | BindingFlags.NonPublic);
                    var originals = field.GetValue(effects) as List<InstantCharacterEffect>;
                    var copies = new List<InstantCharacterEffect>();
                    if (originals != null) foreach (var effect in originals) copies.Add(Clone(effect));
                    field.SetValue(effects, copies);
                    effects.takeDamageEffect = Clone(effects.takeDamageEffect);
                    effects.TakeBlockedDamageEffect = Clone(effects.TakeBlockedDamageEffect);
                }
                if (component is WorldUtilityManager utility)
                {
                    var field = typeof(WorldUtilityManager).GetField("enviroLayers", BindingFlags.Instance | BindingFlags.NonPublic);
                    LayerMask existing = (LayerMask)field.GetValue(utility);
                    field.SetValue(utility, (LayerMask)(existing.value | LayerMask.GetMask("Environment")));
                }
                if (component is WorldAIManager || component is WorldObjectManager) InitializeLists(component);
                if (component is PlayerUIHudManager hud)
                {
                    var field = typeof(PlayerUIHudManager).GetField("SkillSlot", BindingFlags.Instance | BindingFlags.NonPublic);
                    if ((field.GetValue(hud) as GameObject) == null)
                    {
                        var slots = new GameObject("Development skill slots", typeof(RectTransform));
                        slots.transform.SetParent(hud.transform, false);
                        field.SetValue(hud, slots);
                    }
                }
                if (component is WorldSaveGameManager save) { save.enabled = false; save.currentCharacterData = new CharacterSaveData(); }
            }
            root.transform.SetParent(null);
            ownedObjects.Add(root);
            root.SetActive(true);
            return root;
        }

        T Clone<T>(T source) where T : ScriptableObject
        {
            if (source == null) return null;
            if (clones.TryGetValue(source, out var copy)) return (T)copy;
            var result = Instantiate(source);
            result.hideFlags = HideFlags.DontSave;
            clones.Add(source, result);
            ownedAssets.Add(result);
            if (result is WeaponItem weapon)
            {
                weapon.oh_RB_Action = Clone(weapon.oh_RB_Action);
                weapon.oh_RT_Action = Clone(weapon.oh_RT_Action);
            }
            return result;
        }

        void InitializeLists(object manager)
        {
            foreach (var field in manager.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>) && field.GetValue(manager) == null)
                    field.SetValue(manager, Activator.CreateInstance(field.FieldType));
        }

        void RegisterPrefab(GameObject prefab)
        {
            // Prefabs is populated on Initialize; serialized lists are authoritative before hosting.
            foreach (var list in network.NetworkConfig.Prefabs.NetworkPrefabsLists)
                if (list != null && list.Contains(prefab)) return;
            foreach (var entry in network.NetworkConfig.Prefabs.Prefabs)
                if (entry.Prefab == prefab) return;
            network.AddNetworkPrefab(prefab);
        }

        void BuildNavigation()
        {
            var sources = new List<NavMeshBuildSource>();
            foreach (var collider in navigationGeometry)
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (collider is BoxCollider box)
                    sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                        transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center), size = box.size, area = 0 });
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                    sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh,
                        transform = mesh.transform.localToWorldMatrix, sourceObject = mesh.sharedMesh, area = 0 });
            }
            if (sources.Count == 0 || NavMesh.GetSettingsCount() == 0) { status = "No navigation geometry; AI movement unavailable."; return; }
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool firstBounds = true;
            foreach (var collider in navigationGeometry)
                if (collider != null && firstBounds) { bounds = collider.bounds; firstBounds = false; }
            foreach (var collider in navigationGeometry) if (collider != null) bounds.Encapsulate(collider.bounds);
            bounds.Expand(10);
            navMeshData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, bounds, Vector3.zero, Quaternion.identity);
            if (navMeshData != null) navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
        }

        void SetupSkills()
        {
            skillManager = player.GetComponent<PlayerSkillManager>();
            if (skillManager == null) skillManager = player.gameObject.AddComponent<PlayerSkillManager>();
            skillManager.slot = new[] { new SkillSlot(), new SkillSlot(), new SkillSlot() };
            foreach (var source in catalog.skills)
            {
                var skill = Clone(source);
                if (skill == null) continue;
                skill.isUnlocked = true;
                skill.ResetCooldown();
                skills.Add(skill);
                int slot = (int)skill.skillType;
                if (slot >= 0 && slot < 3 && skillManager.slot[slot].skillSlot == null) skillManager.slot[slot].skillSlot = skill;
            }
            // Some HUD variants do not include cooldown images. Keep production API safe.
            if (SkillCooldownUI.instance == null)
            {
                var canvas = new GameObject("Development skill cooldown backing UI", typeof(RectTransform), typeof(Canvas));
                ownedObjects.Add(canvas);
                var ui = canvas.AddComponent<SkillCooldownUI>();
                ui.enabled = false;
                ui.skillCooldownImage = new UnityEngine.UI.Image[3];
                for (int i = 0; i < 3; i++)
                {
                    var image = new GameObject("Cooldown " + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    image.transform.SetParent(canvas.transform);
                    ui.skillCooldownImage[i] = image.GetComponent<UnityEngine.UI.Image>();
                    ui.skillCooldownImage[i].enabled = false;
                }
            }
        }

        void SetupRespawn()
        {
            var respawn = FindAnyObjectByType<SavePositionOnTrigger>();
            if (respawn == null) respawn = gameObject.AddComponent<SavePositionOnTrigger>();
            respawn.savedPostion = new[] { playerSpawn != null ? playerSpawn : transform };
            respawn.numberSave = 0;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) SetOpen(!open);
            if (!ready || player == null) return;
            if (godMode) player.characterNetworkManager.isInvulnerable.Value = true;
            if (infiniteHealth && player.playerNetworkManager.currentHealth.Value < player.playerNetworkManager.maxHealth.Value) Heal();
            if (noCooldowns) ResetCooldowns(false);
            spawnedEnemies.RemoveAll(item => item == null);
        }

        void LateUpdate()
        {
            if (showcase == null || previewAnimation == null) return;
            previewAnimation.SampleAnimation(showcase, previewAnimation.length > 0 ? Time.unscaledTime % previewAnimation.length : 0);
            showcase.transform.SetPositionAndRotation(previewPosition, previewRotation);
        }

        void SetOpen(bool value)
        {
            open = value;
            Cursor.visible = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            if (!ready || PlayerInputManager.instance == null) return;
            var input = PlayerInputManager.instance;
            input.enabled = !value;
            if (value)
            {
                input.playerControls?.Disable();
                input.vertical_Input = input.horizontal_Input = input.moveAmount = 0;
                input.cameraHorizontal_Input = input.cameraVertical_Input = 0;
            }
            else input.playerControls?.Enable();
            if (skillManager != null) skillManager.enabled = !value;
        }

        public void Heal()
        {
            if (player == null) return;
            if (player.isDead.Value) player.ReviveCharacter();
            player.playerNetworkManager.currentHealth.Value = player.playerNetworkManager.maxHealth.Value;
        }

        void ApplyAttributes()
        {
            if (player == null) return;
            if (int.TryParse(vitality, out int healthStat)) player.playerNetworkManager.vitality.Value = Mathf.Clamp(healthStat, 1, 10000);
            if (int.TryParse(endurance, out int enduranceStat)) player.playerNetworkManager.endurance.Value = Mathf.Clamp(enduranceStat, 1, 10000);
            player.playerNetworkManager.SetNewMaxHealthValue(0, player.playerNetworkManager.vitality.Value);
        }

        void ResetCooldowns(bool cancelTimers)
        {
            foreach (var skill in skills) skill.ResetCooldown();
            if (skillManager == null) return;
            if (cancelTimers) skillManager.StopAllCoroutines();
            foreach (var slot in skillManager.slot) slot.canUse = true;
            if (SkillCooldownUI.instance != null && SkillCooldownUI.instance.skillCooldownImage != null)
                foreach (var image in SkillCooldownUI.instance.skillCooldownImage) if (image != null) image.enabled = false;
        }

        public void Teleport(Transform target)
        {
            if (player == null || target == null) return;
            bool wasEnabled = player.characterController != null && player.characterController.enabled;
            if (wasEnabled) player.characterController.enabled = false;
            player.transform.SetPositionAndRotation(target.position, target.rotation);
            player.characterNetworkManager.networkPosition.Value = target.position;
            player.characterNetworkManager.networkRotation.Value = target.rotation;
            if (wasEnabled) player.characterController.enabled = true;
        }

        public void SpawnEnemies()
        {
            if (network == null || !network.IsServer || catalog.enemies.Length == 0) return;
            enemyIndex = Mathf.Clamp(enemyIndex, 0, catalog.enemies.Length - 1);
            var entry = catalog.enemies[enemyIndex];
            if (entry.prefab == null) return;
            bool networkCharacter = entry.prefab.GetComponent<AICharacterManager>() != null;
            if (networkCharacter && entry.prefab.GetComponent<NetworkObject>() == null)
            { status = "Network enemy is missing its NetworkObject."; return; }
            Vector3 center = enemySpawn != null ? enemySpawn.position : player.transform.position + player.transform.forward * 8;
            int baseIndex = spawnedEnemies.Count;
            for (int i = 0; i < Mathf.Clamp(spawnCount, 1, 50); i++)
            {
                int gridIndex = baseIndex + i;
                Vector3 position = center + new Vector3((gridIndex % 7 - 3) * 3, 0, -(gridIndex / 7) * 3);
                if (!NavMesh.SamplePosition(position, out var hit, 5, NavMesh.AllAreas))
                { status = "No NavMesh at enemy spawn. Build navigation geometry first."; break; }
                var enemy = Instantiate(entry.prefab, hit.position, Quaternion.identity);
                spawnedEnemies.Add(enemy);
                var character = enemy.GetComponent<AICharacterManager>();
                if (character != null && character.isBoss && character.portal == null)
                {
                    character.portal = new GameObject("Development boss exit marker");
                    character.portal.transform.SetParent(enemy.transform);
                    character.portal.SetActive(false);
                }
                var boss = enemy.GetComponent<AIBossCharacterManager>();
                if (boss != null)
                {
                    var data = WorldSaveGameManager.instance.currentCharacterData;
                    data.bossesAwakened[boss.bossID] = false;
                    data.bossesDefeated[boss.bossID] = false;
                }
                if (networkCharacter) enemy.GetComponent<NetworkObject>().Spawn();
                if (character != null && WorldAIManager.instance != null) WorldAIManager.instance.AddCharacterToSpawnedCharactersList(character);
                var legacyControl = enemy.GetComponent<global::BossControl>();
                if (legacyControl != null)
                    typeof(global::BossControl).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(legacyControl, player.transform);
                if (boss != null) boss.WakeBoss();
                if (aiPaused) SetEnemyPaused(enemy, true);
            }
        }

        public void ClearEnemies()
        {
            if (network == null || !network.IsServer) return;
            if (player != null) { player.playerNetworkManager.isLockedOn.Value = false; PlayerCamera.instance.ClearLockOnTargets(); }
            foreach (var enemy in spawnedEnemies)
            {
                if (enemy == null) continue;
                var boss = enemy.GetComponent<AIBossCharacterManager>();
                if (boss != null && PlayerUIManager.instance != null)
                {
                    var hud = PlayerUIManager.instance.playerUIHudManager;
                    if (hud != null && hud.bossHealthBarParent != null)
                        foreach (var bar in hud.bossHealthBarParent.GetComponentsInChildren<UI_Boss_HP_Bar>(true))
                            if (typeof(UI_Boss_HP_Bar).GetField("bossCharacter", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(bar) as AIBossCharacterManager == boss)
                                Destroy(bar.gameObject);
                }
                var character = enemy.GetComponent<AICharacterManager>();
                if (character != null && WorldAIManager.instance != null)
                    foreach (var field in typeof(WorldAIManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                        if (field.GetValue(WorldAIManager.instance) is IList list && list.Contains(character)) list.Remove(character);
                var obj = enemy.GetComponent<NetworkObject>();
                if (obj != null && obj.IsSpawned) obj.Despawn(); else Destroy(enemy);
            }
            spawnedEnemies.Clear();
        }

        IEnumerator Preview(DevPlaygroundAsset entry)
        {
            if (entry.prefab == null) yield break;
            int version = ++previewVersion;
            previewAnimation = null;
            if (showcase != null) Destroy(showcase);
            var root = Instantiate(entry.prefab, staging.transform);
            root.SetActive(false);
            // Awake runs on activation even for disabled scripts: remove them while inactive.
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) Destroy(behaviour);
            // Animator state behaviours and animation events can call removed gameplay scripts.
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var animation in root.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
#if UNITY_EDITOR
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
#endif
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            yield return null;
            if (root == null) yield break;
            if (version != previewVersion) { Destroy(root); yield break; }
            root.transform.SetParent(null);
            root.transform.position = showcaseAnchor != null ? showcaseAnchor.position : Vector3.zero;
            root.SetActive(true);
            showcase = root;
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 anchor = showcaseAnchor != null ? showcaseAnchor.position : Vector3.zero;
                root.transform.position += anchor - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            previewPosition = root.transform.position;
            previewRotation = root.transform.rotation;
            status = "Visual preview: " + entry.path;
        }

        public void EquipWeapon(WeaponItem source)
        {
            if (player == null || source == null) return;
            var weapon = Clone(source);
            var registered = WorldItemDatabase.Instance.GetWeaponByID(weapon.itemID);
            if (registered != weapon) { status = "Weapon is absent from the production item database."; return; }
            player.playerInventoryManager.weaponsInRightHandSlots[0] = weapon;
            player.playerInventoryManager.rightHandWeaponIndex = 0;
            if (player.playerNetworkManager.currentRightHandWeaponID.Value == weapon.itemID)
                player.playerNetworkManager.OnCurrentRightHandWeaponIDChange(weapon.itemID, weapon.itemID);
            else player.playerNetworkManager.currentRightHandWeaponID.Value = weapon.itemID;
        }

        void OnGUI()
        {
            if (!open) { GUI.Label(new Rect(12, 12, 350, 25), "Dev Playground · F1 tools"); return; }
            window.width = Mathf.Min(560, Screen.width - 24);
            window.height = Mathf.Min(760, Screen.height - 24);
            window = GUILayout.Window(0x4b415241, window, DrawWindow, "Karawara · Dev Playground · F1");
        }

        void DrawWindow(int id)
        {
            GUILayout.Label(status);
            if (!ready || player == null || network == null) { GUI.DragWindow(); return; }
            tab = GUILayout.Toolbar(tab, tabs);
            scroll = GUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawPlayer();
            if (tab == 1) DrawEnemies();
            if (tab == 2) DrawAssets();
            if (tab == 3) DrawSystems();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        void DrawPlayer()
        {
            GUILayout.Label($"Health {player.playerNetworkManager.currentHealth.Value}/{player.playerNetworkManager.maxHealth.Value}");
            bool nextGodMode = GUILayout.Toggle(godMode, "God mode (production invulnerability)");
            if (nextGodMode != godMode) { godMode = nextGodMode; player.characterNetworkManager.isInvulnerable.Value = godMode; }
            infiniteHealth = GUILayout.Toggle(infiniteHealth, "Continuously restore health");
            noCooldowns = GUILayout.Toggle(noCooldowns, "Ignore skill cooldowns");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal / revive")) Heal();
            if (GUILayout.Button("Respawn")) { Heal(); Teleport(playerSpawn); }
            if (GUILayout.Button("Reset cooldowns")) ResetCooldowns(true);
            if (GUILayout.Button("Clear skills activated from tools")) ClearActiveSkills();
            GUILayout.EndHorizontal();
            GUILayout.Label("Vitality / endurance");
            GUILayout.BeginHorizontal(); vitality = GUILayout.TextField(vitality); endurance = GUILayout.TextField(endurance);
            if (GUILayout.Button("Apply")) ApplyAttributes(); GUILayout.EndHorizontal();
            GUILayout.Label("Stamina and mana are not active resources in the production scripts.");
            GUILayout.Label("Movement attributes (runtime instance only)");
            foreach (var setting in new[] { "walkingSpeed", "runningSpeed", "sprintingSpeed", "jumpHeight", "dodgeDistance" })
            {
                var field = typeof(PlayerLocomotionManager).GetField(setting, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                float current = (float)field.GetValue(player.playerLocomotionManager);
                GUILayout.Label(setting + ": " + current.ToString("0.0"));
                float value = GUILayout.HorizontalSlider(current, 0.1f, 30);
                if (!Mathf.Approximately(value, current)) field.SetValue(player.playerLocomotionManager, value);
            }
            var currentWeapon = player.playerInventoryManager.currentRightHandWeapon;
            if (currentWeapon != null)
            {
                GUILayout.Label("Equipped weapon physical damage: " + currentWeapon.physicalDamage);
                int damage = Mathf.RoundToInt(GUILayout.HorizontalSlider(currentWeapon.physicalDamage, 0, 2000));
                if (damage != currentWeapon.physicalDamage)
                {
                    currentWeapon.physicalDamage = damage;
                    player.playerEquipmentManager.LoadRightWeapon();
                }
            }
            GUILayout.Label("All catalog skills are unlocked on runtime clones. Equip by category, or activate directly.");
            foreach (var skill in skills)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(skill.name + " · " + skill.skillType);
                if (GUILayout.Button("Equip", GUILayout.Width(60))) skillManager.slot[(int)skill.skillType].skillSlot = skill;
                if (GUILayout.Button("Activate", GUILayout.Width(70))) ActivateSkill(skills.IndexOf(skill));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Production weapons");
            foreach (var weapon in catalog.weapons)
                if (weapon != null && GUILayout.Button("Equip " + weapon.name)) EquipWeapon(weapon);
            GUILayout.Label("Production elements");
            var element = player.GetComponent<ElementManager>();
            if (element != null)
                foreach (Element choice in Enum.GetValues(typeof(Element)))
                    if (GUILayout.Button(choice.ToString())) element.SetElement(choice);
            GUILayout.Label("Production transformation choices");
            bool hasTransformationChoices = false;
            foreach (var choice in FindObjectsByType<ChoseAnimalElement>(FindObjectsInactive.Include))
                if (choice.animal != null && choice.roda != null)
                { hasTransformationChoices = true; if (GUILayout.Button(choice.element)) SelectTransformation(choice); }
            if (!hasTransformationChoices) GUILayout.Label("Legacy animal transformation UI is not wired in the current project.");
        }

        void DrawEnemies()
        {
            int living = 0;
            foreach (var enemy in spawnedEnemies)
                if (enemy != null && (enemy.GetComponent<AICharacterManager>() == null || !enemy.GetComponent<AICharacterManager>().isDead.Value)) living++;
            GUILayout.Label($"Spawned: {spawnedEnemies.Count} · alive: {living} · server: {network.IsServer}");
            if (catalog.enemies.Length == 0) { GUILayout.Label("No production enemy prefabs found."); return; }
            GUILayout.Label("Choose any production enemy");
            for (int i = 0; i < catalog.enemies.Length; i++)
                if (GUILayout.Toggle(enemyIndex == i, catalog.enemies[i].label)) enemyIndex = i;
            GUILayout.Label("Count: " + spawnCount);
            spawnCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(spawnCount, 1, 50));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn")) SpawnEnemies();
            if (GUILayout.Button("Spawn all")) SpawnAllEnemies();
            if (GUILayout.Button("Clear")) ClearEnemies();
            if (GUILayout.Button("Respawn selection")) { ClearEnemies(); SpawnEnemies(); }
            GUILayout.EndHorizontal();
            bool pause = GUILayout.Toggle(aiPaused, "Pause spawned AI state machines");
            if (pause != aiPaused)
            {
                aiPaused = pause;
                foreach (var enemy in spawnedEnemies) if (enemy != null) SetEnemyPaused(enemy, pause);
            }
            if (GUILayout.Button("Defeat spawned enemies"))
                foreach (var enemy in spawnedEnemies)
                    if (enemy != null)
                    {
                        var character = enemy.GetComponent<AICharacterManager>();
                        var health = enemy.GetComponent<CucaBossHealth>();
                        if (character != null) character.characterNetworkManager.currentHealth.Value = 0;
                        else if (health != null) health.TakeDamage(float.MaxValue);
                        else Destroy(enemy);
                    }
        }

        public void SpawnAllEnemies()
        {
            int selection = enemyIndex;
            int count = spawnCount;
            spawnCount = 1;
            for (int i = 0; i < catalog.enemies.Length; i++) { enemyIndex = i; SpawnEnemies(); }
            enemyIndex = selection;
            spawnCount = count;
        }

        void SetEnemyPaused(GameObject enemy, bool paused)
        {
            foreach (var behaviour in enemy.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour is AICharacterManager || behaviour is BossAISystem || behaviour is global::BossControl || behaviour is global::EnemyAI)
                { behaviour.enabled = !paused; if (paused) behaviour.StopAllCoroutines(); }
            foreach (var agent in enemy.GetComponentsInChildren<NavMeshAgent>(true))
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = paused;
        }

        public void SelectTransformation(ChoseAnimalElement choice)
        {
            if (choice == null || choice.animal == null || choice.roda == null) return;
            if (choice.roda.CurrentAnimal != null) choice.roda.CurrentAnimal.SetActive(false);
            choice.roda.CurrentAnimal = choice.animal;
            choice.animal.SetActive(true);
            choice.roda.elementoAtual = choice.element;
            choice.roda.canTransform = true;
            if (choice.displayElement != null) choice.displayElement.text = choice.element;
        }

        void DrawAssets()
        {
            GUILayout.Label("Safe visual previews remove scripts/network behavior and disable collisions.");
            search = GUILayout.TextField(search);
            if (GUILayout.Button("Clear preview")) ClearPreview();
            if (showcase != null)
            {
                GUILayout.Label("Preview animations (sampled on this model)");
                if (GUILayout.Button("Stop animation")) previewAnimation = null;
                foreach (var clip in catalog.animations)
                    if (clip != null && MatchesSearch(clip.name) && GUILayout.Button("Animation · " + clip.name))
                    {
                        foreach (var animator in showcase.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                        previewAnimation = clip;
                    }
                GUILayout.Label("Preview materials");
                foreach (var material in catalog.materials)
                    if (material != null && MatchesSearch(material.name) && GUILayout.Button("Material · " + material.name))
                        foreach (var renderer in showcase.GetComponentsInChildren<Renderer>(true))
                        {
                            var slots = renderer.sharedMaterials;
                            for (int i = 0; i < slots.Length; i++) slots[i] = material;
                            renderer.sharedMaterials = slots;
                        }
            }
            foreach (var entry in catalog.showcase)
                if (entry.prefab != null && (string.IsNullOrEmpty(search) || (entry.label + entry.path).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    if (GUILayout.Button(entry.label)) StartCoroutine(Preview(entry));
        }

        bool MatchesSearch(string label) => string.IsNullOrEmpty(search) || label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        void DrawSystems()
        {
            GUILayout.Label("Teleport to a test station");
            foreach (var point in teleportPoints) if (point != null && GUILayout.Button(point.name)) Teleport(point);
            if (GUILayout.Button("Interact using production interaction manager")) player.playerInteractionManager.Interact();
            if (GUILayout.Button("Light attack using equipped weapon"))
            {
                var weapon = player.playerInventoryManager.currentRightHandWeapon;
                if (weapon != null) { player.playerNetworkManager.SetCharacterActionHand(true); player.playerCombatManager.PerformWeaponBasedAction(weapon.oh_RB_Action, weapon); }
            }
            GUILayout.Label("Effects (safe visual preview; skill activation above tests live effects)");
            foreach (var entry in catalog.effects) if (entry.prefab != null && GUILayout.Button(entry.label)) StartCoroutine(Preview(entry));
            GUILayout.Label("Interactable assets (visual preview; scene stations test wired interactions)");
            foreach (var entry in catalog.interactables) if (entry.prefab != null && GUILayout.Button(entry.label)) StartCoroutine(Preview(entry));
            GUILayout.Label($"NGO host {network.IsHost}; player owner {player.IsOwner}; NavMesh valid {navMeshInstance.valid}");
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
            if (showcase != null) Destroy(showcase);
            // Keep isolation active until host callbacks and owned objects have been torn down.
            if (network != null && network.IsListening) network.Shutdown();
            if (player != null) Destroy(player.gameObject);
            foreach (var enemy in spawnedEnemies) if (enemy != null) Destroy(enemy);
            foreach (var effect in spawnedEffects) if (effect != null) Destroy(effect);
            foreach (var root in ownedObjects) if (root != null) Destroy(root);
            foreach (var asset in ownedAssets) if (asset != null) Destroy(asset);
            if (navMeshInstance.valid) navMeshInstance.Remove();
            if (navMeshData != null) Destroy(navMeshData);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
#else
        void Awake() { gameObject.SetActive(false); }
#endif
    }
}
