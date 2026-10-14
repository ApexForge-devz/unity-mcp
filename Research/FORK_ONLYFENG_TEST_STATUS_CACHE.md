# Direct Fork Review — onlyfeng Test-Run Status Cache and Configuration/Path Changes

**Target fork:** [onlyfeng/unity-mcp](https://github.com/onlyfeng/unity-mcp), branch beta  
**Relationship:** Direct fork of CoplayDev/unity-mcp  
**Compare snapshot:** 52 ahead, 0 behind; 29 changed files.  
**Reviewed:** 2026-10-14  
**Scope:** async test-job cache semantics plus selected surrounding run-tests/transport changes. Source review only; no Python or Unity test suite was run here.

## 1. Highest-value delta: test-job status cache that respects result detail level

Fork source: [run_tests.py](https://github.com/onlyfeng/unity-mcp/blob/beta/Server/src/services/tools/run_tests.py), blob dcee543ff450e26483d54e4ec1582bd126dc1bde.  
Fork regression tests: [test_run_tests_async.py](https://github.com/onlyfeng/unity-mcp/blob/beta/Server/tests/integration/test_run_tests_async.py), blob f6daeef4bd97e25f14ea55d6a210dd344e525237.  
Target source: [run_tests.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/run_tests.py), blob 4994b2491397f3bad42480ac9968758b0fc09196.

The fork adds a bounded LRU cache for job status snapshots (maximum 20 jobs) and uses it when a status poll fails due to transient Unity transport/timeouts:
- Cache identity includes transport mode, authenticated user ID where applicable, normalized Unity instance hash and job ID. This helps prevent one user's or project's cached job snapshot from being returned for another.
- Cached data gets its own timestamp so “time since this server observed status” can be reported separately from the Editor's last-progress timestamp.
- A terminal snapshot can satisfy a poll immediately if the cached result includes the detail level the caller requested.
- Cache detail metadata records whether it contains all results or only failed/skipped results; a lower-detail terminal snapshot is not treated as authoritative when a caller asks for higher-detail results.
- A lower-detail terminal response does not clobber an existing higher-detail terminal response in the LRU.
- The wait loop continues through transient transport errors until the deadline and can return a cached terminal result immediately; at deadline it can return a clearly marked degraded cached snapshot with the transport error and cache age.
- A genuine tool error (such as an invalid/expired job ID) is not silently masked as a successful cached result.
- Tests cover transient retry, deadline fallback, cache age/stall diagnostics, detail compatibility, protection against low-detail terminal clobbering, LRU recency and per-instance/per-user isolation.

The target already has a mature async job and focus-nudge path, background-task retention, per-job identity in the nudge state and cancellation/timeout handling. The cache described here is an **additional status-resilience layer** not seen in the inspected target file. It is especially useful when Unity enters a modal stall or drops a bridge response after a test completed: the agent need not mistake a transport failure for the test job's actual terminal outcome.

## 2. Important caveat: terminal cache should be monotonic under concurrent polling

The fork's tests cover detail-level preservation and scope isolation, but the reviewed _remember_test_job_data function is not passed the poll's observation_order, even though callers allocate an observation order for focus-nudge ordering. Under overlapping polls, an older poll that began while a test was running could return late and replace a cached terminal result with a stale running snapshot. That is a **code-derived race hypothesis, not a reproduced bug**.

**Recommendation:** include an observation sequence (or a monotonically increasing Editor job revision/last-update field) in cache entries and reject cache writes older than the current observation. Add a deterministic test where an older “running” response is released after a newer terminal response. Preserve the detail-superset rule too. Do not infer terminal status based solely on transport success/failure.

Other cache policy questions:
- A running snapshot returned after a long stall should stay marked degraded and not be confused with live status.
- TTL/eviction behavior should be explicit. The fixed 20-entry LRU bounds memory; terminal entries can be long-lived in-process, which is safe only if job IDs aren't reused within the same project scope.
- Make sure every execution mode with concurrent clients supplies the same user/instance identity used by the cache key.

## 3. Regression coverage observed

The added fork test functions include:
- returning cached status after a transient transport retry;
- using cache age to diagnose a suspected server/editor stall;
- not masking specific blocked reasons or genuine errors;
- continuing polling after transient failure and falling back at deadline;
- returning a cached terminal result without waiting for the full timeout;
- preventing a terminal snapshot that lacks requested details from satisfying a high-detail poll;
- preventing lower-detail terminal data from overwriting richer terminal data;
- scoping cached jobs by Unity instance and user, and allowing the same instance's cache identity to survive session reconnect.

These tests are specific and map to the code's real contract. They have not been executed in this research session.

## 4. Other source deltas in this fork: investigate individually

Compare output for onlyfeng reports changes in:
- AssetPathUtility and PathResolverService;
- client configuration merging/OpenClaw/OpenCode support and skill sync;
- WebSocketTransportClient;
- run_tests async polling and tests.

The fork's recent commits include “drop blocking uvx preflight” and tests for the “uv tool run” prefix plus non-blocking WebSocket execution. Those are potentially useful for the target's Editor responsiveness and launch-path handling but need separate narrow reviews. Do not treat the entire 52-commit delta as a single adoptable patch.

The inspected WebSocket changes include connection URI candidate fallback and more explicit lifecycle-loop cleanup, but the displayed establishment/connect path has sensitive differences in how endpoint fallback and error details are recorded. Verify endpoint authority, auth-header behavior, cancellation and config persistence before adopting any transport code.

## 5. Recommendation

**Decision: ADAPT the result-cache contract (P2 reliability), do not merge the fork.**

Recommended change set:
1. Add a small, bounded test-status cache scoped by transport + authenticated caller + Unity instance + job ID.
2. Store result-detail fidelity as part of the cache record, and never report a terminal result as fully detailed when the request asked for more.
3. Only use cache to bridge clearly classified transient transport failures; return true test/job errors unmodified.
4. Mark fallback results as stale/degraded, with observation time and suspected-stall metadata.
5. Make cache writes monotonic under overlapping polls; add the missing out-of-order response test.
6. Add isolation tests for remote multi-user hosting, two Unity instances, a reconnect after domain reload, and eviction/TTL.

This is a narrow, testable adaptation to existing job infrastructure; it does not require a new job manager or changes to Unity's native Test Runner.
