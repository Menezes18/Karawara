# Unity 6000.4.6f1 asset audit

Read-only production audit on 2026-09-30. Production files were not modified by this audit. These results describe the inspected snapshot; subsequent repairs must be verified separately in Unity.

## Scope and method

Indexed 20,338 GUIDs from Assets, Packages and Library/PackageCache; excluded Unity built-in GUIDs. Scanned 60 scenes, 380 prefabs and 48 controllers. Followed serialized external references from SceneMenu, CucaLoad, Video1, t and MapaEsboco into 844 reachable assets. Found 64 unresolved references in 14 reachable files. This traversal does not prove that every reachable asset is instantiated at runtime, nor validate imported subasset fileIDs, animation events, VFX compilation or visual output.

## Concrete production findings and recovery evidence

| Asset | Evidence | Recovery assessment |
|---|---|---|
| Assets/Data/Prefabs/Player.prefab | Missing controller e57226dd04c4b8043a144c9cbe654b63 in nested Persona.fbx Animator overrides at 2398, 3527, 4661, 5896, 9330 | Historical Assets/Animation/Controller/PlayerAnimatorController2.controller, deleted in b1c04d21 (Redo). Present in HEAD, so predates migration. Root Animator correctly uses current Humanoid.controller. Do not restore obsolete controller and deleted clips blindly. Explicitly clearing unresolved support Animator overrides preserves their current null behavior; determine whether each support Animator should use Humanoid before assigning. |
| Assets/Scenes/SceneMenu.unity:568110 | Active UI object Fps has missing script a022b4a18bd43614a9b172b4de9565eb | Exactly recoverable SystemFps.cs and meta via git show fbe5d86e^:Assets/Scripts/Menu/SystemFps.cs. Deleted in fbe5d86e. Current serialized component has no fpsText/fpsToggle fields, so restoring source also requires deliberate wiring. TMP example counters are unsuitable drop-ins because they create new text and set targetFrameRate=1000. |
| Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref.prefab and variants 1, 2 | Missing source fd2344e6346e3764b833f1b7802fccac, 16 references per prefab; source at Circle_ref:4946 | Each contains two complete GameObject/Transform pairs and full ParticleSystem/ParticleSystemRenderer, plus one PrefabInstance. No stripped objects. Local FireCircle effect data survives. No matching source meta found in all Git history. Editor unpack of the missing nested source, preserving local data, is a candidate repair requiring before/after component and effect verification. |
| Assets/Scripts/Boss Cuca/MeleeBoss/Character/Animations/StarterAssetsThirdPerson.controller:714 | Fly state references missing clip d3b2083e086810047ab04d540db8afbc | No matching meta in all Git history. Flying Kick.anim exists but its name is insufficient evidence for substitution. Inspect state usage and intended animation before changing. |
| Assets/Data/Level/MundoAberto/Materials/MundoAbertoMaterial.mat:62 | Missing _EmissionMap texture 01676a5432c79154b86bab000cfc99e4 | Inspect active shader property/keywords before visual repair. Saved stale properties are not proof of visible damage. |
| Assets/VFX_Shader/SkillTree/Spirit_Attck/Dissolver.mat:42,46 | Missing texture 20b377714c6688e4d8a632272da81e88 in two saved graph properties | Verify current graph property names and imported material appearance before substitution. |

Player.prefab also has two TransformationsBase components with suporte={fileID:0}: base human component at 6548 and the component on GameObject 352998117226773943. TransformationsBase.Start originally accessed suporte.enabled unconditionally at line 22. This is a concrete null-reference risk independent of the missing controller overrides. Other two serialized transformation support references resolve.

Six reachable VFX graphs (Hit, Spirit_effs2, Spirit_effs, HealSpell, WeaponVFX, Fogo) contain missing serialized editor subobject script GUID 081ffb0090424ba4cb05370a42ead6b9. Those empty subobjects may be migration residue. Require actual VFX import diagnostics before declaring effects broken or deleting graph data.

## Rendering, navigation and loading

URP 17.4.0 is configured for all three quality tiers. Balanced and High Fidelity have active SSAO; Performant has no renderer feature. High Fidelity requests depth and opaque textures; the other tiers do not. All renderer assets select Forward rendering. Referenced shader GUIDs in reachable material assets resolve; this does not prove shaders compile or render correctly.

CucaLoad, Video1 and t have resolved baked NavMesh data references. MapaEsboco has no serialized baked NavMeshSurface data, so a new playground requires its own NavMesh bake. Menu global NavMeshData is null, which is not itself a defect.

No Addressables package, settings or gameplay usage found. Gameplay scripts have no Resources.Load paths. Resources loading hits belong to TMP examples and xNode editor. Neither package absence nor Resources directory existence is an error.

## Playground prefab catalog

All paths below are relative to Assets/Data/Prefabs. Their script GUIDs resolve.

| Prefab | Purpose |
|---|---|
| Player.prefab | Actual player/network/combat managers; legacy nested controller overrides noted above |
| PlayerCamera (Left & Right).prefab | PlayerCamera, URP camera data, PostProcessLayer and Volume |
| PlayerInputManager.prefab | Actual input singleton |
| Dummmy.prefab | AICharacter and AIUndeadCombat melee target |
| DummmyRanged.prefab | AICharacter target with MagicSpeel |
| HumanoDummy.prefab | Humanoid combat target |
| Durk.prefab | AIDurk boss/combat and boss network managers |
| FOG.prefab | FogWallInteractable |
| SiteOfGraceInteractable.prefab | Rest/checkpoint interaction |
| AISpawner.prefab; AISpawnerMapinguari.prefab | AICharacterSpawner |
| EventTrigger/Event Trigger - Wake Boss.prefab | Boss activation trigger |
| World Managers/* | Existing network, save, AI, item, effects, sound and action manager architecture |

Assets/Data/Modelos/Humanos/Human_melee.prefab is a model with lock-on and damage colliders, without the full AICharacter manager stack. Prefer the gameplay prefabs for representative testing.

## Nonproduction findings

52 unresolved script references in eight prefabs across the broader project, predominantly SettingsContainer, Boitata exports and TMP examples; two unresolved state behaviour scripts in the Straight Sword demo controller. Additional missing animation references occur in Press Start Button, older StarterAssets controller, ThunderLord, Village controllers and demo Sword controller. These must not be reported as production regressions without establishing active usage.

No local AGENTS.md found. Existing dirty production changes were preserved. This audit ran no Editor operations or runtime tests.

## Implemented repairs and pending Editor operation

Restored SystemFps.cs using its original historical GUID. The new component resolves fpsText from its local TextMeshProUGUI when unwired. Assigned toggles retain the original initially hidden display and onValueChanged control. The listener is removed on destruction rather than disabling, because hiding the FPS text may disable the component itself and the toggle must still be able to reactivate it. No target frame rate or layout setting is changed. The unwired existing Fps object retains its active state and receives FPS text.

Cleared exactly five unresolved Player.prefab nested controller override references to fileID zero; all other bytes in the existing dirty prefab were preserved. Root/controller transformation references remain valid. Added only a null condition before TransformationsBase.Start disables optional support Animator; eroding initialization is unchanged.

Added Editor menu Tools/Karawara/Repair Preserved FireCircle Prefabs in Assets/DevTools/Editor/LegacyAssetRepair.cs. It validates all three prefabs before writing, requires the exact complete six local object structure with no stripped objects, removes only obsolete missing-source provenance and its PrefabInstance, and compares the remaining object data after excluding provenance. A Python dry-run of the same transformation confirmed identical local object data for all three assets. Root subsequently executed the repair and confirmed preserved local object bytes after excluding obsolete provenance; this closes the pending repair operation. Visual effect/runtime validation remains a separate evidence boundary. Missing clips/textures and potentially stale VFX editor subobjects were not replaced.
