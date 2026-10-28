using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceAuthoredVisualValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate Authored Visual Children (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned PlayMode child callbacks required.");
            var cases = new List<object>();
            foreach (string change in new[] { "baseline", "hand-component", "hand-object", "hitbox-roundtrip", "hitbox-persistent", "hitbox-trigger", "reparent", "destroy-hand", "replace-collider", "child-reference", "lock-component", "unrelated-hitbox" })
            {
                try { WithVisual(change); cases.Add(new { name = change, passed = true, detail = "passed" }); }
                catch (Exception error) { cases.Add(new { name = change, passed = false, detail = error.ToString() }); }
            }
            foreach (string invalid in new[] { "missing", "foreign", "duplicate", "null-entry", "controller-subtree" })
            {
                try { RejectMalformed(invalid); cases.Add(new { name = "Reject " + invalid, passed = true, detail = "passed" }); }
                catch (Exception error) { cases.Add(new { name = "Reject " + invalid, passed = false, detail = error.ToString() }); }
            }
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                populationVerified = false, boundary = "Owned child/collider callbacks only; not creation closure, rendering state or timing acceptance" };
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-authored-visual-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented)); Debug.Log("Authored visual fixture: " + path);
        }

        static FieldInfo Children() => typeof(PerformanceAuthoredVisual).GetField("reviewedComponents") ?? throw new InvalidOperationException("Explicit reviewed child references are missing");
        static GameObject NewVisual(out PerformanceAuthoredVisual source, out UndeadHandDamageCollider hand, out LockOnTransform target)
        {
            var root = new GameObject("Owned visual children"); root.SetActive(false);
            source = root.AddComponent<PerformanceAuthoredVisual>(); source.visual = root.AddComponent<Animator>(); root.AddComponent<SkinnedMeshRenderer>();
            var child = new GameObject("Owned hand"); child.transform.SetParent(root.transform); child.AddComponent<BoxCollider>(); hand = child.AddComponent<UndeadHandDamageCollider>();
            var lockRoot = new GameObject("Owned lock transform"); lockRoot.transform.SetParent(root.transform); target = lockRoot.AddComponent<LockOnTransform>();
            return root;
        }
        static void WithVisual(string change)
        {
            var field = Children();
            var root = NewVisual(out var source, out var hand, out var target);
            PerformanceActorRosterContext context = null; PerformanceActorLifecycleScope scope = null;
            GameObject unrelated = null;
            try
            {
                field.SetValue(source, new MonoBehaviour[] { hand, target }); root.SetActive(true);
                var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                Require(census.report.actors.Any(a => a.objectId == root.GetInstanceID() && a.supportedBinding), "Reviewed visual was not bound");
                context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false);
                scope = PerformanceActorLifecycleScope.Begin(new[] { "-karawaraPerformanceRoster" }, context);
                Require(context.ReadLivingHostileCount() == 0, "Visual must not count as a hostile");
                if (change == "hand-component") { hand.enabled = false; hand.enabled = true; }
                if (change == "hand-object") { hand.gameObject.SetActive(false); hand.gameObject.SetActive(true); }
                if (change == "hitbox-roundtrip") { hand.EnableDamageCollider(); hand.DisableDamageCollider(); }
                if (change == "hitbox-persistent") hand.GetComponent<Collider>().enabled = true;
                if (change == "hitbox-trigger") hand.GetComponent<Collider>().isTrigger = !hand.GetComponent<Collider>().isTrigger;
                if (change == "reparent") hand.transform.SetParent(target.transform);
                if (change == "destroy-hand") Object.DestroyImmediate(hand);
                if (change == "replace-collider") { Object.DestroyImmediate(hand.GetComponent<Collider>()); hand.gameObject.AddComponent<BoxCollider>(); }
                if (change == "child-reference") field.SetValue(source, new MonoBehaviour[] { target });
                if (change == "lock-component") { target.enabled = false; target.enabled = true; }
                if (change == "unrelated-hitbox")
                {
                    unrelated = new GameObject("Unbound normal hitbox"); unrelated.SetActive(false); unrelated.AddComponent<BoxCollider>();
                    var normal = unrelated.AddComponent<UndeadHandDamageCollider>(); unrelated.SetActive(true); normal.EnableDamageCollider(); normal.DisableDamageCollider(); normal.enabled = false; normal.enabled = true;
                }
                var result = new CaptureResult(); context.Validate(result);
                bool changed = change != "baseline" && change != "unrelated-hitbox";
                Require(result.conditionsChanged == changed && !result.populationVerified, "Child change was lost, unrelated gameplay was invalidated or certification was granted");
                if (!changed)
                {
                    long notifications = scope.NotificationCount;
                    hand.DisableDamageCollider(); hand.DisableDamageCollider();
                    Require(scope.NotificationCount == notifications, "Unchanged disabled hitbox emitted a false source transition");
                    for (int i = 0; i < 64; i++) context.ReadLivingHostileCount();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount();
                    Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed child polling allocated managed bytes");
                }
            }
            finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); if (unrelated != null) Object.DestroyImmediate(unrelated); }
        }
        static void RejectMalformed(string invalid)
        {
            var field = Children();
            var root = NewVisual(out var source, out var hand, out var target);
            var foreign = new GameObject("Foreign reviewed child"); foreign.SetActive(false);
            try
            {
                // Establish the actual Awake-created collider reference before testing
                // each malformed declaration, so rejection cannot pass on an unrelated null.
                field.SetValue(source, new MonoBehaviour[] { hand, target }); root.SetActive(true); root.SetActive(false);
                MonoBehaviour[] declared = invalid == "missing" ? Array.Empty<MonoBehaviour>() : invalid == "foreign" ? new MonoBehaviour[] { hand, target, foreign.AddComponent<LockOnTransform>() } :
                    invalid == "duplicate" ? new MonoBehaviour[] { hand, target, target } : invalid == "null-entry" ? new MonoBehaviour[] { hand, target, null } : new MonoBehaviour[] { hand, target, root.AddComponent<NPCATIVAR>() };
                field.SetValue(source, declared);
                bool rejected = false;
                try { using (PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false)) { } }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Malformed child declaration was accepted");
                var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                Require(census.report.unclassifiedAnimators.Any(a => a.componentId == source.visual.GetInstanceID()), "Malformed declaration hid its Animator");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(foreign); }
        }
        static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
