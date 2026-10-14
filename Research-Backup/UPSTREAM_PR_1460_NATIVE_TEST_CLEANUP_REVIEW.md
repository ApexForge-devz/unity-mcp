# Upstream PR #1460 — Native Test-Run Cleanup After Timeouts

**Reviewed:** 2026-10-14  
**PR:** [CoplayDev/unity-mcp #1460](https://github.com/CoplayDev/unity-mcp/pull/1460)  
**Target baseline:** beta at 142ea0af4890337f0ff6658df61db23982313717  
**PR head inspected:** 25be22066fdb11fed07948659462cf28c89279bf  
**Status at review time:** Open, not merged  
**Decision:** **ADAPT AFTER FOCUSED VALIDATION**, not a blind cherry-pick.

## Executive assessment

The target has a real lifecycle gap: a caller-side initialization timeout can mark the MCP job failed while Unity's native Test Runner is still executing or restoring scenes/preferences. A terminal MCP job is not proof that the native run is idle. Releasing the operation lock too early risks overlapping runs and late callbacks being attributed to another job.

PR #1460 separates MCP job outcome from the native Unity run lifecycle. It retains Unity's run GUID, tracks startup/timeout/cleanup state, applies background-interaction throttling changes before Execute, cancels after timeout when supported, observes native cleanup and persists relevant state through SessionState. The PR is still open on the exact beta base used for this review; these changes are not in the current target baseline.

## 1. Target baseline gap

Target TestRunnerService.RunTestsAsync calls TestRunnerApi.Execute(settings) but discards the returned native run ID. TestJobManager tracks the MCP job by its own ID and can force-clear a stale job by clearing the current job ID. Managed tasks and the operation lock are recreated across a domain reload, so the baseline cannot robustly associate the recovered MCP job with the specific Unity native run.

A timeout must distinguish:
- **MCP status:** the request exceeded an initialization deadline or the caller stopped waiting.
- **Unity status:** native run active, cancelling, restoring state, idle or unknown.

Do not treat the first as evidence of the second.

## 2. What the PR improves

- Retains the native Unity run GUID alongside the MCP job ID.
- Disables background interaction throttling before Execute for both EditMode and PlayMode, restoring prior preferences after ordinary start failures and completed cleanup.
- On initialization timeout, fails the MCP job but retains native ownership. When supported, cancellation waits for RunStarted so Unity can establish the open-scene restore state.
- Tracks cleanup after RunFinished and OnError, retaining pending results while the Test Runner restores state.
- Persists run GUID, timeout and cleanup-pending flags through domain reload.
- Treats native-state query exceptions as unknown/possibly active, not idle; it retries for a bounded 30-second window and fails visibly while retaining ownership.
- Adds regression tests for timeout/cancel/reload, delayed callbacks and native-query failures.

The safety invariant is correct: keep the native run lock until Unity is positively idle, or expose uncertainty and require explicit recovery.

## 3. Residual risks

### 3.1 Private Test Framework reflection is version-sensitive

The PR resolves cancellation/status APIs with reflection, including non-public TestRunnerApi members and internal holder/runner methods. This is not a stable cross-version contract.

The PR reports runtime execution on Unity 6000.3.20f1 / Test Framework 1.6.0. Test Framework 1.1.31/1.1.33 were checked at source level, but not runtime-verified; the PR's Unity CI matrix had unavailable-version legs skipped.

**Gate:** test method discovery and real cancellation/status behavior across every supported Test Framework line. If the contract cannot be resolved, return explicit unsupported-query status and keep ownership until a terminal callback or explicit recovery. Never assume idle.

### 3.2 Active before RunStarted can remain locked indefinitely

The PR waits for RunStarted before cancelling a timed-out run so cancellation does not disrupt Unity's scene save/restore setup. However, a run that remains reported active and never emits RunStarted, RunFinished or OnError will not reach the 30-second query-failure timeout, because that timeout bounds repeated *query exceptions*, not a healthy query that keeps returning active forever.

This is conservative against overlap, but not complete self-recovery.

**Gate:** add explicit policy/test for a timed-out run that remains active before RunStarted. Demonstrate Unity always reaches a callback/idle state or provide a safe operator-visible recovery path without silently enabling another run.

### 3.3 A monitor exception can detach the monitor while ownership remains

In the PR-head CompleteRunWhenIdle, an outer catch removes the update callback and logs the exception. The main native-state query is wrapped by IsNativeRunActiveOrUnknown, so that known query-failure case is handled. But an exception in later completion/finalization work could still enter the outer catch, detach the monitor and leave the owning job/lock in a blocked state with no active retry.

**Gate:** inject cleanup/finalization exceptions and verify the monitor retries or persists a visible owner-retaining recovery state. Never release ownership just because cleanup code threw.

### 3.4 clear_stuck must not allow overlap after reload

The PR warns that run_tests(clear_stuck=true) clears the MCP job queue entry but may not release the native test-run lock. In the current process, service-level tracking may still block a new run; after a later domain reload, clearing the only current-job association can prevent recovery code from reattaching to a native run that is still active.

**Gate:** test clear_stuck=true while native activity is real, then reload the domain and attempt a new run. If activity is active or unknown, the new run must stay blocked. Prefer reconciliation that retains the native run correlation rather than deleting all ownership evidence.

### 3.5 SessionState is not process-restart persistence

The change persists through domain reload in the same Editor session. It does not make native Test Runner state durable across closing/restarting Unity. A restart should mark the old run interrupted/unknown, not restore a stale Running state as live or successful.

## 4. Verification and maturity

The PR was open/unmerged on 2026-10-14. Its author reports:
- EditMode lifecycle and PlayMode tests on macOS, Unity 6000.3.20f1 / Test Framework 1.6.0.
- Python tests: 1,575 passed, 18 skipped.
- Compile checks in a fork workflow for Windows/macOS/Linux compile defines.
- Four other Unity-version CI legs skipped because the Editors were unavailable; licence-dependent fork Unity/E2E jobs were skipped.
- Additional native-query retry tests passed locally on 6000.3.20f1; Windows/Linux and old Test Framework runtime behavior remain unverified.

This is useful author-reported evidence, not an independent test run by this review. The latest PR head includes a follow-up to an earlier review comment about query exceptions; do not assess only the initial commit.

## 5. Adoption plan

1. Adapt the state model rather than blindly porting private API names: MCP job ID, Unity run ID, timeout result, native-started flag, cleanup-pending flag and pending result remain separate.
2. Validate private API behavior across the declared Test Framework matrix.
3. Add tests for pre-RunStarted hangs, monitor exceptions and clear-stuck/domain-reload.
4. Keep native ownership while state is unknown; make any manual recovery semantics explicit and distinguish queue cleanup from terminating a native run.
5. Restore throttling and Enter Play Mode options only after confirmed cleanup or a documented destructive recovery.
6. Verify scene state after cancellation at each stage.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Timeout before RunStarted | MCP job fails, but native ownership remains until cleanup is confirmed; no overlap. |
| Timeout after RunStarted | Cancellation uses the same native GUID; a late passing callback does not overwrite the timeout failure. |
| RunFinished before native teardown completes | Result is retained; Editor preferences restore only after idle is confirmed. |
| Reload during cancellation/cleanup | Native GUID and pending outcome are restored; no new run is spawned. |
| Native activity query throws | Monitor retries and keeps the lock; no false idle result. |
| Query remains unavailable for 30 seconds | Unconfirmed state and recovery guidance are exposed; ownership remains. |
| Native state stays active forever before RunStarted | Explicit bounded liveness/recovery policy; no silent overlap. |
| Cleanup/finalization throws | Monitor is retried or owner-retaining recovery is persisted; no invisible permanent lock. |
| Private API missing on older Test Framework | Capability gap is explicit; no false confirmation of idleness. |
| clear_stuck=true then reload while native run active | New run remains blocked until native activity is idle or explicitly terminated. |
| Editor quits with cleanup pending | Relaunch reports interrupted/unknown, not live or successful. |
| Pre-existing interaction preferences | Exact values restore after success, timeout, start exception, cancellation and recovery. |

## Source references

- [PR #1460](https://github.com/CoplayDev/unity-mcp/pull/1460) (open at review time)
- [PR files](https://github.com/CoplayDev/unity-mcp/pull/1460/files)
- Target [TestJobManager.cs](https://github.com/CoplayDev/unity-mcp/blob/beta/MCPForUnity/Editor/Services/TestJobManager.cs)
- Target [TestRunnerService.cs](https://github.com/CoplayDev/unity-mcp/blob/beta/MCPForUnity/Editor/Services/TestRunnerService.cs)
- Target [TestRunnerNoThrottle.cs](https://github.com/CoplayDev/unity-mcp/blob/beta/MCPForUnity/Editor/Services/TestRunnerNoThrottle.cs)
- PR-head [TestRunnerService.cs](https://github.com/CoplayDev/unity-mcp/blob/25be22066fdb11fed07948659462cf28c89279bf/MCPForUnity/Editor/Services/TestRunnerService.cs)
- PR-head [TestJobManagerLifecycleTests.cs](https://github.com/CoplayDev/unity-mcp/blob/25be22066fdb11fed07948659462cf28c89279bf/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Services/TestJobManagerLifecycleTests.cs)
