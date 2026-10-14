# Priority Roadmap

This is the decision layer for implementation planning, not a claim that every item is a reproduced production defect. Recheck the current baseline and upstream proposals before starting a feature.

## Phase 0 — Correctness and trust boundaries

### P0.1 Enforce policy for in-process C# execution

**Decision: ADAPT.** Add server-enforced capability policy before final Unity dispatch. Support an explicit trusted-local development mode and a more restrictive remote/unattended mode. Dangerous calls must not become authorized because `safety_checks=false`, a generic proxy, alias or batch wrapper was used.

**Gate:** tests prove unauthorized calls are denied before dispatch across all entry paths; policy survives reconnect/domain reload; denial is auditable. Do not describe regex checks as a sandbox.

### P0.2 Make batch behavior truthful and results bounded

**Decision: ADAPT.** Document that Unity-side commands execute sequentially on the Editor main thread. Preserve order, fail-fast behavior and partial outcomes; state clearly that batches are not transactional. Add a concise summary mode and a hard serialized response budget with recoverable detail when necessary.

**Gate:** ordering/count/partial failure invariants hold; oversized nested output is capped; caller disconnect does not trigger automatic re-execution of an uncertain mutation.

### P0.3 Bind mutations to the intended project and Editor

**Decision: VERIFY / ADAPT.** Test final-route authorization, Editor/project identity, stale ports, multiple Editors/worktrees and shared server start/stop. Reuse existing routing/authentication layers rather than adding a second permission or identity registry.

**Gate:** wrong-project calls fail closed; uncertain owner/connection state never stops an unrelated or shared server.

## Phase 1 — Protocol compatibility and context cost

### P1.1 Modern/legacy MCP compatibility spike

The current dependency cap and per-session tool visibility design should be tested against the 2026-07-28 MCP protocol and FastMCP 4 before changing dependencies. Prototype a stable modern tool catalog, separately configured catalogs/endpoints, or authoritative request-authorization filtering. Retain legacy behavior only where negotiated and tested.

**Gate:** protocol negotiation, deterministic tool ordering, tools/list behavior, resources/prompts, errors, cancellation, auth and all supported transports have contract tests. No framework-major upgrade based only on package release notes.

### P1.2 Universal result budget and retrieval contract

Add a central return-boundary policy: structured short summary by default, configurable fields/pages, hard byte cap, truncation metadata, continuation/result handle, expiry and project/session scoping. Apply the same limit to async status/retrieval paths; do not allow raw oversized output to be materialized without a bound where feasible.

**Gate:** nested outputs/errors/images/async jobs stay within contract, essential failures and pagination metadata survive, and handles cannot cross project/principal boundaries.

### P1.3 Preserve meaning while compacting schemas

Core-first grouping and description compaction are useful, but blind prefix truncation can remove warnings, preconditions, supported actions or path constraints. Prefer curated summaries; preserve full definitions through details retrieval; include an explicit truncation/details indicator.

**Gate:** run a whole-catalog report and warning-focused evaluation. Compare schema bytes/tokens, tool selection and task success on representative requests, not character counts alone.

### P1.4 Normalize arguments before dispatch

Normalize JSON-encoded scalar strings only at a clearly defined boundary, then validate against the declared schema before invoking handlers. Preserve distinctions between invalid input, unsupported action and handler failure; do not broaden behavior silently.

**Gate:** table-driven tests cover numeric/boolean/null/scalar inputs, malformed JSON, ranges/enums, blacklists, and every wrapper that shares the dispatch path.

### P1.5 Reduce eager asset search work

Count matches and apply filters/pagination before materializing expensive asset previews or metadata for every candidate. Preserve exact total/page semantics and folder scoping.

**Gate:** correctness fixtures plus a synthetic large project benchmark demonstrate bounded work for small pages without changing results.

## Phase 2 — Generator lifecycle and Editor reliability

### P2.1 Complete the deterministic generator lifecycle

Persistent generator source → edit with preconditions → compile diagnostics → ready state → one correlated invocation → bounded receipt → code assertions → save → targeted visual diagnosis. See [Script-First Workflow](SCRIPT_FIRST_WORKFLOW.md).

**Gate:** compile failure prevents execution; timeout can be reconciled without blind re-run; receipt proves which generator/revision/scene ran; repeat runs do not accumulate duplicates; failed assertions do not silently commit/save bad output.

### P2.2 Bound main-thread work and improve liveness diagnostics

Keep normal Unity API work serialized on the main thread. Add queue/backpressure and age/timeout telemetry; distinguish “busy but pumping,” “blocked/modal,” “disconnected” and “unknown.” Cancelled client waits must not free a running command slot or imply the command stopped.

**Gate:** FIFO/non-overlap, queue-full behavior, bounded shutdown, modal transitions and cancellation semantics are tested.

### P2.3 Fix shared-server lifecycle and modal identity

Use project/port-scoped ownership metadata and a tested handoff/cleanup lease. Bind modal answers to a short-lived identity representing the exact observed dialog, not an optional title or generic button label.

**Gate:** two-Editor, launcher-exits-first, attach-during-shutdown, stale-modal, same-title/different-buttons and duplicate-answer cases pass.

### P2.4 Improve targeted Editor diagnostics

Prefer direct compiler diagnostics via Unity `CompilationPipeline` events over Console scraping as the primary compile-result source. Use existing screenshots and Frame Debugger features, adding clear pixel/view count/payload budgets and capture-source semantics. Keep full scene/hierarchy dumps out of the default loop.

**Gate:** compile cycle IDs and diagnostics survive domain reload; screenshot requests return bounded data and explicit readiness/fallback state.

## Phase 3 — Specialist capability, only after proof of need

- Asset dependency/reverse-reference analysis, with conservative “potentially unused” semantics; never automatic deletion.
- Queryable Memory Profiler snapshot analysis after API/package/version validation; existing capture/list/file comparison is not object-level querying.
- Frame Debugger capture consistency improvements where the current handler is demonstrably deficient.
- Editor test lifecycle cleanup, Play Mode startup readiness, nonblocking status refresh, custom server instructions, console filtering and fixed Game View size only after checking their upstream PR/merge status.
- Play Mode input simulation or DOTS/ECS tooling only as separately scoped optional modules with explicit supported-version matrices.

## Defer for the intended agent workflow

One-off scene-object mutation, prefab override apply/revert and related manual-edit convenience are not the default workflow; generator source + targeted assertions should own repeatable scene changes. Don't add a new screenshot subsystem, batching system, editor registry, result server or operation façade without demonstrating a gap and measuring it.

## Non-goals

No external or local image/mesh/audio/video generation integrations, provider APIs, SDKs, credentials, remote generator jobs, or provider-specific artifact pipelines. No whole-repository rewrite and no feature port solely because a fork contains it.
