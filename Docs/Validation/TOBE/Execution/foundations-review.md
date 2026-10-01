# Foundations independent review

Scope: storage re-review after the root-property/provenance correction, and initial 01B Task 1 capture-contract review. Read-only implementation review; only this report was created. No Unity/MCP execution, saves, scenes, commits or runtime-host/save-slot/WorldSave review performed.

## Storage — approved within reviewed scope

The previous Important finding in `storage-review.md` is resolved. `SaveFileDataWriter.cs:73-86` parses an actual root object, rejects duplicate properties, requires root properties with the expected scalar types, bounds integer/float conversions, and checks the deserialized DTO. Nested required fields no longer substitute for absent root fields; primitive/array roots fail parsing and are rejected through Core's validator exception boundary (`AtomicFileStore.cs:81-85`). Optional legacy primitives are type-checked when present without requiring new schema fields.

`AtomicFileStore.cs:25-30` selects the valid backup after an invalid primary and reports `RecoveredFromBackup`; `SaveFileDataWriter.cs:22` now preserves that message in the typed DTO result. `AtomicFileStore.cs:42-47` reuses the same corrected validator before deciding whether a primary can replace the backup. Invalid nested-field primaries therefore cannot overwrite the valid backup during a new save.

Inspected the actual adapter fixture source, `legacy-save-writer-green.json` (2026-10-01 18:34:03 UTC, completed/passed/cleanedUp, 12/12), including nested rejection, backup fallback with provenance, and exact backup preservation on overwrite. Inspected `atomic-storage-reviewed-fix-green.json` (Core 13/13, zero failures/skips). These are coordinator-run Editor evidence; this reviewer did not rerun them. No remaining Important/Critical finding identified in this storage scope. Integration and standalone acceptance remain separate pending work.

## Performance 01B Task 1 — changes required

### Important — a terminal stall is omitted while the capture remains comparison-valid

Locations: `Assets/DevTools/Runtime/Performance/PerformanceCaptureController.cs:147-168,176-190`; `PerformanceCaptureData.cs:58-60`.

Concrete trigger: in standalone, with matching providers and stable conditions, record normal samples through elapsed 149 seconds, then incur a multi-second main-thread stall; the next Update runs at 154 seconds. `IncludesFrame(149,154)` is false, so that five-second interval is absent from both the raw frame list and computed statistics. Update ends the run, and Finish marks `fullWindowCompleted=true` solely because elapsed is at least 150 and at least one earlier sample exists. Consequently `comparisonValid` can be true despite missing the slow interval that intersected the measured window. A more severe stall shortly after the first captured frame can certify a nearly empty measured series. This is a capture-contract defect, independently of the pending standalone baseline tasks.

Required correction: explicitly account for measurement coverage and the terminal straddling interval. Preserve the fixed warm-up/window contract, but prevent a materially incomplete sample window from receiving comparison validity; retain enough raw boundary information to explain excluded intervals. Add deterministic coverage of a terminal stall and a normal boundary crossing, rather than only checking the IncludesFrame predicate and elapsed-time threshold separately.

### Important — provider failures bypass automatic termination and leave native recorders active

Location: `Assets/DevTools/Runtime/Performance/PerformanceCaptureController.cs:174-177`.

Concrete trigger: an injected camera provider references a camera destroyed during a scene transition and throws `MissingReferenceException`, or either delegate throws an ordinary exception. The exception leaves Update before updating `previous` or checking the 150-second end condition. If the provider keeps throwing, each subsequent Update repeats the exception, `IsCapturing` remains true and owned native recorders remain active indefinitely until someone calls End or disables/destroys the controller. Transient exceptions can also omit the condition-validation step without recording any result error.

Required correction: contain provider/sampling exceptions, record the failure, mark the capture invalid, and stop/dispose deterministically. An exceptional route harness must not bypass the fixed-duration stop or produce an apparently valid result. Add a throwing-provider regression that establishes the invalid result and zero owned recorders. This does not require claiming actual lifecycle callbacks or standalone timing evidence complete.

No Critical finding identified.

## Performance evidence and remaining boundaries

Reviewed runtime controller/data, Editor fixture, Task 1 brief, TO-BE 5.7/V5, `performance-capture-report.md`, and `performance-baseline-index.json`. Inspected `performance-capture-edit-green.json`: completed/passed with 124 passing cases, actual PlayMode disable/destroy flags false. Fixture validates artificial boundaries/statistics, missing-counter null, actual counter enumeration/handle ownership, immediate End cleanup, output parsing and non-overwrite; it does not execute Update sampling or a 150-second measured capture, so it cannot detect the two findings above.

Positive static checks: exact descriptor enumeration with ambiguous-name rejection; unavailable metrics remain false/null; scoped streams and exclusive run/output files; preallocated primitive sampling buffers with DTO/serialization after measurement; observed resolution/quality/render scale/VSync/cap/hardware metadata; provider-presence/focus/settings tracking; ordinary non-QA Release rejects Begin. No production scans or automatic activation introduced.

Actual counter/sample correspondence, allocation/recorder overhead, true OnDisable/OnDestroy callbacks, standalone durations and Release timing remain pending as honestly reported. Tasks 2–4 route/hardware/rendering/memory work are not required to be completed to resolve this Task 1 review. The report contains older pending-GREEN prose followed by the timestamped GREEN result, but its final evidence statement and index do not claim a measured baseline.
