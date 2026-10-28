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
    // Reflection keeps the fixture compilable before the proposed runtime contract exists.
    public static class PerformanceActorRosterValidation
    {
        const string ContextName = "RPGKarawara.DevTools.Performance.PerformanceActorRosterContext";
        const string LifecycleName = "RPGKarawara.DevTools.Performance.PerformanceActorLifecycle";
        static readonly string[] Activation = { "-karawaraPerformanceRoster" };
        [Serializable] sealed class Case { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public string utc;
            public bool completed, passed, cleanedUp;
            public List<Case> cases = new List<Case>();
            public List<string> limits = new List<string>();
        }
        [MenuItem("Karawara/Validation/TOBE/Validate Performance Actor Roster")]
        public static void Run()
        {
            if (Application.isPlaying) { Debug.LogError("Actor roster fixture requires EditMode; it never drives production actors."); return; }
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            var baseline = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Test(report, "No explicit flag is a no-op before malformed binding inspection", () =>
            {
                var value = Create(Array.Empty<string>(), null, null, null, false);
                Require(value == null, "Absent activation must create no session.");
            });
            Test(report, "Duplicate explicit activation is rejected", () =>
                Rejected(() => Create(new[] { Activation[0], Activation[0] }, Array.Empty<Component>(), Array.Empty<string>(), Array.Empty<GameObject>(), false)));
            Test(report, "Null actor or role arrays with activation are rejected", () =>
            {
                Rejected(() => Create(Activation, null, Array.Empty<string>(), Array.Empty<GameObject>(), false));
                Rejected(() => Create(Activation, Array.Empty<Component>(), null, Array.Empty<GameObject>(), false));
            });
            Test(report, "Mismatched roles and null actor entries are rejected", () =>
            {
                Rejected(() => Create(Activation, Array.Empty<Component>(), new[] { "hostile" }, Array.Empty<GameObject>(), false));
                Rejected(() => Create(Activation, new Component[] { null }, new[] { "hostile" }, Array.Empty<GameObject>(), false));
            });
            Test(report, "Unknown roles and hostile NPC role misuse are rejected", () =>
            {
                using (var f = new Fixture())
                {
                    var npc = f.Npc();
                    Rejected(() => Create(Activation, new Component[] { npc }, new[] { "invented" }, Array.Empty<GameObject>(), false));
                    Rejected(() => Create(Activation, new Component[] { npc }, new[] { "hostile" }, Array.Empty<GameObject>(), false));
                }
            });
            Test(report, "Duplicate actor roots are rejected rather than counted twice", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca();
                    Rejected(() => Create(Activation, new Component[] { cuca, cuca.GetComponent<HealthManager>() }, new[] { "hostile", "hostile" }, Array.Empty<GameObject>(), false));
                }
            });
            Test(report, "Separate hostile NPC and animal roles with copied binding arrays", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); var npc = f.Npc(); var animal = f.Root("animal").AddComponent<Animator>();
                    var actors = new Component[] { cuca, npc, animal };
                    var roles = new[] { "hostile", "ambient-npc", "ambient-animal" };
                    f.Bind(actors, roles);
                    actors[0] = npc; roles[0] = "ambient-npc";
                    Require(Read(f.session) == 1, "Caller array mutation must not alter frozen typed hostile binding.");
                    var result = Result(); Validate(f.session, result);
                    Require(!result.populationVerified && !result.comparisonValid, "Animal creation-path coverage remains unavailable in this fixture.");
                }
            });
            Test(report, "Empty roster and asserted creation closure do not certify a population", () =>
            {
                using (var f = new Fixture())
                {
                    f.Bind(Array.Empty<Component>(), Array.Empty<string>(), covered: true);
                    Require(Read(f.session) == 0, "Empty typed roster has zero observed hostile members.");
                    var result = Result(); Validate(f.session, result);
                    Require(!result.populationVerified && !result.comparisonValid && result.validationUnavailableReasons.Count > 0,
                        "A caller boolean without actual owner/session/provenance must leave verification unavailable.");
                }
            });
            Test(report, "Actual unspawned NGO AI is not a fabricated living network actor", () =>
            {
                using (var f = new Fixture())
                {
                    var root = f.Root("unspawned AI");
                    var network = root.AddComponent<AICharacterNetworkManager>();
                    var ai = root.AddComponent<AICharacterManager>();
                    ai.characterNetworkManager = network; ai.aiCharacterNetworkManager = network;
                    f.Bind(new Component[] { ai }, new[] { "hostile" });
                    Require(!ai.IsSpawned && Read(f.session) == 0, "EditMode AI must not be counted as an NGO-spawned living hostile.");
                    var result = Result(); Validate(f.session, result);
                    Require(!result.populationVerified, "No NGO population proof is produced by this EditMode fixture.");
                }
            });
            Test(report, "Transient disable and enable notifications remain latched", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    Observe(f.session, cuca, "Disabled"); Observe(f.session, cuca, "Enabled");
                    Invalid(f.session, "Matching endpoint must not clear an observed lifecycle violation.");
                    var diagnostic = Result(); Validate(f.session, diagnostic);
                    Require(diagnostic.errors.Exists(e => e.Contains("[Lifetime]")), "Export the actual latched violation category for failed standalone discovery.");
                }
            });
            Test(report, "Same-count replacement and unregistered transient actor remain latched", () =>
            {
                using (var f = new Fixture())
                {
                    var original = f.Cuca(); f.Bind(new Component[] { original }, new[] { "hostile" });
                    var replacement = f.Cuca();
                    Observe(f.session, original, "Disabled"); Observe(f.session, replacement, "Enabled");
                    Observe(f.session, replacement, "Destroyed"); Observe(f.session, original, "Enabled");
                    Invalid(f.session, "A new typed actor bypassing WorldAI registration must invalidate even if it disappears before sampling.");
                }
            });
            Test(report, "Network despawn and respawn notification cannot restore validity", () =>
            {
                using (var f = new Fixture())
                {
                    var root = f.Root("unspawned AI notifications");
                    var network = root.AddComponent<AICharacterNetworkManager>(); var ai = root.AddComponent<AICharacterManager>();
                    ai.characterNetworkManager = network; ai.aiCharacterNetworkManager = network;
                    f.Bind(new Component[] { ai }, new[] { "hostile" });
                    Observe(f.session, ai, "NetworkDespawned"); Observe(f.session, ai, "NetworkSpawned");
                    Invalid(f.session, "These notifications test only the latch; no NGO ownership/spawn is faked.");
                }
            });
            Test(report, "Destroyed frozen reference invalidates without a count replacement", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    Object.DestroyImmediate(cuca.gameObject);
                    Invalid(f.session, "Cached destroyed actor must be detected without a discovery scan.");
                }
            });
            Test(report, "Cuca damage above death boundary is permitted; <=30 is latched", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); var health = cuca.GetComponent<HealthManager>();
                    f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    health.currentHealth = 31; Observe(f.session, health, "HealthChanged");
                    Require(Read(f.session) == 1, "Ordinary combat damage above the actual death boundary is not roster churn.");
                    health.currentHealth = 30; Observe(f.session, health, "HealthChanged");
                    health.currentHealth = 3000; Observe(f.session, health, "HealthChanged");
                    Invalid(f.session, "Transient crossing of Cuca's <=30 damage/death boundary cannot be hidden by healing.");
                }
            });
            Test(report, "Transient legacy maximum health changes remain latched after restoration", () =>
            {
                foreach (float changed in new[] { 3100f, float.NaN, float.PositiveInfinity })
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); var health = cuca.GetComponent<HealthManager>();
                    f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    health.maxHealth = changed; Observe(f.session, health, "HealthChanged");
                    health.maxHealth = 3000; Observe(f.session, health, "HealthChanged");
                    Invalid(f.session, "Transient maxHealth mutation must remain latched after endpoint restoration.");
                }
            });
            Test(report, "Transient network maximum health change remains latched after restoration", () =>
            {
                using (var f = new Fixture())
                {
                    var root = f.Root("unspawned AI health notification");
                    var network = root.AddComponent<AICharacterNetworkManager>(); var ai = root.AddComponent<AICharacterManager>();
                    ai.characterNetworkManager = network; ai.aiCharacterNetworkManager = network;
                    int original = network.maxHealth.Value;
                    f.Bind(new Component[] { ai }, new[] { "hostile" });
                    network.maxHealth.Value = original + 1; Observe(f.session, network, "HealthChanged");
                    network.maxHealth.Value = original; Observe(f.session, network, "HealthChanged");
                    Invalid(f.session, "Transient maxHealth notification is invalid even for an unspawned actor.");
                }
            });
            Test(report, "Unexpected boss phase notification invalidates fixed-phase route", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    Observe(f.session, cuca, "PhaseChanged");
                    Invalid(f.session, "Phase notification is separate from permitted positive-HP damage.");
                }
            });
            Test(report, "Known effect root is excluded; actor hidden in effect root is rejected", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); var effect = f.Root("projectile").AddComponent<ProjectileMovement>();
                    f.Bind(new Component[] { cuca }, new[] { "hostile" }, new[] { effect.gameObject });
                    Observe(f.session, effect, "Enabled"); Observe(f.session, effect, "Destroyed");
                    Require(Read(f.session) == 1, "Known projectile lifecycle does not change hostile count.");
                    ((IDisposable)f.session).Dispose(); f.session = null;
                    effect.gameObject.AddComponent<HealthManager>();
                    Rejected(() => Create(Activation, new Component[] { cuca }, new[] { "hostile" }, new[] { effect.gameObject }, false));
                }
            });
            Test(report, "Unknown actor type and null or duplicate effect bindings are rejected", () =>
            {
                using (var f = new Fixture())
                {
                    var unknown = f.Root("unknown collider").AddComponent<BoxCollider>();
                    Rejected(() => Create(Activation, new Component[] { unknown }, new[] { "hostile" }, Array.Empty<GameObject>(), false));
                    Rejected(() => Create(Activation, Array.Empty<Component>(), Array.Empty<string>(), null, false));
                    var effect = f.Root("effect").AddComponent<ProjectileMovement>();
                    Rejected(() => Create(Activation, Array.Empty<Component>(), Array.Empty<string>(), new[] { effect.gameObject, effect.gameObject }, false));
                }
            });
            Test(report, "Companion coverage is explicitly unavailable even with closure boolean", () =>
            {
                using (var f = new Fixture())
                {
                    var companion = f.Root("fixture companion").AddComponent<WolfAI>();
                    f.Bind(new Component[] { companion }, new[] { "companion" }, covered: true);
                    Require(Read(f.session) == 0, "Companion is not a hostile enemy.");
                    var result = Result(); Validate(f.session, result);
                    Require(!result.populationVerified && !result.comparisonValid && result.validationUnavailableReasons.Count > 0,
                        "Unsupported companion lifetime/creation adapter must remain explicit.");
                }
            });
            Test(report, "Malformed lifecycle enum rejected; final validation preserves camera evidence", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    var lifecycle = FindType(LifecycleName);
                    Rejected(() => Context().GetMethod("ObserveLifecycle").Invoke(f.session, new[] { (object)cuca, Enum.ToObject(lifecycle, int.MaxValue) }));
                    Observe(f.session, cuca, "Disabled");
                    var result = Result(); result.errors.Add("existing camera evidence"); Validate(f.session, result);
                    Require(result.cameraVerified && result.errors.Contains("existing camera evidence") && result.conditionsChanged && !result.populationVerified,
                        "Actor finalizer must preserve existing camera result and append its own invalidation.");
                }
            });
            Test(report, "Transient maximum-health change remains latched after restoration", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca(); var health = cuca.GetComponent<HealthManager>();
                    f.Bind(new Component[] { cuca }, new[] { "hostile" });
                    health.maxHealth = 3100; Observe(f.session, health, "HealthChanged");
                    health.maxHealth = 3000; Observe(f.session, health, "HealthChanged");
                    Invalid(f.session, "A maximum-health configuration change cannot disappear between sampled endpoints.");
                }
            });
            Test(report, "Idempotent cleanup and sequential owner reuse before Begin", () =>
            {
                using (var f = new Fixture())
                {
                    var cuca = f.Cuca();
                    for (int i = 0; i < 3; i++)
                    {
                        f.Bind(new Component[] { cuca }, new[] { "hostile" });
                        var session = f.session; ((IDisposable)session).Dispose(); ((IDisposable)session).Dispose();
                        Require((bool)Context().GetProperty("IsDisposed").GetValue(session), "Disposed status must be observable.");
                        bool stopped = false; try { Read(session); } catch (TargetInvocationException e) { stopped = e.InnerException is ObjectDisposedException; }
                        Require(stopped, "A disposed provider cannot continue validating actors.");
                        f.session = null;
                    }
                }
            });
            report.cleanedUp = baseline.All(x => x != null) && Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(x => !x.name.StartsWith("Karawara roster fixture ", StringComparison.Ordinal));
            report.completed = true; report.passed = report.cleanedUp && report.cases.All(x => x.passed);
            report.limits.Add("EditMode typed count/explicit-notification fixtures only. No NGO ownership, network spawn, actual source callback coverage, runtime actor manifest or performance baseline is certified.");
            report.limits.Add("Fixture-owned Cuca health fields are assigned directly; TakeDamage/Die, scene loading, host creation and production actor mutation are never invoked.");
            report.limits.Add("Creation closure boolean alone cannot certify population. Unsupported companion/animal creation paths and missing actual runtime owner/provenance remain unavailable.");
            report.limits.Add("Allocations and production lifecycle/source hooks require separate source review and actual Unity Play/standalone proof; this reflective fixture allocates outside timing.");
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-actor-roster-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Performance actor roster fixture result: " + path);
            if (!report.passed) Debug.LogError("Performance actor roster fixture FAILED; " + path);
        }
        sealed class Fixture : IDisposable
        {
            readonly List<GameObject> roots = new List<GameObject>();
            public object session;
            public GameObject Root(string role)
            {
                var root = new GameObject("Karawara roster fixture " + role) { hideFlags = HideFlags.HideAndDontSave };
                roots.Add(root); return root;
            }
            public CucaBoss Cuca()
            {
                var root = Root("Cuca"); var health = root.AddComponent<HealthManager>();
                health.maxHealth = 3000; health.currentHealth = 3000;
                return root.AddComponent<CucaBoss>();
            }
            public NPCDialogue Npc() => Root("NPC").AddComponent<NPCDialogue>();
            public void Bind(Component[] actors, string[] roles, GameObject[] effects = null, bool covered = false)
            {
                session = Create(Activation, actors, roles, effects ?? Array.Empty<GameObject>(), covered);
                Require(session is IDisposable, "Explicit binding must return an owned IDisposable context.");
            }
            public void Dispose()
            {
                try { (session as IDisposable)?.Dispose(); }
                finally { for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]); }
            }
        }
        static Type FindType(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(x => x != null);
            Require(type != null, name + " implementation missing."); return type;
        }
        static Type Context() => FindType(ContextName);
        static object Create(string[] args, Component[] actors, string[] roles, GameObject[] effects, bool covered)
            => Context().GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string[]), typeof(Component[]), typeof(string[]), typeof(GameObject[]), typeof(bool) }, null)
                .Invoke(null, new object[] { args, actors, roles, effects, covered });
        static int Read(object session) => (int)Context().GetMethod("ReadLivingHostileCount").Invoke(session, null);
        static void Observe(object session, Component actor, string kind)
            => Context().GetMethod("ObserveLifecycle").Invoke(session, new[] { (object)actor, Enum.Parse(FindType(LifecycleName), kind) });
        static CaptureResult Result() => new CaptureResult { conditions = new CaptureConfig(), cameraVerified = true, populationVerified = true, comparisonValid = true };
        static void Validate(object session, CaptureResult result) => Context().GetMethod("Validate").Invoke(session, new object[] { result });
        static void Invalid(object session, string detail)
        {
            var result = Result(); Validate(session, result);
            Require(result.conditionsChanged && !result.populationVerified && !result.comparisonValid && result.errors.Count > 0, detail);
        }
        static void Rejected(Func<object> action)
        {
            Context(); object unexpected = null; bool rejected = false;
            try { unexpected = action(); }
            catch (TargetInvocationException e) { rejected = e.InnerException is ArgumentException; }
            finally { (unexpected as IDisposable)?.Dispose(); }
            Require(rejected, "Malformed binding must throw ArgumentException before subscribing or creating a session.");
        }
        static void Rejected(Action action) => Rejected(() => { action(); return null; });
        static void Test(Report report, string name, Action action)
        {
            var value = new Case { name = name };
            try { action(); value.passed = true; value.detail = "passed"; }
            catch (Exception e) { value.detail = e.ToString(); }
            report.cases.Add(value);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
