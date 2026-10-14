# Upstream Branch, Issue and Pull Request Status

**Snapshot:** 2026-10-14  
**Repository:** [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)  
**Research branch base:** ApexForge-devz/unity-mcp beta at 142ea0af4890337f0ff6658df61db23982313717.

## 1. Branch relationship — direction clarified

- Target/default branch is beta, with HEAD 142ea0af4890337f0ff6658df61db23982313717 (merge PR #1452; Unity package 10.3.1-beta.7, committed 2026-10-08).
- Upstream main HEAD at this snapshot is aa5fc638d623f56178d50329d9ad8c541a57fe66 (merge PR #1425; v10.3.0 release, committed 2026-10-04).
- GitHub compare with base=beta, head=main reports status behind, 0 ahead and 115 behind. In this direction that means **upstream main is 115 commits behind beta**. The research branch correctly starts from beta; this is not an instruction to rebase it onto main.
- The target beta and upstream beta had identical HEAD at this snapshot. The target is a source-identical upstream mirror on its public beta branch.

Useful refs:
- [Target beta HEAD](https://github.com/ApexForge-devz/unity-mcp/commit/142ea0af4890337f0ff6658df61db23982313717)
- [Upstream main HEAD](https://github.com/CoplayDev/unity-mcp/commit/aa5fc638d623f56178d50329d9ad8c541a57fe66)
- [Upstream release PR #1425](https://github.com/CoplayDev/unity-mcp/pull/1425)

## 2. Cross-branch source comparison for the current backlog

The following paths were fetched from upstream main. For the critical files below, the blob SHAs match the reviewed beta versions unless noted.

| File | main blob SHA | Current assessment |
|---|---|---|
| Server/pyproject.toml | 83800f55c65984102b61a035613a2ca40459eb91 | Same FastMCP upper bound <4 as beta; no sign of the protocol migration in this dependency file |
| Server/src/services/tools/batch_execute.py | 2c717d9c45cc25b2be878bda7eb76db580becdfb | Same batch schema, including parallel/max_parallelism inputs |
| Server/src/services/tools/manage_tools.py | b8875e4d5fd3673c26063c26681b31fb43703fd3 | Same per-session tool visibility design |
| MCPForUnity/Editor/Tools/BatchExecute.cs | aca4a1994a4dd2857e132b9f068146bc4d8960ff | Same sequential Unity main-thread behavior |
| MCPForUnity/Editor/Services/AssetGen/Import/SafeZipExtractor.cs | b5ef2b3c00e412c124d0bf76e6524d1b2e52d2dd | Same inspected path containment/extension filtering; no archive expansion quotas evident |
| MCPForUnity/Editor/Tools/ManageAsset.cs | 69aaf53cc93a0b638d5a10139f4751dc42c13787 | Same GetAssetData dependencies metadata TODO |
| MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs | 0876fdb121755c4bd4d71b1f6219a00f4bf199b8 | Same three Frame Debugger actions in the reviewed handler |
| MCPForUnity/Editor/Tools/ExecuteCode.cs | main: bef08d49a7e661181780e04694be466fd2a2a5e7; beta: 10325861e52def46cc72fe7cb2d5d4e28ffbb828 | Three assembly-load compatibility call sites differ. This does not resolve the caller-controlled safety_checks or establish a general sandbox |

This mapping means the high-priority contract/security/resource-limit issues described in the backlog are not obviously fixed simply by comparing the current main tree to beta. The reviewed high-impact source files remain materially identical; ExecuteCode differs only in the selected assembly load/path compatibility calls in the inspected line comparison.

## 3. Upstream open proposals and related issue history

### PR #1466 — AssetBundle explorer (open at snapshot)

[feat(asset): explore built AssetBundle contents](https://github.com/CoplayDev/unity-mcp/pull/1466), created 2026-10-11 and last updated 2026-10-13, was open at the time of inspection.

The proposal adds read-only manage_asset actions:
- list_asset_bundles, to enumerate likely built AssetBundle files/directories without loading bundles;
- get_asset_bundle_info, to inspect name, size, asset/scene counts and contained names, and unload only the bundle it opened.

The PR body says paths are project-restricted, symbolic links are not followed, results are paginated (default 25, max 200), common non-file entries are excluded, and optional AssetDatabase updates are avoided. The PR author marks Python tests and Unity 6000.3.20f1 tests as tested. These claims were not independently rerun here.

**Backlog implication:** track this proposal before designing another AssetBundle explorer. Do not conflate it with the separate AssetDatabase dependency/reference graph idea: listing a built bundle and finding reverse dependencies of project assets are different operations. Re-check merge state before implementing either.

### Issue #1438 — execute_code file-delete safety granularity (open)

[Path allowlist for execute_code destructive file ops](https://github.com/CoplayDev/unity-mcp/issues/1438), opened 2026-10-05, says the current safety check blocks System.IO.File.Delete outright and offers either a project-path allowlist for selected generated folders or clearer guidance around the all-or-nothing safety_checks=false switch.

**Backlog implication:** this is adjacent to the broader trust-boundary concern in P0.2 but not a complete security-policy solution. A path allowlist may improve routine build cleanup while retaining restrictions on Assets/out-of-project paths; it does not sandbox arbitrary in-process C# or fix remote/unattended authorization. Evaluate the issue/proposed UX before designing overlapping settings. The issue is a user request, not proof a patch is implemented.

### Issue #709 — disabled tools visible in clients (closed historical issue)

[Disabled Tools Reappear and Still Show Up in Client Lists](https://github.com/CoplayDev/unity-mcp/issues/709) was opened 2026-02-10 and closed 2026-02-12 against v9.4.0. It reports disabled tools returning after restart and still appearing in client tool lists.

**Backlog implication:** this is historical, and it must not be reported as an unresolved bug in the current v10.3.1-beta.7 branch without reproduction. It demonstrates that tool visibility/persistence has been a real compatibility area before; the 2026 protocol change presents a new, standards-level constraint that requires its own tests.

### PR #1470 — no-Unity-session recovery message (open)

[Recovery guidance when no Unity session is available](https://github.com/CoplayDev/unity-mcp/pull/1470) was open at snapshot and is based on the 142ea0af beta commit. It improves the error message/troubleshooting guidance without changing the underlying routing lifecycle. Its PR body states 1,575 Python tests passed and 18 were skipped, but this is author-reported evidence and was not independently run here.

**Backlog implication:** editor-liveness work should complement actionable state/error categories, not replace existing no-session responses and recovery guidance. Re-check whether this PR is merged before editing the same response path.

## 4. Research rule for upstream proposals

Before implementing any feature in this research branch:
1. Check whether it is already in target beta.
2. Check upstream open/merged PRs and recent commits after the current beta base.
3. If a PR exists, assess its behavior, test coverage and risks rather than duplicating it blindly.
4. Keep upstream PR test claims labeled as claims unless CI logs or local test execution independently verify them.


## 5. Additional open PRs reviewed on 2026-10-14

The repository's open-PR list was fetched again. These proposals are useful for avoiding duplicate feature plans and for understanding recurring reliability problems:

| PR | Topic | State at snapshot | Relevance |
|---|---|---|---|
| [#1472](https://github.com/CoplayDev/unity-mcp/pull/1472) | Custom tools with required parameters after optional parameters | Open | Python 3.14 registration/schema regression; add version-specific registration tests if not already fixed upstream |
| [#1468](https://github.com/CoplayDev/unity-mcp/pull/1468) | Preserve custom stdio launcher and environment during auto-configuration | Open | User config is state that automation should merge/preserve, not overwrite |
| [#1465](https://github.com/CoplayDev/unity-mcp/pull/1465) | Move Editor window focus-refresh I/O off main thread | Open | UI responsiveness issue separate from command-dispatch liveness |
| [#1463](https://github.com/CoplayDev/unity-mcp/pull/1463) | Wait for Play Mode startup, then run code | Open | Correct readiness boundary after domain reload/scene Start |
| [#1462](https://github.com/CoplayDev/unity-mcp/pull/1462) | Prefab instance override inspection/apply/revert | Open | Common source of scene-vs-prefab mismatch; scoped writes and rollback deserve tests |
| [#1461](https://github.com/CoplayDev/unity-mcp/pull/1461) | Append a user-provided instructions file | Open | Project-specific context can be configuration rather than hardcoded generic instructions |
| [#1460](https://github.com/CoplayDev/unity-mcp/pull/1460) | Test-run initialization and cleanup after timeout | Open | Failing MCP job does not necessarily mean Unity test operation/lock has been cleaned up |
| [#1459](https://github.com/CoplayDev/unity-mcp/pull/1459) | Asset-search performance/path validation | Open | Direct overlap with target draft PR #1 |
| [#1417](https://github.com/CoplayDev/unity-mcp/pull/1417) | Native Editor-window screenshots | Open | Target draft PR #1 contains files byte-identical to this contributor PR's C#/Python/test/docs files |
| [#1350](https://github.com/CoplayDev/unity-mcp/pull/1350) | Modal-dialog reporting and answering | Open | Direct conceptual overlap with target draft PR #1; target source is longer and materially different |
| [#1286](https://github.com/CoplayDev/unity-mcp/pull/1286) | Session cleanup and deduplicate tool notifications | Open | Relevant to modern protocol migration, connection cleanup and avoiding repeated list-change notifications |
| [#1285](https://github.com/CoplayDev/unity-mcp/pull/1285) | Scope HTTP endpoint/server ownership per project | Open | Relevant to cross-project isolation and instance routing |
| [#1194](https://github.com/CoplayDev/unity-mcp/pull/1194) | Auto-select Unity instance by launch directory/worktree | Open | Could reduce repeated instance selection but must fail closed if identity remains ambiguous |

PR statuses and descriptions are point-in-time. Before porting, inspect current status, head SHA, patch, relevant tests and actual CI state. A green workflow umbrella can contain skipped Unity/E2E jobs; inspect individual jobs rather than treating every run with a success conclusion as full runtime validation.


## 6. Additional active proposals — source and test implications

These open PRs were rechecked on 2026-10-14 and their main changes were inspected through per-file patches. Their state can change after this snapshot.

### PR #1463 — Wait for Play Mode startup and execute code once ready

[PR #1463](https://github.com/CoplayDev/unity-mcp/pull/1463) distinguishes “Editor entered Play Mode” from “scene initialization is complete.”

Source reviewed: the new PlayModeStartupService.cs, Python startup orchestration, and ManageEditorStartupTests.cs, all linked from the [PR file view](https://github.com/CoplayDev/unity-mcp/pull/1463/files).

The design gives manage_editor(action="play") optional wait_until=scene_loaded|first_frame, on_ready_code and bounded wait_timeout. Unity retains startup jobs in SessionState across domain reload; code marked executing at reload is failed as interrupted and not replayed. The Python layer generates a request ID, receives or reconstructs a job ID, polls for completion and tells callers not to resubmit code after a lost reply. Tests cover invalid options, timeouts, request IDs and job lifecycle; the PR says four multi-version checks were skipped because those Unity versions were not installed.

**Research decision:** strong P2 candidate. This fills a distinct lifecycle gap for workflows whose next operation depends on scene Start/first frame; it is not a replacement for generic Editor liveness or the target's current play action. Before adoption, verify CLI and Python REST paths have identical no-resend semantics, startup jobs stay bound to the correct Unity project, domain reload does not duplicate code side effects, and cancellation/Play Mode stop cleans up every job.

### PR #1460 — Test-run timeout and native cleanup ownership

[PR #1460](https://github.com/CoplayDev/unity-mcp/pull/1460) keeps the native Unity Test Runner identity after an MCP initialization timeout rather than declaring the operation finished while Unity is still executing or cleaning up.

Source reviewed: TestJobManager.cs and TestJobManagerLifecycleTests.cs in the [PR file view](https://github.com/CoplayDev/unity-mcp/pull/1460/files).

The patch tracks the native Unity run GUID and states such as initialization timed out, native run started, and native cleanup pending. It disables Editor interaction throttling before native test execution in both modes and restores captured settings even when startup throws. After timeout it keeps ownership, registers cleanup callbacks across domain reload, and blocks a new test run until the old native run is actually cleaned up; late callbacks are prevented from overwriting the timeout failure.

**Research decision:** prioritize as a high-value reliability fix if current beta can reproduce the failure. It addresses a concrete failure mode where an MCP job times out but Unity's test run or operation lock remains live. Validate EditMode and PlayMode, timeout before/after native RunStarted, domain reload, native errors, user cancellation, and that preferences/locks are restored before another job starts. The PR's test claims are author-reported; no Unity tests were run during this research session.

### PR #1465 — Keep Editor-window focus refresh off the main thread

[PR #1465](https://github.com/CoplayDev/unity-mcp/pull/1465) moves slow connection, executable and client-config checks away from the Editor UI thread.

Source reviewed: EditorAsyncRefresh.cs, EditorAsyncRefreshTests.cs and WindowFocusRefreshTests.cs in the [PR file view](https://github.com/CoplayDev/unity-mcp/pull/1465/files).

The helper runs bounded work on a worker, applies results back from EditorApplication.update, coalesces duplicate focus requests, caches successful results for a TTL, uses a generation/request key to discard stale results, and stops UI updates for detached windows. Tests model slow I/O, request coalescing, TTL reuse, superseded values and window-detach behavior.

**Research decision:** worthwhile Editor UX/reliability work, complementary to command bridge liveness. The code must keep all Unity APIs and UI writes on the main thread, use only thread-safe filesystem/process/network operations in the worker, and avoid stale completions rewriting configuration or connection status after settings change. Review Task.Run work for hidden Unity dependencies and test Editor shutdown/domain reload while work is pending.

### PR #1468 — Preserve manual client configuration

[PR #1468](https://github.com/CoplayDev/unity-mcp/pull/1468) prevents automatic configuration checks and package migrations from overwriting user-maintained stdio launch commands, environment variables, working directories or wrapper executables.

The patch validates the args field as a string array, preserves custom launch commands when determining transport, and reuses common JSON entry/URL helpers instead of independently rewriting config. The PR describes tests for custom launch commands, invalid argument shapes and configuration migrations.

**Research decision:** a practical P1 data-loss prevention item. Treat client configuration as user-owned state: prefer narrow, atomic merges; validate before writing; preserve unrelated fields and formatting where feasible; and require explicit action for “replace with generated configuration.” Test backup/rollback behavior and every supported client layout.

### PR #1285 — Scope local endpoint and server ownership by Unity project

[PR #1285](https://github.com/CoplayDev/unity-mcp/pull/1285) addresses machine-global EditorPrefs contamination when multiple projects/worktrees run Unity. Its changes scope local endpoint/transport, PID/port/PID-file and instance token by project hash, add a process-level UNITY_MCP_HTTP_URL override, and migrate prior global settings atomically under a cross-process mutex. It restricts launch URL schemes to HTTP(S) and retains machine-local settings rather than writing endpoint values into the project.

**Research decision:** high priority if confirmed absent in current target. Compare the PR against ProjectIdentityUtility, EditorConfigurationCache, PID/server manager and HttpEndpointUtility before implementing overlapping storage. Add two-project/two-worktree tests, migration conflicts, inherited environment overrides, invalid URL schemes and crash/restart ownership recovery. Request Unity-instance identity and process-owned endpoint are related but distinct.

### PR #1194 — Select Unity instance by launch directory / MCP roots

[PR #1194](https://github.com/CoplayDev/unity-mcp/pull/1194) uses the client launch directory (or advertised file:// MCP roots) to choose a matching Editor project when several sessions are connected. It parses UNC paths, Windows drive URIs and percent encoding; normalizes the stdio Assets path to project root; and caches roots per client session. A missing or ambiguous match falls back to asking rather than choosing a random Editor.

**Research decision:** useful improvement to multi-instance routing if current selection relies mainly on an explicit instance or a single running Editor. Keep a fail-closed ambiguity rule. Test symlinks/real-path normalization, Windows UNC and drive URIs, macOS/Linux paths, roots inside Assets, unrelated nested repositories and two Unity worktrees with the same project name. Inspect client-root support before making it a required path, and ensure cache keys do not leak roots across tenants.

### PR #1462 — Prefab instance overrides

[PR #1462](https://github.com/CoplayDev/unity-mcp/pull/1462) adds list_overrides, apply_overrides and revert_overrides for a selected prefab instance. It distinguishes property/component/GameObject additions and removals, normally excludes Unity default overrides, validates prefab lineage and narrows writes to a target. Ambiguous same-name siblings are rejected; instance-ID targeting is limited to that object; array child selectors do not widen to the whole array. Some removed-GameObject override APIs are reflected and report whether the operation is available on the current Unity version.

**Research decision:** potentially high-value workflow for diagnosing “changed prefab asset but scene still looks unchanged.” Keep as an upstream candidate rather than developing a competing interface now. Before adoption, check exact selection semantics, Undo behavior, prefab-stage contexts, added/removed object operations, and Unity-version behavior for unavailable APIs.

### PR #1472 — Required custom-tool parameters after optional parameters

[PR #1472](https://github.com/CoplayDev/unity-mcp/pull/1472) fixes custom tool registration by declaring dynamic function parameters keyword-only, matching the handler's kwargs calling convention. The author reports 1,577 Python tests passing plus a real FastMCP registration smoke where optional limit=10 preceding required action preserves schema requiredness and defaults.

**Research decision:** narrow compatibility fix with good regression-test value. Re-check merged state before porting; keep a Python-version and FastMCP matrix and tests verifying the visible schema excludes injected context while preserving parameter type, ordering, default and requiredness.

### PR #1461 — File-backed server instructions

[PR #1461](https://github.com/CoplayDev/unity-mcp/pull/1461) adds additional instructions from a UTF-8 file through --instructions-file or UNITY_MCP_INSTRUCTIONS_FILE, appending them to built-in instructions for stdio and HTTP. The description says missing/unreadable/invalid UTF-8 files fail with a configuration error without printing file content; default instructions remain unchanged for an unset or empty file.

**Research decision:** useful opt-in for project-specific conventions and workflows. Risks are shared-server scope (one instructions file may affect several projects/users), untrusted instructions if a path is configured through an untrusted environment, and accidental secret inclusion. Define precedence, permissions, size limits, content trust and sharing behavior before implementing this pattern.

## 7. Relationship to existing target draft PR #1

The target draft PR already covers some work that appears in upstream PRs #1459, #1417 and #1350. Other newly reviewed proposals are distinct:
- #1463: explicit startup readiness and one-shot post-startup work.
- #1460: native test-run cleanup ownership after MCP timeout.
- #1465: nonblocking Editor UI refresh.
- #1468: preserve user-configured client launchers.
- #1285 / #1194: per-project endpoint scoping and client-worktree-aware instance selection.
- #1462: prefab override inspection.
- #1472: custom tool signature robustness.
- #1461: project/user instructions loaded at startup.

Before building anything, re-check open/merged states and compare relevant files against the current feature/unity6-enhancements head, beta baseline and contributor branch. Don't equate “PR exists” with “feature is in beta.”


## 8. Additional patch-level review — MCP↔Unity proposals (2026-10-14)

Added [RECENT_UPSTREAM_PR_REVIEW_2026-10-14.md](RECENT_UPSTREAM_PR_REVIEW_2026-10-14.md) after re-checking target and upstream `beta` heads. Both resolved to `142ea0af4890337f0ff6658df61db23982313717`; these changes are proposals, not already-present target behavior at this snapshot.

New patch-reviewed areas include #1380 (shared local HTTP server lifetime), #1459 (lazy asset-search hydration and path/page validation), #1469 and #1472 (argument normalization and dynamic tool signatures), #1455 (C# wrapper aliases and diagnostics), #1448 (optional MCP console-log filtering), #1135 (Undo-safe GameObject deletion), #1450 (fixed Game View dimensions and render-target readback), and #1464 (exact optional package versions).

The review separates in-scope MCP↔Unity reliability from lower-priority Editor convenience work and records testing gaps. None of these proposals were assumed to be merged. Re-check status and compare landed code before implementation; do not duplicate a feature that has arrived in beta.


## 9. PR #1380 lifecycle ownership re-check

Patch-level review was deepened against target beta's actual PID/handshake implementation and the proposed branch. See [SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md](SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md). Two separate findings now supersede a simple “high priority to verify” note:

1. The target stores global EditorPrefs handshake/tracking keys despite keeping PID files under each project's Library directory. This permits a different Editor to resolve and potentially stop the last stored managed server.
2. PR #1380's SessionState launch marker prevents borrowed Editors from stopping an owner’s process, but can lose all cleanup ownership if the original launcher exits before the last borrower. A bounded instance-count probe also does not eliminate the attach-between-count-and-stop race.

**Decision:** adapt the project/port state isolation and fail-safe unknown-count policy, but do not port the proposal unchanged. Require transfer/adoption or server-side idle cleanup plus a two-Editor lifecycle test matrix.


## 10. PR #1469 — one-layer argument normalization and final-route safety

The deeper review is recorded in [ARGUMENT_NORMALIZATION_REVIEW.md](ARGUMENT_NORMALIZATION_REVIEW.md). Target-beta source confirms a typed Literal schema for Read Console action/format, no matching decoder in the inspected C# handler, and a menu-path blacklist in ExecuteMenuItem.cs. The upstream patch adds a pre-validation decoder, explicit allowlist checks, Unity-side decoding before the menu blacklist and tests for malformed/nested inputs.

**Decision:** adapt narrowly. The critical invariant is one-layer decoding at an opted-in boundary, followed by typed validation before dispatch. Do not decode arbitrary strings globally. Run the actual FastMCP integration test plus Unity EditMode tests before calling the behavior validated.


## 11. PR #1459 — asset search pagination reviewed against actual handler

The detailed comparison is now in [ASSET_SEARCH_PAGINATION_REVIEW.md](ASSET_SEARCH_PAGINATION_REVIEW.md). Target SearchAssets hydrates every match with GetAssetData, then applies Skip/Take; invalid folder scopes silently become null/global scope. PR #1459 moves asset hydration into the requested index window, rejects invalid scopes, validates page values and adds EditMode assertions that off-page assets remain unloaded.

The optimization is meaningful but bounded: FindAssets still materializes the full GUID array and the loop remains O(N) when computing exact total count and applying date filters. The accurate adoption claim is page-bounded asset-load/preview work, not fully bounded or constant-time search. Benchmark on large projects before claiming speedup; re-check open PR state before implementing.
