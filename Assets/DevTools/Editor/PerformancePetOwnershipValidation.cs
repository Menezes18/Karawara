using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara;
using RPGKarawara.SkillTree;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    // Actual production component callbacks in an otherwise empty Play scene.
    // Fixture-only durations/prefabs do not qualify authored standalone retention.
    public static class PerformancePetOwnershipValidation
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Names = { "Actual timer expiration releases pet and bonus", "Destroying controller releases orbit child and power", "Repeated Init does not duplicate pet", "Old controller expiry preserves replacement owner", "Ten actual forced lifecycle cycles retain no orbit children", "Actual authored PetSpirit survives its zero-default controller and expires at skill duration", "Replacement expiry restores original bonus", "External bonus changes survive cleanup" };
        static readonly List<object> cases = new List<object>();
        static string path;
        static int index, state, actionFrame, cycle;
        static float actionTime;
        static GameObject fixture, orbit, prefab;
        static PlayerSkillManager manager, priorManager;
        static PlayerNetworkManager network;
        static PetAttackController owner, replacement;
        static AttackSkill authoredSkill;
        static float authoredDuration, prefabDuration;
        static double deadline;

        public static string Run()
        {
            if (!Application.isPlaying || path != null || Object.FindObjectsByType<PlayerNetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0 || PlayerSkillManager.instance != null)
                throw new InvalidOperationException("Exclusive empty Play scene without production player/skill manager required.");
            path = Path.GetFullPath("Docs/Validation/TOBE/Execution/pet-ownership-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            cases.Clear(); index = state = cycle = 0;
            EditorApplication.update += Tick;
            return path;
        }
        static void Setup()
        {
            priorManager = PlayerSkillManager.instance;
            fixture = new GameObject("Owned pet lifetime fixture");
            var player = new GameObject("Owned unspawned network context"); player.transform.SetParent(fixture.transform);
            network = player.AddComponent<PlayerNetworkManager>(); network.enabled = false;
            manager = player.AddComponent<PlayerSkillManager>(); manager.enabled = false; manager.damageSpirit = 3;
            orbit = new GameObject("Owned Orbit"); orbit.tag = "Orbit"; orbit.transform.SetParent(fixture.transform);
            prefab = new GameObject("Owned inactive pet template"); prefab.SetActive(false); prefab.transform.SetParent(fixture.transform);
            if (index == 5)
            {
                authoredSkill = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AttackSkill>("Assets/Scripts/SkillTree/Data/PetSpirit.asset"));
                authoredDuration = authoredSkill.cooldownDuration; prefabDuration = authoredSkill.prefab.GetComponent<PetAttackController>().duration;
                Require(authoredSkill.isUnlocked && authoredDuration == 10 && prefabDuration == 0, "Actual authored zero-default/10-second boundary changed; inspect it.");
                authoredSkill.Activate(player);
                owner = Object.FindFirstObjectByType<PetAttackController>(); Require(owner != null, "Authored production activation created no controller.");
                owner.transform.SetParent(fixture.transform);
            }
            else owner = NewOwner(10, index == 0 ? .05f : 10);
            Require(orbit.transform.childCount == 1 && network.isPowerPet.Value && manager.damageSpirit == 10, "Actual production Init did not instantiate/bind the fixture pet.");
            if (index == 1 || index == 4) Object.Destroy(owner.gameObject);
            if (index == 2) owner.Init();
            if (index == 3)
            {
                replacement = NewOwner(20, 10);
                typeof(PetAttackController).GetMethod("DesativarPower", Private).Invoke(owner, null);
            }
            if (index == 6) replacement = NewOwner(20, .05f);
            if (index == 7) { manager.damageSpirit = 77; Object.Destroy(owner.gameObject); }
            actionFrame = Time.frameCount; actionTime = Time.time;
        }
        static PetAttackController NewOwner(float damage, float duration)
        {
            var root = new GameObject("Owned pet controller"); root.SetActive(false); root.transform.SetParent(fixture.transform);
            var controller = root.AddComponent<PetAttackController>(); controller.petPrefab = prefab; controller.damageExtra = damage; controller.duration = duration;
            root.SetActive(true); return controller;
        }
        static void Tick()
        {
            try
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Play fixture interrupted.");
                if (state == 0) { deadline = EditorApplication.timeSinceStartup + 15; Setup(); state = 1; return; }
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Pet lifetime fixture timed out.");
                if (Time.frameCount < actionFrame + 2 || ((index == 0 || index == 6) && Time.time < actionTime + .15f)) return;
                if (index == 5 && state == 1)
                {
                    if (Time.time < actionTime + .2f) return;
                    Require(owner != null && orbit.transform.childCount == 1 && network.isPowerPet.Value && owner.duration == authoredDuration, "Authored skill lost its pet/power before the declared duration.");
                    state = 2; return;
                }
                if (index == 5 && Time.time < actionTime + authoredDuration + .3f) return;
                if (index == 2)
                    Require(orbit.transform.childCount == 1 && network.isPowerPet.Value, "Repeated Init left duplicate pets or cleared power.");
                else if (index == 3)
                    Require(replacement != null && orbit.transform.childCount == 1 && network.isPowerPet.Value && manager.damageSpirit == 20, "Old controller cleared replacement pet/power/bonus.");
                else if (index == 7)
                    Require(orbit.transform.childCount == 0 && !network.isPowerPet.Value && manager.damageSpirit == 77, "Cleanup overwrote an external damage bonus.");
                else
                    Require(orbit.transform.childCount == 0 && !network.isPowerPet.Value && manager.damageSpirit == 3, "Controller cleanup left pet/power/bonus behind: children=" + orbit.transform.childCount + ", power=" + network.isPowerPet.Value + ", bonus=" + manager.damageSpirit);
                if (index == 4 && ++cycle < 10)
                {
                    owner = NewOwner(10, 10); Object.Destroy(owner.gameObject); actionFrame = Time.frameCount; return;
                }
                CompleteCase(true, "passed");
            }
            catch (Exception error) { CompleteCase(false, error.ToString()); }
        }
        static void CompleteCase(bool passed, string detail)
        {
            cases.Add(new { name = Names[index], passed, detail, completedForcedCycles = index == 4 ? cycle : 0 });
            if (fixture != null) Object.DestroyImmediate(fixture);
            if (authoredSkill != null) Object.DestroyImmediate(authoredSkill);
            authoredSkill = null;
            PlayerSkillManager.instance = priorManager;
            fixture = null; owner = replacement = null; manager = null; network = null; orbit = prefab = null;
            if (++index < Names.Length && Application.isPlaying) { state = 0; return; }
            EditorApplication.update -= Tick;
            string output = path; path = null;
            bool all = true; foreach (var row in cases) all &= (bool)row.GetType().GetProperty("passed").GetValue(row);
            File.WriteAllText(output, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = all, cases, authoredDuration, prefabDuration,
                boundary = "Actual production lifecycle in an unspawned fixture context; one case uses the cloned authored PetSpirit asset/prefab at its full10-second duration. Other fixture cases shorten expiry. No standalone/native memory/full skill retention/G1B acceptance." }, Formatting.Indented));
            Debug.Log("Pet lifetime fixture " + all + ": " + output);
        }
        static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    }
}
