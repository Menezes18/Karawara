using NUnit.Framework;
using RPGKarawara;
using RPGKarawara.SkillTree;
using UnityEngine;
namespace Karawara.Tests.Gameplay
{
    public class GameplayDefinitionTests
    {
        [Test] public void CatalogInitializationDoesNotRewriteAuthoredActionId()
        {
            var gameObject = new GameObject("G3A action definitions"); gameObject.SetActive(false);
            var action = ScriptableObject.CreateInstance<WeaponItemAction>(); action.actionID = 123;
            try
            {
                var catalog = gameObject.AddComponent<WorldActionManager>(); catalog.weaponItemActions = new[] { action };
                typeof(WorldActionManager).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(catalog, null);
                Assert.That(action.actionID, Is.EqualTo(123), "Catalog startup mutated shared action definition");
            }
            finally { Object.DestroyImmediate(gameObject); Object.DestroyImmediate(action); }
        }
        [Test] public void DefinitionUseDoesNotChangeSerializedContent()
        {
            var user = new GameObject("G3A edit owner"); user.SetActive(false);
            var definition = ScriptableObject.CreateInstance<AttackSkill>();
            var before = JsonUtility.ToJson(definition);
            try
            {
                var owner = user.AddComponent<PlayerSkillManager>();
                owner.UnlockSkill(definition); definition.Activate(user);
                Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
                Assert.That(typeof(PlayerSkillManager).Assembly.GetName().Name, Is.EqualTo("Karawara.Gameplay"));
            }
            finally { Object.DestroyImmediate(user); Object.DestroyImmediate(definition); }
        }
    }
}
