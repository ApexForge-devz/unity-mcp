# Consolidated Unity-MCP Improvement Backlog

**Status:** Source-informed recommendations; implementation and runtime validation remain separate tasks.  
**Snapshot:** 2026-10-14  
**Baseline:** ApexForge-devz/unity-mcp beta, currently identical to upstream CoplayDev/unity-mcp beta at the observed commit SHA.

This is a research backlog, not a claim that every issue has manifested in production. Rank reflects cross-workflow value, risk reduction and prerequisite value—not just feature appeal.

## Executive recommendation

Do not rewrite the architecture or inflate tool count. Keep the Python MCP service plus Unity C# Editor bridge. Prioritize protocol compatibility, privileged-operation policy, resource limits, and truthful/bounded tool contracts. Then improve editor liveness diagnostics and asset reference analysis. Add specialist features only after verifying existing profiling coverage and benchmarking value.

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

### P0.3 ZIP expansion/resource limits

**Finding:** SafeZipExtractor.cs already resolves entry paths to prevent Zip-Slip and filters extensions through an allowlist. Existing SafeZipExtractorTests.cs verifies normal extraction, disallowed extensions, and traversal rejection. The inspected implementation iterates archive entries and copies entry streams, but caps on entry count or expanded byte totals were not evident; the existing tests do not cover expansion quotas.

**Recommendation — ADAPT:**
- Keep path-containment and extension-allowlist defenses.
- Add hard caps for entry count, per-entry expanded bytes, total expanded bytes, destination depth/path length, and optionally suspicious compression ratios.
- Enforce expanded-byte caps while streaming, not merely by trusting ZIP directory metadata.
- Fail with sanitized errors and clean up partial output according to an explicit import policy.

**Acceptance tests:** keep existing traversal/extension/normal-entry cases and add absolute path, both slash styles, duplicate/case-colliding paths, many tiny entries, one oversized entry, total oversized archive, malformed ZIP, partial-failure cleanup, pre-existing-file conflict, and safe nested directories.

**Evidence:** [SafeZipExtractor.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Import/SafeZipExtractor.cs). Target beta and upstream main have the same inspected blob at this snapshot; this limitation is not resolved merely by moving to upstream main. See [UPSTREAM_BRANCH_COMPARISON.md](UPSTREAM_BRANCH_COMPARISON.md).

## P1 — MCP compatibility and context efficiency

### P1.1 FastMCP 4 / MCP 2026-07-28 protocol migration spike

**Finding:** The server dependency is constrained to fastmcp >=3.0.2,<4; the existing manage_tools design changes tool visibility per session. The current [MCP 2026-07-28 Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) says tools/list must not vary per connection or as a side effect of other requests on that connection. The available set may change over time or vary by request authorization, and servers should order it deterministically. The modern protocol also retires the initialize/initialized handshake and Mcp-Session-Id. These are concrete compatibility constraints, not just a general dependency-age concern.

**Recommendation — INVESTIGATE, then ADAPT:**
- Create a compatibility spike that serves modern stateless protocol clients and supported legacy clients.
- Decide how user/editor-selected tool groups map onto a stable tools/list contract. A possible approach is to keep schemas stable and gate invocation through capability state rather than changing list contents in modern mode.
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

**Finding:** upstream PR #1285 identifies cross-project contamination from machine-global EditorPrefs for local endpoint, transport selection and server PID/port/instance token. Upstream PR #1468 identifies automatic configuration rewrites that can replace user-managed stdio executables and remove env/cwd fields. PRs #1194 and #1465 add launch-directory-based project selection and safe asynchronous refresh, respectively. These are connected operational risks but distinct subsystems.

**Recommendation — VERIFY / ADAPT only where the current target lacks the protections:**
- Compare project identity/prefs implementation and connection manager against PR #1285 before making a second scoping framework.
- Treat generated client config as user-owned state. Preserve custom commands/env/cwd/unrelated fields by default; make destructive “regenerate” an explicit operation.
- When several Unity Editors are open, use explicit project/launch-directory identity where the client exposes it; fail closed if matching is ambiguous.
- Keep filesystem/process/network work off the Editor main thread with coalescing/cache and stale-result protection, while all Unity UI work remains on the main thread.

**Acceptance tests:** two projects with different HTTP endpoints/PIDs; two linked Git worktrees with identical project names; process restart/domain reload; config custom launcher/env/cwd preserved through checks/migration; malformed JSON and args shape do not trigger rewrite; worker completion after settings change/window close cannot update stale UI; ambiguous roots prompt rather than selecting randomly; per-principal/project/session cache isolation.

**Sources:** [PR #1285 project-scoped ownership](https://github.com/CoplayDev/unity-mcp/pull/1285), [PR #1468 preserve manual stdio config](https://github.com/CoplayDev/unity-mcp/pull/1468), [PR #1194 launch-dir auto-selection](https://github.com/CoplayDev/unity-mcp/pull/1194), [PR #1465 async focus refresh](https://github.com/CoplayDev/unity-mcp/pull/1465).



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

**Finding:** ManageAsset.GetAssetData has an explicit TODO for extra metadata such as dependencies. A source-reviewed alternative exposes direct/transitive dependencies, reverse dependents, cycles, unused candidates, size impact and reference validation.

**Upstream overlap check:** open PR [#1466](https://github.com/CoplayDev/unity-mcp/pull/1466) proposes read-only AssetBundle file listing and bundle-info inspection. Track/validate that proposal instead of adding a duplicate bundle explorer. This is not the same as an AssetDatabase dependency/reverse-reference graph.

**Recommendation — ADAPT:**
- Implement a narrow paginated analyze_asset_dependencies capability backed by Unity AssetDatabase.
- Include dependency/dependent graph, reference validation and size impact.
- Treat “unused” as a review candidate, not an authoritative delete instruction. Explain scan coverage, roots and limitations.
- Cache only with robust invalidation across imports, moves, deletes, script/domain reload and relevant build-settings changes.
- Keep analysis separate from asset CRUD/search behavior.

**Acceptance tests:** direct and transitive graph, shared dependency, cycles where supported, missing/broken references, folders/packages/built-ins, GUID move, import invalidation, large-project paging, Addressables when supported, conservative false-positive behavior for unused candidates.

**Sources:** [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), [Ozan AssetDependencyToolHandler.js](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/src/handlers/asset/AssetDependencyToolHandler.js). Inspect the alternative’s Unity-side handler and tests before copying semantics.

### P2.3 Frame Debugger capture depth and consistency

**Target baseline:** the current ManageProfiler/FrameDebuggerOps implementation provides only three Frame Debugger actions: enable, disable and paginated get_events. Each event may expose selected descriptor fields plus selected details such as shader/pass/render target/vertex/index/instance counts/mesh name. It uses reflection to accommodate Unity API namespace/signature differences. In the inspected get_events loop, per-event data retrieval failures are caught and skipped, so response data can lack details without a field-level completeness status. This handler does not provide capture sessions, immutable snapshots, detailed render-state/shader-resource inspection, render-target previews, event search/diff, or an asynchronous capture lifecycle.

**Useful derivative:** [pashara/Unity-MCP-CoplayDev-FrameDebugger](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger) packages a more extensive frame capture workflow. Its source has begin_capture / operation_status, sessions, event search/detail, summary, diagnostics, export, diff and render-target preview concepts, plus a backend abstraction and fake-backend tests. Its commit description and source document fixes to live/animated scene capture: replay the paused frame without advancing simulation, verify payload m_FrameEventIndex, re-read the event tree up to three times, and mark unavailable payload explicitly rather than associating stale data with the wrong event.

**Recommendation — INVESTIGATE then ADAPT:** this is a genuine capability lead rather than merely a small fix. Map desired features onto the existing profiling tool and consider integrating the smallest useful extension through current conventions. First reproduce correctness issues and inspect the derivative’s Unity-version/API constraints and package license/attribution. Do not copy the entire extension blindly.

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

### P2.5 Release/version/source alignment

**Finding:** target Python package metadata says 10.3.0 while Unity package metadata has reported 10.3.1-beta.7. This may be intentional, but requires an explicit versioning rule. Metadata also retains upstream package/support references.

**Recommendation — ADAPT:**
- Declare which components share versions and where independent versioning is intentional.
- Test manifest, package JSON, Python metadata, lockfile and release artifacts against that policy; do not hard-code equality if version skew is intended.
- Keep fork identity/support ownership clear if distribution materially differentiates.
- Verify release packaging includes only intended content and preserves asset licenses/provenance.

**Acceptance tests:** reproducible release metadata, locked dependency install, declared Unity/server compatibility matrix, built package contents match allowlisted manifest, generated references remain in sync.



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

## P3 — Conditional / benchmark-driven enhancements

Potential later investigations:
- Diff two screenshots or scene snapshots and return only important visual changes.
- Play Mode input simulation and task-oriented test workflows beyond current automation.
- Bounded editor event/operations-history resources to reduce repeated polling.
- Additional URP/Shader Graph/VFX/ProBuilder helpers only if a task-coverage review shows real gaps.
- Curated tool profiles layered on current groups for small-context clients.

For each, establish a task baseline and controlled experiment first. Do not accept proposals based only on another project's tool count or unverified speed claims.

## Proposed milestones

| Milestone | Work | Exit condition |
|---|---|---|
| M0 — safety/contract | Batch schema, privileged C# policy, archive quotas | Tests prove truthful semantics, server policy and bounded ZIP expansion |
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
