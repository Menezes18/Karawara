# Network migration map — inventory only

Date: 2026-10-01. Source HEAD: `a2b43f09`. Scope: plan 02 Task 1 Step 1 and TO-BE §§5.1–5.4/6. G1A/G1B are not closed; no local runtime, state, registry, fixture, audit MenuItem, script, asset or package change is delivered here. This document is a read-only static inventory for future work.

The scan covers all `Assets/**/*.cs`, packages manifest/lock and serialized text assets, including DevTools and code outside Assets/Scripts. Comments are removed with line numbers preserved before counting active code; commented stamina declarations and commented RPC calls are excluded. These are source dependencies expected to compile in the existing assemblies, not a new compilation result. Preprocessor expressions were not evaluated by Unity; Editor folders remain included as QA dependencies. Own gameplay/DevTools match files are listed below; vendor/package source is not a migration target.

## Behavioral replacements and fixtures

All fixture names below are planned acceptance checks, not run results. Core state stores primitive gameplay data; Unity object resolution, Animator, VFX, equipment, HUD and session orchestration stay in Gameplay. Registry IDs are session-local and must never enter saves.

| Family | Preserve / local replacement | Planned fixture |
|---|---|---|
| HP/max HP/vitality/endurance/death | SetMaxHealth/SetHealth clamp 0..max; old/new events only once per change; death idempotent; HUD reads only. Vitality currently recalculates max HP and fully heals; retain order until characterized by 1A. Endurance is saved even though stamina mechanics are commented. | HealthClampsToMax, HealthChangedFiresOnce, DeathIsIdempotent; local player damage/heal/revive/HUD; G1A round-trip |
| active/movement/flags | Explicit local active state and Animator setters/events; invulnerability/block/spirit power remain gameplay state. Do not discard flags because the publisher is an animation event. | player movement/jump/roll/Q/Ctrl/caster charge; re-enable callback uniqueness |
| position/rotation and animation float replicas | networkPosition/networkRotation/horizontalMovement/verticalMovement/moveAmount are replication mirrors. Retain local Transform, root motion and locomotion variables; discard mirror/smoothing only after QA teleport callers migrate. | teleport/direct entry/revive/root motion; production route parity |
| target ID/locked-on | Replace ulong with CharacterRuntimeState/local CharacterManager reference resolved via registry; clear target on unregister/disable/death/unload. | TargetClearsOnUnregister, RegistryDoesNotReturnInactiveCharacter; lock-on switching |
| equipment/spell/name/hand | Local observable IDs/catalog lookups and string; preserve Instantiate/load model/quickslot icon/current action weapon/Animator charge callbacks and same-ID refresh used by QA. | first spear/caster action, swap both hands, equip same ID, spell charge |
| boss progress/fog/checkpoint | Scene-owned registration and events; apply legacy progress before awake/fight presentation; keep save dictionaries until phase 3B. | local world wake/phase/death/music/fog/checkpoint/reset/unload |
| RPC gameplay effects | One local call to damage/effect receiver; keep dedup per activation, source/caster and friendly-fire rules. Animation already plays locally, so dropping broadcast must not replay it. | every hit once; block/invulnerable/spirit; projectile source; late hitbox event cleanup |
| host/menu/session/QA | GameBootstrap/SceneContext readiness → player bind → apply once → HUD → input. New/load/return Busy/cancellation use unscaled time and isolated QA save root. | MenuCreatesNoHost, NewGameCreatesExactlyOnePlayer, LoadAppliesOnceAfterReady, ReturnToMenuDisposesSession, DuplicateTransitionReturnsBusy, PausedLoadCompletes, CancelledLoadReleasesInput, DirectQaSceneUsesIsolatedSession |

## Critical lifecycle and integration observations

- CharacterManager spawn initializes Animator and active state, then subscribes isMoving/isActive; despawn removes both. Player spawn binds camera/input/save singleton player, scene audio callback, client-connected callback, vitality/HP/target/equipment/spell/charge listeners. Player despawn removes them and disables audio. Disable alone currently does not replace all network teardown; local Shutdown must cover re-enable and scene unload symmetrically.
- Player HP subscribers include CheckHP, owner HUD and non-owner floating bar. CheckHP starts ProcessDeathEvent and disables tagged Corruption, and clamps HP by writing it again. Boss CheckHP additionally tests IsSpawned/IsOwner, switches phase once using hasShifted, and restores background music on death. A plain bool/field conversion would lose these behaviors.
- PlayerNetworkManager SetNewMaxHealthValue changes max HP, sets HUD max and refills HP. Equipment ID listeners Instantiate templates, change inventory/current weapon, load models and update quickslots; spell listener clones or clears the spell. QA explicitly invokes a callback when the ID is unchanged, so state events alone cannot replace refresh commands.
- AI spawn clones idle/pursue/combat/attack ScriptableObjects per instance; boss adds sleep clone and phase shift creates a combat clone. Source currently lacks symmetric disposal of all those clones. Local lifecycle must own them without sharing mutable FSM instances. AI floating HP uses OnEnable/OnDisable; health CheckHP uses spawn/despawn.
- Boss spawn restores dictionaries, starts fog-wall discovery coroutine, toggles fog and character active, then plays sleep if not awakened. Discovery waits while the global list is empty; current fog iteration can occur before the coroutine has populated the boss list. Future SceneContext readiness should characterize/fix ordering without treating an empty world as an infinite loading condition.
- Boss wake starts fight/music/bar/fog and records awakened progress. Death sets HP/dead/fight/fog/defeated, updates legacy dictionaries and saves. Boss bar owns health listener and destroys/unsubscribes on scene changes; boss also removes its owned bar on disable/despawn/activeSceneChanged. Preserve single owner and delayed removal.
- Fog spawn applies active visual state and registers with WorldObjectManager; despawn unregisters. SiteOfGrace subscribes activation at spawn, restores progress in Start, changes particles/text on event, heals player during interaction and resets AI on rest. Interactable hostOnly/ownership checks must become explicit local interaction rules.
- WorldAIManager spawns only on server, tracks enemies/bosses and despawns tracked characters; its reset clears character list but does not clear spawnedInBosses. WorldObjectManager persists via DontDestroyOnLoad, tracks spawners/fog and self-creates for direct scenes. WorldGameSessionManager has no Netcode import but maintains player list from the client-connected callback; local session must clear/remove it explicitly.
- BossSpawnerInput spawns before assigning bossID, then registers/wakes boss. Local initialization must apply serialized/configured boss identity before progress reads. AICharacterSpawner applies element separately; its ChangeElement call is currently commented in the spawn path.
- CharacterAnimatorManager already applies animation/root motion/action/movement flags before broadcasting. Broadcast clients skip sender. Damage RPC instead resolves two SpawnedObjects IDs, checks invulnerability, builds TakeDamageEffect and processes it. Melee, hand/club and FireBall call RPC; Durk stomp already calls the effect locally under victim ownership. Unifying paths must not double damage or lose caster identity.
- AddHealth/AddMaxHealth/IncreaseSprintingSpeed RPCs have no active external C# callers in the scan; the speed RPC finds a player globally. Keep them on the serialized method audit list before removal. Player weapon-action RPC has only its definition/internal forwarding; external PlayerCombat caller is commented. FX RPC remains called from CharacterCombatManeger.
- WorldSave readiness depends on IsSpawned/IsOwner; Player load skips host via IsOwner && !IsServer, adds +100 to restored Y and restores skills from slot01. These are G1A characterization dependencies. LoadScene has no direct network token but must participate in readiness/cancellation/time-scale transition checks.
- PlayerUIManager's F1 debug branch Shutdown/StartClient, TitleScreen host startup and LocalHostStartup port probing remain compiled dependencies; DevPlayground registers prefabs, starts host, rewrites network poses, spawns/despawns and asserts ownership. All QA runners must migrate as well.
- Durk and Saci subclasses inherit Netcode without literal network tokens. SpellProjectileDamageCollider holds a CharacterManager caster despite no direct token. SaciKickDamageCollider.cs actually defines SaciWhirlwindDamage; SaciWhirlwindDamageCollider.cs defines SaciWhirlwindController. Preserve actual class/metas and animation methods. CucaBoss and HealthManager are already local; adapting inherited consumers is distinct from rewriting Cuca.

## Counts

- 63 comment-stripped C# match files (6 DevTools; 57 gameplay); 35 active NetworkVariable declarations.
- 19 own direct/indirect NetworkBehaviour-derived classes.
- 16 serialized assets reference package network scripts or own network-derived scripts; raw GUID occurrences are not instantiated runtime object counts.

## Field → writers → subscribers → replacement → fixture

Line references use current source paths. Every access line is recorded in the appendix; writers below use `.Value` assignment operators (including +=/-=), subscribers list binding sites/handler names. Same-name isActive is disambiguated by receiver/source in the appendix. No active writer means retain/characterize readers before any removal.

### Assets/Scripts/FogWallInteractable.cs:17 — `isActive: bool`

- Writers: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:105; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:114; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:117; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:162; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:229.
- Subscribers: Assets/Scripts/FogWallInteractable.cs:24 `OnIsActiveChanged`; Assets/Scripts/States/Character/CharacterManager.cs:111 `characterNetworkManager.OnIsActiveChanged`.
- Replacement/fixture: world progress/presentation local events; boss/music/fog/checkpoint/save/unload regression.
- Access evidence: Assets/Scripts/FogWallInteractable.cs:23; Assets/Scripts/FogWallInteractable.cs:24; Assets/Scripts/FogWallInteractable.cs:32; Assets/Scripts/FogWallInteractable.cs:38; Assets/Scripts/States/Character/CharacterManager.cs:108; Assets/Scripts/States/Character/CharacterManager.cs:111; Assets/Scripts/States/Character/CharacterManager.cs:119; Assets/Scripts/States/Character/CharacterNetworkManager.cs:162; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:105; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:114; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:117; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:162; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:229.

### Assets/Scripts/SiteOfGraceInteractable.cs:17 — `isActivated: bool`

- Writers: Assets/Scripts/SiteOfGraceInteractable.cs:35; Assets/Scripts/SiteOfGraceInteractable.cs:39; Assets/Scripts/SiteOfGraceInteractable.cs:127.
- Subscribers: Assets/Scripts/SiteOfGraceInteractable.cs:61 `OnIsActivatedChanged`.
- Replacement/fixture: world progress/presentation local events; boss/music/fog/checkpoint/save/unload regression.
- Access evidence: Assets/Scripts/SiteOfGraceInteractable.cs:35; Assets/Scripts/SiteOfGraceInteractable.cs:39; Assets/Scripts/SiteOfGraceInteractable.cs:43; Assets/Scripts/SiteOfGraceInteractable.cs:59; Assets/Scripts/SiteOfGraceInteractable.cs:61; Assets/Scripts/SiteOfGraceInteractable.cs:79; Assets/Scripts/SiteOfGraceInteractable.cs:127; Assets/Scripts/SiteOfGraceInteractable.cs:167.

### Assets/Scripts/States/Character/CharacterManager.cs:12 — `isDead: bool`

- Writers: Assets/Scripts/States/Character/CharacterManager.cs:128; Assets/Scripts/States/Character/Player/PlayerManager.cs:244; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:157.
- Subscribers: none found.
- Replacement/fixture: gameplay health/stats/death local state; clamp/change-once/idempotence + HUD/revive/G1A regression.
- Access evidence: Assets/Scripts/States/Character/CharacterManager.cs:128; Assets/Scripts/States/Colliders/DurkClubDamageCollider.cs:22; Assets/Scripts/States/Colliders/TakeBlockedDamageEffect.cs:40; Assets/Scripts/States/Colliders/TakeBlockedDamageEffect.cs:111; Assets/Scripts/States/Colliders/UndeadHandDamageCollider.cs:22; Assets/Scripts/States/Effects/TakeDamageEffect.cs:52; Assets/Scripts/States/Effects/TakeDamageEffect.cs:117; Assets/Scripts/States/State/AttackState.cs:26; Assets/Scripts/States/Character/Player/PlayerCamera.cs:281; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:202; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:231; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:569; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:176; Assets/Scripts/States/Character/Player/PlayerManager.cs:244; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:157; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:258; Assets/Scripts/States/AI/AIChar/AICharacterCombatManager.cs:55; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:141; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:147; Assets/DevTools/Editor/PlaygroundUIRegression.cs:99; Assets/DevTools/Runtime/DevPlaygroundController.cs:428; Assets/DevTools/Runtime/DevPlaygroundController.cs:687.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:14 — `isActive: bool`

- Writers: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:105; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:114; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:117; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:162; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:229.
- Subscribers: Assets/Scripts/FogWallInteractable.cs:24 `OnIsActiveChanged`; Assets/Scripts/States/Character/CharacterManager.cs:111 `characterNetworkManager.OnIsActiveChanged`.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/FogWallInteractable.cs:23; Assets/Scripts/FogWallInteractable.cs:24; Assets/Scripts/FogWallInteractable.cs:32; Assets/Scripts/FogWallInteractable.cs:38; Assets/Scripts/States/Character/CharacterManager.cs:108; Assets/Scripts/States/Character/CharacterManager.cs:111; Assets/Scripts/States/Character/CharacterManager.cs:119; Assets/Scripts/States/Character/CharacterNetworkManager.cs:162; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:105; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:114; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:117; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:162; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:229.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:18 — `networkPosition: Vector3`

- Writers: Assets/Scripts/States/Character/CharacterManager.cs:63; Assets/DevTools/Runtime/DevPlaygroundController.cs:456.
- Subscribers: none found.
- Replacement/fixture: replication mirror; preserve local locomotion/Transform and QA teleport, then discard network mirror; movement/root-motion/teleport regression.
- Access evidence: Assets/Scripts/States/Character/CharacterManager.cs:63; Assets/Scripts/States/Character/CharacterManager.cs:72; Assets/Scripts/States/AI/AIChar/AICharacterAnimatorManager.cs:41; Assets/DevTools/Runtime/DevPlaygroundController.cs:456.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:19 — `networkRotation: Quaternion`

- Writers: Assets/Scripts/States/Character/CharacterManager.cs:64; Assets/DevTools/Runtime/DevPlaygroundController.cs:457.
- Subscribers: none found.
- Replacement/fixture: replication mirror; preserve local locomotion/Transform and QA teleport, then discard network mirror; movement/root-motion/teleport regression.
- Access evidence: Assets/Scripts/States/Character/CharacterManager.cs:64; Assets/Scripts/States/Character/CharacterManager.cs:78; Assets/DevTools/Runtime/DevPlaygroundController.cs:457.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:24 — `isMoving: bool`

- Writers: Assets/Scripts/States/Character/Player/PlayerInputManager.cs:310; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:338; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:342; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:424; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:136; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:165; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:169; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:174.
- Subscribers: Assets/Scripts/States/Character/CharacterManager.cs:110 `characterNetworkManager.OnIsMovingChanged`.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/States/Character/CharacterManager.cs:107; Assets/Scripts/States/Character/CharacterManager.cs:110; Assets/Scripts/States/Character/CharacterManager.cs:118; Assets/Scripts/States/Character/CharacterNetworkManager.cs:157; Assets/Scripts/States/State/CombatStanceState.cs:39; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:310; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:338; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:342; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:424; Assets/Scripts/States/AI/AIChar/AICharacterCombatManager.cs:131; Assets/Scripts/States/AI/AIChar/AICharacterLocomotionManager.cs:11; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:136; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:165; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:169; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:174.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:25 — `horizontalMovement: float`

- Writers: Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:66.
- Subscribers: none found.
- Replacement/fixture: replication mirror; preserve local locomotion/Transform and QA teleport, then discard network mirror; movement/root-motion/teleport regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:66; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:72.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:26 — `verticalMovement: float`

- Writers: Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:65.
- Subscribers: none found.
- Replacement/fixture: replication mirror; preserve local locomotion/Transform and QA teleport, then discard network mirror; movement/root-motion/teleport regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:65; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:71.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:27 — `moveAmount: float`

- Writers: Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:67.
- Subscribers: none found.
- Replacement/fixture: replication mirror; preserve local locomotion/Transform and QA teleport, then discard network mirror; movement/root-motion/teleport regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:67; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:73.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:30 — `currentTargetNetworkObjectID: ulong`

- Writers: Assets/Scripts/States/Character/CharacterCombatManeger.cs:40.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:113 `playerNetworkManager.OnLockOnTargetIDChange`.
- Replacement/fixture: local target + registry invalidation; TargetClearsOnUnregister/RegistryDoesNotReturnInactiveCharacter.
- Access evidence: Assets/Scripts/States/Character/CharacterCombatManeger.cs:40; Assets/Scripts/States/Character/Player/PlayerManager.cs:113; Assets/Scripts/States/Character/Player/PlayerManager.cs:171; Assets/Scripts/States/Character/Player/PlayerManager.cs:304.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:33 — `isBlocking: bool`

- Writers: Assets/Scripts/SkillTree/SkillEscudo/SkillEscudo.cs:9; Assets/Scripts/SkillTree/SkillEscudo/SkillEscudo.cs:16.
- Subscribers: none found.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/States/Colliders/DamageCollider.cs:53; Assets/Scripts/States/Colliders/MeleeWeaponDamageCollider.cs:67; Assets/Scripts/SkillTree/SkillEscudo/SkillEscudo.cs:9; Assets/Scripts/SkillTree/SkillEscudo/SkillEscudo.cs:16.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:34 — `isPowerPet: bool`

- Writers: Assets/Scripts/SkillTree/SkillSpirit/PetAttackController.cs:32; Assets/Scripts/SkillTree/SkillSpirit/PetAttackController.cs:39; Assets/DevTools/Runtime/DevPlaygroundController.cs:85.
- Subscribers: none found.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/States/Colliders/MeleeWeaponDamageCollider.cs:157; Assets/Scripts/SkillTree/SkillSpirit/PetAttackController.cs:32; Assets/Scripts/SkillTree/SkillSpirit/PetAttackController.cs:39; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:431; Assets/DevTools/Runtime/DevPlaygroundController.cs:85.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:35 — `isInvulnerable: bool`

- Writers: Assets/Scripts/ResetActionFlag.cs:35; Assets/Scripts/States/Character/CharacterCombatManeger.cs:52; Assets/Scripts/States/Character/CharacterCombatManeger.cs:58; Assets/DevTools/Runtime/DevPlaygroundController.cs:68; Assets/DevTools/Runtime/DevPlaygroundController.cs:86; Assets/DevTools/Runtime/DevPlaygroundController.cs:394; Assets/DevTools/Runtime/DevPlaygroundController.cs:620.
- Subscribers: none found.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/ResetActionFlag.cs:35; Assets/Scripts/States/Character/CharacterCombatManeger.cs:52; Assets/Scripts/States/Character/CharacterCombatManeger.cs:58; Assets/Scripts/States/Character/CharacterNetworkManager.cs:278; Assets/Scripts/States/Colliders/TakeBlockedDamageEffect.cs:32; Assets/Scripts/States/Effects/TakeDamageEffect.cs:46; Assets/Scripts/Item/Spell/FireBallDamageCollider.cs:38; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:88; Assets/DevTools/Editor/PlaygroundUIRegression.cs:57; Assets/DevTools/Runtime/DevPlaygroundController.cs:68; Assets/DevTools/Runtime/DevPlaygroundController.cs:86; Assets/DevTools/Runtime/DevPlaygroundController.cs:394; Assets/DevTools/Runtime/DevPlaygroundController.cs:620.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:36 — `isLockedOn: bool`

- Writers: Assets/Scripts/States/Character/Player/PlayerCamera.cs:350; Assets/Scripts/States/Character/Player/PlayerCamera.cs:386; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:233; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:250; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:266; Assets/DevTools/Runtime/DevPlaygroundController.cs:502.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:112 `playerNetworkManager.OnIsLockedOnChanged`.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerCamera.cs:131; Assets/Scripts/States/Character/Player/PlayerCamera.cs:323; Assets/Scripts/States/Character/Player/PlayerCamera.cs:350; Assets/Scripts/States/Character/Player/PlayerCamera.cs:386; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:226; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:233; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:246; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:250; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:255; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:266; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:277; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:292; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:347; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:75; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:215; Assets/Scripts/States/Character/Player/PlayerManager.cs:112; Assets/Scripts/States/Character/Player/PlayerManager.cs:170; Assets/Scripts/States/Character/Player/PlayerManager.cs:302; Assets/Scripts/Item/Spell/FireBallSpell.cs:108; Assets/Scripts/Item/Spell/FireBallSpell.cs:208; Assets/DevTools/Runtime/DevPlaygroundController.cs:502.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:37 — `isSprinting: bool`

- Writers: Assets/Scripts/States/Character/Player/PlayerInputManager.cs:395; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:282; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:287; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:292.
- Subscribers: none found.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/LightAttackWeaponItemAction.cs:37; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:347; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:349; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:353; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:395; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:75; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:77; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:81; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:90; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:95; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:133; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:217; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:282; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:287; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:292; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:417.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:38 — `isJumping: bool`

- Writers: Assets/Scripts/ResetActionFlag.cs:34; Assets/Scripts/ResetIsJumping.cs:20; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:407.
- Subscribers: none found.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/ResetActionFlag.cs:34; Assets/Scripts/ResetIsJumping.cs:20; Assets/Scripts/Item/TestSpell.cs:48; Assets/Scripts/States/Character/CharacterLocomotionManager.cs:50; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:544; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:154; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:399; Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs:407; Assets/Scripts/Item/Spell/FireBallSpell.cs:230.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:39 — `isChargingAttack: bool`

- Writers: none found.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:130 `playerNetworkManager.OnIsChargingAttackChanged`.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/States/Character/CharacterNetworkManager.cs:149; Assets/Scripts/States/Character/Player/PlayerManager.cs:130; Assets/Scripts/States/Character/Player/PlayerManager.cs:179.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:40 — `isAttacking: bool`

- Writers: Assets/Scripts/HeavyAttackWeaponItemAction.cs:23; Assets/Scripts/LightAttackWeaponItemAction.cs:34; Assets/Scripts/ResetActionFlag.cs:36; Assets/Scripts/Item/CastIncantationAction.cs:29.
- Subscribers: none found.
- Replacement/fixture: gameplay state / local typed setters/events; player/world regression.
- Access evidence: Assets/Scripts/HeavyAttackWeaponItemAction.cs:23; Assets/Scripts/LightAttackWeaponItemAction.cs:34; Assets/Scripts/ResetActionFlag.cs:36; Assets/Scripts/Item/CastIncantationAction.cs:29.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:43 — `currentHealth: int`

- Writers: Assets/Scripts/SiteOfGraceInteractable.cs:183; Assets/Scripts/States/Character/CharacterManager.cs:127; Assets/Scripts/States/Character/CharacterNetworkManager.cs:81; Assets/Scripts/States/Character/CharacterNetworkManager.cs:91; Assets/Scripts/States/Character/CharacterNetworkManager.cs:94; Assets/Scripts/States/Character/CharacterNetworkManager.cs:129; Assets/Scripts/States/Colliders/TakeBlockedDamageEffect.cs:85; Assets/Scripts/States/Effects/TakeDamageEffect.cs:89; Assets/Scripts/States/Character/Player/PlayerManager.cs:245; Assets/Scripts/States/Character/Player/PlayerManager.cs:285; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:48; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:156; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:17; Assets/Scripts/SkillTree/SkillHealCircle/HealCircle.cs:46; Assets/DevTools/Editor/PlaygroundUIRegression.cs:93; Assets/DevTools/Runtime/DevPlaygroundController.cs:429; Assets/DevTools/Runtime/DevPlaygroundController.cs:713.
- Subscribers: Assets/Scripts/States/UI/UI_Boss_HP_Bar.cs:21 `OnBossHPChanged`; Assets/Scripts/States/Character/Player/PlayerManager.cs:99 `PlayerUIManager.instance.playerUIHudManager.SetNewHealthValue`; Assets/Scripts/States/Character/Player/PlayerManager.cs:106 `characterUIManager.OnHPChanged`; Assets/Scripts/States/Character/Player/PlayerManager.cs:109 `playerNetworkManager.CheckHP`; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:75 `aiCharacterNetworkManager.CheckHP`; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:102 `characterUIManager.OnHPChanged`.
- Replacement/fixture: gameplay health/stats/death local state; clamp/change-once/idempotence + HUD/revive/G1A regression.
- Access evidence: Assets/Scripts/SiteOfGraceInteractable.cs:183; Assets/Scripts/States/Character/CharacterManager.cs:127; Assets/Scripts/States/Character/CharacterNetworkManager.cs:59; Assets/Scripts/States/Character/CharacterNetworkManager.cs:79; Assets/Scripts/States/Character/CharacterNetworkManager.cs:81; Assets/Scripts/States/Character/CharacterNetworkManager.cs:91; Assets/Scripts/States/Character/CharacterNetworkManager.cs:92; Assets/Scripts/States/Character/CharacterNetworkManager.cs:94; Assets/Scripts/States/Character/CharacterNetworkManager.cs:96; Assets/Scripts/States/Character/CharacterNetworkManager.cs:129; Assets/Scripts/States/Colliders/TakeBlockedDamageEffect.cs:85; Assets/Scripts/States/Effects/TakeDamageEffect.cs:89; Assets/Scripts/States/UI/UI_Boss_HP_Bar.cs:21; Assets/Scripts/States/UI/UI_Boss_HP_Bar.cs:24; Assets/Scripts/States/UI/UI_Boss_HP_Bar.cs:43; Assets/Scripts/States/UI/UI_Character_HP_Bar.cs:76; Assets/Scripts/States/Character/Player/PlayerManager.cs:99; Assets/Scripts/States/Character/Player/PlayerManager.cs:106; Assets/Scripts/States/Character/Player/PlayerManager.cs:109; Assets/Scripts/States/Character/Player/PlayerManager.cs:156; Assets/Scripts/States/Character/Player/PlayerManager.cs:162; Assets/Scripts/States/Character/Player/PlayerManager.cs:165; Assets/Scripts/States/Character/Player/PlayerManager.cs:245; Assets/Scripts/States/Character/Player/PlayerManager.cs:263; Assets/Scripts/States/Character/Player/PlayerManager.cs:285; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:48; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:156; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:17; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:38; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:47; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:75; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:82; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:102; Assets/Scripts/States/AI/AIChar/AICharacterManager.cs:110; Assets/Scripts/SkillTree/SkillHealCircle/HealCircle.cs:46; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:113; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:117; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:123; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:126; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:131; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:141; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:148; Assets/DevTools/Editor/PlaygroundUIRegression.cs:93; Assets/DevTools/Runtime/DevPlaygroundController.cs:395; Assets/DevTools/Runtime/DevPlaygroundController.cs:429; Assets/DevTools/Runtime/DevPlaygroundController.cs:618; Assets/DevTools/Runtime/DevPlaygroundController.cs:713.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:44 — `maxHealth: int`

- Writers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:105; Assets/Scripts/States/Character/CharacterNetworkManager.cs:124; Assets/Scripts/States/Character/Player/PlayerManager.cs:283; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:46; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:18.
- Subscribers: none found.
- Replacement/fixture: gameplay health/stats/death local state; clamp/change-once/idempotence + HUD/revive/G1A regression.
- Access evidence: Assets/Scripts/SiteOfGraceInteractable.cs:183; Assets/Scripts/States/Character/CharacterNetworkManager.cs:79; Assets/Scripts/States/Character/CharacterNetworkManager.cs:81; Assets/Scripts/States/Character/CharacterNetworkManager.cs:92; Assets/Scripts/States/Character/CharacterNetworkManager.cs:94; Assets/Scripts/States/Character/CharacterNetworkManager.cs:105; Assets/Scripts/States/Character/CharacterNetworkManager.cs:106; Assets/Scripts/States/Character/CharacterNetworkManager.cs:124; Assets/Scripts/States/UI/UI_Boss_HP_Bar.cs:23; Assets/Scripts/States/UI/UI_Character_HP_Bar.cs:57; Assets/Scripts/States/UI/UI_Character_HP_Bar.cs:76; Assets/Scripts/States/Character/Player/PlayerManager.cs:245; Assets/Scripts/States/Character/Player/PlayerManager.cs:283; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:46; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:47; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:48; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:18; Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs:45; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:131; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:138; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:148; Assets/DevTools/Runtime/DevPlaygroundController.cs:395; Assets/DevTools/Runtime/DevPlaygroundController.cs:429; Assets/DevTools/Runtime/DevPlaygroundController.cs:618.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:49 — `vitality: int`

- Writers: Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs:167; Assets/Scripts/States/Character/Player/PlayerManager.cs:279; Assets/DevTools/Runtime/DevPlaygroundController.cs:435.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:95 `playerNetworkManager.SetNewMaxHealthValue`.
- Replacement/fixture: gameplay health/stats/death local state; clamp/change-once/idempotence + HUD/revive/G1A regression.
- Access evidence: Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs:167; Assets/Scripts/States/Character/Player/PlayerManager.cs:95; Assets/Scripts/States/Character/Player/PlayerManager.cs:152; Assets/Scripts/States/Character/Player/PlayerManager.cs:266; Assets/Scripts/States/Character/Player/PlayerManager.cs:279; Assets/Scripts/States/Character/Player/PlayerManager.cs:283; Assets/Scripts/States/Character/Player/PlayerStatsManager.cs:25; Assets/DevTools/Runtime/DevPlaygroundController.cs:435; Assets/DevTools/Runtime/DevPlaygroundController.cs:437.

### Assets/Scripts/States/Character/CharacterNetworkManager.cs:50 — `endurance: int`

- Writers: Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs:168; Assets/Scripts/States/Character/Player/PlayerManager.cs:280; Assets/DevTools/Runtime/DevPlaygroundController.cs:436.
- Subscribers: none found.
- Replacement/fixture: gameplay health/stats/death local state; clamp/change-once/idempotence + HUD/revive/G1A regression.
- Access evidence: Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs:168; Assets/Scripts/States/Character/Player/PlayerManager.cs:267; Assets/Scripts/States/Character/Player/PlayerManager.cs:280; Assets/DevTools/Runtime/DevPlaygroundController.cs:436.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:11 — `characterName: FixedString64Bytes`

- Writers: Assets/Scripts/States/Character/Player/PlayerManager.cs:275.
- Subscribers: none found.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/UI/UI_Character_HP_Bar.cs:53; Assets/Scripts/States/Character/Player/PlayerManager.cs:258; Assets/Scripts/States/Character/Player/PlayerManager.cs:275.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:14 — `currentSpellID: int`

- Writers: Assets/Scripts/States/Character/Player/PlayerManager.cs:127; Assets/DevTools/Editor/PlaygroundInputRegression.cs:208; Assets/DevTools/Runtime/DevPlaygroundController.cs:590.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:121 `playerNetworkManager.OnCurrentSpellIDChange`.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerManager.cs:121; Assets/Scripts/States/Character/Player/PlayerManager.cs:127; Assets/Scripts/States/Character/Player/PlayerManager.cs:177; Assets/Scripts/States/Character/Player/PlayerManager.cs:300; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:103; Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs:297; Assets/DevTools/Editor/PlaygroundInputRegression.cs:67; Assets/DevTools/Editor/PlaygroundInputRegression.cs:208; Assets/DevTools/Runtime/DevPlaygroundController.cs:588; Assets/DevTools/Runtime/DevPlaygroundController.cs:590.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:15 — `currentWeaponBeingUsed: int`

- Writers: Assets/Scripts/WeaponItemAction.cs:16.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:120 `playerNetworkManager.OnCurrentWeaponBeingUsedIDChange`.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/WeaponItemAction.cs:16; Assets/Scripts/States/Character/Player/PlayerManager.cs:120; Assets/Scripts/States/Character/Player/PlayerManager.cs:176.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:16 — `currentRightHandWeaponID: int`

- Writers: Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:106; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:111; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:127; Assets/DevTools/Editor/PlaygroundInputRegression.cs:207; Assets/DevTools/Runtime/DevPlaygroundController.cs:578.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:118 `playerNetworkManager.OnCurrentRightHandWeaponIDChange`.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:106; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:111; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:127; Assets/Scripts/States/Character/Player/PlayerManager.cs:118; Assets/Scripts/States/Character/Player/PlayerManager.cs:174; Assets/Scripts/States/Character/Player/PlayerManager.cs:296; Assets/DevTools/Editor/PlaygroundInputRegression.cs:66; Assets/DevTools/Editor/PlaygroundInputRegression.cs:207; Assets/DevTools/Runtime/DevPlaygroundController.cs:576; Assets/DevTools/Runtime/DevPlaygroundController.cs:578.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:17 — `currentLeftHandWeaponID: int`

- Writers: Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:207; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:212; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:225.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:119 `playerNetworkManager.OnCurrentLeftHandWeaponIDChange`.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:207; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:212; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:225; Assets/Scripts/States/Character/Player/PlayerManager.cs:119; Assets/Scripts/States/Character/Player/PlayerManager.cs:175; Assets/Scripts/States/Character/Player/PlayerManager.cs:297.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:18 — `isUsingRightHand: bool`

- Writers: Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:35; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:39.
- Subscribers: none found.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/Item/TestSpell.cs:17; Assets/Scripts/States/Character/Player/PlayerCombatManager.cs:91; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:255; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:273; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:35; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:39; Assets/Scripts/Item/Spell/FireBallSpell.cs:20; Assets/Scripts/Item/Spell/FireBallSpell.cs:38; Assets/Scripts/Item/Spell/FireBallSpell.cs:69; Assets/Scripts/Item/Spell/FireBallSpell.cs:137; Assets/Scripts/Item/Spell/FireBallSpell.cs:168.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:19 — `isUsingLeftHand: bool`

- Writers: Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:34; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:40.
- Subscribers: none found.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:261; Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs:278; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:34; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:40.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:21 — `isChargingRightSpell: bool`

- Writers: Assets/Scripts/States/Character/Player/PlayerInputManager.cs:404; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:408.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:116 `playerNetworkManager.OnIsChargingRightSpellChanged`.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerInputManager.cs:404; Assets/Scripts/States/Character/Player/PlayerInputManager.cs:408; Assets/Scripts/States/Character/Player/PlayerManager.cs:116; Assets/Scripts/States/Character/Player/PlayerManager.cs:168.

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:22 — `isChargingLeftSpell: bool`

- Writers: none found.
- Subscribers: Assets/Scripts/States/Character/Player/PlayerManager.cs:115 `playerNetworkManager.OnIsChargingLeftSpellChanged`.
- Replacement/fixture: gameplay/presentation local equipment/string/hand/charge; first action/equip/same-ID/charge regression.
- Access evidence: Assets/Scripts/States/Character/Player/PlayerManager.cs:115; Assets/Scripts/States/Character/Player/PlayerManager.cs:167; Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:116.

### Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:18 — `bossFightIsActive: bool`

- Writers: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:158; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:213.
- Subscribers: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:73 `OnBossFightIsActiveChanged`.
- Replacement/fixture: world progress/presentation local events; boss/music/fog/checkpoint/save/unload regression.
- Access evidence: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:73; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:74; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:131; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:158; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:213; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:236.

### Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:19 — `hasBeenAwakened: bool`

- Writers: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:94; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:214.
- Subscribers: none found.
- Replacement/fixture: world progress/presentation local events; boss/music/fog/checkpoint/save/unload regression.
- Access evidence: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:94; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:101; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:121; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:208; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:214.

### Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:20 — `hasBeenDefeated: bool`

- Writers: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:93; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:175.
- Subscribers: none found.
- Replacement/fixture: world progress/presentation local events; boss/music/fog/checkpoint/save/unload regression.
- Access evidence: Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:93; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:110; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:175; Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs:258.

## RPC methods and callers

- `ServerRpc AddHealthServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:86); active C# callers: none; check serialized bindings before removal.
- `ServerRpc AddMaxHealthServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:100); active C# callers: none; check serialized bindings before removal.
- `ServerRpc IncreaseSprintingSpeedServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:109); active C# callers: none; check serialized bindings before removal.
- `ClientRpc IncreaseSprintingSpeedClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:115); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:112.
- `ClientRpc NotifyClientsOfMaxHealthChangeClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:121); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:106.
- `ClientRpc NotifyClientsOfHealthChangeClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:126); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:96.
- `ServerRpc NotifyTheServerOfActionAnimationServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:166); active C# callers: Assets/Scripts/States/Character/CharacterAnimatorManager.cs:189.
- `ClientRpc PlayActionAnimationForAllClientsClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:177); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:172.
- `ServerRpc NotifyTheServerOfAttackActionAnimationServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:195); active C# callers: Assets/Scripts/States/Character/CharacterAnimatorManager.cs:216.
- `ClientRpc PlayAttackActionAnimationForAllClientsClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:205); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:201.
- `ServerRpc NotifyTheServerOfCharacterDamageServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:223); active C# callers: Assets/Scripts/States/Colliders/DurkClubDamageCollider.cs:70; Assets/Scripts/States/Colliders/MeleeWeaponDamageCollider.cs:132; Assets/Scripts/States/Colliders/UndeadHandDamageCollider.cs:70; Assets/Scripts/Item/Spell/FireBallDamageCollider.cs:71.
- `ClientRpc NotifyTheServerOfCharacterDamageClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:243); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:239.
- `ServerRpc DestroyAllCurrentActionFXServerRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:299); active C# callers: Assets/Scripts/States/Character/CharacterCombatManeger.cs:93.
- `ClientRpc DestroyAllCurrentActionFXClientRpc` (Assets/Scripts/States/Character/CharacterNetworkManager.cs:307); active C# callers: Assets/Scripts/States/Character/CharacterNetworkManager.cs:304.
- `ServerRpc NotifyTheServerOfWeaponActionServerRpc` (Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:93); active C# callers: none; check serialized bindings before removal.
- `ClientRpc NotifyTheServerOfWeaponActionClientRpc` (Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:102); active C# callers: Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs:98.

RPC declarations: **16**. Gameplay side effects and broadcast-only distinctions are described above.

## Source dependency inventory (actual active match lines)


### Assets/DevTools/Editor/DevPlaygroundBuilder.cs

```csharp
92: catalog.networkManagerPrefab = RequiredPrefab("Assets/Data/Prefabs/World Managers/World NetworkManager.prefab");
```

### Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs

```csharp
88: originalGodMode = controller.Player.characterNetworkManager.isInvulnerable.Value;
93: foreach (Type type in new[] { typeof(Unity.Netcode.NetworkManager), typeof(WorldItemDatabase), typeof(WorldUtilityManager),
98: Require(controller.Network.IsHost && controller.Network.IsServer && controller.Network.IsListening, "Local NGO host not listening.");
99: var transport = controller.Network.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
101: Require(controller.Player.IsOwner && controller.Player.NetworkObject.IsSpawned, "Production player not owned/spawned.");
103: Require(initialSpell != null && controller.Player.playerNetworkManager.currentSpellID.Value == initialSpell.itemID,
113: controller.SetGodMode(false); controller.Heal(); healthBefore = controller.Player.playerNetworkManager.currentHealth.Value;
117: int health = controller.Player.playerNetworkManager.currentHealth.Value;
123: controller.SetGodMode(true); healthBefore = controller.Player.playerNetworkManager.currentHealth.Value; Damage(7);
126: Require(controller.Player.playerNetworkManager.currentHealth.Value == healthBefore, "Invulnerable player health changed.");
131: Require(controller.Player.playerNetworkManager.currentHealth.Value == controller.Player.playerNetworkManager.maxHealth.Value,
138: Damage(controller.Player.playerNetworkManager.maxHealth.Value + 1);
141: Require(controller.Player.playerNetworkManager.currentHealth.Value <= 0 && controller.Player.isDead.Value,
148: Require(controller.Player.playerNetworkManager.currentHealth.Value == controller.Player.playerNetworkManager.maxHealth.Value,
297: Require(controller.Player.playerNetworkManager.currentSpellID.Value == equipped.itemID,
421: Require(controller.Network.IsHost && controller.Player.IsOwner, "Preview disturbed host/player ownership.");
431: Require(!controller.Player.playerNetworkManager.isPowerPet.Value, "Spirit power remained active after clearing tool effects.");
```

### Assets/DevTools/Editor/PlaygroundInputRegression.cs

```csharp
66: previousWeaponID = controller.Player.playerNetworkManager.currentRightHandWeaponID.Value;
67: previousSpellID = controller.Player.playerNetworkManager.currentSpellID.Value;
207: player.playerNetworkManager.currentRightHandWeaponID.Value = previousWeaponID;
208: player.playerNetworkManager.currentSpellID.Value = previousSpellID;
```

### Assets/DevTools/Editor/PlaygroundUIRegression.cs

```csharp
57: oldGod = controller.Player.characterNetworkManager.isInvulnerable.Value;
89: boss = Object.FindObjectsByType<AIBossCharacterManager>(FindObjectsInactive.Exclude).Single(b => b.IsSpawned);
93: boss.characterNetworkManager.currentHealth.Value = 0;
```

### Assets/DevTools/Editor/ProductionSceneSmokeTest.cs

```csharp
7: using Unity.Netcode;
101: var network = NetworkManager.Singleton;
102: if (network == null) throw new InvalidOperationException("SceneMenu has no NetworkManager singleton.");
106: var transport = network.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
111: else if (!network.IsHost) throw new InvalidOperationException("Existing network session is not a host.");
129: var network = NetworkManager.Singleton;
131: if (network != null && network.IsHost && player != null && player.IsSpawned && player.IsOwner)
241: var network = NetworkManager.Singleton;
244: return UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOwner);
262: phase.hostListening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsListening;
265: phase.playerOwner = player != null && player.IsOwner;
266: phase.playerSpawned = player != null && player.IsSpawned;
```

### Assets/DevTools/Runtime/DevPlaygroundController.cs

```csharp
6: using Unity.Netcode;
32: NetworkManager network;
61: public NetworkManager Network => network;
68: public void SetGodMode(bool value) { godMode = value; if (player != null) player.characterNetworkManager.isInvulnerable.Value = value; }
85: player.playerNetworkManager.isPowerPet.Value = false;
86: player.characterNetworkManager.isInvulnerable.Value = godMode;
106: if (NetworkManager.Singleton != null || PlayerManagerExists())
152: network = NetworkManager.Singleton;
154: { Fail("Production NetworkManager or player prefab is missing."); yield break; }
158: var transport = network.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
163: if (entry.prefab != null && entry.prefab.GetComponent<NetworkObject>() != null && registered.Add(entry.prefab))
167: if (!LocalHostStartup.TryStartHost(network, out ushort hostPort)) { Fail("Local host failed to start."); yield break; }
394: if (godMode) player.characterNetworkManager.isInvulnerable.Value = true;
395: if (infiniteHealth && player.playerNetworkManager.currentHealth.Value < player.playerNetworkManager.maxHealth.Value) Heal();
429: player.playerNetworkManager.currentHealth.Value = player.playerNetworkManager.maxHealth.Value;
435: if (int.TryParse(vitality, out int healthStat)) player.playerNetworkManager.vitality.Value = Mathf.Clamp(healthStat, 1, 10000);
436: if (int.TryParse(endurance, out int enduranceStat)) player.playerNetworkManager.endurance.Value = Mathf.Clamp(enduranceStat, 1, 10000);
437: player.playerNetworkManager.SetNewMaxHealthValue(0, player.playerNetworkManager.vitality.Value);
456: player.characterNetworkManager.networkPosition.Value = target.position;
457: player.characterNetworkManager.networkRotation.Value = target.rotation;
463: if (network == null || !network.IsServer || catalog.enemies.Length == 0) return;
468: if (networkCharacter && entry.prefab.GetComponent<NetworkObject>() == null)
469: { status = "Network enemy is missing its NetworkObject."; return; }
489: if (networkCharacter) enemy.GetComponent<NetworkObject>().Spawn();
501: if (network == null || !network.IsServer) return;
502: if (player != null) { player.playerNetworkManager.isLockedOn.Value = false; PlayerCamera.instance.ClearLockOnTargets(); }
519: var obj = enemy.GetComponent<NetworkObject>();
520: if (obj != null && obj.IsSpawned) obj.Despawn(); else Destroy(enemy);
576: if (player.playerNetworkManager.currentRightHandWeaponID.Value == weapon.itemID)
577: player.playerNetworkManager.OnCurrentRightHandWeaponIDChange(weapon.itemID, weapon.itemID);
578: else player.playerNetworkManager.currentRightHandWeaponID.Value = weapon.itemID;
588: if (player.playerNetworkManager.currentSpellID.Value == spell.itemID)
589: player.playerNetworkManager.OnCurrentSpellIDChange(spell.itemID, spell.itemID);
590: else player.playerNetworkManager.currentSpellID.Value = spell.itemID;
618: GUILayout.Label($"Health {player.playerNetworkManager.currentHealth.Value}/{player.playerNetworkManager.maxHealth.Value}");
620: if (nextGodMode != godMode) { godMode = nextGodMode; player.characterNetworkManager.isInvulnerable.Value = godMode; }
688: GUILayout.Label($"Spawned: {spawnedEnemies.Count} · alive: {living} · server: {network.IsServer}");
713: if (character != null) character.characterNetworkManager.currentHealth.Value = 0;
789: if (weapon != null) { player.playerNetworkManager.SetCharacterActionHand(true); player.playerCombatManager.PerformWeaponBasedAction(weapon.oh_RB_Action, weapon); }
795: GUILayout.Label($"NGO host {network.IsHost}; player owner {player.IsOwner}; NavMesh valid {navMeshInstance.valid}");
803: if (network != null && network.IsListening) network.Shutdown();
```

### Assets/Scripts/BossSpawnerInput.cs

```csharp
4: using Unity.Netcode;
27: NetworkObject networkObject = instantiatedBoss.GetComponent<NetworkObject>();
```

### Assets/Scripts/FogWallInteractable.cs

```csharp
4: using Unity.Netcode;
8: public class FogWallInteractable : NetworkBehaviour
17: public NetworkVariable<bool> isActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
19: public override void OnNetworkSpawn()
21: base.OnNetworkSpawn();
28: public override void OnNetworkDespawn()
30: base.OnNetworkDespawn();
```

### Assets/Scripts/HeavyAttackWeaponItemAction.cs

```csharp
15: if (!playerPerformingAction.IsOwner)
22: if (playerPerformingAction.IsOwner)
23: playerPerformingAction.playerNetworkManager.isAttacking.Value = true;
```

### Assets/Scripts/Interactable.cs

```csharp
5: using Unity.Netcode;
11: public class Interactable : NetworkBehaviour
42: if (!player.IsOwner)
93: if (!player.playerNetworkManager.IsHost && hostOnlyInteractable)
96: if (!player.IsOwner)
109: if (!player.playerNetworkManager.IsHost && hostOnlyInteractable)
112: if (!player.IsOwner)
```

### Assets/Scripts/Item/CastIncantationAction.cs

```csharp
14: if (!playerPerformingAction.IsOwner)
28: if (playerPerformingAction.IsOwner)
29: playerPerformingAction.playerNetworkManager.isAttacking.Value = true;
```

### Assets/Scripts/Item/Spell/FireBallDamageCollider.cs

```csharp
38: if (!damageTarget.characterNetworkManager.isInvulnerable.Value)
69: if (spellCaster.IsOwner)
71: damageTarget.characterNetworkManager.NotifyTheServerOfCharacterDamageServerRpc(
72: damageTarget.NetworkObjectId,
73: spellCaster.NetworkObjectId,
```

### Assets/Scripts/Item/Spell/FireBallSpell.cs

```csharp
20: if (player.playerNetworkManager.isUsingRightHand.Value)
38: if (player.playerNetworkManager.isUsingRightHand.Value)
62: if (player.IsOwner)
69: if (player.playerNetworkManager.isUsingRightHand.Value)
108: if (player.playerNetworkManager.isLockedOn.Value)
130: if (player.IsOwner)
137: if (player.playerNetworkManager.isUsingRightHand.Value)
161: if (player.IsOwner)
168: if (player.playerNetworkManager.isUsingRightHand.Value)
208: if (player.playerNetworkManager.isLockedOn.Value)
230: if (player.playerNetworkManager.isJumping.Value)
```

### Assets/Scripts/Item/Spell/TesteSpellManager.cs

```csharp
35: inventory != null && combatManager != null && combatManager.player.IsOwner &&
```

### Assets/Scripts/Item/TestSpell.cs

```csharp
17: if (player.playerNetworkManager.isUsingRightHand.Value)
48: if (player.playerNetworkManager.isJumping.Value)
```

### Assets/Scripts/LightAttackWeaponItemAction.cs

```csharp
27: if (!playerPerformingAction.IsOwner)
33: if (playerPerformingAction.IsOwner)
34: playerPerformingAction.playerNetworkManager.isAttacking.Value = true;
37: if (playerPerformingAction.characterNetworkManager.isSprinting.Value)
```

### Assets/Scripts/LocalHostStartup.cs

```csharp
4: using Unity.Netcode;
5: using Unity.Netcode.Transports.UTP;
13: public static bool TryStartHost(NetworkManager network, out ushort activePort)
21: return network.IsHost;
24: return network.StartHost();
32: if (!network.StartHost())
```

### Assets/Scripts/NetworkObjectSpawner.cs

```csharp
4: using Unity.Netcode;
8: public class NetworkObjectSpawner : MonoBehaviour
32: instantiatedGameObject.GetComponent<NetworkObject>().Spawn();
```

### Assets/Scripts/ResetActionFlag.cs

```csharp
32: if (character.IsOwner)
34: character.characterNetworkManager.isJumping.Value = false;
35: character.characterNetworkManager.isInvulnerable.Value = false;
36: character.characterNetworkManager.isAttacking.Value = false;
```

### Assets/Scripts/ResetIsJumping.cs

```csharp
18: if (character.IsOwner)
20: character.characterNetworkManager.isJumping.Value = false;
```

### Assets/Scripts/SiteOfGraceInteractable.cs

```csharp
5: using Unity.Netcode;
17: public NetworkVariable<bool> isActivated = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
31: if (IsOwner)
53: public override void OnNetworkSpawn()
55: base.OnNetworkSpawn();
58: if (!IsOwner)
75: public override void OnNetworkDespawn()
77: base.OnNetworkDespawn();
183: player.playerNetworkManager.currentHealth.Value = player.playerNetworkManager.maxHealth.Value;
```

### Assets/Scripts/SkillTree/SkillEscudo/SkillEscudo.cs

```csharp
6: PlayerNetworkManager playerNetworkManager;
8: playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
9: playerNetworkManager.isBlocking.Value = true;
16: playerNetworkManager.isBlocking.Value = false;
```

### Assets/Scripts/SkillTree/SkillHealCircle/HealCircle.cs

```csharp
46: _playerManager.playerNetworkManager.currentHealth.Value += _healingAmount;
```

### Assets/Scripts/SkillTree/SkillSpirit/PetAttackController.cs

```csharp
11: private PlayerNetworkManager _playerNetworkManager;
31: _playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
32: _playerNetworkManager.isPowerPet.Value = true;
39: _playerNetworkManager.isPowerPet.Value = false;
```

### Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs

```csharp
4: using Unity.Netcode;
18: public NetworkVariable<bool> bossFightIsActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
19: public NetworkVariable<bool> hasBeenAwakened = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
20: public NetworkVariable<bool> hasBeenDefeated = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
69: public override void OnNetworkSpawn()
71: base.OnNetworkSpawn();
76: if (IsOwner)
82: if (IsServer)
117: aiCharacterNetworkManager.isActive.Value = false;
127: public override void OnNetworkDespawn()
129: base.OnNetworkDespawn();
154: if (IsOwner)
156: characterNetworkManager.currentHealth.Value = 0;
206: if (IsOwner)
```

### Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs

```csharp
7: public class AIBossCharacterNetworkManager : AICharacterNetworkManager
33: if (aiBossCharacter == null || !aiBossCharacter.IsSpawned) return;
36: if (aiBossCharacter.IsOwner)
```

### Assets/Scripts/States/AI/AIChar/AICharacterAnimatorManager.cs

```csharp
21: if (aiCharacter.IsOwner)
41: aiCharacter.characterNetworkManager.networkPosition.Value,
42: ref aiCharacter.characterNetworkManager.networkPositionVelocity,
43: aiCharacter.characterNetworkManager.networkPositionSmoothTime);
```

### Assets/Scripts/States/AI/AIChar/AICharacterCombatManager.cs

```csharp
131: if (aiCharacter.aiCharacterNetworkManager.isMoving.Value)
```

### Assets/Scripts/States/AI/AIChar/AICharacterLocomotionManager.cs

```csharp
11: if (aiCharacter.aiCharacterNetworkManager.isMoving.Value)
```

### Assets/Scripts/States/AI/AIChar/AICharacterManager.cs

```csharp
18: [HideInInspector] public AICharacterNetworkManager aiCharacterNetworkManager;
43: aiCharacterNetworkManager = GetComponent<AICharacterNetworkManager>();
62: public override void OnNetworkSpawn()
64: base.OnNetworkSpawn();
66: if (IsOwner)
75: aiCharacterNetworkManager.currentHealth.OnValueChanged += aiCharacterNetworkManager.CheckHP;
78: public override void OnNetworkDespawn()
80: base.OnNetworkDespawn();
82: aiCharacterNetworkManager.currentHealth.OnValueChanged -= aiCharacterNetworkManager.CheckHP;
102: characterNetworkManager.currentHealth.OnValueChanged += characterUIManager.OnHPChanged;
110: characterNetworkManager.currentHealth.OnValueChanged -= characterUIManager.OnHPChanged;
123: if (IsOwner)
136: aiCharacterNetworkManager.isMoving.Value = false;
165: aiCharacterNetworkManager.isMoving.Value = true;
169: aiCharacterNetworkManager.isMoving.Value = false;
174: aiCharacterNetworkManager.isMoving.Value = false;
```

### Assets/Scripts/States/AI/AIChar/AICharacterNetworkManager.cs

```csharp
5: using Unity.Netcode;
10: public class AICharacterNetworkManager : CharacterNetworkManager
```

### Assets/Scripts/States/AI/AIChar/AICharacterSpawner.cs

```csharp
4: using Unity.Netcode;
33: var AIBossCharacterManager = character.GetComponent<AIBossCharacterNetworkManager>();
48: instantiatedGameObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
```

### Assets/Scripts/States/Character/CharacterAnimatorManager.cs

```csharp
4: using Unity.Netcode;
189: character.characterNetworkManager.NotifyTheServerOfActionAnimationServerRpc(NetworkManager.Singleton.LocalClientId, targetAnimation, applyRootMotion);
216: character.characterNetworkManager.NotifyTheServerOfAttackActionAnimationServerRpc(NetworkManager.Singleton.LocalClientId, targetAnimation, applyRootMotion);
```

### Assets/Scripts/States/Character/CharacterCombatManeger.cs

```csharp
4: using Unity.Netcode;
8: public class CharacterCombatManeger : NetworkBehaviour
35: if (character.IsOwner)
40: character.characterNetworkManager.currentTargetNetworkObjectID.Value = newTarget.GetComponent<NetworkObject>().NetworkObjectId;
51: if (character.IsOwner)
52: character.characterNetworkManager.isInvulnerable.Value = true;
57: if (character.IsOwner)
58: character.characterNetworkManager.isInvulnerable.Value = false;
93: character.characterNetworkManager.DestroyAllCurrentActionFXServerRpc();
```

### Assets/Scripts/States/Character/CharacterLocomotionManager.cs

```csharp
50: if (!character.characterNetworkManager.isJumping.Value && !fallingVelocityHAsBeenSet)
```

### Assets/Scripts/States/Character/CharacterManager.cs

```csharp
4: using Unity.Netcode;
8: public class CharacterManager : NetworkBehaviour
12: public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
17: [HideInInspector] public CharacterNetworkManager characterNetworkManager;
41: characterNetworkManager = GetComponent<CharacterNetworkManager>();
61: if (IsOwner)
63: characterNetworkManager.networkPosition.Value = transform.position;
64: characterNetworkManager.networkRotation.Value = transform.rotation;
72: characterNetworkManager.networkPosition.Value,
73: ref characterNetworkManager.networkPositionVelocity,
74: characterNetworkManager.networkPositionSmoothTime);
78: characterNetworkManager.networkRotation.Value,
79: characterNetworkManager.networkRotationSmoothTime);
103: public override void OnNetworkSpawn()
105: base.OnNetworkSpawn();
107: animator.SetBool("isMoving", characterNetworkManager.isMoving.Value);
108: characterNetworkManager.OnIsActiveChanged(false, characterNetworkManager.isActive.Value);
110: characterNetworkManager.isMoving.OnValueChanged += characterNetworkManager.OnIsMovingChanged;
111: characterNetworkManager.isActive.OnValueChanged += characterNetworkManager.OnIsActiveChanged;
114: public override void OnNetworkDespawn()
116: base.OnNetworkDespawn();
118: characterNetworkManager.isMoving.OnValueChanged -= characterNetworkManager.OnIsMovingChanged;
119: characterNetworkManager.isActive.OnValueChanged -= characterNetworkManager.OnIsActiveChanged;
125: if (IsOwner)
127: characterNetworkManager.currentHealth.Value = 0;
```

### Assets/Scripts/States/Character/CharacterNetworkManager.cs

```csharp
5: using Unity.Netcode;
9: public class CharacterNetworkManager : NetworkBehaviour
14: public NetworkVariable<bool> isActive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
18: public NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
19: public NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
24: public NetworkVariable<bool> isMoving = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
25: public NetworkVariable<float> horizontalMovement = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
26: public NetworkVariable<float> verticalMovement = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
27: public NetworkVariable<float> moveAmount = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
30: public NetworkVariable<ulong> currentTargetNetworkObjectID = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
33: public NetworkVariable<bool> isBlocking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
34: public NetworkVariable<bool> isPowerPet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
35: public NetworkVariable<bool> isInvulnerable = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
36: public NetworkVariable<bool> isLockedOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
37: public NetworkVariable<bool> isSprinting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
38: public NetworkVariable<bool> isJumping = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
39: public NetworkVariable<bool> isChargingAttack = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
40: public NetworkVariable<bool> isAttacking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
43: public NetworkVariable<int> currentHealth = new NetworkVariable<int>(400, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
44: public NetworkVariable<int> maxHealth = new NetworkVariable<int>(400, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
49: public NetworkVariable<int> vitality = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
50: public NetworkVariable<int> endurance = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
77: if (character.IsOwner)
86: [ServerRpc]
87: public void AddHealthServerRpc(int amount)
89: if (IsServer)
96: NotifyClientsOfHealthChangeClientRpc(currentHealth.Value);
100: [ServerRpc]
101: public void AddMaxHealthServerRpc(int amount)
103: if (IsServer)
106: NotifyClientsOfMaxHealthChangeClientRpc(maxHealth.Value);
109: [ServerRpc]
110: public void IncreaseSprintingSpeedServerRpc(float amount)
112: IncreaseSprintingSpeedClientRpc(amount);
115: [ClientRpc]
116: private void IncreaseSprintingSpeedClientRpc(float amount)
121: [ClientRpc]
122: private void NotifyClientsOfMaxHealthChangeClientRpc(int newMaxHealth)
126: [ClientRpc]
127: private void NotifyClientsOfHealthChangeClientRpc(int newHealth)
133: if (!IsOwner)
135: character.characterCombatManager.currentTarget = NetworkManager.Singleton.SpawnManager.SpawnedObjects[newID].gameObject.GetComponent<CharacterManager>();
166: [ServerRpc]
167: public void NotifyTheServerOfActionAnimationServerRpc(ulong clientID, string animationID, bool applyRootMotion)
170: if (IsServer)
172: PlayActionAnimationForAllClientsClientRpc(clientID, animationID, applyRootMotion);
177: [ClientRpc]
178: public void PlayActionAnimationForAllClientsClientRpc(ulong clientID, string animationID, bool applyRootMotion)
181: if (clientID != NetworkManager.Singleton.LocalClientId)
195: [ServerRpc]
196: public void NotifyTheServerOfAttackActionAnimationServerRpc(ulong clientID, string animationID, bool applyRootMotion)
199: if (IsServer)
201: PlayAttackActionAnimationForAllClientsClientRpc(clientID, animationID, applyRootMotion);
205: [ClientRpc]
206: public void PlayAttackActionAnimationForAllClientsClientRpc(ulong clientID, string animationID, bool applyRootMotion)
209: if (clientID != NetworkManager.Singleton.LocalClientId)
223: [ServerRpc(RequireOwnership = false)]
224: public void NotifyTheServerOfCharacterDamageServerRpc(
237: if (IsServer)
239: NotifyTheServerOfCharacterDamageClientRpc(damagedCharacterID, characterCausingDamageID, physicalDamage, magicDamage, fireDamage, holyDamage, poiseDamage, angleHitFrom, contactPointX, contactPointY, contactPointZ);
243: [ClientRpc]
244: public void NotifyTheServerOfCharacterDamageClientRpc(
274: CharacterManager damagedCharacter = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damagedCharacterID].gameObject.GetComponent<CharacterManager>();
275: CharacterManager characterCausingDamage = NetworkManager.Singleton.SpawnManager.SpawnedObjects[characterCausingDamageID].gameObject.GetComponent<CharacterManager>();
278: if (damagedCharacter.characterNetworkManager.isInvulnerable.Value)
299: [ServerRpc]
300: public void DestroyAllCurrentActionFXServerRpc()
302: if (IsServer)
304: DestroyAllCurrentActionFXClientRpc();
307: [ClientRpc]
308: public void DestroyAllCurrentActionFXClientRpc()
```

### Assets/Scripts/States/Character/Player/PlayerCamera.cs

```csharp
131: if (player.playerNetworkManager.isLockedOn.Value)
323: if (player.playerNetworkManager.isLockedOn.Value)
350: player.playerNetworkManager.isLockedOn.Value = false;
386: player.playerNetworkManager.isLockedOn.Value = true;
```

### Assets/Scripts/States/Character/Player/PlayerCombatManager.cs

```csharp
4: using Unity.Netcode;
57: if (player.IsOwner){
82: if (player.IsOwner)
91: if (player.playerNetworkManager.isUsingRightHand.Value)
102: if (!player.IsOwner)
```

### Assets/Scripts/States/Character/Player/PlayerEquipmentManager.cs

```csharp
65: if (!player.IsOwner)
106: player.playerNetworkManager.currentRightHandWeaponID.Value = selectedWeapon.itemID;
111: player.playerNetworkManager.currentRightHandWeaponID.Value = firstWeapon.itemID;
127: player.playerNetworkManager.currentRightHandWeaponID.Value = player.playerInventoryManager.weaponsInRightHandSlots[player.playerInventoryManager.rightHandWeaponIndex].itemID;
165: if (!player.IsOwner)
207: player.playerNetworkManager.currentLeftHandWeaponID.Value = selectedWeapon.itemID;
212: player.playerNetworkManager.currentLeftHandWeaponID.Value = firstWeapon.itemID;
225: player.playerNetworkManager.currentLeftHandWeaponID.Value = player.playerInventoryManager.weaponsInLeftHandSlots[player.playerInventoryManager.leftHandWeaponIndex].itemID;
255: if (player.playerNetworkManager.isUsingRightHand.Value)
261: else if (player.playerNetworkManager.isUsingLeftHand.Value)
273: if (player.playerNetworkManager.isUsingRightHand.Value)
278: else if (player.playerNetworkManager.isUsingLeftHand.Value)
```

### Assets/Scripts/States/Character/Player/PlayerInputManager.cs

```csharp
226: if (player.playerNetworkManager.isLockedOn.Value)
233: player.playerNetworkManager.isLockedOn.Value = false;
246: if (lockOn_Input && player.playerNetworkManager.isLockedOn.Value)
250: player.playerNetworkManager.isLockedOn.Value = false;
255: if (lockOn_Input && !player.playerNetworkManager.isLockedOn.Value)
266: player.playerNetworkManager.isLockedOn.Value = true;
277: if (player.playerNetworkManager.isLockedOn.Value)
292: if (player.playerNetworkManager.isLockedOn.Value)
310: player.playerNetworkManager.isMoving.Value = false;
338: player.playerNetworkManager.isMoving.Value = true;
342: player.playerNetworkManager.isMoving.Value = false;
347: if (!player.playerNetworkManager.isLockedOn.Value || player.playerNetworkManager.isSprinting.Value)
349: player.playerAnimatorManager.UpdateAnimatorMovementParameters(0, moveAmount, player.playerNetworkManager.isSprinting.Value);
353: player.playerAnimatorManager.UpdateAnimatorMovementParameters(horizontal_Input, vertical_Input, player.playerNetworkManager.isSprinting.Value);
395: player.playerNetworkManager.isSprinting.Value = false;
404: player.playerNetworkManager.isChargingRightSpell.Value = true;
408: player.playerNetworkManager.isChargingRightSpell.Value = false;
424: player.playerNetworkManager.isMoving.Value = false;
460: player.playerNetworkManager.SetCharacterActionHand(true);
481: player.playerNetworkManager.SetCharacterActionHand(true);
544: if (player.isPerformingAction || player.playerNetworkManager.isJumping.Value)
```

### Assets/Scripts/States/Character/Player/PlayerInteractionManager.cs

```csharp
25: if (!player.IsOwner)
```

### Assets/Scripts/States/Character/Player/PlayerLocomotionManager.cs

```csharp
63: if (player.IsOwner)
65: player.characterNetworkManager.verticalMovement.Value = verticalMovement;
66: player.characterNetworkManager.horizontalMovement.Value = horizontalMovement;
67: player.characterNetworkManager.moveAmount.Value = moveAmount;
71: verticalMovement = player.characterNetworkManager.verticalMovement.Value;
72: horizontalMovement = player.characterNetworkManager.horizontalMovement.Value;
73: moveAmount = player.characterNetworkManager.moveAmount.Value;
75: if (!player.playerNetworkManager.isLockedOn.Value || player.playerNetworkManager.isSprinting.Value)
77: player.playerAnimatorManager.UpdateAnimatorMovementParameters(0, moveAmount, player.playerNetworkManager.isSprinting.Value);
81: player.playerAnimatorManager.UpdateAnimatorMovementParameters(horizontalMovement, verticalMovement, player.playerNetworkManager.isSprinting.Value);
90: player.playerAnimatorManager.UpdateAnimatorMovementParameters(PlayerInputManager.instance.horizontal_Input, PlayerInputManager.instance.vertical_Input, player.playerNetworkManager.isSprinting.Value);
95: player.playerAnimatorManager.UpdateAnimatorMovementParameters(PlayerInputManager.instance.horizontal_Input, PlayerInputManager.instance.vertical_Input, player.playerNetworkManager.isSprinting.Value);
133: if (player.playerNetworkManager.isSprinting.Value)
154: if (player.playerNetworkManager.isJumping.Value)
215: if (player.playerNetworkManager.isLockedOn.Value)
217: if (player.playerNetworkManager.isSprinting.Value || player.playerLocomotionManager.isRolling)
282: player.playerNetworkManager.isSprinting.Value = false;
287: player.playerNetworkManager.isSprinting.Value = true;
292: player.playerNetworkManager.isSprinting.Value = false;
399: if (player.playerNetworkManager.isJumping.Value)
407: player.playerNetworkManager.isJumping.Value = true;
417: if (player.playerNetworkManager.isSprinting.Value)
```

### Assets/Scripts/States/Character/Player/PlayerManager.cs

```csharp
5: using Unity.Netcode;
13: [HideInInspector] public PlayerNetworkManager playerNetworkManager;
32: playerNetworkManager = GetComponent<PlayerNetworkManager>();
46: if (!IsOwner)
61: if (!IsOwner)
79: public override void OnNetworkSpawn()
81: base.OnNetworkSpawn();
85: NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
88: if (IsOwner)
95: playerNetworkManager.vitality.OnValueChanged += playerNetworkManager.SetNewMaxHealthValue;
99: playerNetworkManager.currentHealth.OnValueChanged += PlayerUIManager.instance.playerUIHudManager.SetNewHealthValue;
105: if (!IsOwner)
106: characterNetworkManager.currentHealth.OnValueChanged += characterUIManager.OnHPChanged;
109: playerNetworkManager.currentHealth.OnValueChanged += playerNetworkManager.CheckHP;
112: playerNetworkManager.isLockedOn.OnValueChanged += playerNetworkManager.OnIsLockedOnChanged;
113: playerNetworkManager.currentTargetNetworkObjectID.OnValueChanged += playerNetworkManager.OnLockOnTargetIDChange;
115: playerNetworkManager.isChargingLeftSpell.OnValueChanged += playerNetworkManager.OnIsChargingLeftSpellChanged;
116: playerNetworkManager.isChargingRightSpell.OnValueChanged += playerNetworkManager.OnIsChargingRightSpellChanged;
118: playerNetworkManager.currentRightHandWeaponID.OnValueChanged += playerNetworkManager.OnCurrentRightHandWeaponIDChange;
119: playerNetworkManager.currentLeftHandWeaponID.OnValueChanged += playerNetworkManager.OnCurrentLeftHandWeaponIDChange;
120: playerNetworkManager.currentWeaponBeingUsed.OnValueChanged += playerNetworkManager.OnCurrentWeaponBeingUsedIDChange;
121: playerNetworkManager.currentSpellID.OnValueChanged += playerNetworkManager.OnCurrentSpellIDChange;
123: if (IsOwner && playerInventoryManager.currentSpell != null && WorldItemDatabase.Instance != null)
127: playerNetworkManager.currentSpellID.Value = equippedSpell.itemID;
130: playerNetworkManager.isChargingAttack.OnValueChanged += playerNetworkManager.OnIsChargingAttackChanged;
134: if (IsOwner && !IsServer)
140: public override void OnNetworkDespawn()
144: base.OnNetworkDespawn();
146: NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
149: if (IsOwner)
152: playerNetworkManager.vitality.OnValueChanged -= playerNetworkManager.SetNewMaxHealthValue;
156: playerNetworkManager.currentHealth.OnValueChanged -= PlayerUIManager.instance.playerUIHudManager.SetNewHealthValue;
161: if (!IsOwner)
162: characterNetworkManager.currentHealth.OnValueChanged -= characterUIManager.OnHPChanged;
165: playerNetworkManager.currentHealth.OnValueChanged -= playerNetworkManager.CheckHP;
167: playerNetworkManager.isChargingLeftSpell.OnValueChanged -= playerNetworkManager.OnIsChargingLeftSpellChanged;
168: playerNetworkManager.isChargingRightSpell.OnValueChanged -= playerNetworkManager.OnIsChargingRightSpellChanged;
170: playerNetworkManager.isLockedOn.OnValueChanged -= playerNetworkManager.OnIsLockedOnChanged;
171: playerNetworkManager.currentTargetNetworkObjectID.OnValueChanged -= playerNetworkManager.OnLockOnTargetIDChange;
174: playerNetworkManager.currentRightHandWeaponID.OnValueChanged -= playerNetworkManager.OnCurrentRightHandWeaponIDChange;
175: playerNetworkManager.currentLeftHandWeaponID.OnValueChanged -= playerNetworkManager.OnCurrentLeftHandWeaponIDChange;
176: playerNetworkManager.currentWeaponBeingUsed.OnValueChanged -= playerNetworkManager.OnCurrentWeaponBeingUsedIDChange;
177: playerNetworkManager.currentSpellID.OnValueChanged -= playerNetworkManager.OnCurrentSpellIDChange;
179: playerNetworkManager.isChargingAttack.OnValueChanged -= playerNetworkManager.OnIsChargingAttackChanged;
190: bool usePlayerListener = IsSpawned && IsOwner;
214: if (!IsServer && IsOwner)
228: if (IsOwner)
242: if (IsOwner)
245: playerNetworkManager.currentHealth.Value = playerNetworkManager.maxHealth.Value;
258: currentCharacterData.characterName = playerNetworkManager.characterName.Value.ToString();
263: currentCharacterData.currentHealth = playerNetworkManager.currentHealth.Value;
266: currentCharacterData.vitality = playerNetworkManager.vitality.Value;
267: currentCharacterData.endurance = playerNetworkManager.endurance.Value;
275: playerNetworkManager.characterName.Value = currentCharacterData.characterName;
279: playerNetworkManager.vitality.Value = currentCharacterData.vitality;
280: playerNetworkManager.endurance.Value = currentCharacterData.endurance;
283: playerNetworkManager.maxHealth.Value = playerStatsManager.CalculateHealthBasedOnVitalityLevel(playerNetworkManager.vitality.Value);
285: playerNetworkManager.currentHealth.Value = currentCharacterData.currentHealth;
296: playerNetworkManager.OnCurrentRightHandWeaponIDChange(0, playerNetworkManager.currentRightHandWeaponID.Value);
297: playerNetworkManager.OnCurrentLeftHandWeaponIDChange(0, playerNetworkManager.currentLeftHandWeaponID.Value);
300: playerNetworkManager.OnCurrentSpellIDChange(0,playerNetworkManager.currentSpellID.Value);
302: if (playerNetworkManager.isLockedOn.Value)
304: playerNetworkManager.OnLockOnTargetIDChange(0, playerNetworkManager.currentTargetNetworkObjectID.Value);
```

### Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs

```csharp
2: using Unity.Netcode;
7: public class PlayerNetworkManager : CharacterNetworkManager
11: public NetworkVariable<FixedString64Bytes> characterName = new NetworkVariable<FixedString64Bytes>("Character", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
14: public NetworkVariable<int> currentSpellID = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
15: public NetworkVariable<int> currentWeaponBeingUsed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
16: public NetworkVariable<int> currentRightHandWeaponID = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
17: public NetworkVariable<int> currentLeftHandWeaponID = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
18: public NetworkVariable<bool> isUsingRightHand = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
19: public NetworkVariable<bool> isUsingLeftHand = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
21: public NetworkVariable<bool> isChargingRightSpell = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
22: public NetworkVariable<bool> isChargingLeftSpell = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
64: if (player.IsOwner)
80: if (player.IsOwner)
93: [ServerRpc]
94: public void NotifyTheServerOfWeaponActionServerRpc(ulong clientID, int actionID, int weaponID)
96: if (IsServer)
98: NotifyTheServerOfWeaponActionClientRpc(clientID, actionID, weaponID);
102: [ClientRpc]
103: private void NotifyTheServerOfWeaponActionClientRpc(ulong clientID, int actionID, int weaponID)
105: if (clientID != NetworkManager.Singleton.LocalClientId)
```

### Assets/Scripts/States/Character/Player/PlayerStatsManager.cs

```csharp
25: CalculateHealthBasedOnVitalityLevel(player.playerNetworkManager.vitality.Value);
```

### Assets/Scripts/States/Character/Player/PlayerUIManager.cs

```csharp
4: using Unity.Netcode;
48: NetworkManager.Singleton.Shutdown();
50: NetworkManager.Singleton.StartClient();
```

### Assets/Scripts/States/Colliders/DamageCollider.cs

```csharp
53: if (damageTarget.characterNetworkManager.isBlocking.Value){
```

### Assets/Scripts/States/Colliders/DurkClubDamageCollider.cs

```csharp
68: if (damageTarget.IsOwner)
70: damageTarget.characterNetworkManager.NotifyTheServerOfCharacterDamageServerRpc(
71: damageTarget.NetworkObjectId,
72: bossCharacter.NetworkObjectId,
```

### Assets/Scripts/States/Colliders/DurksStompCollider.cs

```csharp
43: if (character.IsOwner)
```

### Assets/Scripts/States/Colliders/MeleeWeaponDamageCollider.cs

```csharp
67: if (damageTarget.characterNetworkManager.isBlocking.Value){
130: if (characterCausingDamage.IsOwner)
132: damageTarget.characterNetworkManager.NotifyTheServerOfCharacterDamageServerRpc(
133: damageTarget.NetworkObjectId,
134: characterCausingDamage.NetworkObjectId,
147: private PlayerNetworkManager _playerNetworkManager;
155: _playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
157: if (_playerNetworkManager.isPowerPet.Value){
```

### Assets/Scripts/States/Colliders/TakeBlockedDamageEffect.cs

```csharp
32: if (character.characterNetworkManager.isInvulnerable.Value)
54: if (!character.IsOwner)
85: character.characterNetworkManager.currentHealth.Value -= finalDamageDealt;
108: if (!character.IsOwner)
```

### Assets/Scripts/States/Colliders/UndeadHandDamageCollider.cs

```csharp
68: if (damageTarget.IsOwner)
70: damageTarget.characterNetworkManager.NotifyTheServerOfCharacterDamageServerRpc(
71: damageTarget.NetworkObjectId,
72: undeadCharacter.NetworkObjectId,
```

### Assets/Scripts/States/Effects/TakeDamageEffect.cs

```csharp
46: if (character.characterNetworkManager.isInvulnerable.Value)
70: if (!character.IsOwner)
89: character.characterNetworkManager.currentHealth.Value -= finalDamageDealt;
114: if (!character.IsOwner)
```

### Assets/Scripts/States/Effects/TakeStaminaDamageEffect.cs

```csharp
19: if (character.IsOwner)
```

### Assets/Scripts/States/State/CombatStanceState.cs

```csharp
39: if (!aiCharacter.aiCharacterNetworkManager.isMoving.Value)
```

### Assets/Scripts/States/UI/UI_Boss_HP_Bar.cs

```csharp
20: if (bossCharacter == null || bossCharacter.aiCharacterNetworkManager == null) return;
21: bossCharacter.aiCharacterNetworkManager.currentHealth.OnValueChanged += OnBossHPChanged;
23: SetMaxStat(bossCharacter.characterNetworkManager.maxHealth.Value);
24: SetStat(bossCharacter.aiCharacterNetworkManager.currentHealth.Value);
42: if (subscribed && bossCharacter != null && bossCharacter.aiCharacterNetworkManager != null)
43: bossCharacter.aiCharacterNetworkManager.currentHealth.OnValueChanged -= OnBossHPChanged;
```

### Assets/Scripts/States/UI/UI_Character_HP_Bar.cs

```csharp
53: characterName.text = playerCharacter.playerNetworkManager.characterName.Value.ToString();
57: slider.maxValue = character.characterNetworkManager.maxHealth.Value;
76: if (character.characterNetworkManager.currentHealth.Value != character.characterNetworkManager.maxHealth.Value)
```

### Assets/Scripts/States/WorldManager/WorldAIManager.cs

```csharp
4: using Unity.Netcode;
35: if (NetworkManager.Singleton.IsServer)
79: character.GetComponent<NetworkObject>().Despawn();
```

### Assets/Scripts/States/WorldManager/WorldObjectManager.cs

```csharp
4: using Unity.Netcode;
13: [SerializeField] List<NetworkObjectSpawner> networkObjectSpawners = new List<NetworkObjectSpawner>();
55: public void SpawnObject(NetworkObjectSpawner networkObjectSpawner)
57: if (NetworkManager.Singleton.IsServer)
```

### Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs

```csharp
26: public bool CanCreateNewGame => player != null && player.playerNetworkManager != null && player.IsSpawned && player.IsOwner && loadscene != null;
153: if (player == null || player.playerNetworkManager == null || !player.IsSpawned || !player.IsOwner)
167: player.playerNetworkManager.vitality.Value = 15;
168: player.playerNetworkManager.endurance.Value = 10;
169: player.playerNetworkManager.gameObject.transform.position = new Vector3(-319.92f, 175.81f, 595.46f);
204: if (player == null || player.playerNetworkManager == null || !player.IsSpawned || currentCharacterData == null)
```

### Assets/Scripts/TitleScreenManager.cs

```csharp
4: using Unity.Netcode;
55: NetworkManager network = NetworkManager.Singleton;
56: bool started = LocalHostStartup.TryStartHost(network, out ushort port);
57: if (started && port == 0 && network.NetworkConfig.NetworkTransport is Unity.Netcode.Transports.UTP.UnityTransport transport)
75: if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || !NetworkManager.Singleton.IsListening)
91: bool hostReady = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsListening;
```

### Assets/Scripts/WeaponItemAction.cs

```csharp
14: if (playerPerformingAction.IsOwner)
16: playerPerformingAction.playerNetworkManager.currentWeaponBeingUsed.Value = weaponPerformingAction.itemID;
```

## Inherited dependency inventory

| Class | Base | Source |
|---|---|---|
| AIBossCharacterManager | AICharacterManager | Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs |
| AIBossCharacterNetworkManager | AICharacterNetworkManager | Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs |
| AICharacterCombatManager | CharacterCombatManeger | Assets/Scripts/States/AI/AIChar/AICharacterCombatManager.cs |
| AICharacterManager | CharacterManager | Assets/Scripts/States/AI/AIChar/AICharacterManager.cs |
| AICharacterNetworkManager | CharacterNetworkManager | Assets/Scripts/States/AI/AIChar/AICharacterNetworkManager.cs |
| AIDurkCharacterManager | AIBossCharacterManager | Assets/Scripts/States/AI/AIDurk/AIDurkCharacterManager.cs |
| AIDurkCombatManager | AICharacterCombatManager | Assets/Scripts/States/AI/AIDurk/AIDurkCombatManager.cs |
| AISaciCharacterManager | AIBossCharacterManager | Assets/Scripts/Saci/AISaciCharacterManager.cs |
| AISaciCombatManager | AICharacterCombatManager | Assets/Scripts/Saci/AISaciCombatManager.cs |
| AIUndeadCombatManager | AICharacterCombatManager | Assets/Scripts/States/AI/AIUndeadCombatManager.cs |
| CharacterCombatManeger | NetworkBehaviour | Assets/Scripts/States/Character/CharacterCombatManeger.cs |
| CharacterManager | NetworkBehaviour | Assets/Scripts/States/Character/CharacterManager.cs |
| CharacterNetworkManager | NetworkBehaviour | Assets/Scripts/States/Character/CharacterNetworkManager.cs |
| FogWallInteractable | NetworkBehaviour | Assets/Scripts/FogWallInteractable.cs |
| Interactable | NetworkBehaviour | Assets/Scripts/Interactable.cs |
| PlayerCombatManager | CharacterCombatManeger | Assets/Scripts/States/Character/Player/PlayerCombatManager.cs |
| PlayerManager | CharacterManager | Assets/Scripts/States/Character/Player/PlayerManager.cs |
| PlayerNetworkManager | CharacterNetworkManager | Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs |
| SiteOfGraceInteractable | Interactable | Assets/Scripts/SiteOfGraceInteractable.cs |

## Package / serialized GUID manifest

GUIDs were read from installed package `.cs.meta` and own `.cs.meta`, then scanned in all Assets text serialization. Package NetworkManager/NetworkObject/transforms/transport are enumerated even where zero references exist. Own script GUIDs must be preserved during class conversion; package network component removal belongs to Task 5 in Editor, after consumers migrate.

| Script | GUID | Raw occurrences | Assets |
|---|---|---|---|
| Assets/Scripts/FogWallInteractable.cs | `8c14d5d9a53f07a4bb173a5c27675a0a` | 1 | 1 |
| Assets/Scripts/Interactable.cs | `e5ee170e9ce26204ea14b4b7a6ae399d` | 27 | 4 |
| Assets/Scripts/Saci/AISaciCharacterManager.cs | `5ae40614d0deb034e94a3a8db335c8a6` | 1 | 1 |
| Assets/Scripts/Saci/AISaciCombatManager.cs | `506d5519da52fdb4d9ddb4f3541ccf23` | 1 | 1 |
| Assets/Scripts/SiteOfGraceInteractable.cs | `6bf73e6ee5fa0fb4eb02c88a06c996e3` | 2 | 2 |
| Assets/Scripts/States/AI/AIBoss/AIBossCharacterNetworkManager.cs | `93c8b0f4e30dab940885e736ecbee6eb` | 3 | 3 |
| Assets/Scripts/States/AI/AIChar/AICharacterManager.cs | `12763c93f7e8635448da079e42364e71` | 3 | 3 |
| Assets/Scripts/States/AI/AIChar/AICharacterNetworkManager.cs | `d123b8a2a0727c447a165062a351bd54` | 3 | 3 |
| Assets/Scripts/States/AI/AIDurk/AIDurkCharacterManager.cs | `6b8b95e9eaabc434a8c15de419ce7f32` | 2 | 2 |
| Assets/Scripts/States/AI/AIDurk/AIDurkCombatManager.cs | `fd8a68e45c8f13446980b918f8ec27d1` | 2 | 2 |
| Assets/Scripts/States/AI/AIUndeadCombatManager.cs | `c6c8275466ff40d4ca98d039457b8cdf` | 3 | 3 |
| Assets/Scripts/States/Character/Player/PlayerCombatManager.cs | `323ab07af6964584c9da7b7693cf32c6` | 1 | 1 |
| Assets/Scripts/States/Character/Player/PlayerManager.cs | `3f583818ea88a2a40b60dbdc7af3dc67` | 1 | 1 |
| Assets/Scripts/States/Character/Player/PlayerNetworkManager.cs | `e7f919221b463fa40abef6f253729f13` | 1 | 1 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Components/AnticipatedNetworkTransform.cs | `5abfce83aadd948498d4990c645a017b` | 0 | 0 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Components/NetworkAnimator.cs | `e8d0727d5ae3244e3b569694d3912374` | 0 | 0 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Components/NetworkRigidbody.cs | `f6c0be61502bb534f922ebb746851216` | 0 | 0 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Components/NetworkRigidbody2D.cs | `80d7c879794dfda4687da0e400131852` | 0 | 0 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Components/NetworkTransform.cs | `e96cb6065543e43c4a752faaa1468eb1` | 0 | 0 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Configuration/NetworkPrefabsList.cs | `e651dbb3fbac04af2b8f5abf007ddc23` | 1 | 1 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Core/NetworkManager.cs | `593a2fe42fa9d37498c96f9a383b6521` | 1 | 1 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Core/NetworkObject.cs | `d5a57f767e5e46a458fc5d3c628d0cbb` | 19 | 13 |
| Library/PackageCache/com.unity.netcode.gameobjects@53246f9d2516/Runtime/Transports/UTP/UnityTransport.cs | `6960e84d07fb87f47956e7a81d71c4e6` | 1 | 1 |

### Asset → network GUID references

| Asset | Script: occurrences |
|---|---|
| Assets/Data/Prefabs/Dummmy.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; AICharacterNetworkManager `d123b8a2a0727c447a165062a351bd54`: 1; AICharacterManager `12763c93f7e8635448da079e42364e71`: 1; AIUndeadCombatManager `c6c8275466ff40d4ca98d039457b8cdf`: 1 |
| Assets/Data/Prefabs/DummmyRanged.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; AICharacterNetworkManager `d123b8a2a0727c447a165062a351bd54`: 1; AICharacterManager `12763c93f7e8635448da079e42364e71`: 1; AIUndeadCombatManager `c6c8275466ff40d4ca98d039457b8cdf`: 1 |
| Assets/Data/Prefabs/Durk 1.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; AIDurkCharacterManager `6b8b95e9eaabc434a8c15de419ce7f32`: 1; AIDurkCombatManager `fd8a68e45c8f13446980b918f8ec27d1`: 1; AIBossCharacterNetworkManager `93c8b0f4e30dab940885e736ecbee6eb`: 1 |
| Assets/Data/Prefabs/Durk.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; AIDurkCharacterManager `6b8b95e9eaabc434a8c15de419ce7f32`: 1; AIDurkCombatManager `fd8a68e45c8f13446980b918f8ec27d1`: 1; AIBossCharacterNetworkManager `93c8b0f4e30dab940885e736ecbee6eb`: 1 |
| Assets/Data/Prefabs/FOG.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; FogWallInteractable `8c14d5d9a53f07a4bb173a5c27675a0a`: 1 |
| Assets/Data/Prefabs/HumanoDummy.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; AICharacterNetworkManager `d123b8a2a0727c447a165062a351bd54`: 1; AICharacterManager `12763c93f7e8635448da079e42364e71`: 1; AIUndeadCombatManager `c6c8275466ff40d4ca98d039457b8cdf`: 1 |
| Assets/Data/Prefabs/Player.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; PlayerNetworkManager `e7f919221b463fa40abef6f253729f13`: 1; PlayerManager `3f583818ea88a2a40b60dbdc7af3dc67`: 1; PlayerCombatManager `323ab07af6964584c9da7b7693cf32c6`: 1 |
| Assets/Data/Prefabs/Saci.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; AISaciCharacterManager `5ae40614d0deb034e94a3a8db335c8a6`: 1; AISaciCombatManager `506d5519da52fdb4d9ddb4f3541ccf23`: 1; AIBossCharacterNetworkManager `93c8b0f4e30dab940885e736ecbee6eb`: 1 |
| Assets/Data/Prefabs/SiteOfGraceInteractable.prefab | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; SiteOfGraceInteractable `6bf73e6ee5fa0fb4eb02c88a06c996e3`: 1 |
| Assets/Data/Prefabs/World Managers/World NetworkManager.prefab | NetworkManager `593a2fe42fa9d37498c96f9a383b6521`: 1; UnityTransport `6960e84d07fb87f47956e7a81d71c4e6`: 1 |
| Assets/DefaultNetworkPrefabs.asset | NetworkPrefabsList `e651dbb3fbac04af2b8f5abf007ddc23`: 1 |
| Assets/DevTools/Scenes/DevPlayground.unity | Interactable `e5ee170e9ce26204ea14b4b7a6ae399d`: 5 |
| Assets/Scenes/Arte.unity | Interactable `e5ee170e9ce26204ea14b4b7a6ae399d`: 7; NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1 |
| Assets/Scenes/CucaLoad.unity | Interactable `e5ee170e9ce26204ea14b4b7a6ae399d`: 8; NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 7 |
| Assets/Scenes/Tutorial_Final.unity | NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1; SiteOfGraceInteractable `6bf73e6ee5fa0fb4eb02c88a06c996e3`: 1 |
| Assets/Scenes/Video1.unity | Interactable `e5ee170e9ce26204ea14b4b7a6ae399d`: 7; NetworkObject `d5a57f767e5e46a458fc5d3c628d0cbb`: 1 |

### Seven enabled scenes + playground transitive dependency coverage

Closure traverses Assets GUID references through text `.unity/.prefab/.asset/.controller/.overrideController/.anim`, with cycles suppressed; includes referenced prefab families and catalogs (which may contain unused variants). Counts are dependency assets, not live object counts. Binary assets, dynamic Resources/Addressables resolution and object override effective state require future Editor audit.

| Root | Text dependency assets | Assets with network script references |
|---|---|---|
| Assets/Scenes/SceneMenu.unity | 197 | 11 |
| Assets/Scenes/Tutorial_Final.unity | 138 | 4 |
| Assets/Scenes/Video1.unity | 228 | 4 |
| Assets/Scenes/CucaLoad.unity | 147 | 6 |
| Assets/Scenes/t.unity | 85 | 4 |
| Assets/Scenes/MapaEsboco.unity | 1 | 0 |
| Assets/Scenes/Creditos.unity | 5 | 0 |
| Assets/DevTools/Scenes/DevPlayground.unity | 746 | 12 |

<details><summary>Network dependencies for Assets/Scenes/SceneMenu.unity</summary>

- Assets/Data/Prefabs/Dummmy.prefab
- Assets/Data/Prefabs/DummmyRanged.prefab
- Assets/Data/Prefabs/Durk 1.prefab
- Assets/Data/Prefabs/Durk.prefab
- Assets/Data/Prefabs/FOG.prefab
- Assets/Data/Prefabs/HumanoDummy.prefab
- Assets/Data/Prefabs/Player.prefab
- Assets/Data/Prefabs/Saci.prefab
- Assets/Data/Prefabs/SiteOfGraceInteractable.prefab
- Assets/Data/Prefabs/World Managers/World NetworkManager.prefab
- Assets/DefaultNetworkPrefabs.asset

</details>

<details><summary>Network dependencies for Assets/Scenes/Tutorial_Final.unity</summary>

- Assets/Data/Prefabs/Dummmy.prefab
- Assets/Data/Prefabs/Player.prefab
- Assets/Data/Prefabs/Saci.prefab
- Assets/Scenes/Tutorial_Final.unity

</details>

<details><summary>Network dependencies for Assets/Scenes/Video1.unity</summary>

- Assets/Data/Prefabs/Dummmy.prefab
- Assets/Data/Prefabs/Durk.prefab
- Assets/Data/Prefabs/Player.prefab
- Assets/Scenes/Video1.unity

</details>

<details><summary>Network dependencies for Assets/Scenes/CucaLoad.unity</summary>

- Assets/Data/Prefabs/Dummmy.prefab
- Assets/Data/Prefabs/FOG.prefab
- Assets/Data/Prefabs/Player.prefab
- Assets/Data/Prefabs/Saci.prefab
- Assets/Data/Prefabs/SiteOfGraceInteractable.prefab
- Assets/Scenes/CucaLoad.unity

</details>

<details><summary>Network dependencies for Assets/Scenes/t.unity</summary>

- Assets/Data/Prefabs/Dummmy.prefab
- Assets/Data/Prefabs/DummmyRanged.prefab
- Assets/Data/Prefabs/Player.prefab
- Assets/Data/Prefabs/Saci.prefab

</details>

<details><summary>Network dependencies for Assets/Scenes/MapaEsboco.unity</summary>



</details>

<details><summary>Network dependencies for Assets/Scenes/Creditos.unity</summary>



</details>

<details><summary>Network dependencies for Assets/DevTools/Scenes/DevPlayground.unity</summary>

- Assets/Data/Prefabs/Dummmy.prefab
- Assets/Data/Prefabs/DummmyRanged.prefab
- Assets/Data/Prefabs/Durk 1.prefab
- Assets/Data/Prefabs/Durk.prefab
- Assets/Data/Prefabs/FOG.prefab
- Assets/Data/Prefabs/HumanoDummy.prefab
- Assets/Data/Prefabs/Player.prefab
- Assets/Data/Prefabs/Saci.prefab
- Assets/Data/Prefabs/SiteOfGraceInteractable.prefab
- Assets/Data/Prefabs/World Managers/World NetworkManager.prefab
- Assets/DefaultNetworkPrefabs.asset
- Assets/DevTools/Scenes/DevPlayground.unity

</details>

### Serialized methods requiring preservation/reconciliation

UnityEvent/animation method names were scanned in the same text assets. These are name matches, not verified persistent-call target types; no Editor resolution was run.

- Assets/Animations/Straight Sword Animation Set/Animations/Spear Animations/Nephilim Animations (Remastered)/Spear Main Hand/spear_main_light_attack_01.anim: `CloseDamageCollider` ×1, `EnableCanDoCombo` ×1, `OpenDamageCollider` ×1.
- Assets/Animations/Straight Sword Animation Set/Animations/main/straight_sword_main_light_attack_01.anim: `CloseDamageCollider` ×1, `DrainStaminaBasedOnAttack` ×1, `EnableCanDoCombo` ×1, `OpenDamageCollider` ×1.
- Assets/Data/Prefabs/Player  Ui Manager.prefab: `SaveGame` ×1.
- Assets/Materiais/Images/UiElementos/Canvas.prefab: `ChangeElement` ×2, `ResetSite` ×1.
- Assets/Scenes/SceneMenu.unity: `CloseDeleteCharacterPopUp` ×1, `CloseLoadGameMenu` ×1, `CloseNoFreeCharacterSlotsPopUp` ×1, `DeleteCharacterSlot` ×1, `LoadGame` ×1, `OpenLoadGameMenu` ×1, `QuitGame` ×1, `SaveGame` ×1, `SelectNoSlot` ×1, `SlotTeste` ×1, `StartNetworkAsHost` ×1, `StartNewGame` ×1.
- Assets/Scripts/Saci/boss_durk_mace_attack_01.anim: `DisableCanRotate` ×1, `EnableCanRotate` ×1.
- Assets/Scripts/Saci/boss_durk_mace_attack_sequence_02.anim: `DisableCanRotate` ×2, `EnableCanRotate` ×2.

## Package dependency removal boundaries

- `com.unity.netcode.gameobjects` 2.11.1, depth 0; dependencies `{"com.unity.nuget.mono-cecil": "1.11.4", "com.unity.transport": "2.6.0"}`; lockfile dependents: none.
- `com.unity.transport` 2.7.2, depth 1; dependencies `{"com.unity.burst": "1.8.24", "com.unity.collections": "2.2.1", "com.unity.mathematics": "1.3.2"}`; lockfile dependents: com.unity.netcode.gameobjects.
- `com.unity.multiplayer.center` 1.0.1, depth 0; dependencies `{"com.unity.modules.uielements": "1.0.0"}`; lockfile dependents: none.

Netcode and Multiplayer Center are direct manifest dependencies; transport is transitive. Multiplayer Center has no own-code namespace usage in this scan. Removal still belongs to closure Task 5. Collections/Burst/Mathematics have other package consumers (e.g. render pipeline); never delete them as assumed network-only dependencies. No custom own gameplay asmdef currently appears; vendor/dialogue/PathCreator asmdefs remain outside scope. Core and Gameplay split is proposed, not installed here.

## Omissions / remaining verification / next safe task

- No Unity compile, tests, Editor audit, build, gameplay, socket/port observation or performance capture was run. This inventory cannot close G1A, G1B or G2 or prove runtime presence/order of any object.
- Source role extraction is static C# token/assignment matching rather than Roslyn/data-flow analysis. Reflection, dynamic invocation, preprocessor define resolution and binary asset bindings are not proven absent. Same-named fields are resolved by inspected receiver snippets; no automated symbol binding is claimed.
- Serialized GUID closure covers text assets, all seven enabled scene roots and existing playground scene roots. Effective prefab variants/overrides, nested component local fileIDs, UnityEvent target resolution, animation-event timing and runtime-spawned Resources assets require the future Editor LocalRuntimeAudit. GUID occurrence counts are deliberately not component/live instance counts.
- The planned audit MenuItem, state/registry, tests and fixtures do not exist as deliveries of this task. Every behavior row is a future required regression; no omission is treated as optional behavior.
- Complete 1A protected legacy restore/slot/save tests and 1B current-host route/capture baseline first. Once both gates are evidenced, the next dependency-safe implementation is plan 02 Task 1 Steps 2–4: isolated pure Core state/registry tests (red), implementation (green), then player migration using this manifest. Keep legacy bridge/metas until each mapped consumer and serialized asset is migrated; remove package/host last.
