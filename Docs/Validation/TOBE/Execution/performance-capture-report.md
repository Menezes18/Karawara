# Performance capture — 1B Task 1

Scope: isolated QA contract and fixture only. No gameplay, scene, quality or renderer settings changed. No runtime baseline captured; no route coordinates or samples invented.

## Fixture

In Unity 6000.4.6f1 run `Karawara/Validation/TOBE/Validate Performance Capture` (static entrypoint `RPGKarawara.DevTools.Editor.PerformanceCaptureValidation.Run`). Result: `Docs/Validation/TOBE/Execution/performance-capture-fixture-result.json`. Coordinator owns Editor refresh/menu execution. RED fixture uses reflection so absence of the runtime implementation is a behavioral failure rather than a compiler failure.

Checks: synthetic 30-second warm-up excluded including straddling frames; capture window ends at 150 seconds; nearest-rank median=10 and p95=30 for [10,10,10,20,30]; IDs unique; absent metric available=false/value=null; parseable output; unique-file protection; ten Begin/End cycles leave no owned profiler recorders.

## Capture protocol

Windows x64 standalone is acceptance platform. Record commit/build ID, build mode, machine CPU/GPU/RAM/OS, actual resolution, render scale, quality, VSync, target FPS, focus, route, enemy count and camera. Explicit QA activation is required. Use 30 seconds warm-up then 120 seconds capture; repeat three times under identical conditions. Disable Deep Profile. Development captures attribute cost; Release timing must be measured separately without this development-only instrumentation. Default proposed conditions: 1920x1080, render scale 1.0, current High Fidelity; this tooling observes settings and never applies them.

Route setup remains pending for Tutorial_Final/MapaEsboco exploration, t combat, CucaLoad, menu/skills/minimap and Freeflow. Exact start/end positions, input sequence, save copies and fixed camera/enemy conditions require gameplay observation in Task 2. Frame Debugger, matched screenshots and memory snapshots remain Tasks 3/4. Missing counters remain unavailable and do not imply zero cost. No conclusion applies to hardware not measured.

## Evidence and limitations

RED/GREEN Editor execution: pending coordinator. Real 150-second standalone runs, counter support by platform, route reproducibility, Release timing and hardware comparison: pending. Artificial fixture statistics are contract evidence only.

Implementation details: `Begin` accepts only explicit `qaEnabled` and the fixed 30/120 durations; config identity fields commit/build/route/camera and outputRoot are mandatory. Run IDs combine a sanitized requested prefix with a GUID. An exclusive `.run` claim and `FileMode.CreateNew` for JSON/CSV/inventory protect prior captures. All streams are scoped; End/disable/destroy dispose owned recorders. No counters are guessed: first export inventory through Begin/End, then pass exact unique descriptor names as `counterNames`. Ambiguous or missing names and unsampled supported counters are unavailable/null; CSV uses an empty value cell. Units are the exact Unity descriptor units; nanoseconds are not silently relabelled as milliseconds. Frame time uses monotonic real-time intervals in milliseconds.

`EnemyCountProvider` and `CameraIdProvider` are optional injected delegates owned by the route harness. They must report actual conditions without allocations; no production object scans are introduced. Without both, `populationVerified`/`cameraVerified` are false and `comparisonValid` is false. Focus loss or resolution/quality/render-scale/VSync/cap/population/camera changes invalidate comparison. Editor evidence never receives standalone comparison validity. Output failures also invalidate returned results.

Sampling buffers allocate before the 30-second warm-up; Update writes primitive buffers and constructs no frame/metric DTOs. Serialization and DTO materialization occur after the interval. The fixed 120000-frame capacity supports up to 1000 fps over 120 seconds; saturation ends invalid. `captureBufferBytesEstimate` reports managed payload overhead (17 bytes/frame plus 16 bytes/frame/requested supported recorder, excluding array headers and inventory). Memory captures include this QA buffer allocation and native recorder overhead: separately measure/account for instrumentation rather than attributing it to gameplay. Profiler/Frame Debugger snapshots and Release confirmation remain necessary. `KARAWARA_QA` enables a dedicated QA Release; ordinary non-development players reject Begin.

Available machine reported by coordinator: Ryzen 7 5700G / RTX 3060, driver 32.0.16.1088. This is hardware inventory, not measured performance.

RED executed by coordinator at 18:20:32 UTC: `passed=false`, implementation missing; preserved `performance-capture-red.json`. GREEN is awaiting coordinator after typed fixture and buffer implementation.

Fixture correction after first GREEN attempt: all contract/output/ten-cycle cases passed, but the EditMode disable assertion failed because ordinary MonoBehaviours do not receive those lifecycle callbacks there. Fixture now proves explicit End cleanup in EditMode and marks actual disable/destroy proof pending; running the same fixture in PlayMode tests real OnDisable and destruction cleanup separately. The fixture finally block explicitly calls End before destroying the temporary component to release native recorders even after an assertion fails. No ExecuteAlways behavior was added.

Editor GREEN evidence (coordinator): 2026-10-01 18:32:42 UTC, `performance-capture-edit-green.json`, completed=true/passed=true, 124 assertions passed. This includes 10 Begin/End cycles, real enumerated counter ownership during supported cycles, zero owned handles after each End, parseable JSON and CSV headers, exclusive output IDs, absent-counter null, fixed artificial timestamp window and nearest-rank statistics. Actual PlayMode disable/destruction flags are correctly false in this Editor result; coordinator will run the same fixture in isolated DevPlayground PlayMode. Root owns import/compile and runtime execution. Static review additionally corrected buildMode to distinguish Development from dedicated KARAWARA_QA Release.

Task 1 implementation is delivered for independent review. Runtime 150-second captures, measured counter availability/sampling accuracy, GC/recorder overhead confirmation, route validity and hardware comparisons remain pending. Checkpoint title prepared only: `chore: add isolated performance capture tooling`; no commit created.
