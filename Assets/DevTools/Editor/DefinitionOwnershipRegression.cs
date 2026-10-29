#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using RPGKarawara.SkillTree;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    public static class DefinitionOwnershipRegression
    {
        [Serializable] private class Report { public bool passed; public string error; public int assetCount; public string boundary = "Editor production skill ownership regression; no gameplay or standalone acceptance."; }
        [MenuItem("Karawara/Validation/TOBE/Run Definition Ownership Regression")]
        public static void Run()
        {
            var report = new Report(); var users = new List<GameObject>(); Skill definition = null;
            var assets = AssetDatabase.FindAssets("t:Skill").Select(AssetDatabase.GUIDToAssetPath).ToDictionary(p => p, Hash);
            var serializedAssets = assets.Keys.ToDictionary(p => p, p => EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<Skill>(p)));
            report.assetCount = assets.Count;
            try
            {
                definition = ScriptableObject.CreateInstance<AttackSkill>(); definition.cooldownDuration = 10;
                definition.isUnlocked = false; definition.active = false;
                var serialized = EditorJsonUtility.ToJson(definition);
                PlayerSkillManager CreateUser()
                {
                    var go = new GameObject("DefinitionOwnershipQA"); go.SetActive(false); users.Add(go);
                    var manager = go.AddComponent<PlayerSkillManager>(); manager.slot = new[] { new SkillSlot() }; return manager;
                }
                var a = CreateUser(); var b = CreateUser();
                object Call(PlayerSkillManager manager, string name, params object[] args)
                {
                    var method = manager.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
                    Require(method != null, "Missing owner adapter: " + name); return method.Invoke(manager, args);
                }
                Call(a, "UnlockSkill", definition);
                Require(!(bool)Call(b, "IsSkillUnlocked", definition), "Unlock leaked to second user");
                Call(b, "UnlockSkill", definition);
                Call(a, "EquipSkill", definition, 0);
                Require((bool)Call(a, "IsSkillEquipped", definition), "First user's equip was lost");
                Require(!(bool)Call(b, "IsSkillEquipped", definition), "Equip leaked to second user");
                // Execute the real Activate adapter; a null AttackSkill prefab preserves a side-effect-free cast.
                definition.Activate(a.gameObject); definition.Activate(b.gameObject);
                Require((float)Call(a, "GetCooldownProgress", definition) > 0, "First user's cast did not start cooldown");
                Require((float)Call(b, "GetCooldownProgress", definition) > 0, "Second user could not cast shared definition");
                Require(EditorJsonUtility.ToJson(definition) == serialized, "Runtime use mutated the definition");
                var treeObject = new GameObject("DefinitionOwnershipSkillUIQA"); treeObject.SetActive(false); users.Add(treeObject);
                var tree = treeObject.AddComponent<SkillTreeUiManager>();
                tree.Bind(a); tree.attackSkills = assets.Keys.Select(AssetDatabase.LoadAssetAtPath<Skill>).ToArray();
                foreach (var authored in tree.attackSkills) tree.UnlockSkill(authored);
                tree.DeactivateAllSkillsAndResetCooldown();
                Require(serializedAssets.All(pair => EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<Skill>(pair.Key)) == pair.Value), "Runtime UI changed authored definition data in memory");
                tree.attackSkills = new[] { definition };
                var data = tree.SaveSkillsToData(new CharacterSaveData());
                tree.Bind(b); tree.LoadSkillsFromData(data); b.LoadSkillSlot(data);
                Require(b.IsSkillUnlocked(definition), "New save lost per-user unlock");
                tree.LoadSkillsFromData(new CharacterSaveData { SkillAttack = new[] { definition } });
                Require(!b.IsSkillUnlocked(definition), "Old save did not preserve legacy locked default");
                Require(assets.All(pair => Hash(pair.Key) == pair.Value), "Authored skill asset hash changed");
                report.passed = true;
            }
            catch (Exception e) { report.error = e.GetBaseException().Message; }
            finally
            {
                foreach (var go in users) UnityEngine.Object.DestroyImmediate(go);
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                Directory.CreateDirectory("Docs/Validation/TOBE/Execution");
                File.WriteAllText("Docs/Validation/TOBE/Execution/g3a-definition-ownership-latest.json", JsonUtility.ToJson(report, true));
            }
            if (!report.passed) throw new InvalidOperationException(report.error);
            Debug.Log("Definition ownership regression passed; " + report.assetCount + " skill asset hashes unchanged.");
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
