# Migration inventory report

Date: 2026-10-01. Static read-only audit of plan 02 Task 1 Step 1 and TO-BE §§5.1–5.4/6. G1A/G1B are incomplete; local runtime has not been implemented.

- 63 comment-stripped C# dependency files, including 6 DevTools files; 35 active NetworkVariable declarations; 16 RPC declarations; 19 own direct/indirect NetworkBehaviour-derived classes.
- 16 serialized text assets reference package network or own inherited network scripts. All seven enabled scenes and 1 existing playground scene root(s) have transitive GUID dependency coverage in the [map](../02-network-migration-map.md). Counts are raw static references, not runtime objects.
- Largest risks: HP/vitality/death callbacks trigger HUD/heal/corruption/boss phase effects; equipment events instantiate models/spells and same-ID refresh; boss initialization orders progress/fog/spawn identity; network spawn/despawn owns listeners/FSM clones/audio; direct scene and persistent manager cleanup can retain targets/enemies/UI; QA still depends on host, ownership and transport.
- Netcode 2.11.1 and Multiplayer Center 1.0.1 are direct dependencies; Transport 2.7.2 is resolved transitively (NGO requires 2.6.0). Package removal belongs after all runtime/QA/serialized consumers migrate.

Only this report and the map were created. No scripts, assets, package files, existing docs, saves, commits or Editor state were changed by this inventory worker; concurrent parent/other-worker changes are outside this report. No Unity build/compile/test/runtime/performance evidence was produced. Source data-flow, binary serialization, effective prefab overrides and UnityEvent target resolution remain explicit omissions; future LocalRuntimeAudit and regressions must resolve them.

Next dependency-safe work: close 1A save protection/legacy restore and 1B current-host baseline. After both gates, implement plan 02 Task 1 pure state/registry tests and contracts before migrating player, world and session; keep package/host removal last.
