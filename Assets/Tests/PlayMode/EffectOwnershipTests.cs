using System.Collections;
using System.Linq;
using Karawara.Core.Characters;
using NUnit.Framework;
using RPGKarawara;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.TestTools;
namespace Karawara.Tests.Gameplay
{
    public class EffectOwnershipTests : SessionFixture
    {
        [UnityTest] public IEnumerator RealSpiritPrefabBindsEachCasterAndReleasesOnlyItsOwnState()
        {
            var first = bootstrap.Session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
            var second = Object.Instantiate(bootstrap.PlayerPrefab, new Vector3(500, 0, 0), Quaternion.identity);
            second.InitializeLocal(second.State, bootstrap.Registry);
            var a = first.GetComponent<PlayerSkillManager>(); var b = second.GetComponent<PlayerSkillManager>();
            var skill = Resources.FindObjectsOfTypeAll<AttackSkill>().FirstOrDefault(s => s.prefab != null && s.prefab.GetComponent<PetAttackController>() != null);
#if UNITY_EDITOR
            if (skill == null) skill = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackSkill>("Assets/Scripts/SkillTree/Data/PetSpirit.asset");
#endif
            Assert.That(skill != null, Is.True, "Authored spirit skill must be present in the production UI");
            float extra = skill.prefab.GetComponent<PetAttackController>().damageExtra;
            float beforeA = a.damageSpirit, beforeB = b.damageSpirit;
            try
            {
                a.UnlockSkill(skill); b.UnlockSkill(skill);
                skill.Activate(first.gameObject);
                Assert.That(first.State.IsPowerPet, Is.True);
                Assert.That(second.State.IsPowerPet, Is.False);
                Assert.That(a.damageSpirit, Is.EqualTo(extra), "First caster received another manager's modifier");
                Assert.That(b.damageSpirit, Is.EqualTo(beforeB));
                skill.Activate(second.gameObject); yield return null;
                Assert.That(first.State.IsPowerPet && second.State.IsPowerPet, Is.True);
                Assert.That(a.damageSpirit, Is.EqualTo(extra)); Assert.That(b.damageSpirit, Is.EqualTo(extra));
                var ownerProperty = typeof(PetAttackController).GetProperty("Owner");
                Assert.That(ownerProperty, Is.Not.Null, "Effect must expose its explicit owner for ownership diagnostics");
                var own = Object.FindObjectsByType<PetAttackController>(FindObjectsSortMode.None).Single(c => ownerProperty.GetValue(c) == first);
                Object.Destroy(own.gameObject); yield return null;
                Assert.That(first.State.IsPowerPet, Is.False);
                Assert.That(a.damageSpirit, Is.EqualTo(beforeA));
                Assert.That(second.State.IsPowerPet, Is.True);
                Assert.That(b.damageSpirit, Is.EqualTo(extra));
            }
            finally
            {
                foreach (var controller in Object.FindObjectsByType<PetAttackController>(FindObjectsSortMode.None)) Object.Destroy(controller.gameObject);
                Object.Destroy(second.gameObject);
            }
            yield return null;
        }
    }
}
