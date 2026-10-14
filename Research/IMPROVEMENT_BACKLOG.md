# Consolidated Unity-MCP Improvement Backlog

**Status:** Source-informed recommendations; implementation and runtime validation remain separate tasks.  
**Snapshot:** 2026-10-14  
**Baseline:** ApexForge-devz/unity-mcp beta, currently identical to upstream CoplayDev/unity-mcp beta at the observed commit SHA.

This is a research backlog, not a claim that every issue has manifested in production. Rank reflects cross-workflow value, risk reduction and prerequisite value—not just feature appeal. Scope is constrained by [SCOPE.md](SCOPE.md): only MCP↔Unity handling and project-local workflows are eligible. External generator/inference/provider integrations are not backlog candidates.

## Executive recommendation

Do not rewrite the architecture or inflate tool count. Keep the Python MCP service plus Unity C# Editor bridge. Prioritize protocol compatibility, privileged-operation policy, resource limits, and truthful/bounded tool contracts. Then improve editor liveness diagnostics, project-local asset reference analysis, and Unity Editor workflows. Add specialist features only after verifying existing profiling coverage and benchmarking value. Do not add external generators, third-party APIs, provider SDKs, or remote generated-asset pipelines.

## P0 — Safety and contract correctness

### P0.1 Batch contract and result budget

**Finding:** Server/src/services/tools/batch_execute.py exposes parallel and max_parallelism. MCPForUnity/Editor/Tools/BatchExecute.cs says Unity operations execute sequentially on the main thread and logs a warning if parallel mode is requested. Existing behavior includes a 25-command default, hard ceiling 100, per-command results and fail-fast. No new batching subsystem is needed.

**Recommendation — ADAPT:**
- Make the public schema and descriptions explicit that Unity API operations execute sequentially on the Editor main thread. Remove unsupported concurrency parameters or redefine them only for a separately verified read-only/server-side stage.
- Preserve deterministic command order and partial results; document that a batch is not transactional.
- Add summary and full response modes. Summary reports totals and concise failures; full preserves per-command result data.
- Enforce a maximum serialized response budget, accounting for nested outputs, while preserving failure details and continuation/spill information.
- Define timeout/disconnect semantics so callers do not blindly retry mutations.

**Acceptance tests:** input order preserved; max command count enforced; parallel request never implies concurrent Unity API work; fail-fast and continue-on-error are deterministic; mixed success/failure totals reconcile; large nested response is bounded and retrievable; timeout does not lead the wrapper to retry a non-idempotent mutation automatically.

**Evidence:** [Python wrapper](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/batch_execute.py), [Unity handler](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/BatchExecute.cs).

### P0.2 Policy for arbitrary C# execution

**Finding:** ExecuteCode.cs compiles and executes supplied C# in the Unity Editor process. safety_checks defaults true but is caller-controlled; blocked-pattern inspection is not a sandbox. Existing ExecuteCodeTests.cs already checks file-delete/process-start/infinite-loop blocklist cases and explicitly verifies that setting safety_checks=false bypasses that blocklist.

**Context:** Upstream issue [#1438](https://github.com/CoplayDev/unity-mcp/issues/1438) is an open request for a project-path allowlist for selected destructive file operations. Consider that path-level UX, but it does not replace an authorization/trust-boundary policy for arbitrary C#.

**Additional design reference:** Funplay's optional strict filesystem mode broadens blocklist coverage for file writes, absolute paths and traversal, but remains source-pattern matching. Reuse the policy tiers and regression-test matrix if useful; do not call a strict regex mode a sandbox.

**Recommendation — ADAPT:**
- Define trust modes, e.g. local trusted development versus authenticated remote/unattended execution.
- Enforce execute_code capability policy server-side before invoking the Unity bridge, including proxy/alias/batch paths.
- For remote and unattended use, disable by default or require explicit administrator-controlled enablement.
- Emit auditable decisions and clear denial diagnostics. Do not present pattern matching as a security boundary.

**Acceptance tests:** unauthorized remote call denied before dispatch; setting safety_checks=false cannot bypass server policy; aliases and batch paths enforce the same policy; trusted-local mode behaves as configured; policy is stable across session changes and domain reload. These are policy tests on top of existing ExecuteCodeTests.cs blocklist coverage, not replacements for it.

**Evidence:** [ExecuteCode.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs), design comparison [Pushkar-Env](https://github.com/pushkar-env/unity-mcp).

## P1 — MCP compatibility and context efficiency

### P1.1 FastMCP 4 / MCP 2026-07-28 protocol migration spike

**Finding:** The server dependency is constrained to fastmcp >=3.0.2,<4; the existing manage_tools design changes tool visibility per session. The current [MCP 2026-07-28 Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) says tools/list must not vary per connection or as a side effect of other requests on that connection. The available set may change over time or vary by request authorization, and servers should order it deterministically. The modern protocol also retires the initialize/initialized handshake and Mcp-Session-Id. These are concrete compatibility constraints, not just a general dependency-age concern.

**Recommendation — INVESTIGATE, then ADAPT:**
- Create a compatibility spike that serves modern stateless protocol clients and supported legacy clients.
- The current target's `manage_tools` directly calls `ctx.enable_components`, `ctx.disable_components`, `ctx.reset_visibility` and reads `ctx._get_visibility_rules`; the module explicitly describes FastMCP 3.x per-session tool visibility. In modern mode, do not let a prior `manage_tools` call change `tools/list` merely due to connection/session state.
- Prototype three compliant surfaces: a stable modern catalog with invocation gating; separately configured server endpoints with fixed curated catalogs; and request-authorization-based filtering only if every list request has real, authoritative caller credentials. Do not treat a tool argument or opaque session variable as authorization.
- Keep legacy per-session visibility only for a negotiated legacy protocol mode if compatibility tests prove it still works. FastMCP 4 advertises modern/legacy protocol support, but that does not prove the target's private visibility API remains compatible.
- Test protocol negotiation and behavior before relaxing the FastMCP upper bound.
- Preserve legacy clients deliberately; do not assume the newest protocol can replace them immediately.
- Add deterministic tool ordering and schema-parity tests.

**Acceptance tests:** modern stateless path; legacy handshake path; tool discovery before/after group selection; stable ordering; resources/prompts/auth/errors/cancellation; same intended permissions under every supported transport.

**Sources:** [MCP 2026-07-28 Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [MCP 2026-07-28 announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/), [FastMCP v4.0.0 release notes](https://github.com/PrefectHQ/fastmcp/releases/tag/v4.0.0), [FastMCP v4 protocol notes](https://github.com/PrefectHQ/fastmcp/blob/main/dev-docs/v4-notes/protocol-2026.md), [Server dependency manifest](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml), [tool-group management](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py), and the detailed [PROTOCOL_COMPATIBILITY.md](PROTOCOL_COMPATIBILITY.md).

### P1.2 Central result shaping, caps and continuation

**Finding:** Individual tools already paginate, truncate or downscale selected outputs; this is not a centralized guarantee for every MCP response. A transport frame-size limit is not an LLM-context budget. Isuzu UnityMCP enforces the serialized JSON size at its endpoint and applies the original tool's limit when an asynchronous job is later read through job_status; this avoids a large result escaping its inline budget just because the call became asynchronous.

**Recommendation — ADAPT:**
- Add a central response normalization/budget layer at the MCP return boundary.
- Default to concise structured summaries; allow callers to request detail fields or bounded pages.
- For large payloads, return a limited preview plus a stable continuation/object/file handle. Bound spill storage, add expiry, and scope handles to the correct project/session.
- Include result byte count, truncation state, next-page info and error status in a stable metadata envelope.
- Enforce the budget in the final response path and on asynchronous job polling, using the original operation's output contract. Do not duplicate result JSON in both content and structured fields if that inflates model context unnecessarily.
- Pair the cap with an explicit job/result handle when the full result is useful; reject or compact oversized payloads rather than returning a normal-looking empty result. Ensure every tool has a declared budget or inherits a safe default.
- Keep images opt-in when visual inspection is unnecessary.

**Acceptance tests:** hierarchy, asset search, console logs, component values, nested batch outputs, exceptions, large screenshots/metadata, continuation expiry and project isolation. Verify valid JSON and preservation of critical error/cursor information.

**Source patterns:** [KitWright MCPRequestHandler.cs](https://github.com/kitwright/unity-mcp/blob/main/Editor/MCP/Server/MCPRequestHandler.cs), [AnkleBreaker result formatting](https://github.com/AnkleBreaker-Studio/unity-mcp-server/blob/main/src/response-format.js). These are design references, not direct performance claims for this repository.

### P1.3 Final-route permissions and editor-instance identity

**Finding:** The target already has authentication and multi-instance routing. The goal is to test safety invariants at final dispatch, not automatically add another registry or permission framework.

**Recommendation — ADAPT / VERIFY:**
- Verify that read-only/write/destructive classification cannot be bypassed via generic proxies, aliases or batch calls.
- When discovering an Editor instance, validate returned Unity/project identity; HTTP success or a stale port record alone is insufficient.
- Fail closed when the requested project/worktree cannot be identified confidently, and give explicit selectable candidates if several Editors are open.
- Include project identity and request correlation IDs in diagnostics without leaking secrets.

**Acceptance tests:** dead/stale port, unrelated HTTP server on old port, two Editors open, two linked worktrees, disabled tool via batch/proxy, wrong project selected, domain reload/reconnect, authentication expiry.

**Source patterns:** [AnkleBreaker identity checks](https://github.com/AnkleBreaker-Studio/unity-mcp-server/blob/main/docs/discovery.md), [Ozan Unity Editor MCP](https://github.com/ozankasikci/unity-editor-mcp).


### P1.4 Prevent silent serialized-property write corruption

**Finding:** In target beta, the integer SerializedProperty branch pre-checks that a value looks integer-like, then uses ParamCoercion.CoerceInt(value, 0) / CoerceLong(value, 0). The coercion helpers catch overflow/parse failures and return defaults, so a large LayerMask bit pattern or other out-of-range payload can become 0 while the surrounding tool path reports success. The1Studio's [commit 970ac290](https://github.com/The1Studio/unity-mcp/commit/970ac29098b4b836cd6de596043f5f92aecf508f) adds TryCoerceInt/TryCoerceLong, switches serialized writes to checked conversion, and checks selected reflection writes by readback. Its [ComponentOps.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/ComponentOps.cs) also supports vector/struct leaves in the SerializedProperty path; our inspected target switch currently rejects those leaves as unsupported.

**Recommendation — ADAPT (P1 correctness):**
- Add explicit TryCoerceInt/TryCoerceLong helpers for mutation paths; preserve permissive/defaulting coercion only where failure-to-default is an intentional read/input behavior.
- Keep a failed conversion as an error. Never turn an invalid write into a plausible default and success response.
- Add write-back/readback verification for value-type intermediate properties where CLR reflection works on boxed copies; do not add blind readback for properties where Unity uses nontrivial normalization without defining expected comparison.
- Add supported vector/struct cases to the SerializedProperty fallback with shared parsers, and document exactly which forms are accepted.
- Preserve existing native-field/UnityEvent component tests; add coverage for large LayerMask values, integer boundaries/overflow, long bounds, bad strings, vector object/array forms, and no-mutation-on-failure.

**Acceptance tests:** int/long min/max and just-outside bounds; very large JSON integer; invalid/empty string; LayerMask values with high bits; known vector/Quaternion/Color/Rect/Bounds/AnimationCurve shapes; malformed shape rejected; failed conversion leaves prior serialized value intact; reflected struct-property write either persists or returns a failure. Run in Unity EditMode across supported versions.

**Evidence:** target [ComponentOps.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/ComponentOps.cs) blob ec9dc63684595811ac1c552271201ce5b65fa704; target [ParamCoercion.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/ParamCoercion.cs) blob 38a8e32d74fcfabfc7bd82a8af7b64363308278d; The1Studio's [checked coercion commit](https://github.com/The1Studio/unity-mcp/commit/970ac29098b4b836cd6de596043f5f92aecf508f) and [vector property commit](https://github.com/The1Studio/unity-mcp/commit/64279d6db40c46d2ffefebc1285b3fbbb5ddc59e).

### P1.5 Re-evaluate operation-level capability discovery against a facade reference

**Finding:** target PR #1 implements Core-first tool descriptions, description compaction and a get_tool_details endpoint, but does not centralize tool-result sizing. [Vangardo/unity-mcp-efficient](https://github.com/Vangardo/unity-mcp-efficient) demonstrates an alternative companion architecture: stable six-tool surface, action-level operation catalog, bounded compact results and an expiring result store. It is a separate server, not a patch to the target.

**Recommendation — INVESTIGATE before changing architecture:**
- Benchmark current PR #1 compaction against an operation-level facade and against current beta without either, using the same current tool catalog/version.
- Compare schema bytes/tokenizer counts, operation search recall/rank, unknown/ambiguous phrase handling, actual task completion, result bytes, extra round-trips and mutation errors.
- Prefer small internal changes (stable action-specific schemas, centralized result compactor and retrieval handles) unless the separate facade materially reduces maintenance or integration costs.
- Preserve operation-level read-only/destructive/idempotency annotations; do not give every call the same conservative mutation annotation simply because it goes through one wrapper.
- Add cross-project and multi-client tests for stored result handles and request IDs before any shared/remote deployment.

**Known risks:** Vangardo's benchmark date/catalog baseline is upstream v10.1.0 (August 2026), the search suite has only 15 cases, action-specific allowlists need schema-parity maintenance, and the inspected result store does not visibly bind result IDs/request receipts to authenticated caller/project. Its compressed-storage byte cap also preserves one oversized item, and the raw result exists before compaction. Treat these as review questions—not proven production exploits—before reusing its design.

**Acceptance tests:** reproduce the same six-tool/operation discovery benchmark on current beta; test schema parity for all action tools; exact/ambiguous/unknown operation names; output budgets for hierarchy/assets/logs/images; TTL/eviction/durable restart; concurrent requests with the same idempotency key; different arguments under the same key are rejected; result handles cannot cross project/principal boundaries; large raw output is capped before unbounded materialization where feasible.



### P1.6 Project-scoped endpoint/ownership and safe client configuration

**Confirmed target-source risk:** target PID files are project-local under the project root's Library directory, but **PidFileManager** stores the last PID-file path, instance token, PID, port, start timestamp and process-argument hash in unscoped EditorPrefs keys. Since EditorPrefs are shared across running Unity projects for the user, another Editor can read/clear the current global handshake. The project-local PID file and token checks prevent many unrelated-process kills, but do not prove that the Editor requesting stop owns the matched valid server. **ServerManagementService.StartLocalHttpServer** also calls the stop path before checking the configured port. See [Shared HTTP server lifecycle review](SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md).

**Upstream #1380 disposition — ADAPT, not cherry-pick as-is:** its per-project/per-port keys, per-process launch marker and bounded instance probe improve ownership isolation. But its launch marker uses SessionState, which Unity clears on Editor exit. If owner A exits while B is connected, A leaves the server running and then loses the only launch marker; B later exits without being able to stop the server. The PR also checks instance count and stops in separate operations, leaving a check/stop race. Do not call the lifecycle fixed until those cases have a tested resolution.

**Recommendation — VERIFY / ADAPT:**
- Namespace persistent PID/token/port state by a stable project identity and port; cleanup must clear only the state associated with the stopped server.
- Verify that the process is a Unity-managed MCP server using PID/port plus launch token/argument fingerprint; never terminate an unrelated listener based only on port or a name heuristic.
- Separate “this Editor launched it” from “this Editor is currently allowed to request safe last-client cleanup.” If a launcher exits while borrowers remain, transfer an adoptable cleanup lease or let a managed server perform bounded idle cleanup after all Unity clients are gone.
- Make shutdown behavior explicit for a server already used by another Editor: refuse to take it over, deliberately reuse it, or coordinate a safe drain; do not silently kill it during startup.
- Treat malformed/timeout/unavailable connected-instance count as unknown and leave the server running. Resolve the non-atomic attach-vs-stop race or explicitly constrain/test the transition.
- Preserve user-owned client configuration and the existing project/instance selection protections; do not build a second identity/preferences registry if the current abstractions can be corrected narrowly.

**Acceptance tests:** two projects/different ports; shared server where borrower quits first; shared server where original owner quits first and borrower quits last; attach during shutdown; startup while another project's server uses the same port; invalid/timeout probe; external listener/server; stale/reused PID and wrong token; process crash; domain reload; moved project/same-name worktrees; config custom launcher/env/cwd preserved. Require unit tests plus actual local HTTP and two-live-Editor integration tests.

**Sources:** [Shared HTTP server lifecycle review](SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md), target [ServerManagementService.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/ServerManagementService.cs), [PidFileManager.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/Server/PidFileManager.cs), [PR #1380](https://github.com/CoplayDev/unity-mcp/pull/1380), [PR #1285 project-scoped ownership](https://github.com/CoplayDev/unity-mcp/pull/1285), [PR #1468 preserve manual stdio config](https://github.com/CoplayDev/unity-mcp/pull/1468), [PR #1194 launch-dir auto-selection](https://github.com/CoplayDev/unity-mcp/pull/1194).

### P1.7 Make tool-schema compaction warning-preserving and measurable

**Finding:** target draft PR #1 shortens tool descriptions to 220 characters and schema description fields to 140 using a normalized prefix with word-boundary truncation. The copy-on-list behavior preserves registered definitions and structural schema constraints, and get_tool_details returns the full registered metadata. However, the test fixture verifies structure only; no whole-catalog token report or task-success study has been reproduced. Truncating prose can remove warnings/preconditions at the tail while leaving the JSON schema unchanged.

**Recommendation — ADAPT the draft rather than reject compaction:**
- Use curated short summaries for high-risk tools where possible, rather than truncating arbitrary long descriptions.
- Preserve key destructive-operation warnings, path constraints, required follow-up/refresh steps, version limitations and error-recovery hints. Include an explicit details-lookup hint whenever full description is omitted.
- Measure actual tool-list serialized bytes and client/model tokenizer tokens on identical beta/draft schemas; keep chars/4 only as an approximate metric.
- Benchmark schema compaction independently from core-first group visibility, and compare with operation-level catalog designs only on the same underlying tool version.
- Retain exact tool metadata/annotations and keep get_tool_details read-only; retrieving details must never enable a group or authorize invocation.

**Acceptance tests:** deterministic whole-catalog schema snapshots; short/long descriptions; warnings at start/middle/end; code paths/URLs/abbreviations/multilingual descriptions; descriptions with multiple paragraphs; all schema keys/required/defaults/enums/bounds/output schema and annotations preserved; hidden group details lookup does not change tool visibility; actual token counts and retrieval/task success on representative Unity tasks; modern and legacy tool-list protocol matrix.

**Evidence:** [Draft schema compaction source review](DRAFT_PR_CODE_REVIEW.md), [tool_schema_optimizer.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/tool_schema_optimizer.py), [schema optimizer tests](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/tests/test_tool_schema_optimizer.py), [protocol compatibility plan](PROTOCOL_COMPATIBILITY.md).



### P1.8 Make modal recovery identity-bound and backpressure-aware

**Finding:** target draft PR #1 adds a Windows modal liveness sampler and an out-of-band answer path. The sampler uses the Editor main-thread heartbeat, a capped background window scan and SendMessageTimeout; it reports native Win32 dialogs as answerable only when buttons were discovered, and verifies that the original HWND closes after PostMessage(BM_CLICK). Current answer_dialog parameters allow expect_title to be omitted, and dispatcher admission has no obvious hard queue cap in the reviewed code. The scene-change guard compares only file modification time.

**Recommendation — VALIDATE AND TIGHTEN BEFORE MERGE:**
- Require an expected dialog identity for button presses. Prefer an opaque short-lived ID bound to title plus handle/button-set observations; at minimum require expect_title rather than treating it as optional. Never blindly press a generic button on a different dialog that appeared after the model read the previous snapshot.
- Return “original modal closed” separately from “the underlying operation succeeded.” If another modal appears after the click, report its presence and ask for another inspection.
- Bound queued command admission by count and serialized bytes while the main thread is stalled. Preserve single-command ownership until an active handler really finishes, even when its caller times out.
- Strengthen scene-file change detection beyond strict mtime-increased checks where reliable detection is required; preserve conservative unknown behavior when a file cannot be read.
- Clearly document that auto may reload a single clean changed scene, reload prefers disk, and keep_editor overwrites disk with in-memory state. Require explicit selection when this can lose data.

**Acceptance tests:** stale/changed dialog identity; same-title or generic-button second dialog; overlapping answers; click accepted but dialog persists; next modal appears; queue count/byte limit and cancellation cleanup; main thread resumes while old request remains; unchanged/older mtime content replacement; changed mtime during several open scenes; inaccessible scene file; Windows native dialog + Unity-drawn modal on actual Editor, plus graceful non-Windows unknown/unsupported behavior.

**Merge gate:** compile-only checks and mocked/EditMode routing tests are insufficient. The feature needs an actual Windows Unity Editor test and the live bridge smoke job. In the last observed PR #1 run, the Unity tests and E2E jobs were skipped due missing license secrets.

**Evidence:** [Modal Liveness Review](DRAFT_PR_MODAL_LIVENESS_REVIEW.md), [ModalDialogProbe.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Helpers/ModalDialogProbe.cs), [SceneExternalChangeGuard.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Helpers/SceneExternalChangeGuard.cs), [answer_dialog.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/tools/answer_dialog.py).



### P1.9 Normalize JSON-encoded scalar arguments before dispatch

**Finding:** the target's Read Console action/format are Literal-backed in the Python schema and then lower-cased in the Unity handler, while Execute Menu Item receives a general menu path and applies its blacklist in C#. Some clients send enum-like scalar strings with one JSON-string encoding layer. Without a narrowly scoped decoder, schema validation or command handling may reject the value inconsistently; any menu-path normalization must happen before the blacklist checks the final path.

**Recommendation — ADAPT PR #1469:** add an opt-in one-layer decoder for Read Console action/format and Execute Menu Item menu_path only. Use a Pydantic BeforeValidator before Literal validation on the Python boundary, repeat validation in the function body for direct callers, and defensively decode at the Unity bridge boundary for direct/batch routes. Decode menu paths before the nonempty/path checks and blacklist. Keep the allowed action/format values typed and explicit; use invariant case normalization for protocol keywords.

- Decode at most one layer; do not recursively decode all JSON-looking strings or coerce encoded numbers/objects/booleans.
- Preserve ordinary strings and quote characters except for the explicitly supported outer JSON-string representation.
- Malformed/unknown/nested-encoded action values must fail before Unity dispatch. An encoded File/Quit path must still be blocked.
- Do not apply a global string-normalization middleware to paths, filters, code or serialized property values.

**Acceptance tests:** actual FastMCP schema validation before and after BeforeValidator; normal and one-layer encoded action/format; invalid/nested/non-string encodings do not dispatch; direct Python calls and Unity direct/batch calls share validation semantics; literal quoted filter preserved; menu path decoded before blacklist; encoded File/Quit remains blocked; nested encoded menu path has zero side effects; no error/default path silently substitutes an allowed value.

**Evidence:** [Argument normalization source review](ARGUMENT_NORMALIZATION_REVIEW.md), target [read_console.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/read_console.py), [ReadConsole.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ReadConsole.cs), [ExecuteMenuItem.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteMenuItem.cs), upstream [PR #1469](https://github.com/CoplayDev/unity-mcp/pull/1469).

## P2 — Reliability and high-value domain work

### P2.1 Editor main-thread liveness and retry safety

**Finding:** The target has editor-state/readiness machinery and asynchronous jobs, but diagnostics should distinguish a still-pumping yet slow command from an Editor loop that is not pumping.

**Recommendation — ADAPT / INVESTIGATE:**
- Track last Editor pump, command start/heartbeat, command ID, target project/editor, cancellation state and last transport error.
- Return distinct outcomes such as editor_busy, editor_not_pumping, bridge_disconnected or command_outcome_unknown instead of a generic timeout.
- Use idempotency/reconciliation for mutations when practical; never automatically repeat a non-idempotent operation merely because the response timed out.
- Do not build a second job runner if existing jobs can expose the needed state.

**Acceptance tests:** slow task while editor pumps; deliberately blocked pump; compile/domain reload; bridge reset during read; bridge reset after mutation but before response; cancellation, late response and stale job ID.

**Source pattern:** [KitWright EditorThreadHelper.cs](https://github.com/kitwright/unity-mcp/blob/main/Editor/Threading/EditorThreadHelper.cs).

### P2.2 Asset dependency/reference analysis

**Target-source finding:** target **ManageAsset.GetAssetData** (blob **69aaf53cc93a0b638d5a10139f4751dc42c13787**, around lines 1034–1120) returns asset identity and optional preview; it still has an explicit TODO for dependencies/importer metadata. The inspected EditMode/Tools directory listing has no clearly named dependency-analysis suite. General asset CRUD/search should not automatically embed graph data because dependency closures can be large.

**Alternative source review:** Ozan Kasikci's [AssetDependencyHandler.cs](https://github.com/ozankasikci/unity-editor-mcp/blob/main/unity-editor-mcp/Editor/Handlers/AssetDependencyHandler.cs) exposes direct/recursive dependencies, reverse dependents, size estimates, cycle checks and potentially-unreferenced candidates. Do **not** copy its simplified depth labels (all indirect dependencies become depth 2), script-only/limited cycle traversal, or whole-project reverse scan that calls **GetDependencies** for every candidate. The request-layer tests mock Unity, so they do not prove graph correctness.

**Unity API constraints:** [AssetDatabase.GetDependencies](https://docs.unity3d.com/ScriptReference/AssetDatabase.GetDependencies.html) returns asset-file dependency paths; recursive mode includes indirect dependencies and the input path itself. Those references are not necessarily build-required dependencies. [FindAssets](https://docs.unity3d.com/ScriptReference/AssetDatabase.FindAssets.html) returns GUIDs and folder scoping can narrow search. [GetAssetDependencyHash](https://docs.unity3d.com/ScriptReference/AssetDatabase.GetAssetDependencyHash.html) is useful for forward dependency change signals but cannot by itself invalidate a reverse map when another asset starts referencing a target.

**Recommendation — ADAPT:** add a dedicated read-only **analyze_asset_dependencies** capability rather than expanding every **GetAssetData** response. Start with direct dependencies and an explicit recursive closure; only return depth when a traversal calculated it. Add reverse dependents within an explicitly reported universe (e.g. **Assets** by default and Packages opt-in), returning scan count, scope, duration, completeness, freshness and bounded page/cursor. Use current async-operation conventions for large global scans instead of blocking the Editor or adding a second job system.

- Treat “unused” as **potentiallyUnreferenced** within a declared scan scope, never as proof of runtime non-use. Serialized graph edges may miss string loads, **Resources**, Addressables, build settings, dynamic content and project-specific runtime workflows.
- Size reporting must distinguish direct source-file bytes and unique dependency-closure file bytes from build/compressed size or runtime memory. Mark built-in/inaccessible package sizes unknown.
- If a reverse cache is introduced, invalidate on imported/deleted/moved/moved-from assets, scope/project/build-setting changes and relevant domain reloads. Cache freshness must be visible. A root's forward dependency hash is not a complete reverse-index invalidator.
- Keep analysis read-only; never automatically delete or move assets based on graph results.

**Acceptance tests:** zero/direct/deep chain/diamond/shared dependency, self-path filtering in recursive results, real depth values, cycle semantics, broken/moved GUIDs, folders/packages/read-only assets, reverse-edge correctness, added-dependent invalidation while target forward hash is unchanged, import/move/delete/domain-reload/project-scope invalidation, cancellation and explicit partial-scan status, deterministic paging/no duplicates, output caps, inaccessible-size handling, conservative unused wording, and no mutation side effects. Benchmark cold/warm scans against a large project, recording asset count, edge count, GetDependencies calls, latency, output bytes and Editor responsiveness.

**Evidence:** [Asset dependency analysis source review](ASSET_DEPENDENCY_ANALYSIS_REVIEW.md), target [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), alternative [AssetDependencyHandler.cs](https://github.com/ozankasikci/unity-editor-mcp/blob/main/unity-editor-mcp/Editor/Handlers/AssetDependencyHandler.cs), and the Unity API links above.

### P2.3 Frame Debugger capture depth and consistency

**Target baseline:** the current ManageProfiler/FrameDebuggerOps implementation provides only three Frame Debugger actions: enable, disable and paginated get_events. Each event may expose selected descriptor fields plus selected details such as shader/pass/render target/vertex/index/instance counts/mesh name. It uses reflection to accommodate Unity API namespace/signature differences. In the inspected get_events loop, per-event data retrieval failures are caught and skipped, so response data can lack details without a field-level completeness status. This handler does not provide capture sessions, immutable snapshots, detailed render-state/shader-resource inspection, render-target previews, event search/diff, or an asynchronous capture lifecycle.

**Useful derivative:** [pashara/Unity-MCP-CoplayDev-FrameDebugger](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger) packages a more extensive frame capture workflow. Its source has begin_capture / operation_status, sessions, event search/detail, summary, diagnostics, export, diff and render-target preview concepts, plus a backend abstraction and fake-backend tests. Its commit description and source document fixes to live/animated scene capture: replay the paused frame without advancing simulation, verify payload m_FrameEventIndex, re-read the event tree up to three times, and mark unavailable payload explicitly rather than associating stale data with the wrong event.

**Recommendation — INVESTIGATE then ADAPT:** this is a genuine capability lead rather than merely a small fix. Map desired features onto the existing profiling tool and consider integrating the smallest useful extension through current conventions. First reproduce correctness issues and inspect the derivative’s Unity-version/API constraints and package license/attribution. Do not copy the entire extension blindly.

**Additional direct-fork lead — Serraya/unity-mcp:** [Serraya's FrameDebuggerOps.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs) expands the existing event list into on-demand event details and render-target PNG capture, with bounded shader-property extraction and missing-field/warning diagnostics. [MarkerCalltreeOps.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/MarkerCalltreeOps.cs) adds bounded thread/row/depth scan budgets; [RecordedFrameAnalysisOps.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/RecordedFrameAnalysisOps.cs) adds frame summaries, hot markers, search and exportable tables. The fork uses a 1-second timeout for user-supplied marker regex and has tests for the regex timeout and profiler-job pump fairness. See [FORK_SERRAYA_PROFILER_REVIEW.md](FORK_SERRAYA_PROFILER_REVIEW.md).

**Adoption safeguards:** its event-output action allocates a full-size Texture2D and encoded PNG without an evident pixel/byte ceiling in the reviewed capture path. Its custom output_path is only blocked inside Assets; outside that folder it can still overwrite an existing .png at an arbitrary filesystem path. Before adapting, enforce a maximum pixel/encoded-byte budget, restrict output to a controlled root by default, make overwrite opt-in/CreateNew, and add output-capture tests. The reviewed fork tree only surfaced two dedicated profiler test files for this broad feature set, so live Unity-version compatibility remains unproven here.


**Acceptance tests:** animated scene capture does not advance simulation; payload event index matches; changing event trees trigger bounded retries; missing payload is marked unavailable; a capture returns stable session results; cancellation/error restores prior pause/Frame Debugger state; fake-backend EditMode tests plus live Editor validation on supported Unity versions.

**Sources:** [Target FrameDebuggerOps.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs) (blob 0876fdb121755c4bd4d71b1f6219a00f4bf199b8); [target ManageProfiler.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/ManageProfiler.cs); [derivative FrameDebuggerMcpTool.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameDebuggerMcpTool.cs) (blob f9370986c0d0d5924d7ec03b7574f32ce8ca8f52); [derivative FrameCaptureOperation.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameCaptureOperation.cs); [derivative tests](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Tests/Editor/FrameDebuggerTests.cs).

### P2.4 Memory Profiler snapshot querying

**Finding:** the target's MemorySnapshotOps can take a .snap file, list snapshots and compare file-level metadata; the source explicitly points object-level comparison back to the Editor window. The [Unity-MCP-MemoryProfiler extension](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler) source has incremental snapshot loading, bounded sessions, query/diff engines, object/reference/path-to-root views and several EditMode test suites.

**Recommendation — INVESTIGATE / ADAPT as an optional specialist capability:**
- Start with snapshot metadata, category summary and paginated query against loaded snapshots.
- Use operation/session handles; allow cancellation and explicit close/disposal.
- Cap concurrent sessions and returned rows, make sort/filter behavior deterministic, and keep the detailed schema outside the default tool surface if not commonly used.
- Make Unity Memory Profiler package/version requirements explicit and return useful “unsupported package/API” outcomes.
- Port domain logic into target conventions rather than importing the extension's IvanMurzak plugin framework.

**Acceptance tests:** open/cancel/close lifecycle; bounded session count; paged/sorted/filtered queries; snapshot metadata and summaries; diff rows for positive and negative changes; paths-to-roots/references; invalid or incompatible snapshots; missing/older package; disposal and domain-reload behavior.

**Sources:** [Target MemorySnapshotOps.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/MemorySnapshotOps.cs); [specialist Tool_MemoryProfiler.cs](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Editor/Scripts/Tool_MemoryProfiler.cs); [SnapshotQueryEngine.cs](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotQueryEngine.cs); [source/test analysis](SPECIALIST_WORKFLOWS.md).

### P2.5 Release/version/source alignment

**Finding:** target Python package metadata says 10.3.0 while Unity package metadata has reported 10.3.1-beta.7. This may be intentional, but requires an explicit versioning rule. Metadata also retains upstream package/support references.

**Recommendation — ADAPT:**
- Declare which components share versions and where independent versioning is intentional.
- Test manifest, package JSON, Python metadata, lockfile and release artifacts against that policy; do not hard-code equality if version skew is intended.
- Keep fork identity/support ownership clear if distribution materially differentiates.
- Verify release packaging includes only intended content and preserves asset licenses/provenance.

**Acceptance tests:** reproducible release metadata, locked dependency install, declared Unity/server compatibility matrix, built package contents match allowlisted manifest, generated references remain in sync.

### P2.6 Optional Play Mode input simulation and task validation

**Finding:** The1Studio's separate testing group provides target discovery, click/double-click/drag/scroll, key/text/touch input, UI bounds, bounded sequences and status polling. Its Input System fix queues state events instead of writing pointer state into an Editor update buffer; a focus guard warns when the Game View may not process the input. The current target beta tree has no equivalent input-simulation tool.

**Recommendation — INVESTIGATE as a testing capability, not core by default.** First choose supported input backends (legacy Input Manager, new Input System, touch/EnhancedTouch), target-resolution modes, foreground/focus policy and cancellation semantics. Use declarative bounded sequences and result handles; don't expose arbitrary code as the workflow language.

**Acceptance tests:** injected key/mouse/touch state observed by a test scene; no false success when Unity/Game View is unfocused; proper bounds and resolution mapping at several DPI/scales; Play Mode transitions; sequence validation/timeout/cancel; legacy/new input system variants; no leaked synthetic key/button state after failure.

**Source:** [The1Studio InputSimulationActions.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/InputSimulation/InputSimulationActions.cs), [ManageInputSimulation.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageInputSimulation.cs), [queued pointer fix](https://github.com/The1Studio/unity-mcp/commit/04424430a7b0116fe4f71f585f9f6130f37745b1).

### P2.7 Unfocused Play Mode liveness and DOTS integration

**Finding:** The1Studio's RunInBackgroundPlayGuard opts into Application.runInBackground while the Editor is in Play Mode and restores the original flag at EnteredEditMode. It openly documents that crash/quit before restoration can leave ProjectSettings changed because SessionState does not persist across process exit. Its ManageDots family supplies a sizeable optional ECS suite absent from target beta.

**Recommendation — INVESTIGATE guard behavior; DEFER the ECS suite to an optional package/group.** Reproduce the focus stall first, design a durable recovery/opt-out contract, and preserve PlayerSettings in normal stop/domain reload/quit paths. For DOTS, separately validate supported com.unity.entities APIs and dependency gating before adding dozens of actions.

**Acceptance tests:** focused/unfocused Play Mode behavior; normal stop/user stop/script stop/domain reload; editor quit/crash recovery strategy; no unexpected ProjectSettings diff; DOTS package absent/old/modern behavior; entity count failures distinguish unreadable from actual zero. Do not copy the entire 70KB+ ManageDots implementation without a scoped feature plan.

**Sources:** [RunInBackgroundPlayGuard.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Services/RunInBackgroundPlayGuard.cs), [DOTS tool](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageDots.cs), [unknown-vs-zero summary](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/EntityCountSummary.cs).

### P2.8 Test-run preflight domain-reload cost

**Finding:** target run_tests currently calls preflight with wait_for_no_compile=true and refresh_if_dirty=true. If external asset changes make the editor dirty, preflight requests refresh with compile=request. The stale bgr fork proposes compile=none to avoid an unnecessary reload before tests, but its commit explicitly says the change could not be compiled/run in Unity.

**Recommendation — INVESTIGATE with a reproducible Unity test fixture.** Do not change the compilation request solely because a fork made the change. Compare clean/dirty asset refresh, external script modification, import-only modification, domain reload count, and whether the test actually uses newly changed scripts. If Unity's refresh automatically compiles modified C# despite compile=none, document and test that contract; otherwise preserve compile readiness.

**Acceptance tests:** dirty non-code asset before tests does not cause needless domain reload; dirty script asset is compiled before tests use it; preflight respects active test-run exclusion; timeout/disconnect leaves a clear state; run outcome records compile/reload status.

**Source:** [Target preflight.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/preflight.py), [target run_tests.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/run_tests.py), [bgr change](https://github.com/bgr/unity-mcp/commit/11cfd4caac713c0b59846f2d0203e148e5977d7c).

### P2.9 Play Mode startup readiness and one-shot post-start actions

**Finding:** entering Play Mode does not guarantee scene initialization is complete; domain reload may reset pre-play static state, and Start/first-frame may not have run yet. Open upstream PR #1463 implements a startup job with scene_loaded/first_frame boundaries, optional one-shot code, status polling and no-resubmit semantics.

**Recommendation — INVESTIGATE, then ADAPT if current beta still has this gap.**
- Keep ordinary play action behavior unchanged unless wait/readiness options are supplied.
- Expose an explicit startup job handle, timeout and ready criterion.
- Generate an idempotency/request identifier before dispatch; after timeout/lost response, poll the known job rather than resending side-effecting on-ready code.
- Persist lifecycle across domain reload. If code was already executing at reload, report interrupted/unknown state and never replay automatically.
- Enforce the same policy for the Python tool and CLI REST path.

**Acceptance tests:** enter Play Mode with and without domain reload; wait for scene load versus first frame; compile/reload during startup; code completes, errors or triggers another reload; response lost after job creation; client timeout followed by status poll; stop Play Mode/cancel; repeated request ID; selected Unity instance changes or multiple projects open.

**Source:** [Upstream PR #1463](https://github.com/CoplayDev/unity-mcp/pull/1463). Check merge status and source diff immediately before implementation.

### P2.10 Native test-run lifecycle cleanup after timeouts

**Finding:** a timed-out MCP test job can be terminal from the caller's perspective while Unity's native test run still owns throttling/preferences/operation lock. Upstream PR #1460 adds native run IDs, cleanup-pending state preserved over domain reload and prevents new tests until cleanup completes.

**Recommendation — PRIORITIZE if the failure is reproducible against current beta.** Do not merely delete the MCP job or free the lock when its response times out. Preserve ownership of the native run, make its cleanup state observable, reject overlapping tests while cleanup is pending, restore the original Editor interaction settings on all exit/error/reload paths, and prevent late callbacks from overwriting an already-reported timeout.

**Acceptance tests:** timeout before and after Test Runner RunStarted; Editor reload mid-run; native exception/onError; cancellation; late RunFinished callback; cleanup then second job; cleanup that never completes with an escalation path; original interaction preferences restored exactly; no concurrent conflicting jobs.

**Source:** [Upstream PR #1460](https://github.com/CoplayDev/unity-mcp/pull/1460).

### P2.11 Safe, nonblocking connection/status refresh in the Editor UI

**Finding:** connection/window focus polling can involve TCP probes, executable discovery, environment/uv validation and client-config file reads. If run synchronously on the Editor main thread, simply opening/focusing the MCP window can delay rendering or tool dispatch. Upstream PR #1465 supplies a testable helper that coalesces requests, caches results briefly, discards stale generations and avoids UI writes after window detach.

**Recommendation — ADAPT if the target baseline still performs slow work synchronously.** Keep Unity API and UI changes on the main thread; move only thread-safe I/O/process calls to workers. Use cache keys and generations so an old request cannot overwrite a newer endpoint/client selection, and cap timeouts/concurrent process launches.

**Acceptance tests:** slow/hung executable and TCP probe; repeat focus events coalesce; cache TTL; endpoint/client selection changes mid-flight; connection disconnect/reconnect mid-flight; window close/dispose and re-open; exceptions without secret/path leakage in logs; UI remains responsive while the worker is blocked.

**Source:** [Upstream PR #1465](https://github.com/CoplayDev/unity-mcp/pull/1465).

### P2.12 Prefab instance override discovery and scoped apply/revert

**Finding:** upstream PR #1462 proposes listing property and structural prefab instance overrides and applying/reverting only the explicitly selected target/property/component/child. Ambiguous same-name siblings and selections that would widen an array write are rejected.

**Recommendation — TRACK UPSTREAM, do not duplicate now.** This is a high-value workflow for understanding why a scene object doesn't visually follow prefab-asset edits. If not merged, inspect against the current manage_prefabs implementation before adopting; require narrow scope and clear Undo behavior.

**Acceptance tests:** list only non-default overrides; property, added/removed component and added/removed GameObject overrides; revert/apply one property without changing sibling overrides; duplicate-name siblings require stable identity; array selection cannot widen unexpectedly; nested prefab lineage and prefab stage; unsupported Unity internal APIs report capability unavailable; Undo/redo and unsaved-scene dirty state.

**Source:** [Upstream PR #1462](https://github.com/CoplayDev/unity-mcp/pull/1462).

### P2.13 Configurable server instructions for project conventions

**Finding:** upstream PR #1461 adds a UTF-8 instructions file loaded once at server startup and appends it to the default server instructions.

**Recommendation — INVESTIGATE before adopting.** File-backed project conventions may reduce repeated instructions in prompts, but a single shared HTTP server can serve several projects and users. Document whether instructions are server-global, project-specific or user-specific; do not derive an instruction file from untrusted project content by default. Add a size cap, UTF-8 validation, startup failure behavior and no-secret logging.

**Acceptance tests:** file unset/empty/valid UTF-8/BOM/invalid UTF-8/unreadable/oversized; command-line vs env precedence; both transports; default instructions preserved; shared server with different projects cannot receive the wrong project instructions; restart applies changes; errors omit sensitive file contents.

**Source:** [Upstream PR #1461](https://github.com/CoplayDev/unity-mcp/pull/1461).

### P2.14 Profiler ownership, data fidelity and sample-size gates

**Finding:** PerfAgent-Unity's ProfilerOwnership records prior Profiler state/history and restores it after domain reload; its rule engine declines dynamic conclusions when sample count is insufficient and labels zero-frame captures explicitly. Its data-fidelity tests prefer ProfilerRecorder metrics where available and propagate unavailable values as NaN/explicit notes rather than silently substituting weaker numbers.

**Recommendation — ADAPT to existing profiling tools:**
- If a diagnostic tool changes Profiler enabled/profileEditor/history settings, capture and restore user state; use SessionState or equivalent if domain reload interrupts cleanup.
- Bound capture history/memory during automated sampling and report if the requested capture target or history cap could not be configured.
- Keep metric provenance (ProfilerRecorder vs estimated GC delta, file metadata vs object-level snapshot, exact vs heuristic asset reference) explicit in results.
- Add a minimum-sample gate for percentile/statistical conclusions and an explicit no-frames error state. Do not emit plausible zero/default data when a counter is unavailable.
- Prefer read-only analysis plus user-confirmed mutations for performance fixes; profiling/diagnostics should not silently change the project.

**Acceptance tests:** profile was originally enabled/disabled; profileEditor/history restoration; domain reload during capture; manual Profiler open; unable to set history bound; no frames; too few frames; missing recorder; GC event negative delta; noisy/unreliable metric is marked unavailable; clean snapshot creates no false-positive gold rules.

**Sources:** [PerfAgent ProfilerOwnership.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Editor/Core/ProfilerOwnership.cs), [PerfRuleEngine.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Editor/Analysis/PerfRuleEngine.cs), [PerfDataFidelityTests.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Tests~/Editor/PerfDataFidelityTests.cs), and [OPERATIONAL_RELIABILITY_PATTERNS.md](OPERATIONAL_RELIABILITY_PATTERNS.md).

### P2.15 Bound Editor dispatcher backlog and share the frame budget fairly

**Finding:** the target's `TransportCommandDispatcher.ProcessQueue` takes every queued non-executing command from its global Pending dictionary and processes the ready list in one Editor update pass. Per-request cancellation is already implemented and checked before handler dispatch; once a command is running, cancelling its caller does not undo the work. The inspected dispatcher does not show a hard pending-count/serialized-byte cap or a per-update count/time slice. A stalled native modal can leave incoming requests retained until cancellation/disconnect, and after the Editor resumes a large ready list can produce a long update.

**Recommendation — ADAPT only after a focused characterization test:**
- Add pending-command count and serialized-byte admission limits with an explicit retryable/backpressure response; don't retain an unlimited number of command JSON bodies while the Editor is blocked.
- Preserve FIFO/order where required and one active Unity API command at a time. If processing multiple short commands in one update, use a bounded item/time budget and continue the rest on later updates.
- Keep cancellation's guarantee precise: cancel before a command begins can prevent its side effects; cancellation after the handler starts means outcome may be unknown or still in progress.
- Return a correlation/job handle for long-running operations so callers poll the original operation instead of repeating a mutation.
- Log queue count/oldest age and operation status, not full command payloads or secrets.
- Check the transport registration and async-handler continuation path for races before adding any queue/timeout refactor; the target already has queue locking, active request state and cancellation cleanup.

**Acceptance tests:** queue count/byte cap, flood during modal stall, FIFO under slicing, no concurrent Unity API execution, cancel before start, cancel during active handler, disconnect cleanup, domain reload with queued commands, one slow command plus many fast commands, queue age observability, and no duplicate side effects when a client times out and polls.

**Source references:** target [TransportCommandDispatcher.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/Transport/TransportCommandDispatcher.cs), target [TestJobManager.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/TestJobManager.cs), and [OPERATIONAL_RELIABILITY_PATTERNS.md](OPERATIONAL_RELIABILITY_PATTERNS.md).



### P2.16 Return identity-scoped cached test outcomes during transient transport stalls

**Finding:** target Server/src/services/tools/run_tests.py has async test jobs and focus-nudge state, but the inspected version has no bounded cache of the last test-job status snapshots. [onlyfeng/unity-mcp](https://github.com/onlyfeng/unity-mcp) adds a 20-entry LRU keyed by transport mode, user, Unity instance and job ID; cached terminal state can be returned after a transient poll timeout instead of misreporting the result as unknown. It records whether a snapshot contains all results or only failed/skipped details and does not claim a low-detail terminal snapshot satisfies a higher-detail request.

**Recommendation — ADAPT the result-resilience contract with a monotonicity fix:**
- Cache only normalized test-job snapshots, bounded by item count and with observation timestamps.
- Use the authenticated caller + target Unity instance in the key, not only the job ID.
- Cache fallback is permitted only for transient transport/timeout failures. Invalid IDs and real test/tool errors must remain errors.
- Mark fallback responses degraded/stale, including cache age and the latest transport error. Never present a cached running state as live.
- Preserve detail fidelity for include_details/include_failed_tests. A richer terminal snapshot must not be overwritten by a lower-detail snapshot.
- Add an observation revision/order so an old in-flight poll cannot overwrite a newer terminal result. The fork's cache write path does not pass the poll observation order to its remember function, so this race remains a test gap even in the fork.

**Acceptance tests:** transient and non-transient failures; no cache; terminal cache returns immediately; wait loop retries transient failure; detail fidelity; user and instance isolation; reconnect under same instance; LRU eviction; stale/degraded diagnostics; simultaneous polls with older-running response arriving after newer-terminal response; test IDs never reused across instance scopes.

**Evidence:** [onlyfeng test-cache review](FORK_ONLYFENG_TEST_STATUS_CACHE.md), [run_tests.py](https://github.com/onlyfeng/unity-mcp/blob/beta/Server/src/services/tools/run_tests.py), [test_run_tests_async.py](https://github.com/onlyfeng/unity-mcp/blob/beta/Server/tests/integration/test_run_tests_async.py).

## P3 — Conditional / benchmark-driven enhancements

Potential later investigations:
- Diff two screenshots or scene snapshots and return only important visual changes.
- Play Mode input simulation and task-oriented test workflows beyond current automation.
- Bounded editor event/operations-history resources to reduce repeated polling.
- Additional URP/Shader Graph/VFX/ProBuilder helpers only if a task-coverage review shows real gaps.
- Curated tool profiles layered on current groups for small-context clients.

For each, establish a task baseline and controlled experiment first. Do not accept proposals based only on another project's tool count or unverified speed claims.


## Upstream overlap guard — 2026-10-14 snapshot

These are upstream proposals, not features already present in the inspected beta baseline. Re-check state and compare the merged diff before implementation; do not develop duplicate code while an equivalent upstream change remains a viable adoption path.

| Proposal | Active backlog relationship | Guardrail |
|---|---|---|
| [#1380 shared HTTP server shutdown](https://github.com/CoplayDev/unity-mcp/pull/1380) | P1.3 / P1.6 / P2.1 — instance identity, server ownership and transport lifecycle | Verify with two simultaneous Editors. Only stop a process-owned server after a bounded probe confirms no other connected instances; probe failure should not kill a potentially shared server. |
| [#1459 asset-search pagination](https://github.com/CoplayDev/unity-mcp/pull/1459) | P1.2 and P2.2 — bound data hydration and asset operations | Overlaps target draft PR #1. Hydrate only the requested page, reject invalid folder/page inputs, and benchmark real projects; current patch still enumerates all matches for total count. |
| [#1469 JSON-encoded action/menu arguments](https://github.com/CoplayDev/unity-mcp/pull/1469) | Tool-contract and dispatch validation | Preserve typed schemas, decode no more than one encoded string layer per boundary, and validate the final action/path before dispatch. |
| [#1472 custom-tool parameter ordering](https://github.com/CoplayDev/unity-mcp/pull/1472) | P1.1/P1.7 and custom-tool schema reliability | Preserve schema requiredness/defaults and injected-context boundaries for both required-before-optional and optional-before-required definitions. |
| [#1455 C# type aliases and diagnostics](https://github.com/CoplayDev/unity-mcp/pull/1455) | Code execution and compile diagnostics | Keep generated-wrapper line offsets synchronized and test CodeDom, Roslyn, fallback and runtime-error paths. |
| [#1448 Console filtering](https://github.com/CoplayDev/unity-mcp/pull/1448) | P2.1/P2.16 observability and result signal | Exclude MCP-prefixed logs only when requested; keep them by default and preserve pagination/count behavior. Prefix classification is heuristic. |
| [#1135 Undo-safe GameObject deletion](https://github.com/CoplayDev/unity-mcp/pull/1135) | Scene/GameObject mutation correctness | Verify delete-then-Undo behavior across supported Unity versions; do not assume a single Editor test proves a cross-version guarantee. |
| [#1450 fixed Game View resolution](https://github.com/CoplayDev/unity-mcp/pull/1450) | P3 visual iteration | Lower priority; requires reflected API compatibility, visible-window/readback handling, clamping disclosure and clarity that EditorPrefs preset state is shared and not restored. |
| [#1464 exact optional package versions](https://github.com/CoplayDev/unity-mcp/pull/1464) | Secondary Unity Editor workflow | Preserve installed/Git/local dependencies unless the user explicitly changes them; distinguish package release version from native API capability. |

Detailed evidence and limitations: [RECENT_UPSTREAM_PR_REVIEW_2026-10-14.md](RECENT_UPSTREAM_PR_REVIEW_2026-10-14.md).

## Proposed milestones

| Milestone | Work | Exit condition |
|---|---|---|
| M0 — safety/contract | Batch schema, privileged C# policy | Tests prove truthful semantics and server policy |
| M1 — protocol | FastMCP 4 / modern and legacy MCP spike | Contract matrix passes before changing dependency pin |
| M2 — results | Central output governor and continuation handles | Representative tools never return unbounded payloads |
| M3 — observability | Editor pump diagnostics, correlation, safe retry semantics | Slow/stalled/disconnected/unknown outcomes distinguishable |
| M4 — domain gap | Asset dependency/reference analyzer | Accurate synthetic fixtures, safe unused-candidate semantics, cache invalidation |
| M5 — feature-specific correctness | Frame Debugger comparison if needed | Existing handlers mapped; fixes cover reproduced gaps only |
| M6 — release quality | Version policy, reproducible packages, sync workflow hygiene | No unexplained drift; tested package/docs alignment |
| M7 — conditional | Visual diff, events, specialist workflows | Task-success/context/latency data justifies maintenance |

## Metrics to establish before and after

- Tool schema bytes and approximate tokens per client/transport.
- Default tool count and response bytes for discovery, category lookup and individual schema.
- Tool result bytes/tokens, truncation rate, continuation count and failure rate.
- Latency p50/p95, Editor pump freshness, uncertain mutation outcomes and repeated side effects.
- Domain reload/compile count/time for representative batch workflows.
- End-to-end task success for scene creation, script edit/compile, EditMode tests, missing-reference diagnosis, material edits and two-frame visual iteration.
- Unity-version and MCP-client compatibility matrix; report skipped tests separately from passing tests.

## Explicit non-goals

- Rewrite the system in another language or replace the current Python/C# split.
- Rebuild Undo/Redo, screenshots, pagination, async jobs, readiness, multi-instance routing or tool groups without verifying the existing implementation is insufficient.
- Add tools just to maximize catalog size.
- Advertise concurrent Unity API operations that must run on the main thread.
- Auto-delete assets based on heuristic “unused” results.
- Treat static source inspection, successful compilation or README claims as substitutes for live Unity correctness/performance tests.
- Add no integrations with Meshy, ComfyUI, Pinokio, hosted/local generation services, third-party generation APIs, provider credentials/SDKs, remote generation-job management, or provider-specific artifact downloads. Provider-specific review notes retained elsewhere are historical and out of scope.
