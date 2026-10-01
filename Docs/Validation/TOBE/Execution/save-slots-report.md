# Save slots and menu commands — Task 2

Scope: `WorldSaveGameManager.cs`, `UI_Character_Save_Slot.cs`, `TitleScreenManager.cs`, `SaveSlotRegression.cs` and its new meta. Existing scene/prefab GUIDs and UnityEvent method names are preserved. No Netcode removal, schema migration or player restore changes are included.

## Changes

- Slot presentation calls typed profile reads and cannot request a world transition. Missing slots preserve the existing hidden layout; invalid profiles display an unavailable status without dereferencing null. Backup recovery uses the storage result message `RecoveredFromBackup`.
- Profile refresh includes inactive menu slot objects so overwrite/delete refresh is not dependent on cached startup data.
- `SlotTeste`, `LoadGame`, `SelectCurrentSlot`, `LoadGameFromCharacterSlot`, overwrite and deletion methods remain available to existing UnityEvents. Legacy `SlotTeste` is a harmless compatibility bridge; `TitleScreenManager.currentSelectedSlot` owns selection, and explicit load validates that slot before replacing both active slot/data or queuing a transition. Failed loads preserve the previous active context.
- Save and delete use typed results. New-game capture constructs a candidate without changing the live player, explicitly sets the gameplay scene index, initial coordinates/attributes and calculated HP, and writes before changing the active save context or player. Failure leaves the player and menu intact. The existing title status display surfaces save errors. Profile refresh runs after successful writes/deletes.
- `TryLoadSelectedSlot(CharacterSlot)` is a read-only typed query. Task 3 can insert pending restore generation after the successful `LoadGame` query and before `RequestWorldSceneLoad`.

## Fixture and evidence

Menu: `Karawara/Validation/TOBE/Run Save Slot Regression`. It uses actual components and the actual `OnEnable` body, invoked directly through required exact `OnEnable` MethodInfo in EditMode because EditMode does not dispatch these lifecycle callbacks. This is component-branch evidence, not a PlayMode lifecycle or standalone result. The only replacement is the scene-request boundary, counted by a subclass to avoid loading production scenes.

Each run injects a unique OS temporary root before enabling any UI. The UI and manager share that root; finally restores singleton/isolation state and deletes only the known unique fixture directory. No fixture writes to the personal persistent root. SaveRoot default policy remains for Task 4.

The corrected RED execution (coordinator, 2026-10-01 18:31:16 UTC) completed 8 cases, with 2 passes/6 failures reproducing the unwanted OnEnable transition, forced slot1, stale corrupt display, corrupt load transition and stale overwrite. Artifact: `save-slots-red-corrected-fixture.json`. It captured 9 native `ShouldRunBehaviour` assertions from EditMode SendMessage lifecycle dispatch. The first implementation run passed 8/8 behavioral cases but retained these 9 harness assertions and was rejected. The fixture now directly invokes the known required callback body in EditMode, or uses natural activation in PlayMode, without invoking native lifecycle dispatch. Earlier `save-slots-red.json` had harness-null failures and is not accepted behavior RED evidence.

The coordinator compiled and executed the expanded fixture after the active-context correction: 11/11 passes and zero captured errors, archived as `save-slots-context-green.json`. This confirms `SlotTeste` leaves active context untouched, successful load commits slot2, and failed corrupt load preserves prior slot/data. A final static correction to NewGame now constructs its initial candidate without live mutation and explicitly writes `worldSceneIndex` rather than the menu scene; compilation and real-host validation of that final revision are pending coordinator execution. Added cases assert actual backup recovery presentation and Windows sharing-lock IO failures for load/delete; captured error messages and stacks are recorded. No standalone/menu gameplay, failed initial write with a real host, new-process persistence or Windows player runtime is claimed. Those remain separate gates. No commit was made.

## Static binding review

`Assets/Scenes/SceneMenu.unity` binds `SlotTeste` followed by `LoadGame` (around lines 101082/101094), and multiple `SelectCurrentSlot` events plus `DeleteCharacterSlot`. `Assets/Data/Prefabs/Save Slot  (0).prefab` binds `LoadGameFromCharacterSlot` (line 131) and `SelectCurrentSlot` (line 175). All public targets were retained; no scene/prefab edits were necessary.
