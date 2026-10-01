# 01A Task 4 — QA root policy and build menu

## Scope

This preparatory slice owns `SaveRootPolicy.cs` and its `.meta`, `SaveRootPolicyTests.cs` and its `.meta`, and the QA Release menu addition in `UnityMigrationAudit.cs`. Existing Core and EditMode test assemblies are reused. No `WorldSaveGameManager` changes, commits, build launches, personal-save reads/writes, or Unity MCP calls were made by this worker. Consumer integration and V4 are coordinated separately.

## Policy

`SaveRootPolicy.Resolve(persistentRoot,args,qaEnabled)` is pure and returns `SaveResult<string>`. Distribution Release (`qaEnabled=false`) ignores override arguments entirely. An explicit QA override must be a fully qualified path and different from the real persistent root after full-path canonicalization, separator normalization, trailing separator removal, and Windows case-insensitive comparison. Relative, drive-relative, rooted-without-drive, malformed, missing-value, and duplicate arguments return `SaveError.InvalidData`. Distinct absolute drive and UNC paths are accepted. Canonicalization preserves the separator on filesystem roots. No directories are created by resolution.

Ruling: `qaEnabled` means override capability, rather than mandatory test mode. Normal Editor/Development calls without an override therefore use `persistentRoot`. The initial `QaRequiresExplicitRoot` test was replaced with `QaWithoutOverrideUsesDefault` following the coordinator's specification ruling. Dedicated `KARAWARA_QA` Release must require the argument at its consumer before permitting save access; this slice does not yet implement the consumer. Cost if omitted: dedicated QA could fall through to the personal root.

## Builder

`Karawara/Validation/TOBE/Build QA Release` invokes `BuildPlayerOptions` with Windows x64, `BuildOptions.None`, and `extraScriptingDefines={KARAWARA_QA}`. Output is `Builds/TOBE-QA/WindowsRelease/Karawara.exe`; its own report is `Docs/Validation/TOBE/01a-qa-release-build.json` with defines, scenes, timestamps, result, errors/warnings, bytes, and build diagnostics. The final report is written even after a caught build exception. Existing ordinary Release builder code is unchanged.

Ruling: build-local extra defines satisfy the configuration-restoration requirement without setting shared/global symbols or development settings, so there are no edited settings to restore after success or failure. Global `ProjectSettings.asset` was inspected and contains no `KARAWARA_QA` definition.

## Test evidence and limits

- Coordinator confirmed RED job `f4e737211b034c6ea25a24573c3af4c2`: 16 cases, 14 expected QA behavior failures and 2 passing ordinary Release cases against the default-root stub.
- Coordinator confirmed GREEN job `9cbfec85937f486f9406a0c2a2106c9e`: 29/29 passed, 0 failures, 0 skipped in 1.189 s (16 policy cases plus 13 atomic-storage cases). The amended no-override capability test follows the ruling above.
- Useful additional edges included: drive-relative/root-relative paths; same personal root via case, slash, trailing separator and `..`; absent override value; empty/whitespace/invalid-character values; duplicate override; UNC root; and malformed override ignored by ordinary Release. Tests do not access personal saves or require existing directories.
- Worker static check: `git diff --check -- Assets/DevTools/Editor/UnityMigrationAudit.cs` returned exit 0, with only Git's existing LF-to-CRLF advisory.
- QA standalone build, consumer integration, player compilation, process A/B round trips, corruption/delete/write-failure flows, and real-save hash comparison are pending separate coordinated verification. This report does not close G1A or establish standalone behavior.
- Root equality is lexical canonical-path equality. Filesystem aliases such as directory junctions/symlinks are not inspected; QA directories must be separate actual locations selected by the coordinator.
