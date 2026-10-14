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

**Finding:** ExecuteCode.cs compiles and executes supplied C# in the Unity Editor process. safety_checks defaults true but is caller-controlled; blocked-pattern inspection is not a sandbox.

**Recommendation — ADAPT:**
- Define trust modes, e.g. local trusted development versus authenticated remote/unattended execution.
- Enforce execute_code capability policy server-side before invoking the Unity bridge, including proxy/alias/batch paths.
- For remote and unattended use, disable by default or require explicit administrator-controlled enablement.
- Emit auditable decisions and clear denial diagnostics. Do not present pattern matching as a security boundary.

**Acceptance tests:** unauthorized remote call denied before dispatch; setting safety_checks=false cannot bypass server policy; aliases and batch paths enforce the same policy; trusted-local mode behaves as configured; policy is stable across session changes and domain reload.

**Evidence:** [ExecuteCode.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs), design comparison [Pushkar-Env](https://github.com/pushkar-env/unity-mcp).

### P0.3 ZIP expansion/resource limits

**Finding:** SafeZipExtractor.cs already resolves entry paths to prevent Zip-Slip and filters extensions through an allowlist. The inspected implementation iterates archive entries and copies entry streams, but caps on entry count or expanded byte totals were not evident.

**Recommendation — ADAPT:**
- Keep path-containment and extension-allowlist defenses.
- Add hard caps for entry count, per-entry expanded bytes, total expanded bytes, destination depth/path length, and optionally suspicious compression ratios.
- Enforce expanded-byte caps while streaming, not merely by trusting ZIP directory metadata.
- Fail with sanitized errors and clean up partial output according to an explicit import policy.

**Acceptance tests:** traversal, absolute path, both slash styles, duplicate/case-colliding paths, many tiny entries, one oversized entry, total oversized archive, malformed ZIP, denied extension, partial-failure cleanup, pre-existing-file conflict, and safe nested directories.

**Evidence:** [SafeZipExtractor.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Import/SafeZipExtractor.cs).

## P1 — MCP compatibility and context efficiency

### P1.1 FastMCP 4 / MCP 2026-07-28 protocol migration spike

**Finding:** The server dependency is constrained to fastmcp >=3.0.2,<4; the existing manage_tools design changes tool visibility per session. The MCP 2026-07-28 specification removes protocol-level sessions and constrains list behavior, creating a compatibility issue for changing tool lists per session.

**Recommendation — INVESTIGATE, then ADAPT:**
- Create a compatibility spike that serves modern stateless protocol clients and supported legacy clients.
- Decide how user/editor-selected tool groups map onto a stable tools/list contract. A possible approach is to keep schemas stable and gate invocation through capability state rather than changing list contents in modern mode.
- Test protocol negotiation and behavior before relaxing the FastMCP upper bound.
- Preserve legacy clients deliberately; do not assume the newest protocol can replace them immediately.
- Add deterministic tool ordering and schema-parity tests.

**Acceptance tests:** modern stateless path; legacy handshake path; tool discovery before/after group selection; stable ordering; resources/prompts/auth/errors/cancellation; same intended permissions under every supported transport.

**Sources:** [MCP 2026-07-28 announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/), [MCP specification changelog](https://modelcontextprotocol.io/specification/draft/changelog), [FastMCP v4 protocol notes](https://github.com/PrefectHQ/fastmcp/blob/main/dev-docs/v4-notes/protocol-2026.md), [Server dependency manifest](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml), [tool-group management](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py).

### P1.2 Central result shaping, caps and continuation

**Finding:** Individual tools already paginate, truncate or downscale selected outputs; this is not a centralized guarantee for every MCP response. A transport frame-size limit is not an LLM-context budget.

**Recommendation — ADAPT:**
- Add a central response normalization/budget layer at the MCP return boundary.
- Default to concise structured summaries; allow callers to request detail fields or bounded pages.
- For large payloads, return a limited preview plus a stable continuation/object/file handle. Bound spill storage, add expiry, and scope handles to the correct project/session.
- Include result byte count, truncation state, next-page info and error status in a stable metadata envelope.
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

### P2.5 Release/version/source alignment

**Finding:** target Python package metadata says 10.3.0 while Unity package metadata has reported 10.3.1-beta.7. This may be intentional, but requires an explicit versioning rule. Metadata also retains upstream package/support references.

**Recommendation — ADAPT:**
- Declare which components share versions and where independent versioning is intentional.
- Test manifest, package JSON, Python metadata, lockfile and release artifacts against that policy; do not hard-code equality if version skew is intended.
- Keep fork identity/support ownership clear if distribution materially differentiates.
- Verify release packaging includes only intended content and preserves asset licenses/provenance.

**Acceptance tests:** reproducible release metadata, locked dependency install, declared Unity/server compatibility matrix, built package contents match allowlisted manifest, generated references remain in sync.

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
