#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Karawara.Core.Characters;
namespace RPGKarawara.DevTools
{
    public static class LocalWorldRegression
    {
        [MenuItem("Karawara/Validation/TOBE/Run Local World Regression")]
        public static void Run()
        {
            foreach (var folder in new[] { "Assets/Scripts/States/AI", "Assets/Scripts/States/State", "Assets/Scripts/States/Colliders" })
                foreach (var path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    if (Regex.IsMatch(File.ReadAllText(path), @"Unity\.Netcode|NetworkVariable|NetworkBehaviour|NetworkObjectId|\bIsOwner\b|\bIsServer\b|\bIsSpawned\b|ServerRpc|ClientRpc"))
                        throw new InvalidOperationException("World gameplay still has a networking dependency: " + path);
            var registry = new CharacterRegistry();
            var attacker = new CharacterRuntimeState();
            var victim = new CharacterRuntimeState();
            registry.Register(attacker); int victimId = registry.Register(victim);
            attacker.SetTarget(victim);
            registry.Unregister(victimId);
            if (attacker.Target != null) throw new InvalidOperationException("Unloaded target remains reachable.");
            if (typeof(AICharacterManager).GetMethod("InitializeLocal").DeclaringType != typeof(AICharacterManager))
                throw new InvalidOperationException("AI lifecycle not migrated.");
            if (typeof(FogWallInteractable).BaseType != typeof(MonoBehaviour))
                throw new InvalidOperationException("Fog wall still needs a network session.");
            VerifyOwnedStateClones();
            Debug.Log("Local World Regression: dependency/lifecycle/target cleanup checks passed. Scene AI, damage, boss, checkpoint and unload gameplay require separate Play Mode/standalone evidence.");
        }
        static void VerifyOwnedStateClones()
        {
            var firstObject = new GameObject("Local world regression first");
            var secondObject = new GameObject("Local world regression second");
            var idle = ScriptableObject.CreateInstance<IdleState>();
            var pursue = ScriptableObject.CreateInstance<PursueTargetState>();
            var combat = ScriptableObject.CreateInstance<CombatStanceState>();
            var attack = ScriptableObject.CreateInstance<AttackState>();
            try
            {
                var first = firstObject.AddComponent<AICharacterManager>();
                var second = secondObject.AddComponent<AICharacterManager>();
                foreach (var actor in new[] { first, second })
                {
                    actor.idle = idle; actor.pursueTarget = pursue;
                    actor.combatStance = combat; actor.attack = attack;
                    typeof(AICharacterManager).GetMethod("AcquireRuntimeStates", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, null);
                }
                if (first.idle == idle || first.idle == second.idle || first.attack == second.attack || first.combatStance == second.combatStance)
                    throw new InvalidOperationException("Mutable FSM states are shared between actors.");
                var owned = first.idle;
                first.ShutdownLocal();
                if (owned != null || first.idle != idle || first.attack != attack)
                    throw new InvalidOperationException("Shutdown did not release clones and restore authored templates.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(idle); UnityEngine.Object.DestroyImmediate(pursue);
                UnityEngine.Object.DestroyImmediate(combat); UnityEngine.Object.DestroyImmediate(attack);
            }
        }

    }
}
#endif

