using System.Collections;
using NUnit.Framework;
using RPGKarawara;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.TestTools;
namespace Karawara.Tests.Gameplay
{
    public class DefinitionOwnershipTests
    {
        [UnityTest] public IEnumerator SharedDefinitionKeepsUnlockEquipAndCooldownPerUser()
        {
            var a = new GameObject("G3A owner A"); a.SetActive(false);
            var b = new GameObject("G3A owner B"); b.SetActive(false);
            var definition = ScriptableObject.CreateInstance<AttackSkill>(); definition.cooldownDuration = 10;
            var before = JsonUtility.ToJson(definition);
            try
            {
                var first = a.AddComponent<PlayerSkillManager>(); var second = b.AddComponent<PlayerSkillManager>();
                first.slot = new[] { new SkillSlot() }; second.slot = new[] { new SkillSlot() };
                first.UnlockSkill(definition); first.EquipSkill(definition, 0);
                Assert.That(second.IsSkillUnlocked(definition), Is.False);
                Assert.That(second.IsSkillEquipped(definition), Is.False);
                second.UnlockSkill(definition);
                Assert.That(first.TryActivateDefinition(definition), Is.True);
                Assert.That(second.TryActivateDefinition(definition), Is.True);
                Assert.That(first.TryActivateDefinition(definition), Is.False);
                Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
            }
            finally { Object.Destroy(a); Object.Destroy(b); Object.Destroy(definition); }
            yield return null;
        }
    }
}
