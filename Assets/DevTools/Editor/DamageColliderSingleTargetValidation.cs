using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    // Isolates trigger dispatch from health, networking, effect assets and lifecycle observers.
    public sealed class DamageColliderValidationTarget : CharacterManager
    {
        protected override void Awake() { }
        protected override void OnEnable() { }
        protected override void OnDisable() { }
    }

    public sealed class DamageColliderValidationProbe : DamageCollider
    {
        public int blockChecks, damageCalls;
        public readonly List<CharacterManager> processed = new List<CharacterManager>();
        public void Configure(Collider collider) { damageCollider = collider; }
        public void Contact(Collider other) { base.OnTriggerEnter(other); }
        protected override void CheckForBlock(CharacterManager target) { blockChecks++; }
        protected override void DamageTarget(CharacterManager target)
        {
            damageCalls++;
            processed.Add(target);
            // The production implementation records the target before processing its effect.
            charactersDamaged.Add(target);
        }
    }

    public static class DamageColliderSingleTargetValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode trigger dispatch fixture required.");
            var cases = new List<object>();
            Action<string, Action<DamageColliderValidationProbe, CharacterManager, List<Collider>, Transform>> test = (name, body) =>
            {
                var root = new GameObject("Owned single-target damage fixture");
                try
                {
                    var source = new GameObject("Attack"); source.transform.SetParent(root.transform);
                    var collider = source.AddComponent<BoxCollider>(); collider.enabled = false;
                    var probe = source.AddComponent<DamageColliderValidationProbe>(); probe.Configure(collider); probe.EnableDamageCollider();
                    var target = NewTarget(root.transform);
                    var contacts = Enumerable.Range(0, 5).Select(i => NewContact(target, "Contact " + i)).ToList();
                    body(probe, target, contacts, root.transform);
                    cases.Add(new { name, passed = true, damageCalls = probe.damageCalls, blockChecks = probe.blockChecks });
                }
                catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); }
                finally { Object.DestroyImmediate(root); }
            };
            test("First contact processes its character", (p, t, c, r) => { p.Contact(c[0]); Require(p.damageCalls == 1 && p.blockChecks == 1 && p.processed[0] == t, "First trigger must process exactly once."); });
            test("Repeated entry of the same collider processes once", (p, t, c, r) => { p.Contact(c[0]); p.Contact(c[0]); Require(p.damageCalls == 1 && p.blockChecks == 1, "Repeated collider entry processed the same character again: damage=" + p.damageCalls + ", block=" + p.blockChecks); });
            test("Two child colliders of one character process once", (p, t, c, r) => { p.Contact(c[0]); p.Contact(c[1]); Require(p.damageCalls == 1 && p.blockChecks == 1, "Sibling colliders processed the same parent character again: damage=" + p.damageCalls + ", block=" + p.blockChecks); });
            test("Five child colliders do not multiply one attack", (p, t, c, r) => { foreach (var contact in c) p.Contact(contact); Require(p.damageCalls == 1 && p.blockChecks == 1, "One attack was multiplied by its target collider count: damage=" + p.damageCalls + ", block=" + p.blockChecks); });
            test("One attack may process two different characters", (p, t, c, r) => { var second = NewTarget(r); p.Contact(c[0]); p.Contact(NewContact(second, "Other target")); Require(p.damageCalls == 2 && p.blockChecks == 2 && p.processed[0] == t && p.processed[1] == second, "Distinct characters must each be processed."); });
            test("Disable and enable permit the next attack against the same character", (p, t, c, r) => { p.Contact(c[0]); p.DisableDamageCollider(); p.EnableDamageCollider(); p.Contact(c[1]); Require(p.damageCalls == 2 && p.blockChecks == 2, "Disabling must clear the previous attack's targets."); });
            test("Enabling an already enabled attack does not reset targets", (p, t, c, r) => { p.Contact(c[0]); p.EnableDamageCollider(); p.Contact(c[1]); Require(p.damageCalls == 1 && p.blockChecks == 1, "An idempotent enable allowed duplicate target processing."); });
            test("Non-character contact does not process damage", (p, t, c, r) => { var prop = new GameObject("Non-character"); prop.transform.SetParent(r); p.Contact(prop.AddComponent<BoxCollider>()); Require(p.damageCalls == 0 && p.blockChecks == 0, "A non-character was processed."); });
            test("Pet Player contact remains excluded", (p, t, c, r) => { p.isPet = true; c[0].gameObject.tag = "Player"; p.Contact(c[0]); Require(p.damageCalls == 0 && p.blockChecks == 0, "Existing pet Player exclusion changed."); p.isPet = false; p.Contact(c[0]); Require(p.damageCalls == 1 && p.blockChecks == 1, "Excluded contact must not consume the attack's target slot."); });

            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/damage-collider-single-target-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), passed = cases.Count == 9 && cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Actual base OnTriggerEnter dispatch on owned EditMode objects. Count-only virtual block/damage sinks record targets without processing damage, effects, health or networking. No physics timing, blocking double-path, natural Cuca combat, native retention or performance acceptance." }, Formatting.Indented));
            return path;
        }

        static CharacterManager NewTarget(Transform parent)
        {
            var target = new GameObject("Controlled CharacterManager target"); target.transform.SetParent(parent);
            return target.AddComponent<DamageColliderValidationTarget>();
        }
        static Collider NewContact(CharacterManager target, string name)
        {
            var contact = new GameObject(name); contact.transform.SetParent(target.transform);
            return contact.AddComponent<BoxCollider>();
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
