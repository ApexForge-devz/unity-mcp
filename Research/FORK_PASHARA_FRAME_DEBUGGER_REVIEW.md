# Specialist Fork Review — Pashara Frame Debugger for CoplayDev MCP

**Reviewed:** 2026-10-14  
**Candidate:** [pashara/Unity-MCP-CoplayDev-FrameDebugger](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger)  
**Upstream project:** [sabriboughanmi/Unity-MCP-FrameDebugger](https://github.com/sabriboughanmi/Unity-MCP-FrameDebugger)  
**Target integration point:** `ApexForge-devz/unity-mcp`, branch `beta`, existing `manage_profiler` tool  
**Decision:** **ADAPT selectively; do not install or copy the whole extension blindly.**  
**Review type:** Source and documentation inspection only. No Unity Editor integration test, C# build, or package test run was performed.

## Executive assessment

This is a useful in-scope specialist project because it improves **read-only rendering diagnosis** after the agent has updated a persistent scene-generator script and regenerated the output scene. It does not depend on external asset generators or image-generation providers.

The target's current Frame Debugger integration has only enable, disable and paginated `get_events` actions. Pashara's derivative develops the workflow around those APIs: a pollable capture operation, immutable event sessions, event-level queries, summaries, heuristic diagnostics, profile export/diff and render-target previews.

The strongest reason to study it is **capture correctness**. Frame Debugger state is not a stable list while Unity is replaying and rendering events. The candidate detects changed event trees, checks that the payload corresponds to the requested event, retries a bounded number of times, and marks persistently stale payloads as unavailable rather than attributing the wrong shader/mesh/event data. Those are worthwhile invariants for any future MCP rendering-inspection extension.

However, this is not a drop-in package recommendation. It relies heavily on Unity Editor internal APIs via reflection, requires Play Mode and a renderable Game View for local capture, retains sessions only in memory, and contains profile-path handling that needs hardening before the I/O helpers are copied.

## 1. Relevant target baseline

The target's [`FrameDebuggerOps.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs) has three actions:

- Enable the Frame Debugger (opens its window and expects the game to be paused).
- Disable the Frame Debugger.
- Read a paginated event list, with reflection compatibility for namespace/signature differences between Unity versions.

It extracts selected event, shader/pass, render-target and mesh/count fields. A per-event data-read exception is caught and skipped; the returned contract does not clearly tell the caller how many rows or fields were unavailable. Repeated page requests also do not share a captured session identifier or stable event-tree hash, so callers should not assume each page describes the same immutable frame.

The public wrapper already routes these operations through the single [`manage_profiler`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_profiler.py) tool. Prefer expanding that existing tool/action family if the requirements are accepted, instead of adding a separate top-level MCP tool per operation.

## 2. High-value contributions worth adapting

### 2.1 Stable capture and explicit completeness

The candidate creates an asynchronous capture operation with an ID, state, progress, timeout, cancellation and error code. The capture processes work incrementally on Editor update ticks instead of trying to inspect hundreds of draw events in one synchronous handler.

It records capture metadata such as Unity version, render pipeline, graphics API, event count and event-tree/data hashes. Captured sessions are treated as read-only snapshots, so subsequent searches or detail queries do not re-trigger the Frame Debugger.

**Adaptation target:** preserve the current simple event-list action for lightweight queries, but add a bounded capture/session path for tasks where consistency matters. Return explicit completeness and unavailable-field reasons, not silent omissions. Keep default responses small and make detailed event reads opt-in.

### 2.2 Live and animated scene correctness

The candidate's documented/fake-backend-tested flow pauses the game, re-renders the same captured frame in place instead of advancing the simulation one frame per event, sets the event limit to the requested event index plus one, and verifies that the payload's own event index matches. If the event tree changes during replay, it re-reads the tree up to three times. If a payload never becomes current, it labels that detail unavailable.

This is materially better than simply trusting an event index against a mutable event list. It is also where the candidate adds the most value over the target's current read loop.

**Required invariants if adapted:**
- The simulation frame does not advance while detailed capture steps through events.
- Details are never stored against a different event index.
- Tree changes trigger bounded restarts, then a clear failure instead of unbounded work.
- Persistently unavailable event data remains explicitly unavailable; it is never replaced with a previous/next event's payload.
- Every failure/cancellation path restores the user's prior pause state, Frame Debugger state, event limit and any window state the tool changed.

### 2.3 Targeted search, summary, diagnostics and comparison

The candidate adds filtered/sorted/paged event search, on-demand detailed state, summary totals grouped by shaders and render targets, heuristic diagnostics, JSON profile export and before/after comparison. Profiles carry a schema name/version, Unity/package versions, capture metadata, summary totals, events, details and diagnostics. Comparisons match events using several keys and return metric deltas.

These features are useful when a generated scene visually differs from expectation—for example, a material is on an unexpected shader/pass, render targets change, draw counts increase, or a clear/blit pattern looks suspicious. However, the comparison uses fallback matching rules, so a matching event/delta should be treated as diagnostic evidence with a confidence/identity basis, not a guaranteed semantic match. Rule-based diagnostics are explicitly heuristic rather than proof of a GPU bottleneck.

**Workflow fit:** use screenshots and scene-specific assertions first. Invoke Frame Debugger analysis only when the visual result suggests a rendering issue that those checks cannot explain.

## 3. Critical integration detail: sessions do not survive generator recompilation

The candidate's `FrameSessionManager` holds sessions in memory and clears them on `AssemblyReloadEvents.beforeAssemblyReload` and Editor quitting. That is reasonable cleanup behavior, but compiling a changed generator script can trigger a domain reload. Therefore, an agent cannot assume it will be able to capture a “before” session, change its generator, regenerate and then query the old in-memory session afterward.

**Reliable before/after workflow:**

1. Capture the baseline scene in Play Mode using an appropriate bounded preset.
2. Export that baseline to a versioned JSON profile **before** editing/compiling the generator.
3. Edit the authoritative generator source and regenerate the explicit output scene.
4. Enter Play Mode and capture the new frame after regeneration.
5. Compare the saved baseline profile to the new session/profile; inspect only relevant event details and optional render-target previews.
6. Close sessions and delete/retain profiles according to an explicit project-owned artifact policy.

This limitation should be visible in tool descriptions. A profile is the persistence boundary for cross-domain-reload comparison; the live session ID is not.

## 4. Risks and adoption safeguards

### 4.1 Unity internal APIs and version compatibility

The implementation reflects into `UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility`, event data types, fields and other Editor internals. The package's capabilities action checks the reflection contract and reports compatibility errors; that is useful but does not substitute for live validation across the Unity versions and graphics APIs the target supports.

The package manifest declares Unity `2022.3` and dependency `com.coplaydev.unity-mcp` `9.3.2`. Verify these against the target branch and supported editor matrix. Port only behind a capability/health check and stable adapter interface; keep Unity-version-specific reflection out of generic MCP handlers.

### 4.2 Requires a playable rendering context

Local frame capture requires Play Mode and a renderable Game View. It cannot be the default validator immediately after an Edit Mode generator run. It can also be relatively expensive: detailed mode replays the frame event by event and has bounded but potentially sizeable per-event work. Use a summary preset first, then a constrained number of details for relevant events.

Remote Development Player capture is optional scope. Do not make remote player support a dependency for improving local Editor diagnosis.

### 4.3 Session and resource limits

The candidate keeps at most four sessions in memory and requires explicit session closure to release them earlier. Active operations are cleaned on assembly reload and Editor quit. Preserve those lifecycle safeguards, make one-capture-at-a-time behavior explicit, and ensure cancellation/timeout/disconnect restores Editor state. Tests should cover both successful cleanup and exceptions during cleanup.

### 4.4 Profile path validation needs changes before reuse

The reviewed `FrameProfileIO.SaveProfile` converts a path to an absolute path and then checks it with `absolutePath.StartsWith(projectDir)`. A string-prefix test is not a path-boundary check: a sibling path such as `<project>-backup/...` can share the project's prefix while residing outside it. On case-insensitive filesystems, inconsistent casing is another issue with the default ordinal comparison.

The reviewed `LoadProfile` checks file existence and schema name, but does not confine reads to the project/export directory. Export/profile comparison therefore should not be copied with the read helper unrestricted.

The save method also writes a temporary file, deletes the existing target when overwrite is allowed, then moves the temp file into place. If the move fails after the delete, the previous profile may be lost.

**Required safeguards before adapting any profile I/O:**
- Canonicalize the root and candidate paths, then enforce a path-boundary-aware containment check (including relative `..` paths and OS path semantics); consider symlink policy explicitly.
- Default profile exports to a specific MCP-owned project folder; reject paths outside it unless a separate explicitly authorized workflow is designed.
- Apply the same confinement check on reads as on writes.
- Require overwrite opt-in; prefer create-new/unique outputs by default. For replacement, use a supported atomic replace/backup strategy instead of deleting the last good file before the new one is in place.
- Test rooted paths, sibling-prefix escapes, mixed separators, `..` escapes, existing files, failed replacement and cleanup of temp files.

### 4.5 Test evidence is encouraging but not independent compatibility proof

The candidate has substantial fake-backend EditMode tests for capture state transitions, timeout/cancellation, event-tree drift, stale event payloads, remote disconnection and profile comparison. These tests are well aligned with the tricky logic. The repository README also says the CI/release workflows are manual and have not run on a CI runner; its changelog records earlier test counts, but this review did not run those tests or validate them against live Unity graphics backends.

Before merging any adapted code, run the focused tests and smoke-test the actual supported Unity Editor/graphics API combinations. Fake-backend success cannot prove every reflected Editor-internal member will resolve in a real Editor.

## 5. Recommended implementation plan

1. **First, harden the existing target event API:** capture a stable event-tree identity, include completeness/diagnostic information, bound event and detail payload sizes, and make errors explicit. Avoid implementing the entire specialist package in one change.
2. **Add a pollable capture operation and immutable in-memory session only if needed:** operation ID, state/progress, cancellation, timeout, cleanup, prior Editor state restoration and one-active-capture policy.
3. **Add focused read-only queries:** summary, event search/page, on-demand event detail, then profile diff/diagnostics. Preserve evidence source and comparison confidence.
4. **Make profile export safe and useful across compile/reload:** project-owned output root, canonical path validation, schema/version validation, explicit overwrite semantics and robust replacement. Export the baseline before a generator recompilation/domain reload.
5. **Adopt only after acceptance tests pass:** fake-backend tests plus live Editor capture on supported Unity versions and graphics APIs. Treat missing reflection members as a structured “unsupported/incomplete” result, not a silent success.

## Acceptance tests

| Scenario | Required result |
|---|---|
| Event payload read fails | The row/detail is marked incomplete or unavailable; it is not silently misattributed. |
| Payload index differs from requested index | Reject or retry the capture; never store it as the requested event. |
| Event tree changes during replay | Re-read within a fixed retry budget; fail with a clear reason if it never settles. |
| Animated Game View | Captured simulation state does not advance across per-event replay. |
| Cancel, timeout or thrown exception | Restore pause state, Frame Debugger enabled state, event limit, window ownership and temporary resources. |
| Generator recompilation between profiles | Baseline is available from a saved profile; the workflow does not depend on an in-memory session surviving reload. |
| Profile path outside export root | Read/write is rejected, including sibling-prefix and `..` escapes. |
| Existing export path | Default is create-new; overwrite only follows an explicit safe policy and keeps the old profile recoverable on failure. |
| Reflection contract differs by Unity version | Capabilities/health returns a structured unsupported result; partial extraction reports completeness accurately. |
| More events than budget | Capture remains bounded, and truncation/completeness is explicit. |
| Unsupported visual diagnosis | Heuristic findings are labeled as evidence/hints, not deterministic proof of a render or performance defect. |

## Source references

- Target [FrameDebuggerOps.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs)
- Target [manage_profiler.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_profiler.py)
- Candidate [README](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/README.md)
- Candidate [FrameCaptureOperation.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameCaptureOperation.cs)
- Candidate [FrameCaptureOperationManager.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameCaptureOperationManager.cs)
- Candidate [FrameSession.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameSession.cs)
- Candidate [FrameProfile.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameProfile.cs)
- Candidate [FrameDebuggerTests.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Tests/Editor/FrameDebuggerTests.cs)
- Candidate [package.json](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/package.json)
