# Task 1 — atomic legacy save storage

Date UTC: 2026-10-01T18:25:09Z. Execution checkout: `C:\Users\migue\Dev\Karawara` (current checkout explicitly selected by coordinator). No commit created. This report covers storage only; the coordinator owns Editor, integration, builds and runtime gates.

## Scope and implementation

Created `Assets/Karawara/Runtime/Core/Karawara.Core.asmdef` (`noEngineReferences: true`) and the four `Persistence` contract/implementation files. Created `Assets/Tests/EditMode/Karawara.Core.EditModeTests.asmdef` and `AtomicFileStoreTests.cs`. Modified only existing `Assets/Scripts/SaveFileDataWriter.cs` within storage scope. Existing DTO/schema, Netcode, third-party code and original script GUID/meta were preserved. Existing dirty documents were not edited by this agent.

`SaveResult<T>` exposes Success/Value/Error/Message and the full specified SaveError enum. `IAtomicFileStore` provides typed Read/Write/Delete. `IFileOperations` isolates real IO and permits deterministic failure after flush. `SystemFileOperations` uses UTF-8, FileStream.Flush(true), File.Replace for existing primary and File.Move for first creation; it never truncates primary as fallback.

Root must be absolute and is normalized. Slot names are flat relative primary filenames; traversal, separators, alternate stream syntax, trailing Windows aliases, and .tmp/.bak aliases return InvalidData. Each operation claims the canonical slot path in a shared process-wide active set; concurrent reads/writes/deletes across store instances return Busy. Entries are released in finally and do not accumulate after operations.

Write validates payload, writes and flushes a sibling .tmp, rereads and validates the exact flushed content, then replaces/moves primary. A failed replace returns IoFailure while preserving the preceding primary. Valid prior primary becomes .bak. Corrupt prior primary is replaced without overwriting an existing valid backup. A failed write attempts temp cleanup; leftover temp is never loaded/promoted. Read validates primary first, then backup. Recovery returns backup data without rewriting original files. Delete removes backup and temp before primary so partial deletion cannot resurrect a deleted primary.

Legacy writer exposes TryLoad/TrySave/TryDelete, preserving DTO JSON serialization. Validator requires legacy scene/name/position/health fields, a nonempty name, finite coordinates/time/stamina, nonnegative elapsed time/scene/health/stats. It rejects empty/partial JSON objects rather than accepting JsonUtility defaults. No new schema/version/skill serialization has been introduced. Existing void save/delete wrappers retain signatures but throw on failed typed operations, preventing silent success; nullable LoadSaveFile wrapper reports non-Missing failures as warnings. New consumers should use the typed methods.

## Test-first evidence

All NUnit IO uses a unique Path.GetTempPath directory (`Karawara-storage-tests-<GUID>`); teardown removes only that fixture directory. No real persistentDataPath was opened or edited by this agent. Tests perform real filesystem writes/replaces/deletes; the injected file seam changes only deterministic timing/failure/corruption.

Coordinator executed EditMode filter `Karawara.Tests.Persistence.AtomicFileStoreTests`:

- Initial red job `8c4b65dd9e41404d90a0a1a9d486916a`: 12 cases, 12 assertion failures against the compileable Missing-result contract skeleton. No storage implementation existed before this run. Coordinator confirmed expected assertion failures, including missing flush/concurrency behavior.
- After the first implementation, added `CorruptedFlushedTempPreservesPrimaryAndBackup` before its fix. Job `7ea8d75b18974d6aa42cdb487bb33da9`: 13 cases, original 12 passed; sole new test failed because the operation returned true instead of false. Then added flushed-temp reread validation and exact content comparison.
- Final green job `b32d4d1c9fb545568bb4497155266fb5` at 18:26 UTC: 13 cases, 13 passed, 0 failures, 0 skipped, duration 0.77 s. Structured result archival is owned by coordinator. Independent review remains pending.

Case inventory: FirstWriteCanBeRead; SecondWritePreservesBackup; ReplaceFailurePreservesPrimary; InvalidPrimaryRecoversBackup; WritingOverInvalidPrimaryPreservesValidBackup; InvalidPayloadDoesNotReplace; ReadIgnoresTmp; ConcurrentSameSlotReturnsBusy; DeleteDoesNotRecoverBackup; CorruptedFlushedTempPreservesPrimaryAndBackup; UnsafeNameIsRejected with three inputs (`../outside.json`, `slot01.json.tmp`, `slot01.json.bak`). Replace failure specifically asserts the temporary write flushed before the injected exception, IoFailure, and unchanged primary bytes. Concurrency blocks the first real file operation and checks a second store instance's Write/Read/Delete all return Busy.

Agent shell check `git diff --check -- Assets/Scripts/SaveFileDataWriter.cs`: exit 0; only Git LF-to-CRLF normalization warning. A broader check earlier reported CR whitespace in generated Karawara.slnx entries for the new assemblies, outside agent-owned scope. Coordinator must reconcile generated files according to baseline policy; they were not edited by this agent.

## Limits and remaining gates

No independent Editor/MCP operations were run by this agent; job summaries above are coordinator-provided evidence. Final entire EditMode suite, legacy bridge/host/menu fixtures, Release/Development compilation, standalone Windows x64 process-new persistence and natural gameplay are coordinator gates. Automated temporary-directory IO does not demonstrate those gates.

Concurrency protection is within the current process, across store instances. No cross-process lock or crash/power-loss experiment was implemented. Flush(true) and same-volume replacement are the intended protection; no process was killed. Root/name checks assume an application-owned directory, not a hostile reparse-point filesystem. Backup recovery does not silently rewrite primary. Legacy skills remain serialized by their pre-existing schema and are not claimed to survive fresh processes. Save/delete wrappers now surface an exception; typed consumer migration and failure UI remain integration scope.


## Independent review correction — strict legacy JSON root validation

Independent reviewer found a real adapter defect: required-property substring searches accepted a JSON with `characterName` at the root and scene/position/health inside an unknown nested object. JsonUtility ignored the nested object and loaded defaults, allowing the corrupt primary to supersede a valid backup on a later write. This finding is documented in `storage-review.md`; Core-only equality validators did not cover it.

Created `Assets/DevTools/Editor/LegacySaveWriterRegression.cs` plus a new unique meta (no existing GUID changed). MenuItem: `Karawara/Validation/TOBE/Run Legacy Save Writer Regression`. The fixture uses independent temporary subdirectories per case, writes a structured completed/per-case report to `legacy-save-writer-fixture-result.json`, and confirms teardown. It creates no gameplay objects or network/host state and never accesses real save directories.

Adapter red at `2026-10-01T18:31:15.4788565Z` completed all 12 cases: 5 passed, 7 failed, temporary directory removed. Failures were NestedRequiredFieldsRejected, WrongSceneTypeRejected, WrongNameTypeRejected, WrongPositionTypeRejected, WrongHealthTypeRejected, NestedPrimaryRecoversBackup, WriteOverNestedPrimaryPreservesBackup. These failures reproduced both selection of a default-filled corrupt primary and destruction of a good backup. Existing passing cases covered valid DTO restoration, malformed/empty payload rejection, null-save rejection, and failure surfacing from the compatibility wrapper. Red ran before the parser change.

Correction: installed Newtonsoft JObject parser validates actual top-level properties and expected scalar types before JsonUtility deserialization. Name must be String; scene/health must be Integer in nonnegative Int32 range; coordinates must be Integer/Float, finite and within Float32 range. Optional stats/time/stamina also undergo primitive-type checks, followed by existing deserialized value checks. Duplicate JSON properties are rejected. Existing DTO and serialization schema remain unchanged; Newtonsoft already exists in this project's installed Unity packages. No Unity types/dependencies were introduced into Core.

Coordinator additionally requested recoverable-slot provenance while preserving typed API signatures. Added a core assertion and adapter assertion before production changes: single core red job `ea00caf0975b49b0b87a68bc1f3d8e37` failed as expected because recovery Message was empty instead of `RecoveredFromBackup`. `SaveResult<T>.Ok` now accepts optional message; Core backup recovery supplies the stable string `RecoveredFromBackup`; legacy TryLoad forwards it. Existing callers of Ok(value) remain compatible. No slot UI logic was changed by this agent.

Final parser/provenance Core green job `effd66763add45b182e01b7c6c65dc66`: full 13/13 passed, 0 failures, 0 skipped. Adapter green at `2026-10-01T18:34:03.1915475Z`: 12/12 passed, 0 failures, completed=true and cleanedUp=true; coordinator archived `legacy-save-writer-green.json`. Agent also directly inspected the live structured adapter result. Independent re-review remains pending. Writer-only diff whitespace check remains exit 0, with normal LF/CRLF normalization warning. Runtime/new-process, host/menu and broader compilation gates remain coordinator-owned and are not established by these adapter cases.

