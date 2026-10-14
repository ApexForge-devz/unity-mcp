# Upstream PR #1465 — Asynchronous, Read-Only Editor Window Refresh

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1465](https://github.com/CoplayDev/unity-mcp/pull/1465)  
**Target baseline:** beta 142ea0af4890337f0ff6658df61db23982313717  
**PR head inspected:** 30b5693cb4e45c7d8714b58cc95e62749126d31b  
**Status at review:** Open, not merged  
**Decision:** **ADAPT selectively after focused validation.**  
**Scope fit:** MCP↔Unity operational reliability; no external generator/provider integration.  
**Review type:** Source and test inspection. No Unity Editor or test suite was run by this review.

## Executive assessment

This is not a new scene-authoring feature, but it addresses a failure mode that can undermine every MCP workflow: expensive status-refresh work running on Unity's Editor main thread. If the agent is waiting for a generated scene to compile, regenerate, validate or capture, the Editor must continue pumping updates and servicing the bridge. Synchronous process/path probes and configuration reads in a focused Editor window can cause UI stalls and timing-dependent MCP failures.

The target baseline contains real synchronous work in those paths:

- **PathResolverService.GetUvxPath()** can validate an executable by launching it with the --version argument and a 5-second timeout; path discovery also scans candidate executable paths.
- **McpClientConfigSection.RefreshClientStatus()** runs **CheckClientStatus(client)** synchronously for non-Claude clients. Status handlers read/parse client configuration files and compare package/transport settings. The baseline can also auto-rewrite a mismatched configuration as part of CheckStatus when attemptAutoRewrite remains true.
- **McpAdvancedSection.UpdatePathOverrides()** invokes this synchronous path resolution while updating UI.
- **McpConnectionSection.UpdateStartHttpButtonState()** makes a direct local-server reachability probe from the UI update path.

PR #1465 adds a shared asynchronous refresh mechanism, captures Editor-only inputs on the main thread, runs bounded read-only checks on workers, caches successful results, coalesces repeated focus refreshes and drops stale results before they update the live display. It also prevents ordinary status refreshes from rewriting client configuration files. These are the right invariants to adapt.

## 1. Useful implementation patterns

### 1.1 Capture on the main thread; do file, process and socket work on a worker

**WindowRefreshContext.Capture()** captures Unity/Editor-owned values such as EditorPrefs, package identity, project directory, platform and selected transport. The context's **Resolve()** method performs filesystem/executable discovery and an optional cached offline package probe. **PathResolverService.CaptureWindowResolution()** follows the same split: read overrides/settings while on the main thread, then return a closure for path discovery and executable validation.

**Adapt:** preserve an explicit main-thread/worker boundary. Do not move arbitrary Unity APIs, EditorPrefs, AssetDatabase or live Editor objects onto a worker just because the caller is a UI refresh. Keep worker inputs immutable or copied.

### 1.2 Read-only checks must not modify live model or user configuration

The PR adds **McpClient.CopyForStatusRefresh()**, which clones the model and its default-fields dictionary, and evaluates a status check against that copy. The refresh path uses **attemptAutoRewrite: false**. Results are packaged into a result object and applied to the live UI/model only when the request is still current.

This is especially relevant because target status-check code can repair a mismatched client configuration during an ordinary check. A background “what is the status?” refresh should not silently rewrite a user's MCP client settings.

**Adapt:** separate status inspection from configuration/migration writes. Keep actual configure, unregister and migration actions explicit and user-triggered.

### 1.3 Coalesce focus refreshes, cache successful results and reject stale completions

**EditorAsyncRefresh<T>** supplies:
- a successful-result cache with a caller-defined TTL;
- a single in-flight task per refresh helper;
- latest-request coalescing when inputs change while earlier work is running;
- generation and current-input checks so stale completions do not overwrite newer UI state;
- main-thread application via EditorApplication.update;
- detach/invalidation handling so a closed/removed UI does not receive late results;
- a generic non-sensitive log message instead of dumping private file paths or credentials on failure.

The local-server probe also counts a fresh failed probe rather than counting repeated reads of the cached false result as multiple failures. This avoids false “server is down” streaks driven by window focus/redraws.

**Adapt:** use this shared pattern for low-risk diagnostic/status reads rather than duplicating one-off Task.Run logic in every UI section. Keep caches short and tied to a complete key of relevant settings; invalidate when the transport, server endpoint, path overrides or active session changes.

## 2. What the PR's tests cover

The PR adds **EditorAsyncRefreshTests.cs** and **WindowFocusRefreshTests.cs**. Source inspection shows tests for:

- slow executable validation and local TCP probes returning promptly from the UI call, while the worker executes off the main thread;
- continued Editor updates during worker work;
- repeated focus refreshes joining the existing task and using the successful-result TTL;
- changed inputs coalescing to the latest queued request and discarding old results;
- detach/reattach invalidation;
- stale status results not applying after settings/session changes;
- refresh status checks being read-only, leaving client config files and EditorPrefs unchanged;
- status checks against copies so live client models change only after an accepted result is applied;
- faulted worker tasks, stale/superseded faults, failure-handler exceptions, and subsequent requests still completing;
- all built-in client displays using captured values without accidentally calling Unity APIs on the worker.

The PR author reports 102 related EditMode tests passing on Unity 6000.3.20f1 / Test Framework 1.6.0 on macOS, 1,575 Python tests passing with 18 skipped, and cross-platform C# compile checks. This is author-reported evidence, not an independent run. The PR body says several Unity-version CI legs were unavailable and Windows/Linux runtime, PlayMode and license-dependent E2E runs were not completed. The PR remains open/unmerged.

## 3. Focused hardening items before adoption

The main design is worth adapting, but the refresh scheduler still deserves a small defensive pass.

### 3.1 Guard the current-request predicate and cached callback

In **EditorAsyncRefresh<T>.Poll()**, **isCurrent()** is evaluated before the surrounding try/catch that handles result/application failures. If a supplied current-input predicate throws—for example, while recapturing Editor settings—the exception can escape the Editor update callback rather than being converted to a refresh failure.

Likewise, the successful-cache fast path calls **stillCurrent()** and **onResult(cached, false)** without a local exception boundary. A UI callback exception on a cache hit can therefore take a different path than the same callback applied to a fresh completion.

**Required tests:** throwing isCurrent on a fresh completion, throwing stillCurrent on a cache hit, and a cache-hit onResult failure. The scheduler should report a bounded failure and remain able to service the next refresh; it must not allow a display exception to abort Editor update processing.

### 3.2 Do not cache a result before its application outcome is defined

The scheduler assigns **cached** and **cachedKey** before calling **apply(result, true)**. If applying the result throws, the failure path runs but the value has already been marked as a successful cached result. A later refresh may apply the same cached value via the separate fast path.

This is not proof that it will produce stale UI in every call site, but it leaves the scheduler's cache semantics ambiguous: does a cache entry mean the background read succeeded, or that its result was successfully applied to the display?

**Required correction:** define these separately or ensure applying a result cannot fail after the cache is committed. Add a test proving the intended recovery behavior after an application callback fails.

### 3.3 Bound background work and keep worker failures non-sensitive

Invalidating a request does not cancel an already-running worker task; instead, its result is ignored when it finishes. This is acceptable for short, bounded probes, but should remain explicit. Any subprocess/network operation added to this framework needs a timeout, and repeated settings changes should not create an unbounded queue of workers.

One path-resolution exception reaches **McpLog.Debug**; verify that any worker-side logging path is supported by the Unity runtime/threading contract, or marshal Editor logging back to the main thread. Do not log captured credentials, full user config contents or private environment values.

## 4. Recommended integration decision

**Priority:** P2 — operational reliability; relevant to keeping Unity responsive during MCP-driven work, but not a replacement for the generator lifecycle or scene validation.

1. Port/adapt the async refresh and captured-context pattern for status/diagnostic UI reads only. Keep configuration writes and server start/stop operations explicit and separate.
2. Route the remaining synchronous path-resolution, file-status and local-server probe work off the Editor main thread, while keeping Unity/Editor settings capture and UI state mutation on the main thread.
3. Use read-only status checks against copied models and apply only results whose context/generation still matches the currently displayed client and session.
4. Add the exception/cache consistency tests in §3 before enabling the helper across all refresh call sites.
5. Run the focused Editor tests on the project's supported Unity/Test Framework matrix and measure the UI-call latency plus Editor update continuity with deliberately slow probes. Do not claim compatibility based only on fake-worker tests or compile checks.

Do not copy the PR as an all-at-once UI refactor without testing on the exact package/UI Toolkit version in this target. For the agent's script-first workflow, the main benefit is to stop UI refreshes from starving the Editor during compilation, generator execution and visual validation; no new scene-mutation tools are needed.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| UV executable takes one second to return --version | UI refresh returns promptly; subprocess runs on a worker; Editor updates continue. |
| Local TCP endpoint accepts slowly or is unavailable | UI refresh remains non-blocking; cached failures do not count as new probes. |
| Window focus fires many times | One matching worker runs; successful result is reused within TTL. |
| Settings or Unity session changes while work is pending | Old result and old failure do not mutate current UI/model. |
| Client configuration mismatches expected transport/version | Status refresh reports mismatch without writing the client config file. |
| Worker raises | UI displays a bounded error state; secret file content/path does not appear in logs; next refresh can succeed. |
| isCurrent/cached callback throws | Error is contained by the refresh mechanism; Editor update processing and subsequent requests remain functional. |
| User explicitly configures/unregisters a client | The explicit write operation still works and is not incorrectly served from a read-only cache. |
| Section detaches during work | Completion is discarded; no references are applied to a disposed UI; reattach can refresh. |
| Repeated key changes while a task runs | Pending work is coalesced to the latest key; no unbounded worker/task queue is created. |

## Source references

- [PR #1465](https://github.com/CoplayDev/unity-mcp/pull/1465)
- Target [McpClientConfigSection.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs)
- Target [McpAdvancedSection.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Windows/Components/Advanced/McpAdvancedSection.cs)
- Target [McpConnectionSection.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Windows/Components/Connection/McpConnectionSection.cs)
- Target [PathResolverService.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/PathResolverService.cs)
- Candidate [EditorAsyncRefresh.cs](https://github.com/CoplayDev/unity-mcp/blob/30b5693cb4e45c7d8714b58cc95e62749126d31b/MCPForUnity/Editor/Helpers/EditorAsyncRefresh.cs)
- Candidate [WindowRefreshContext.cs](https://github.com/CoplayDev/unity-mcp/blob/30b5693cb4e45c7d8714b58cc95e62749126d31b/MCPForUnity/Editor/Helpers/WindowRefreshContext.cs)
- Candidate [McpClientConfigSection.cs](https://github.com/CoplayDev/unity-mcp/blob/30b5693cb4e45c7d8714b58cc95e62749126d31b/MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs)
- Candidate [EditorAsyncRefreshTests.cs](https://github.com/CoplayDev/unity-mcp/blob/30b5693cb4e45c7d8714b58cc95e62749126d31b/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Windows/EditorAsyncRefreshTests.cs)
- Candidate [WindowFocusRefreshTests.cs](https://github.com/CoplayDev/unity-mcp/blob/30b5693cb4e45c7d8714b58cc95e62749126d31b/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Windows/WindowFocusRefreshTests.cs)
