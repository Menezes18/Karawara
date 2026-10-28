#if UNITY_EDITOR
using System;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Karawara.Core.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace RPGKarawara.DevTools
{
    public static class LocalPlayerRegression
    {
        [MenuItem("Karawara/Validation/TOBE/Run Local Player Regression")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run this isolated component fixture outside Play Mode.");
            var type = typeof(CharacterManager);
            if (type.BaseType != typeof(MonoBehaviour)) throw new InvalidOperationException("Player still depends on a gameplay networking base.");
            foreach (var name in new[] { "State", "Registry", "LocalId", "IsInitialized" })
                if (type.GetProperty(name) == null) throw new InvalidOperationException("Missing local lifecycle property: " + name);
            foreach (var folder in new[] { "Assets/Scripts/States/Character", "Assets/Scripts/States/Effects", "Assets/Scripts/Item" })
                foreach (var path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    if (Regex.IsMatch(File.ReadAllText(path), @"Unity\.Netcode|NetworkVariable|NetworkBehaviour|ServerRpc|ClientRpc|\bIsOwner\b|\bIsServer\b"))
                        throw new InvalidOperationException("Player still depends on networking: " + path);
            VerifyComponentStateAndCleanup();
            Debug.Log("Local Player Regression passed: authoritative player state, health/HUD/vitality, jump/charge flags, repeated equipment commands, revive, idempotent initialization and shutdown. Movement, roll animation, caster timing and scene audio require separate Play Mode/standalone evidence.");
        }
        static void Awake(Component component)
        { component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static int NetworkCount() => Resources.FindObjectsOfTypeAll<MonoBehaviour>().Count(value => value != null && value.GetType().FullName == "Unity.Netcode.NetworkManager");
        static void VerifyComponentStateAndCleanup()
        {
            int networkBefore = NetworkCount();
            var playerObject = new GameObject("Local player component fixture");
            var uiObject = new GameObject("Local player HUD fixture");
            var barObject = new GameObject("Health", typeof(RectTransform), typeof(Slider), typeof(UI_StatBar));
            var previousUi = PlayerUIManager.instance;
            var previousCameraPlayer = PlayerCamera.instance != null ? PlayerCamera.instance.player : null;
            var previousSavePlayer = WorldSaveGameManager.instance != null ? WorldSaveGameManager.instance.player : null;
            var previousInputPlayer = PlayerInputManager.instance != null ? PlayerInputManager.instance.player : null;
            PlayerManager player = null;
            try
            {
                var ui = uiObject.AddComponent<PlayerUIManager>();
                var hud = uiObject.AddComponent<PlayerUIHudManager>();
                var bar = barObject.GetComponent<UI_StatBar>();
                Awake(bar);
                typeof(UI_StatBar).GetField("scaleBarLengthWithStats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bar, false);
                typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, bar);
                ui.playerUIHudManager = hud;
                PlayerUIManager.instance = ui;
                var values = playerObject.AddComponent<PlayerNetworkManager>();
                playerObject.AddComponent<PlayerStatsManager>();
                playerObject.AddComponent<PlayerInventoryManager>();
                player = playerObject.AddComponent<PlayerManager>();
                Awake(player); Awake(values);
                var registry = new CharacterRegistry();
                var state = new CharacterRuntimeState();
                player.InitializeLocal(state, registry);
                int id = player.LocalId;
                player.InitializeLocal(state, registry);
                Require(player.LocalId == id && registry.TryGet(id, out var registered) && ReferenceEquals(registered, state), "Initialization duplicated or lost identity.");
                int healthEvents = 0;
                values.currentHealth.OnValueChanged += (oldValue, nextValue) => healthEvents++;
                values.currentHealth.Value = 250;
                Require(state.Health == 250 && barObject.GetComponent<Slider>().value == state.Health && healthEvents == 1, "HP/HUD do not use the same state exactly once.");
                state.SetHealth(200);
                Require(values.currentHealth.Value == 200 && barObject.GetComponent<Slider>().value == 200 && healthEvents == 2, "Core writes fail to reach player presentation.");
                state.SetMaxHealth(300);
                Require(barObject.GetComponent<Slider>().value == state.Health && state.Health == 200, "Changing max HP visually refilled health.");
                values.isJumping.Value = true; values.isChargingRightSpell.Value = true; values.isMoving.Value = true;
                Require(state.IsJumping && state.IsChargingRightSpell && state.IsMoving, "Local action flags are mirrored instead of authoritative.");
                int commands = 0;
                var command = new ObservableValue<int>(3);
                command.OnValueChanged += (previous, next) => commands++;
                command.SetAndNotify(3); command.SetAndNotify(3);
                Require(commands == 2, "Repeated same-ID equipment commands disappeared.");
                state.SetHealth(0);
                Require(player.isDead.Value && !registry.TryGet(id, out _), "Death retained an active registry identity.");
                player.ReviveCharacter();
                Require(!state.IsDead && state.Health == state.MaxHealth && registry.TryGet(player.LocalId, out _), "Revive failed to restore local state/identity.");
                values.vitality.Value = 10;
                Require(state.MaxHealth == 150 && state.Health == 150 && barObject.GetComponent<Slider>().maxValue == 150, "Vitality side effects changed.");
                player.ShutdownLocal();
                int before = healthEvents;
                state.SetHealth(100);
                Require(!player.IsInitialized && player.Registry == null && !registry.TryGet(player.LocalId, out _) && healthEvents == before, "Shutdown retained listeners or registry identity.");
                Require(!state.IsJumping && !state.IsChargingRightSpell && !state.IsChargingAttack, "Shutdown retained transient action flags.");
                player.InitializeLocal(state, registry);
                values.currentHealth.Value = 90;
                Require(healthEvents == before + 1, "Reinitialization duplicated state listeners.");
                Require(NetworkCount() == networkBefore, "Fixture created a gameplay network manager.");
            }
            finally
            {
                if (player != null) player.ShutdownLocal();
                PlayerUIManager.instance = previousUi;
                if (PlayerCamera.instance != null) PlayerCamera.instance.player = previousCameraPlayer;
                if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.player = previousSavePlayer;
                if (PlayerInputManager.instance != null) PlayerInputManager.instance.BindSessionPlayer(previousInputPlayer);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(uiObject);
                UnityEngine.Object.DestroyImmediate(barObject);
            }
        }
    }
}
#endif
