# Reliability, Safety and Ownership

This document groups cross-cutting invariants. Prefer fixing them at existing dispatch, transport, routing and Editor lifecycle seams over adding duplicate frameworks.

## 1. Authorization and dynamic code

The Unity `ExecuteCode` handler compiles and runs supplied C# in-process. Pattern checks are useful guardrails but not a general sandbox, especially when callers can disable checks. Apply policy on the server before final dispatch, covering aliases, proxies and batch calls as well as the primary tool.

Suggested modes:
- **Trusted local development:** explicit, documented access to the full workflow.
- **Remote/unattended:** privileged C# execution disabled by default or enabled only through administrator-controlled policy; selected destructive operations may require approval.
- **Auditing:** record decision, caller/project/Editor identity, operation class and request ID without logging secrets.

A filesystem allowlist can improve destructive-file-operation ergonomics but does not make arbitrary C# safe. Do not describe regex matching as isolation.

## 2. Identity and shared-server lifecycle

The baseline already has authentication and multi-Editor routing. Validate their invariants rather than creating another registry:
- identify both project/worktree and the intended Unity process before a mutation;
- reject stale port/PID records and unrelated listeners;
- scope persistent owner/port/token metadata by stable project identity and port;
- separate “this Editor launched the server” from “this Editor may stop it now”;
- define a safe handoff/cleanup lease when the launcher exits before borrowers;
- on unknown, malformed, timed-out or unavailable client-count probes, leave the shared server running;
- resolve or explicitly constrain attach-versus-stop races;
- preserve user-owned MCP client configuration during automated configuration.

Source review found global EditorPrefs entries among the PID/server metadata and a startup stop-before-port-check path. The upstream shared-server proposal improves some isolation but its SessionState launch marker can be lost when the launcher exits, and its check-then-stop flow can race. Do not label ownership fixed without lifecycle tests.

## 3. Dispatch, cancellation, retries and idempotency

Unity API calls normally need serialized main-thread execution. Keep FIFO/no-overlap behavior and add explicit queue capacity, queue age, per-command timing, cancellation state and bounded shutdown.

A caller timeout/disconnect does not prove the Unity operation stopped. For mutations:
- distinguish request accepted, queued, running, completed and outcome-unknown;
- return a request/run ID for reconciliation;
- never blindly retry a potentially non-idempotent operation;
- use idempotency keys only with a defined principal/project scope, argument fingerprint and expiry;
- reserve an in-flight key atomically before execution if concurrent duplicate requests are possible;
- keep partial outcomes for non-transactional batches.

Editor liveness telemetry should distinguish responsive, slow/busy, modal-blocked, disconnected and unknown. A response from a background thread does not itself prove the Editor main loop is pumping.

## 4. Modal handling

A modal can stall normal dispatch, but a liveness probe must not block behind the same main-thread queue it is measuring. Bounded out-of-band snapshots and short OS timeouts are useful patterns. When answering a dialog, bind to a short-lived identity of the exact observed window/title/button set and same Unity instance; do not accept an optional title plus generic “OK” when a different dialog could have appeared. A queued click is not proof of success unless the intended dialog is verified to have closed.

Bound sampler work, queue growth and shutdown. Classify Unity-drawn modals that cannot be safely answered as blocked but non-answerable instead of guessing.

## 5. Scene external-change guard

The reviewed draft's mtime-based external scene-change detection may miss changes when timestamps are preserved or coarse-grained. This is a risk hypothesis, not a reproduced defect. Consider a cheap signature (mtime + length) and conditional hash of known open-scene files when stronger detection is needed; benchmark hashing cost on large scenes. Preserve explicit auto/reload/keep-editor behavior and recoverability on save failure.

## 6. Bounded visual/profiler diagnostics

The target already has capture and profiler tools. Before adding capability, hard-bound:
- screenshot dimensions/pixel or byte budget before RenderTexture/Texture2D allocation;
- surround/orbit view counts and total rendered pixels;
- serialized result/inline-image size and asynchronous file-readiness semantics;
- profiler sample counts/time ranges and capture-state ownership;
- clear distinction between Game View, Scene View, camera render and fallback output.

Use targeted diagnostics when answering a specific failure; do not default to huge scene dumps, full logs or large image payloads.

## 7. Acceptance gate

Test two open Editors/projects, stale port/PID and unrelated listener cases, launch-owner exit ordering, attach during shutdown, wrong-project routing, policy bypass through aliases/batch, concurrent duplicate IDs, timeout/unknown outcome, modal swap between read and answer, bounded queue shutdown, oversized screenshots and stored-result isolation. See [Validation Plan](VALIDATION_PLAN.md).
