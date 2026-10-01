# Task 1 storage review

Scope: independent read-only review of the uncommitted implementation against base `a2b43f091dad6459acc65094eceedf9200d28373`, the Task 1 brief, and TO-BE sections 5.5 and 6. Reviewed Core persistence files and asmdef, AtomicFileStoreTests and test asmdef, and SaveFileDataWriter. No implementation changes or commits performed.

## Decision: changes required

### Important — required legacy fields are checked anywhere in the JSON instead of at the DTO root

Location: `Assets/Scripts/SaveFileDataWriter.cs:72-76`.

Concrete trigger: place the following syntactically valid but structurally incomplete JSON in the primary, alongside a valid legacy `.bak`:

```json
{"characterName":"Character","ignored":{"sceneIndex":1,"xPosition":1,"yPosition":2,"zPosition":3,"currentHealth":100}}
```

Every substring check passes. JsonUtility ignores the unknown `ignored` object, so the actual DTO has missing scene/position/health fields supplied as defaults. The nonnegative and finite checks accept these defaults. `TryLoad` therefore selects the incomplete primary instead of recovering the valid backup. A subsequent normal save also classifies this primary as valid (`AtomicFileStore.cs:42`) and replaces the good backup with it (`AtomicFileStore.cs:47`), losing the previous recoverable data.

Required correction: verify required fields are actual top-level properties with the expected scalar types before treating the deserialized legacy DTO as valid. Preserve the current DTO/schema. Add an adapter-level regression using a nested-field payload and a valid backup; assert fallback selects the backup and writing over the invalid primary preserves it. Existing Core tests use equality against two hardcoded strings, so they do not exercise this validator.

No Critical finding identified. The reviewed Core implementation follows the intended temp/flush/readback/replace sequence, returns typed failures, ignores temp files during recovery, protects overlapping same-slot operations across store instances, and deletes all three candidates on successful deletion. Core and test assembly dependency direction matches the brief.

## Evidence boundary

Inspected `atomic-storage-red.json` (12 executed tests with expected failing baseline) and `atomic-storage-green.json` (13/13 passed, including flushed-temp corruption). These are coordinator-produced Unity EditMode evidence; this reviewer did not rerun Unity or execute the adapter reproduction. The validator finding follows from the actual source, missing-field JsonUtility behavior explicitly acknowledged by the implementation, and the concrete payload above.

Host/UI integration and Windows x64 gameplay acceptance are future Task 2–4 work and remain unvalidated by this Task 1 review. Their absence is not reported as a defect in the current storage task.
