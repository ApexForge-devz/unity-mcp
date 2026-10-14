# Recent In-Scope Upstream Proposal Review — 2026-10-14

**Purpose:** Keep recommendations tied to actual Unity-MCP source and avoid duplicating open upstream work.  
**Target baseline:** `ApexForge-devz/unity-mcp:beta`.  
**Compared upstream:** `CoplayDev/unity-mcp:beta`.  
**Observed baseline SHA:** `142ea0af4890337f0ff6658df61db23982313717` for both heads at review time.  
**Status snapshot:** 2026-10-14. PR states can change; re-check before implementation.  
**Scope:** MCP↔Unity behavior and project-local Unity work only, per [SCOPE.md](SCOPE.md).

This is a source/patch review of selected candidates, not a review of every open PR. No pull request was merged and no code was changed in this research tranche.

## Summary and recommended disposition

| PR | Observed status | Area | Source-review conclusion | Action |
|---|---|---|---|---|
| [#1380](https://github.com/CoplayDev/unity-mcp/pull/1380) | Open; last update observed 2026-09-27 | Shared HTTP server lifetime | Per-Editor launch ownership and a last-connected-Editor shutdown check reduce the risk of one Unity project killing a server used by another. A failed liveness probe deliberately leaves it running. | **High priority to verify**; reproduce with two Editors and test quit/restart before any port. |
| [#1459](https://github.com/CoplayDev/unity-mcp/pull/1459) | Open; updated 2026-10-13 | Asset search pagination | Loads asset metadata/previews only for the requested page and rejects invalid path/page inputs rather than silently searching the whole project. It still enumerates all matching paths to calculate the total, so it reduces hydration and retained results rather than making search sublinear. | **High priority; overlaps target draft PR #1.** Avoid duplicate implementation while checking the branch and merge status. |
| [#1469](https://github.com/CoplayDev/unity-mcp/pull/1469) | Open; updated 2026-10-13 | Tool argument normalization | Adds one-layer decoding for JSON-encoded strings on `read_console` action/format and menu paths. Includes real-FastMCP plus Editor-side regression tests, including invalid and nested-encoded values. | **High-priority correctness candidate**; ensure validation occurs before dispatch/menu execution. |
| [#1472](https://github.com/CoplayDev/unity-mcp/pull/1472) | Open; updated 2026-10-13 | Custom tool registration | Uses keyword-only parameters, matching kwargs-based handler invocation and allowing required parameters after optional ones. Tests cover declaration order, requiredness, annotations and defaults. | **Small, well-scoped fix**; verify Python/FastMCP compatibility and generated tool schema. |
| [#1455](https://github.com/CoplayDev/unity-mcp/pull/1455) | Open; updated 2026-10-10 | C# execution ergonomics | Aliases bare `Object`/`Random` to UnityEngine, preserves explicit `System.Random`, adjusts wrapper line offsets and improves compiler hints. Tests cover compiler routes and line mapping. | **Useful quality fix**; keep wrapper source and reported line numbers aligned across CodeDom, Roslyn and fallback paths. |
| [#1448](https://github.com/CoplayDev/unity-mcp/pull/1448) | Open; updated 2026-10-07 | Console diagnostics | Adds opt-in exclusion of known MCP-prefixed logs before count/pagination while including MCP logs by default. Tests check paging and preserve project messages that merely mention MCP. | **Useful diagnostic improvement**; keep default behavior and treat prefix matching as a heuristic, not trusted provenance. |
| [#1135](https://github.com/CoplayDev/unity-mcp/pull/1135) | Open in snapshot; older proposal | Undo semantics for direct GameObject deletion | Replaces `Object.DestroyImmediate` with `Undo.DestroyObjectImmediate`, plus an Undo round-trip regression test. The baseline still documents its delete path as not undoable. | **Deferred for the target agent workflow**; retain only as general MCP tool-parity work, not as a scene-authoring priority. |
| [#1450](https://github.com/CoplayDev/unity-mcp/pull/1450) | Open; updated 2026-10-09 | Fixed Game View resolution | Adds requested/preset/target/allocated render-size readback and an explicit settled/matches-requested result. Uses reflected Editor APIs and waits up to five seconds for render-target readback. | **Lower-priority visual workflow enhancement**; require version tests and explicit state-change wording. |
| [#1464](https://github.com/CoplayDev/unity-mcp/pull/1464) | Open; updated 2026-10-11 | Optional dependency version selection | Adds exact-version fields and distinguishes an installed package release from detectable native API support; preserves existing dependency selections unless the user changes them. | **Secondary Editor workflow**; never upgrade project dependencies implicitly. |

## Source checks against the actual target baseline

Relevant files were fetched directly from the target's `beta` branch:

- [`ManageAsset.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), blob `69aaf53cc93a0b638d5a10139f4751dc42c13787`: search currently builds result data while traversing matches, then paginates. Invalid folder scope may fall back to a project-wide search. #1459 proposes page-only hydration and explicit input errors. The total count still requires scanning the matches unless the API/data model changes.
- [`ExecuteCode.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs), blob `10325861e52def46cc72fe7cb2d5d4e28ffbb828`: the current wrapper line offset is 10 and the UnityEngine aliases from #1455 are absent. Wrapper generation and diagnostic offsets must stay in agreement.
- [`ReadConsole.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ReadConsole.cs), blob `b4abb5882d5726964c312b945137c794f0b19aa3`: current action/format normalization uses ordinary `ToLower()`; the baseline has no `includeMcpLogs` filter or JSON-string decoder.
- [`GameObjectDelete.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/GameObjects/GameObjectDelete.cs), blob `74382ef499f4f5bb31b6caf8124c6db40c2162e5`: delete calls `Object.DestroyImmediate` and notes that deletion is not undoable.
- [`ManageEditor.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageEditor.cs), blob `6d2b27be7e933aee02627355b8209a696e65fd63`: fixed Game View size actions from #1450 are absent.

The repository baseline has not advanced merely because a pull request exists. Treat each as a proposal until its behavior is visible in beta. After a merge, re-audit the backlog instead of duplicating the feature.

## Patch-level notes and edge conditions

### #1380 — Shared server lifetime

The design records which port the current Editor launched in process-scoped `SessionState`, keys persistent process-handshake data by project and port, and probes how many other Unity instances remain connected before shutdown. It only stops the managed server when the probe confirms zero other instances; a timeout or malformed response leaves it running. Tests cover zero, positive and unknown counts plus per-port tracking.

This is a sound failure preference: an orphaned server can be cleaned up, while terminating a server still used by another Editor breaks active clients. Still verify races where an Editor quits as another attaches, externally launched servers, endpoint changes, duplicate ports, batch mode and shutdown time limits. A local probe timeout must not block Editor shutdown indefinitely.

### #1459 — Asset search work and input contracts

The change rejects empty/non-folder scope and non-positive page size/number. It counts matches and calls `GetAssetData` only while the match lies in the requested window and the page has room. Its regression fixture creates assets and asserts that non-page assets remain unloaded. That is stronger than checking only returned list length. The scan still needs a total count, so benchmark a large project and record latency, allocations and loaded-asset deltas; do not describe it as constant-time pagination.

### #1469 — JSON-encoded arguments

The useful invariant is **decode at most one JSON-string layer at a given input boundary**. Ordinary strings—including strings containing quote characters—must be preserved; malformed/nested values or unsupported actions must not dispatch. The patch tests the real FastMCP schema separately from Unity EditMode handlers. Extend coverage across aliases and batch dispatch, and ensure blacklists/allowlists validate the final normalized path. Do not change a typed enum/Literal schema into an unrestricted string to compensate for client quirks.

### #1472 — Dynamic custom-tool signatures

Declaring custom parameters keyword-only is consistent with kwargs-based handler invocation and avoids Python's positional default-order restriction. Verify required fields remain required in generated MCP JSON Schema, injected context does not leak into the visible schema, defaults remain typed, and clients that pass all arguments by name interoperate.

### #1455 — C# wrapper diagnostics

The aliases remove ambiguity introduced by importing both `System` and `UnityEngine`, while explicit `System.Random` remains available. Because added `using` lines change generated wrapper length, the line offset must derive from actual wrapper generation and be tested for CodeDom, Roslyn, fallback-to-CodeDom and runtime errors. Suggest Roslyn only when a compiler limitation is the real cause.

### #1448 — Console filtering

The filter excludes messages matching known MCP logging prefixes before count/pagination. This gives the agent more project-console signal when transport logs are noisy, but it classifies messages by text prefix. Keep filtering opt-in, retain MCP logs by default for troubleshooting, and avoid silently treating arbitrary project text beginning with that prefix as definitely generated by the bridge.

### #1135 — Undo-safe delete

The regression pattern should create a GameObject, delete it through the tool, call `Undo.PerformUndo()`, and verify restored state. Run it on the supported Unity matrix because the current implementation's comment records a prior concern about Undo behavior in tests. The upstream test is evidence worth investigating, not proof across every supported Editor.

### #1450 — Game View render-size readback

The result distinguishes requested dimensions, selected preset, Unity's target dimensions and actual allocated render size. That avoids claiming success when GPU/VRAM or Unity constraints produce another size. The patch relies on reflected Editor APIs and expects a visible/repainting Game View; it stores a named preset in EditorPrefs shared across projects and does not restore the previous resolution. Require bounded readback behavior, explicit state-change wording, multi-GameView disambiguation and version-specific tests before treating this as more than a convenience feature.

## Suggested priority order

1. Reproduce and validate shared server ownership with two live Unity Editors (#1380).
2. Track argument-contract fixes (#1469, #1472) and asset-search lazy hydration (#1459), checking upstream status and tests before porting.
3. Audit CodeDom/Roslyn mapping (#1455) and console signal-to-noise (#1448) against project tests. Treat Undo semantics for direct GameObject delete (#1135) as deferred for this agent unless general MCP tool parity is requested.
4. Consider fixed Game View sizing (#1450) after confirming visual-iteration tasks justify reflected-API maintenance.
5. Treat exact optional dependency versions (#1464) as secondary; package changes must remain explicit and preserve current project state.

## Validation limits

This tranche reviewed PR patch content and target source files. It did not execute Python tests, Unity EditMode/PlayMode tests, multi-Editor lifecycle tests or performance benchmarks. PR-reported test counts are not independently verified here.


## Post-review correction — PR #1380 is not safe to cherry-pick without an ownership handoff fix

A deeper source review against target PID/EditorPrefs state and Unity SessionState semantics found an important gap. PR #1380 moves the “this editor launched the server” marker into SessionState and correctly leaves the server running if other connected projects remain. But SessionState is cleared when Unity exits. If the original launcher exits first, it loses the marker; a remaining connected Editor does not have the marker and will not stop the server when it later becomes the last client. This leaves the managed headless process running indefinitely unless another cleanup path exists.

The target beta has the complementary risk: PID/token state is globally stored in EditorPrefs even though PID files are project-local, so a second project may read and act on another project's valid managed-server handshake. The existing PID/token check identifies the server process but is not an ownership proof for the Editor requesting termination.

Detailed code paths, alternative lifecycle design, race concerns, the /api/instances endpoint verification, and required two-Editor tests are in [SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md](SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md). Updated adoption judgment: **ADAPT, not cherry-pick as-is**. Required lifecycle tests include owner-first-exit/borrower-last-exit, attachment during shutdown, startup on another project's occupied port, external server protection and process-crash recovery.


## Post-review detail — PR #1469 argument normalization

Added [ARGUMENT_NORMALIZATION_REVIEW.md](ARGUMENT_NORMALIZATION_REVIEW.md) after checking the exact target schemas and Unity handlers. The current Read Console Python tool uses Literal actions/formats; the C# ReadConsole handler normalizes but lacks the proposed decoder. Execute Menu Item forwards menu_path and its C# handler checks a case-insensitive blacklist before execution.

The PR's important safety invariant is **decode exactly one supported JSON string layer before validation and before the menu blacklist**, while retaining typed Literal schemas and validating the final value. The patch adds BeforeValidator on Read Console action/format, one-layer decoder helpers on the Python and C# boundaries, and tests for nested strings, malformed/unknown actions, quoted-filter preservation and encoded File/Quit denial. The target baseline tests inspected do not cover encoded scalar action/format or encoded menu paths.

Adoption judgment: **ADAPT**, limited to these explicit fields. Do not turn it into recursive or global JSON-string coercion. Tests must run against the pinned FastMCP dependency, and direct/batch C# routes must validate without relying on Python.


## Deep source review — PR #1459 asset pagination

Added [ASSET_SEARCH_PAGINATION_REVIEW.md](ASSET_SEARCH_PAGINATION_REVIEW.md) after comparing the current SearchAssets/ GetAssetData implementation, Python manage_asset scope normalization and the exact upstream patch/test file.

Confirmed baseline behavior: each match is passed to GetAssetData before Skip/Take, so it loads every result and can create PNG/Base64 previews for off-page assets. An invalid folder path is set to null scope and then searches across the project. PR #1459 calls GetAssetData only for page rows, rejects blank/invalid scope, validates positive page values, uses long offset math and adds tests checking actual loaded asset state across normal, last, beyond-end and date-filtered pages.

Caveat: the PR still holds the FindAssets GUID array and loops all candidates to compute total count/apply the date filter. Its improvement is O(pageSize) hydration/preview work, while candidate discovery/count remains O(N). Treat the patch as a high-value optimization, but require large-project measurements before claiming latency/peak-memory gains. The PR remained open in the 2026-10-14 metadata snapshot; recheck before implementing so we do not duplicate landed work.


## Deep source review — PR #1455 Execute Code diagnostics

Added [EXECUTE_CODE_DIAGNOSTICS_REVIEW.md](EXECUTE_CODE_DIAGNOSTICS_REVIEW.md) after checking target ExecuteCode.cs, compile-diagnostic offset conversion, existing ExecuteCodeTests, and the upstream patch.

The good changes are the UnityEngine Object/Random aliases and changing WrapperLineOffset from 10 to 12 after adding two wrapper lines; the patch adds tests for alias compatibility and an error reported on user line 3 across CodeDom/auto.

Caveat: the proposed CodeDom syntax hint is appended to every CodeDom compilation failure, not only diagnostics indicating a C# language-version problem. Ordinary type errors, missing types and reference errors may receive a misleading hint. Recommend advisory wording (“if the code uses C# 8+ syntax…”) or diagnostic-ID classification; the current returned CodeDom messages discard error IDs. Runtime exception stack traces are not normalized by the compiler-error offset.

Adoption: **ADAPT** aliases and tests, with the hint wording/classification tightened. Test changes in the upstream patch were not run in this research session.


## Deeper source review — PR #1135 Undo-safe GameObject deletion

Added [GAMEOBJECT_DELETE_UNDO_REVIEW.md](GAMEOBJECT_DELETE_UNDO_REVIEW.md) after comparing target GameObjectDelete.cs, baseline EditMode deletion tests and the upstream patch with Unity's current Undo API documentation.

The target currently calls Object.DestroyImmediate and comments that this makes deletion non-undoable. PR #1135 replaces it with Undo.DestroyObjectImmediate and tests a handler-level delete → Undo.PerformUndo → restored-object round trip. Unity's current API docs explicitly state that Undo.DestroyObjectImmediate records a destroy operation so an object can be recreated.

Adoption judgment: **ADAPT** the narrow change after running tests. The proposal covers one root object; require additional tests for multi-object grouping, hierarchy/component state restoration, redo, cleanup isolation and supported Unity versions before describing the entire operation as fully Undo-safe. The PR remained open in this snapshot and no Unity tests were run here.


## Agent workflow priority correction — script-first scene regeneration (2026-10-14)

The target agent's actual scene-authoring workflow is now explicitly defined in [DETERMINISTIC_SCENE_AUTHORING.md](DETERMINISTIC_SCENE_AUTHORING.md). The persistent generator script is the source of truth; scene changes are made by editing that script and running it again. The agent does not author a scene through direct GameObject/component CRUD calls and then inspect the whole scene to understand what changed.

This changes **implementation priority**, not whether generic Unity-MCP APIs may be useful to other clients:
- New highest-value scene-workflow item: source-script editing, compile/reload readiness, safe generator invocation, deterministic/idempotent regeneration, targeted code-based validation and concise diffs/reports.
- Direct serialized-property component writes (#The1Studio), prefab instance override apply/revert (#1462), and Undo safety for GameObject deletion (#1135) are moved out of the active agent-path backlog into deferred general-tool parity.
- Scene lifecycle, script compile diagnostics, asset/reference resolution, screenshots, Frame Debugger and profiling remain in-scope when used to verify a specific generator output issue. Read-only queries should be targeted; full hierarchy dumps are not the normal validation path.
- This update changes no runtime code and does not remove any existing tools.
