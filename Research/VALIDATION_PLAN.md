# Validation Plan and Adoption Gates

Do not mark a recommendation “done” because code exists or a unit test was added. Validation should distinguish static review, server unit tests, Unity EditMode/PlayMode, live Editor/client interoperability and benchmarks.

## Gate A — Source and upstream recheck

Before starting an item:
- fetch the current target base and record its SHA;
- check current upstream PR/issue/commit state for overlapping changes;
- verify whether the capability already exists and read its tests;
- record exact expected behavior, supported Unity/client versions, failure semantics and out-of-scope constraints.

## Gate B — Server contract tests

Cover policy and contract boundaries:
- privileged execution denied/allowed by server policy as configured, regardless of `safety_checks`, aliases, batch or proxies;
- batch order, limits, fail-fast/continue behavior, partial outcomes and truthful sequential semantics;
- modern/legacy protocol negotiation and deterministic discovery;
- valid/invalid scalar normalization and schema validation;
- universal output budget for nested data, exceptions, images and async retrieval;
- continuation/result-handle expiry and isolation by caller/project/Editor;
- concurrent duplicate request IDs, argument mismatch and in-flight reservations;
- startup configuration merges preserve user-owned client settings.

## Gate C — Unity EditMode/integration tests

- compiler-cycle correlation using CompilationPipeline events, diagnostics capture, domain reload persistence and failure gating;
- generator invocation/result receipts and source hash/revision correlation;
- deterministic generator re-run with no duplicate/stale generated content;
- targeted assertion output, missing scripts/references, save failure and last-known-good recovery;
- shared server start/stop, owner/borrower exit order, stale PIDs/ports and attach-versus-stop races;
- FIFO main-thread dispatch, queue capacity, cancellation, bounded shutdown and modal liveness;
- exact-modal identity, stale/changed dialog rejection and verified answer outcome;
- screenshot dimension/view-count caps and explicit capture-source/fallback semantics.

## Gate D — Live Editor and real MCP clients

Run on each supported Unity/client combination, not just mocks:
- modern stateless and supported legacy clients across supported transports;
- multiple open Unity projects/Editors and worktrees;
- script edit → compile/reload → generator → assertions → save → visual inspection end to end;
- modal/busy/stalled Editor recovery;
- Play Mode startup and capture in focused/unfocused states;
- config/launcher startup and shared server shutdown;
- screenshot and profiler calls on the exact supported Editor platforms.

A test that was added but not executed is not a pass. An Editor API behavior inferred from source is not a live integration result.

## Gate E — Benchmarks

Capture baseline and post-change values with repeatable fixtures:

| Area | Metrics |
|---|---|
| Tool discovery / compaction | serialized schema bytes; real tokenizer count where available; required-description/warning retention; tool-choice accuracy; extra retrieval calls |
| Result shaping | p50/p95 serialized bytes, truncation rate, retrieval round-trips, memory peak for large results |
| Asset search | time, allocations and asset metadata/preview materializations versus total matches and page size |
| Generator workflow | edit-to-ready, compile duration, first-run latency, receipt latency, end-to-end task success, repeat-run semantic diff |
| Dispatch/liveness | queue wait/execution time, queue depth, Editor heartbeat age, false timeout/unknown outcome and cancellation behavior |
| Visual/profiler tools | render dimensions/pixels, peak allocations, request latency, payload size, capture-source accuracy |

Report machine, Unity version, package revision, test fixture, sample count and whether the result is measured or estimated. Do not claim speedups from architectural intuition.

## Rollout order

1. Contract/security tests and output budget definitions.
2. Protocol migration spike and whole-registry schema/task benchmark.
3. Generator compile/result lifecycle contract.
4. Identity/shared-server and queue/modal failure cases.
5. Specialist features only after source recheck, capability gap proof and measured demand.

## Current state

The research materials are source-review artifacts, not a test report. During this pass, no Unity Editor compilation, EditMode/PlayMode suite, live MCP client matrix, or performance benchmark was executed. The reviewed draft PR #1 explicitly left local/Unity execution unclaimed. Check its current CI status independently before relying on its test coverage.
