using System;
using System.Reflection;
using NUnit.Framework;
using RPGKarawara;
using RPGKarawara.SkillTree;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Karawara.Tests.Gameplay
{
    public class SharedEffectResourceTests
    {
        [Test] public void SummonsRequireExplicitCasterInsteadOfGlobalPlayer()
        {
            var owner = new GameObject("Caster"); owner.SetActive(false);
            var effect = new GameObject("Summon"); effect.SetActive(false);
            try
            {
                var player = owner.AddComponent<PlayerManager>();
                var orbit = new GameObject("Own orbit"); orbit.transform.SetParent(owner.transform); orbit.tag = "Orbit";
                var wolf = effect.AddComponent<WolfAI>(); var bird = effect.AddComponent<Orbita>();
                var wolfBind = typeof(WolfAI).GetMethod("BindOwner"); var birdBind = typeof(Orbita).GetMethod("BindOwner");
                Assert.That(wolfBind, Is.Not.Null, "Panther must receive the casting player");
                Assert.That(birdBind, Is.Not.Null, "Bird must receive the casting player");
                wolfBind.Invoke(wolf, new object[] { player }); birdBind.Invoke(bird, new object[] { player });
                Assert.That(wolf.player, Is.SameAs(owner.transform));
                Assert.That(bird.centerObject, Is.SameAs(orbit.transform));
            }
            finally { Object.DestroyImmediate(effect); Object.DestroyImmediate(owner); }
        }
        [Test] public void MeleeSpiritMultiplierComesFromItsSourceActor()
        {
            var source = new GameObject("Source owner"); source.SetActive(false);
            var other = new GameObject("Unrelated owner"); other.SetActive(false);
            var weapon = new GameObject("Owned weapon"); weapon.SetActive(false);
            var damage = ScriptableObject.CreateInstance<TakeDamageEffect>();
            var previousSingleton = PlayerSkillManager.instance;
            try
            {
                var actor = source.AddComponent<CharacterManager>(); actor.State.SetPowerPet(true);
                var skills = source.AddComponent<PlayerSkillManager>(); skills.damageSpirit = 3;
                var unrelated = other.AddComponent<PlayerSkillManager>(); unrelated.damageSpirit = 7; PlayerSkillManager.instance = unrelated;
                var collider = weapon.AddComponent<MeleeWeaponDamageCollider>(); collider.characterCausingDamage = actor;
                damage.physicalDamage = 10;
                typeof(MeleeWeaponDamageCollider).GetMethod("ApplyAttackDamageModifiers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collider, new object[] { 1f, damage, actor });
                Assert.That(damage.physicalDamage, Is.EqualTo(30), "A different actor's singleton changed this actor's damage");
            }
            finally { PlayerSkillManager.instance = previousSingleton; Object.DestroyImmediate(source); Object.DestroyImmediate(other); Object.DestroyImmediate(weapon); Object.DestroyImmediate(damage); }
        }
        [Test] public void MaterialLeaseOutOfOrderReleasePreservesOtherEffect()
        {
            var acquire = typeof(CustomRenderPassFeature).GetMethod("AcquireMaterial");
            Assert.That(acquire, Is.Not.Null, "Shared render feature needs independent effect ownership");
            var feature = ScriptableObject.CreateInstance<CustomRenderPassFeature>();
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var original = new Material(shader); var first = new Material(shader); var second = new Material(shader);
            try
            {
                feature.settings.material = original; feature.Create();
                var a = (IDisposable)acquire.Invoke(feature, new object[] { new object(), first });
                var b = (IDisposable)acquire.Invoke(feature, new object[] { new object(), second });
                var current = typeof(CustomRenderPassFeature).GetProperty("CurrentMaterial");
                Assert.That(current.GetValue(feature), Is.SameAs(second));
                a.Dispose(); Assert.That(current.GetValue(feature), Is.SameAs(second));
                b.Dispose(); Assert.That(current.GetValue(feature), Is.SameAs(original));
                a.Dispose(); Assert.That(current.GetValue(feature), Is.SameAs(original));
            }
            finally { Object.DestroyImmediate(feature); Object.DestroyImmediate(original); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
    }
}
