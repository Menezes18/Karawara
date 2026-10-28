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
    public static class PerformanceActorSourceValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate Actor Source Lifecycle (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Actual source callbacks require PlayMode.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            test("Unflagged source scope does not bind malformed context", () => Require(Bind(null, Array.Empty<string>()) == null, "Absent opt-in must be a no-op"));
            test("Cuca actual root disable/re-enable is latched between polls", () => WithCuca((root, cuca, context, scope) => { root.SetActive(false); root.SetActive(true); Invalid(context); }));
            test("Cuca component toggle is latched while root stays active", () => WithCuca((root, cuca, context, scope) => { cuca.enabled = false; cuca.enabled = true; Invalid(context); }));
            test("Unknown Cuca created and removed entirely between polls invalidates", () => WithCuca((root, cuca, context, scope) =>
            {
                var unknown = NewCuca(); try { unknown.SetActive(true); unknown.SetActive(false); } finally { Object.DestroyImmediate(unknown); }
                Invalid(context);
            }));
            test("Actual damage/heal notifications permit living combat damage", () => WithCuca((root, cuca, context, scope) =>
            {
                var health = root.GetComponent<HealthManager>();
                health.TakeDamage(10); health.Heal(10);
                Require(context.ReadLivingHostileCount() == 1, "Positive living combat health is permitted");
                Require((long)scope.GetType().GetProperty("NotificationCount").GetValue(scope) >= 2, "Actual health methods must notify");
            }));
            test("Destroying a never-active frozen root invalidates its retained reference", () =>
            {
                var root = NewCuca(); PerformanceActorRosterContext context = null; IDisposable scope = null;
                try
                {
                    var cuca = root.GetComponent<CucaBoss>(); var health = root.GetComponent<HealthManager>(); health.currentHealth = health.maxHealth;
                    context = Context(cuca); scope = Bind(context); Object.DestroyImmediate(root);
                    // Unity does not send OnDestroy to a behaviour that was never active.
                    // The frozen reference remains destroyed and cannot be replaced by a same-count actor.
                    Invalid(context);
                }
                finally { scope?.Dispose(); context?.Dispose(); if (root != null) Object.DestroyImmediate(root); }
            });
            test("Destroying an active frozen root invokes its owned witness", () => WithCuca((root, cuca, context, scope) =>
            {
                root.SetActive(false);
                long before = (long)scope.GetType().GetProperty("NotificationCount").GetValue(scope);
                Object.DestroyImmediate(root);
                Require((long)scope.GetType().GetProperty("NotificationCount").GetValue(scope) > before, "Destruction must notify beyond the already completed source OnDisable callbacks");
                Invalid(context);
            }));
            test("NPC component toggle preserves cleanup while latching lifecycle", () =>
            {
                var root = new GameObject("Owned NPC source fixture"); var npc = root.AddComponent<NPCDialogue>();
                var context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { npc }, new[] { "ambient-npc" }, Array.Empty<GameObject>(), false);
                IDisposable scope = null;
                try { scope = Bind(context); npc.enabled = false; npc.enabled = true; Invalid(context); }
                finally { scope?.Dispose(); context.Dispose(); Object.DestroyImmediate(root); }
            });
            test("Cached health notification path allocates no owned managed bytes after warm-up", () => WithCuca((root, cuca, context, scope) =>
            {
                var method = Scope().GetMethod("Notify");
                var notification = (Action<Component, PerformanceActorLifecycle>)Delegate.CreateDelegate(typeof(Action<Component, PerformanceActorLifecycle>), method);
                var health = root.GetComponent<HealthManager>();
                for (int i = 0; i < 200; i++) notification(health, PerformanceActorLifecycle.HealthChanged);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) notification(health, PerformanceActorLifecycle.HealthChanged);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(allocated == 0, "Source callback path allocated " + allocated + " bytes");
            }));
            foreach (string mutation in new[] { "root", "maximum", "dead" })
                test("Actual unspawned NGO actor source transition: " + mutation, () =>
                {
                    var root = new GameObject("Owned NGO source fixture"); root.SetActive(false);
                    root.AddComponent<Animator>(); root.AddComponent<CharacterUIManager>().hasFloatingHPBar = false;
                    var network = root.AddComponent<AICharacterNetworkManager>(); var ai = root.AddComponent<AICharacterManager>();
                    PerformanceActorRosterContext context = null; IDisposable scope = null;
                    try
                    {
                        root.SetActive(true); context = Context(ai); scope = Bind(context);
                        Require(!ai.IsInitialized, "Fixture must not fabricate network spawn/ownership");
                        if (mutation == "root") { root.SetActive(false); root.SetActive(true); }
                        else if (mutation == "maximum") { int max = network.maxHealth.Value; network.maxHealth.Value = max + 1; network.maxHealth.Value = max; }
                        else { ai.isDead.Value = true; ai.isDead.Value = false; }
                        Invalid(context);
                    }
                    finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
                });
            test("Context disposal releases source owner and repeated old disposal preserves replacement", () =>
            {
                var root = NewCuca(); root.SetActive(true);
                var first = Context(root.GetComponent<CucaBoss>()); var second = Context(root.GetComponent<CucaBoss>());
                IDisposable old = null, replacement = null;
                try
                {
                    old = Bind(first); first.Dispose(); replacement = Bind(second); old.Dispose();
                    root.GetComponent<CucaBoss>().enabled = false; root.GetComponent<CucaBoss>().enabled = true;
                    Invalid(second);
                }
                finally { replacement?.Dispose(); old?.Dispose(); first.Dispose(); second.Dispose(); Object.DestroyImmediate(root); }
            });
            test("Second concurrent receiver is rejected without losing original", () => WithCuca((root, cuca, context, scope) =>
            {
                using (var other = Context(cuca))
                {
                    bool rejected = false;
                    try { using (Bind(other)) { } } catch (TargetInvocationException e) { rejected = e.InnerException is InvalidOperationException; }
                    Require(rejected, "Concurrent source ownership must be exclusive");
                    root.SetActive(false); root.SetActive(true); Invalid(context);
                }
            }));
            foreach (var npcType in new[] { typeof(NPCMovement), typeof(RandomNPCMovement), typeof(NPCATIVAR) })
                test("Actual ambient controller callbacks: " + npcType.Name, () =>
                {
                    var root = new GameObject("Owned ambient controller fixture"); root.SetActive(false);
                    var actor = (Behaviour)root.AddComponent(npcType);
                    PerformanceActorRosterContext context = null; IDisposable scope = null;
                    try
                    {
                        root.SetActive(true);
                        context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { actor }, new[] { "ambient-npc" }, Array.Empty<GameObject>(), false);
                        scope = Bind(context);
                        Require(context.ReadLivingHostileCount() == 0, "Ambient NPC is not an enemy");
                        actor.enabled = false; actor.enabled = true;
                        Require((long)scope.GetType().GetProperty("NotificationCount").GetValue(scope) >= 2, "Actual component callbacks must notify");
                        Invalid(context);
                    }
                    finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
                });
            test("Mixed NPC movement/interaction root is bound once and watches the secondary component", () =>
            {
                var root = new GameObject("Owned mixed NPC fixture"); root.SetActive(false);
                var movement = root.AddComponent<NPCMovement>(); var interaction = root.AddComponent<NPCATIVAR>();
                PerformanceActorRosterContext context = null; IDisposable scope = null;
                try
                {
                    root.SetActive(true);
                    var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    var actor = census.report.actors.Single(a => a.objectId == root.GetInstanceID());
                    Require(actor.role == "ambient-npc" && actor.supportedBinding && actor.components.Length == 2, "Mixed NPC root must remain one supported ambient actor");
                    context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { movement }, new[] { "ambient-npc" }, Array.Empty<GameObject>(), false);
                    scope = Bind(context);
                    Require(root.GetComponents<PerformanceActorLifetimeWitness>().Length == 1, "One destruction witness per actor root");
                    for (int i = 0; i < 64; i++) context.ReadLivingHostileCount();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount();
                    Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed mixed-controller polling must allocate zero managed bytes");
                    interaction.enabled = false; interaction.enabled = true; Invalid(context);
                }
                finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
            });
            test("Unknown ambient controllers created and removed between polls invalidate", () =>
            {
                foreach (var npcType in new[] { typeof(NPCMovement), typeof(RandomNPCMovement), typeof(NPCATIVAR) })
                    WithCuca((root, cuca, context, scope) =>
                    {
                        var unknown = new GameObject("Owned unknown ambient fixture"); unknown.SetActive(false);
                        try { unknown.AddComponent(npcType); unknown.SetActive(true); unknown.SetActive(false); }
                        finally { Object.DestroyImmediate(unknown); }
                        Invalid(context);
                    });
            });
            test("A never-enabled secondary NPC component destroyed between polls invalidates", () =>
            {
                var root = new GameObject("Owned inactive NPC component fixture"); root.SetActive(false);
                var movement = root.AddComponent<NPCMovement>(); var interaction = root.AddComponent<NPCATIVAR>(); interaction.enabled = false;
                PerformanceActorRosterContext context = null; IDisposable scope = null;
                try
                {
                    root.SetActive(true);
                    context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { movement }, new[] { "ambient-npc" }, Array.Empty<GameObject>(), false);
                    scope = Bind(context); Object.DestroyImmediate(interaction); Invalid(context);
                }
                finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
            });
            test("Disabling the explicit process owner releases source/replay ownership and reports failure", () =>
            {
                string temp = Path.Combine(Path.GetTempPath(), "Karawara-source-disable-" + Guid.NewGuid().ToString("N"));
                var actor = NewCuca(); actor.SetActive(true);
                var owned = new GameObject("Owned process disable fixture"); owned.SetActive(false);
                var context = Context(actor.GetComponent<CucaBoss>()); IDisposable scope = null;
                try
                {
                    scope = Bind(context);
                    var process = owned.AddComponent<PerformanceTutorialProcess>();
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    Action<string, object> set = (name, value) => typeof(PerformanceTutorialProcess).GetField(name, flags).SetValue(process, value);
                    set("initialized", true); set("personalRoot", Path.Combine(temp, "personal-unused"));
                    set("personalBefore", new Dictionary<string, string>()); set("outputPath", Path.Combine(temp, "terminal.json"));
                    set("request", new PerformanceProcessResponse { saveRoot = Path.Combine(temp, "saves-unused") });
                    set("actorContext", context); set("actorScope", scope);
                    owned.SetActive(true); process.enabled = false;
                    Require((bool)scope.GetType().GetProperty("IsDisposed").GetValue(scope) && context.IsDisposed, "Disabled process retained its diagnostic source owner");
                    var terminal = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(temp, "terminal.json")));
                    Require((bool)terminal["completed"] && !(bool)terminal["passed"], "Disabled owner must export an explicit failed terminal result");
                }
                finally { scope?.Dispose(); context.Dispose(); Object.DestroyImmediate(owned); Object.DestroyImmediate(actor); if (Directory.Exists(temp)) Directory.Delete(temp, true); }
            });
            test("Preflight census groups actual Cuca health and records uncovered Animator roots", () =>
            {
                var root = NewCuca(); root.SetActive(true);
                var visual = new GameObject("Owned unknown animated workload"); visual.AddComponent<Animator>();
                try
                {
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceActorCensus")).FirstOrDefault(t => t != null);
                    Require(type != null, "Runtime actor census missing");
                    Require(type.GetMethod("Capture").Invoke(null, new object[] { Array.Empty<string>() }) == null, "Unflagged census must be a no-op");
                    var snapshot = type.GetMethod("Capture").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" } });
                    var json = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeObject(snapshot));
                    var actors = (Newtonsoft.Json.Linq.JArray)json["report"]["actors"];
                    var own = actors.Where(a => (int)a["objectId"] == root.GetInstanceID()).ToArray();
                    Require(own.Length == 1 && ((Newtonsoft.Json.Linq.JArray)own[0]["components"]).Count == 2, "Cuca and HealthManager must remain one actual actor root");
                    Require((float?)own[0]["currentHealth"] == root.GetComponent<HealthManager>().currentHealth &&
                        (float?)own[0]["maximumHealth"] == 3000, "Census must retain actual scalar health for combat discovery, without certifying the route.");
                    Require(((Newtonsoft.Json.Linq.JArray)json["report"]["unclassifiedAnimators"]).Any(a => (int)a["objectId"] == visual.GetInstanceID()), "Unknown animated workloads cannot disappear from census");
                    Require(!(bool)json["report"]["creationPathsVerified"], "Discovery alone must not certify creation-path closure");
                }
                finally { Object.DestroyImmediate(root); Object.DestroyImmediate(visual); }
            });
            test("Arara movement census and actual transient callbacks", () =>
            {
                var root = new GameObject("Owned arara source fixture"); root.SetActive(false);
                var bird = root.AddComponent<AraraMovement>(); bird.waypoints = Array.Empty<Transform>();
                var visual = new GameObject("Owned bird visual"); visual.transform.SetParent(root.transform); visual.AddComponent<Animator>();
                PerformanceActorRosterContext context = null; IDisposable scope = null;
                try
                {
                    root.SetActive(true);
                    var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    var entry = census.report.actors.SingleOrDefault(a => a.objectId == root.GetInstanceID());
                    Require(entry != null && entry.role == "ambient-animal" && entry.supportedBinding, "Actual AraraMovement must be discovered without name classification");
                    Require(!census.report.unclassifiedAnimators.Any(a => a.objectId == visual.GetInstanceID()), "Child Animator belongs to movement source");
                    context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { bird }, new[] { "ambient-animal" }, Array.Empty<GameObject>(), false);
                    scope = Bind(context); Require(context.ReadLivingHostileCount() == 0, "Animal is not an enemy");
                    bird.enabled = false; bird.enabled = true;
                    Require((long)scope.GetType().GetProperty("NotificationCount").GetValue(scope) >= 2, "Actual arara callbacks must notify"); Invalid(context);
                }
                finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
            });
            test("Unknown Arara created and removed between polls invalidates", () => WithCuca((root, cuca, context, scope) =>
            {
                var birdRoot = new GameObject("Owned unknown bird"); birdRoot.SetActive(false);
                try { var bird = birdRoot.AddComponent<AraraMovement>(); bird.waypoints = Array.Empty<Transform>(); birdRoot.SetActive(true); birdRoot.SetActive(false); }
                finally { Object.DestroyImmediate(birdRoot); }
                Invalid(context);
            }));
            test("Pending loader visual remains unclassified and rejects freezing", () => WithWorldVisual((visual, manager) =>
            {
                var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                Require(!census.report.actors.Any(a => a.objectId == visual.GetInstanceID() && a.supportedBinding) &&
                    census.report.unclassifiedAnimators.Any(a => a.objectId == visual.GetInstanceID()), "Pending texture must not qualify visual");
                bool rejected = false;
                try { using (PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { visual.GetComponent<Animator>() }, new[] { "world-visual" }, Array.Empty<GameObject>(), false)) { } }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Pending visual context accepted");
            }));
            test("Actual loader creation between polls invalidates even for inactive prefab", () => WithCuca((root, cuca, context, scope) =>
            {
                WithWorldVisual((visual, manager) => Require(!visual.activeInHierarchy, "Prefab stays authored inactive")); Invalid(context);
            }));
            test("Cached visual references detect destroyed children without warmed allocations", () =>
            {
                var root = new GameObject("Owned visual Animator fixture"); root.SetActive(false);
                var animator = root.AddComponent<Animator>();
                var child = new GameObject("Owned child Animator"); child.transform.SetParent(root.transform); var secondary = child.AddComponent<Animator>();
                PerformanceActorRosterContext context = null;
                try
                {
                    context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { animator }, new[] { "world-visual" }, Array.Empty<GameObject>(), false);
                    for (int i = 0; i < 64; i++) context.ReadLivingHostileCount();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount();
                    Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed visual polling allocated managed bytes");
                    Object.DestroyImmediate(secondary); Invalid(context);
                }
                finally { context?.Dispose(); Object.DestroyImmediate(root); }
            });
            foreach (bool transient in new[] { false, true })
                test("Visual child GameObject activity is frozen, transient=" + transient, () =>
                {
                    var root = new GameObject("Owned visible root"); root.SetActive(false); var animator = root.AddComponent<Animator>();
                    var child = new GameObject("Owned independently active visual"); child.transform.SetParent(root.transform); child.AddComponent<Animator>();
                    PerformanceActorRosterContext context = null; IDisposable scope = null;
                    try
                    {
                        root.SetActive(true);
                        context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new Component[] { animator }, new[] { "world-visual" }, Array.Empty<GameObject>(), false);
                        scope = Bind(context); Require(context.ReadLivingHostileCount() == 0, "Visual baseline must be valid");
                        child.SetActive(false); if (transient) child.SetActive(true); Invalid(context);
                    }
                    finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
                });
            foreach (string transition in new[] { "root-toggle", "component-toggle", "destroy", "unknown-transient", "reference", "baseline" })
                test("Authored visual exact binding and source transition: " + transition, () =>
                {
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceAuthoredVisual")).FirstOrDefault(t => t != null);
                    Require(type != null, "Explicit authored visual source is missing");
                    var root = new GameObject("Owned authored visual " + Guid.NewGuid().ToString("N")); root.SetActive(false);
                    var animator = root.AddComponent<Animator>(); root.AddComponent<SkinnedMeshRenderer>();
                    var source = root.AddComponent(type);
                    PerformanceActorRosterContext context = null; IDisposable scope = null;
                    try
                    {
                        type.GetField("visual").SetValue(source, animator); root.SetActive(true);
                        var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                        Require(census.report.actors.Any(a => a.objectId == root.GetInstanceID() && a.role == "world-visual" && a.supportedBinding), "Authored source was not bound by exact component");
                        Require(!census.report.unclassifiedAnimators.Any(a => a.componentId == animator.GetInstanceID()), "Bound visual remains unclassified");
                        context = PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new[] { source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false);
                        scope = Bind(context); Require(context.ReadLivingHostileCount() == 0, "Visual was counted as hostile");
                        if (transition == "root-toggle") { root.SetActive(false); root.SetActive(true); }
                        if (transition == "component-toggle") { ((Behaviour)source).enabled = false; ((Behaviour)source).enabled = true; }
                        if (transition == "destroy") Object.DestroyImmediate(source);
                        if (transition == "reference") type.GetField("visual").SetValue(source, null);
                        if (transition == "unknown-transient")
                        {
                            var unknown = new GameObject("Owned transient authored visual"); unknown.SetActive(false);
                            try { var other = unknown.AddComponent(type); type.GetField("visual").SetValue(other, unknown.AddComponent<Animator>()); unknown.AddComponent<SkinnedMeshRenderer>(); unknown.SetActive(true); unknown.SetActive(false); }
                            finally { Object.DestroyImmediate(unknown); }
                        }
                        if (transition != "baseline") Invalid(context);
                        else
                        {
                            for (int i = 0; i < 64; i++) context.ReadLivingHostileCount();
                            long before = GC.GetAllocatedBytesForCurrentThread();
                            for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount();
                            Require(GC.GetAllocatedBytesForCurrentThread() == before, "Authored visual warmed polling allocated");
                            var result = new CaptureResult(); context.Validate(result);
                            Require(!result.conditionsChanged && !result.populationVerified, "Bounded visual proof granted global certification");
                        }
                    }
                    finally { scope?.Dispose(); context?.Dispose(); Object.DestroyImmediate(root); }
                });
            test("Malformed authored source stays unclassified and cannot hide its Animator", () =>
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceAuthoredVisual")).First(t => t != null);
                var root = new GameObject("Owned malformed visual"); root.SetActive(false);
                var animator = root.AddComponent<Animator>(); root.AddComponent<SkinnedMeshRenderer>(); root.AddComponent(type);
                try
                {
                    var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    Require(census.report.actors.Any(a => a.objectId == root.GetInstanceID() && !a.supportedBinding), "Invalid source was accepted");
                    Require(census.report.unclassifiedAnimators.Any(a => a.componentId == animator.GetInstanceID()), "Invalid source swallowed an unbound visual");
                }
                finally { Object.DestroyImmediate(root); }
            });
            foreach (string scenario in new[] { "pending", "unbound", "duplicate" })
                test("Spawner census retains exact creation ownership: " + scenario, () =>
                {
                    var owner = new GameObject("Owned spawner association fixture"); owner.SetActive(false);
                    var second = new GameObject("Owned second spawner fixture"); second.SetActive(false);
                    var created = new GameObject("Same name must not establish actor ownership"); created.SetActive(false);
                    var prefab = new GameObject(created.name); prefab.SetActive(false);
                    try
                    {
                        var spawner = owner.AddComponent<AICharacterSpawner>();
                        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(AICharacterSpawner).GetField("characterGameObject", fields).SetValue(spawner, prefab);
                        if (scenario != "pending") typeof(AICharacterSpawner).GetField("instantiatedGameObject", fields).SetValue(spawner, created);
                        if (scenario == "duplicate") typeof(AICharacterSpawner).GetField("instantiatedGameObject", fields).SetValue(second.AddComponent<AICharacterSpawner>(), created);
                        var reportObject = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }).report;
                        var json = Newtonsoft.Json.Linq.JObject.FromObject(reportObject);
                        var rows = json["spawnerAssociations"] as Newtonsoft.Json.Linq.JArray;
                        Require(rows != null, "Exact spawner associations are missing from the census");
                        var row = rows.Single(r => (int)r["spawnerId"] == spawner.GetInstanceID());
                        Require((int)row["prefabId"] == prefab.GetInstanceID(), "Exact prefab reference was not retained");
                        Require((int)row["createdObjectId"] == (scenario == "pending" ? 0 : created.GetInstanceID()), "Created identity was inferred from a name");
                        Require(!(bool)row["linked"], "Unspawned/unbound/duplicate ownership was certified");
                        Require((string)row["status"] == (scenario == "pending" ? "not-created" : scenario == "duplicate" ? "ambiguous-owner" : "unbound-actor"), "Wrong fail-closed creation status");
                        Require(!reportObject.creationPathsVerified, "Association diagnostics granted global creation certification");
                    }
                    finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(second); Object.DestroyImmediate(created); Object.DestroyImmediate(prefab); }
                });
            foreach (bool transient in new[] { false, true })
                test("Separate actor phase preserves rejected transition and observes new scope: " + transient, () => WithCuca((root, cuca, context, scope) =>
                {
                    var added = NewCuca(); IDisposable phase = null; GameObject unknown = null;
                    try
                    {
                        added.SetActive(true);
                        var type = typeof(PerformanceActorCensus).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceActorPhase");
                        Require(type != null, "Separate actor phase owner is missing");
                        var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                        phase = (IDisposable)type.GetMethod("Begin").Invoke(null, new object[] { census, context, scope, 5.0 });
                        if (transient) { unknown = NewCuca(); unknown.SetActive(true); unknown.SetActive(false); Object.DestroyImmediate(unknown); unknown = null; }
                        type.GetMethod("Complete").Invoke(phase, new object[] { 10.0 });
                        var reportJson = Newtonsoft.Json.Linq.JObject.FromObject(type.GetProperty("Evidence").GetValue(phase));
                        Require((bool)reportJson["previousValidation"]["conditionsChanged"], "Previous UnknownActor violation was erased");
                        Require((bool)reportJson["validation"]["conditionsChanged"] == transient, "New scope did not retain its own lifecycle boundary");
                        Require(!(bool)reportJson["validation"]["populationVerified"], "Separate phase granted population certification");
                        Invalid(context);
                    }
                    finally { phase?.Dispose(); if (unknown != null) Object.DestroyImmediate(unknown); Object.DestroyImmediate(added); }
                }));
            test("Failed phase evidence cannot prevent repeated process cleanup", () => WithCuca((root, cuca, context, scope) =>
            {
                var owner = new GameObject("Owned inactive phase cleanup process"); owner.SetActive(false);
                var type = typeof(PerformanceActorCensus).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceActorPhase");
                IDisposable phase = null;
                try
                {
                    var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    phase = (IDisposable)type.GetMethod("Begin").Invoke(null, new object[] { census, context, scope, 0.0 });
                    phase.Dispose(); // Actual unavailable phase, not a mocked exception.
                    var process = owner.AddComponent<PerformanceTutorialProcess>();
                    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(PerformanceTutorialProcess).GetField("postMissionActorPhase", fields).SetValue(process, phase);
                    typeof(PerformanceTutorialProcess).GetField("actorScope", fields).SetValue(process, scope);
                    typeof(PerformanceTutorialProcess).GetField("actorContext", fields).SetValue(process, context);
                    var stop = typeof(PerformanceTutorialProcess).GetMethod("StopRehearsal", fields);
                    stop.Invoke(process, new object[] { true }); stop.Invoke(process, new object[] { true });
                    Require(typeof(PerformanceTutorialProcess).GetField("actorContext", fields).GetValue(process) == null, "Previous context cleanup was skipped");
                    var errors = (List<string>)typeof(PerformanceTutorialProcess).GetField("errors", fields).GetValue(process);
                    Require(errors.Any(e => e.Contains("Post-mission actor evidence unavailable")), "Failed evidence was silently discarded");
                }
                finally { phase?.Dispose(); Object.DestroyImmediate(owner); }
            }));
            foreach (string scenario in new[] { "opt-in", "no-prefab", "ownership", "bounded", "allocation" })
                test("Bounded AI creation history through production source: " + scenario, () =>
                {
                    var type = typeof(PerformanceActorCensus).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory");
                    Require(type != null, "AI creation history scope missing");
                    Func<string[], IDisposable> begin = args => (IDisposable)type.GetMethod("Begin").Invoke(null, new object[] { args });
                    Func<IDisposable, Newtonsoft.Json.Linq.JObject> capture = scope => Newtonsoft.Json.Linq.JObject.FromObject(type.GetMethod("Capture").Invoke(scope, new object[] { null }));
                    var root = new GameObject("Owned inactive creation-history source"); root.SetActive(false);
                    var spawner = root.AddComponent<AICharacterSpawner>(); IDisposable owned = null;
                    try
                    {
                        if (scenario == "opt-in")
                        {
                            Require(begin(Array.Empty<string>()) == null, "Unflagged history was activated");
                            bool rejected = false;
                            try { owned = begin(new[] { "-karawaraPerformanceRoster", "-karawaraPerformanceRoster" }); }
                            catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                            Require(rejected, "Duplicate history activation was accepted"); return;
                        }
                        owned = begin(new[] { "-karawaraPerformanceRoster" });
                        if (scenario == "ownership")
                        {
                            bool rejected = false;
                            try { using (begin(new[] { "-karawaraPerformanceRoster" })) { } }
                            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                            Require(rejected, "Concurrent history receivers were accepted");
                            var previous = owned; previous.Dispose(); owned = begin(new[] { "-karawaraPerformanceRoster" }); previous.Dispose();
                        }
                        if (scenario == "bounded") for (int i = 0; i < 1100; i++) spawner.AttemptToSpawnCharacter();
                        else if (scenario == "allocation")
                        {
                            for (int i = 0; i < 64; i++) spawner.AttemptToSpawnCharacter();
                            long before = GC.GetAllocatedBytesForCurrentThread();
                            for (int i = 0; i < 100; i++) spawner.AttemptToSpawnCharacter();
                            Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed creation callbacks allocated owned managed bytes");
                        }
                        else spawner.AttemptToSpawnCharacter();
                        var evidence = capture(owned);
                        Require(!(bool)evidence["currentAiHistoryMatched"] && !(bool)evidence["creationPathsVerified"], "No-census/no-creation fixture granted history or global certification");
                        if (scenario == "bounded") Require((bool)evidence["overflow"] && evidence["attempts"].Count() <= 1024 && evidence["events"].Count() <= 8192, "History overflow was not bounded and latched");
                        else
                        {
                            Require(!(bool)evidence["sourceFaulted"] && (int)evidence["failedAttempts"] > 0, "Missing production prefab should be retained as failed creation, not an internal observer fault");
                            Require((int)evidence["attempts"][0]["spawnerId"] == spawner.GetInstanceID() && (bool)evidence["attempts"][0]["finished"] && !(bool)evidence["attempts"][0]["registered"], "Production no-op attempt lost its owner/completion or fabricated registration");
                            Require(evidence["events"].Count() >= 2, "Production request/completion events were not retained");
                        }
                    }
                    finally { owned?.Dispose(); Object.DestroyImmediate(root); }
                });
            foreach (string scenario in new[] { "transient-identities", "detached-snapshot", "disposed-scope", "bounded", "allocation", "subject-owner", "new-component-same-root" })
                test("Actor source event history: " + scenario, () => WithCuca((root, cuca, context, scope) =>
                {
                    var method = Scope().GetMethod("CaptureHistory"); Require(method != null, "Actor lifecycle identity journal missing");
                    Func<Newtonsoft.Json.Linq.JObject> snapshot = () => Newtonsoft.Json.Linq.JObject.FromObject(method.Invoke(scope, null));
                    if (scenario == "subject-owner")
                    {
                        var reportObject = method.Invoke(scope, null); var evidence = Newtonsoft.Json.Linq.JObject.FromObject(reportObject);
                        var subjects = evidence["initiallyBoundSubjects"];
                        Require(subjects != null && subjects.Any(s => (int)s["componentId"] == cuca.GetInstanceID() && (int)s["rootId"] == root.GetInstanceID() && (int)s["ownerRootId"] == root.GetInstanceID()), "Exact initially bound source/owner map missing");
                        PerformanceActorLifecycleScope.Notify(cuca, PerformanceActorLifecycle.HealthChanged);
                        Require((int)snapshot()["events"].Last()["ownerRootId"] == root.GetInstanceID(), "Known callback lost its exact owner");
                        var rows = (Array)reportObject.GetType().GetField("initiallyBoundSubjects").GetValue(reportObject);
                        var first = rows.GetValue(0); first.GetType().GetField("ownerRootId").SetValue(first, -1); rows.SetValue(first, 0);
                        Require(snapshot()["initiallyBoundSubjects"].All(s => (int)s["ownerRootId"] == root.GetInstanceID()), "Returned owner map mutated observer state");
                    }
                    else if (scenario == "new-component-same-root")
                    {
                        var added = root.AddComponent<NPCDialogue>();
                        try
                        {
                            PerformanceActorLifecycleScope.Notify(added, PerformanceActorLifecycle.Enabled);
                            var row = snapshot()["events"].Last();
                            Require((int)row["rootId"] == root.GetInstanceID() && !(bool)row["initiallyBound"] && (int)row["ownerRootId"] == 0,
                                "New unreviewed component on an old root was treated as an initially bound subject");
                        }
                        finally { Object.DestroyImmediate(added); }
                    }
                    else if (scenario == "transient-identities")
                    {
                        var unknown = new GameObject("Owned transient journal NPC"); unknown.SetActive(false);
                        int rootId = unknown.GetInstanceID(); var npc = unknown.AddComponent<NPCDialogue>(); int componentId = npc.GetInstanceID();
                        unknown.SetActive(true); unknown.SetActive(false); Object.DestroyImmediate(unknown);
                        var history = snapshot(); var rows = history["events"].Where(row => (int)row["rootId"] == rootId).ToArray();
                        Require(rows.Length >= 2 && rows.All(row => (int)row["componentId"] == componentId && !(bool)row["initiallyBound"] && (string)row["componentType"] == typeof(NPCDialogue).FullName), "Unknown transient lost its actual identity/type/source binding");
                        Require(!(bool)history["sourceFaulted"] && !(bool)history["overflow"], "Valid transient history faulted"); Invalid(context);
                    }
                    else
                    {
                        for (int i = 0; i < 64; i++) PerformanceActorLifecycleScope.Notify(cuca, PerformanceActorLifecycle.HealthChanged);
                        if (scenario == "allocation")
                        {
                            long before = GC.GetAllocatedBytesForCurrentThread();
                            for (int i = 0; i < 100; i++) PerformanceActorLifecycleScope.Notify(cuca, PerformanceActorLifecycle.HealthChanged);
                            Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed journal callback allocated owned managed bytes");
                        }
                        if (scenario == "bounded") for (int i = 0; i < 16400; i++) PerformanceActorLifecycleScope.Notify(cuca, PerformanceActorLifecycle.HealthChanged);
                        if (scenario == "detached-snapshot")
                        {
                            var reportObject = method.Invoke(scope, null); var rows = (Array)reportObject.GetType().GetField("events").GetValue(reportObject);
                            var first = rows.GetValue(0); first.GetType().GetField("rootId").SetValue(first, -1); rows.SetValue(first, 0);
                            Require((int)snapshot()["events"][0]["rootId"] == root.GetInstanceID(), "Exported snapshot mutated live journal");
                        }
                        if (scenario == "disposed-scope")
                        {
                            int before = snapshot()["events"].Count(); scope.Dispose(); PerformanceActorLifecycleScope.Notify(cuca, PerformanceActorLifecycle.HealthChanged);
                            Require(snapshot()["events"].Count() == before, "Disposed scope received new source events");
                        }
                        var evidence = snapshot();
                        if (scenario == "bounded") Require((bool)evidence["overflow"] && (bool)evidence["sourceFaulted"] && (int)evidence["dropped"] > 0 && evidence["events"].Count() == 16384, "Overflow did not latch a bounded failure");
                        else Require(!(bool)evidence["overflow"] && !(bool)evidence["sourceFaulted"] && evidence["events"].All(row => (bool)row["initiallyBound"]), "Owned source journal was inconsistent");
                    }
                }));
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                populationVerified = false, boundary = "Actual synchronous source callbacks on owned PlayMode actors only; no gameplay host spawn, route closure or timing proof" };
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-actor-source-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented)); Debug.Log("Actor source fixture: " + path);
        }
        static Type Scope() => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceActorLifecycleScope")).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("Source lifecycle scope missing");
        static IDisposable Bind(PerformanceActorRosterContext context, string[] args = null) => (IDisposable)Scope().GetMethod("Begin").Invoke(null, new object[] { args ?? new[] { "-karawaraPerformanceRoster" }, context });
        static PerformanceActorRosterContext Context(Component actor) => PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, new[] { actor }, new[] { "hostile" }, Array.Empty<GameObject>(), false);
        static GameObject NewCuca()
        {
            var root = new GameObject("Owned source Cuca fixture"); root.SetActive(false);
            var health = root.AddComponent<HealthManager>(); health.maxHealth = 3000;
            root.AddComponent<CucaBoss>(); return root;
        }
        static void WithCuca(Action<GameObject, CucaBoss, PerformanceActorRosterContext, IDisposable> body)
        {
            var root = NewCuca(); PerformanceActorRosterContext context = null; IDisposable scope = null;
            try { root.SetActive(true); var cuca = root.GetComponent<CucaBoss>(); context = Context(cuca); scope = Bind(context); body(root, cuca, context, scope); }
            finally { scope?.Dispose(); context?.Dispose(); if (root != null) Object.DestroyImmediate(root); }
        }
        static void Invalid(PerformanceActorRosterContext context)
        {
            var result = new CaptureResult(); context.Validate(result);
            Require(result.conditionsChanged && result.errors.Count > 0 && !result.populationVerified, "Real transient source transition was not latched");
        }
        static void WithWorldVisual(Action<GameObject, WorldSaveGameManager> body)
        {
            var managerRoot = new GameObject("Owned inactive loader fixture"); managerRoot.SetActive(false);
            var manager = managerRoot.AddComponent<WorldSaveGameManager>();
            var prefab = new GameObject("Owned loader prefab " + Guid.NewGuid().ToString("N")); prefab.SetActive(false); prefab.AddComponent<Animator>();
            var data = ScriptableObject.CreateInstance<AssetData>(); data.prefab = prefab;
            GameObject created = null; System.Collections.IEnumerator operation = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                typeof(WorldSaveGameManager).GetField("assetData", flags).SetValue(manager, data);
                operation = (System.Collections.IEnumerator)typeof(WorldSaveGameManager).GetMethod("InstantiatePrefabWithTextureAsync", flags).Invoke(manager, new object[] { prefab, "OwnedFixtureUnusedTexture" });
                Require(operation.MoveNext(), "Actual loader must reach its texture request yield");
                created = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(a => a.gameObject).Single(g => g.name == prefab.name + "(Clone)");
                var property = typeof(WorldSaveGameManager).GetProperty("QaLoadedWorldObject");
                Require(property != null && property.GetValue(manager) as GameObject == created, "Actual loader creation reference is unavailable");
                created.name = "Renamed owned world visual"; body(created, manager);
            }
            finally
            {
                (operation as IDisposable)?.Dispose();
                if (created != null) Object.DestroyImmediate(created);
                Object.DestroyImmediate(prefab); Object.DestroyImmediate(managerRoot); Object.DestroyImmediate(data);
            }
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
