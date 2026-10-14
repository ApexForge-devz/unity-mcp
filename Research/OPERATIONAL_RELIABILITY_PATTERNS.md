# Cross-Repository Reliability Patterns — Deferred Jobs, Result Budgets, and Evidence Quality

**Review date:** 2026-10-14  
**Purpose:** Record implementation patterns found during the continued ecosystem review, with source paths and explicit adoption caveats. The recommendations are not a claim that any third-party tests were run in this research session.

## 1. Isuzu UnityMCP — a concrete job/result contract

Repository: [isuzu-shiranui/UnityMCP](https://github.com/isuzu-shiranui/UnityMCP).

Source reviewed:
- [McpMainThreadDispatcher.cs](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Core/McpMainThreadDispatcher.cs), blob `f9d346eb174f3a1455a9678ca03e4833d2beb752`.
- [McpJobRegistry.cs](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Core/McpJobRegistry.cs), blob `94797eb441fd277a6a6a87435310f2d785ce5e39`.
- [JobRegistryTests.cs](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Tests/JobRegistryTests.cs), blob `a07ca36aa27cc4eb215ed0381abdf11f7ec6718c8`.
- [DeferredToolResult.cs](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Core/DeferredToolResult.cs), blob `9104f18734e7ffb40901237df1ed9dac45354df8`.
- [ResultSizeCapTests.cs](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Tests/ResultSizeCapTests.cs), blob `76e786f5ffd800eb198bd73b2902026e53a350a5`.
- [DefinedToolsTests.cs](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Tests/DefinedToolsTests.cs), blob `03c8b2f95c9bce1282f0632186b03922e0ec9934`.
- [Security model](https://github.com/isuzu-shiranui/UnityMCP/blob/main/docs/en/security.md), blob `3a7636d1960c9ef077c876cd416df198b614c767`.
- [MCP Streamable HTTP endpoint](https://github.com/isuzu-shiranui/UnityMCP/blob/main/jp.shiranui-isuzu.unity-mcp/Editor/Core/McpStreamableHttpEndpoint.cs), blob `0a781747da248a6e4700567f2c8717850922dc14`.

### Patterns to adapt

**A. Abandon pending work atomically before it starts.** The dispatcher uses a Pending → Running/Abandoned state machine. A caller can abandon only a still-pending work item, guaranteeing it will not start later. Once it has started, the caller must assume its side effects can still happen. This makes a crucial distinction between “cancelled before start” and “caller stopped waiting while a mutation continues.” The target's dispatcher and draft modal queue should preserve that distinction.

**B. Return a job receipt rather than a plain timeout.** The registry creates a job id when synchronous waiting expires, so the caller polls for the operation's existing result instead of reissuing a mutation. IDs include a readable tool label, registry-generation component and counter. Completed entries retain results for ten minutes; the registry caps at 256 and evicts completed items first while retaining running jobs to avoid losing their handles. A generation component prevents an old ID from naming new work after a reload.

**C. Put a serialized result cap at the actual response boundary.** The HTTP endpoint measures the actual serialized JSON response and returns an MCP error if the tool's declared max size is exceeded. The cap is applied after the job result is unwrapped and uses the *original tool's* descriptor when polling `job_status`; otherwise a large result that fails inline could bypass the limit after becoming a job. Tests cover over-limit, within-limit and tools without a declared limit.

**D. Explicitly represent deferred results.** `DeferredToolResult` carries the work item through the ordinary object return channel, but the runner must unwrap it on every path. An escaped marker would otherwise become a meaningless empty JSON object. This highlights that deferred work needs one fully specified result path, not an alternate hidden channel.

**E. Main-thread work must yield between requests.** Its dispatcher executes dequeued actions outside the queue lock, limits each pump to 50 ms, abandons queued work atomically, and drains/fails pending requests when the server stops. This limits how much backlog of short items can execute in one editor frame. A single long item cannot be interrupted by the pump budget, and the visible logging drain happens before the timed loop, so those boundaries still require tests.

### Caveats

- Its HTTP endpoint declares only protocol versions through `2025-11-25`, with older versions in its explicit list. Its implementation is a reference for result/job semantics, **not** the current source of truth for the 2026-07-28 protocol migration.
- A per-tool declared output size only works if every relevant tool declares a conservative bound and the endpoint checks serialized bytes/chars consistently. Its tests also show that a tool without a declared limit is not held to the size cap; a robust target-wide budget should not permit accidental unlimited tools.
- Its job registry never evicts running work. That's safer than losing a handle, but a pathological queue of permanently running jobs still needs admission/backpressure and a recovery policy.
- Read the security note before adopting custom-defined tools. Its shared and per-project directories load JSON definitions that can point to arbitrary absolute C# scripts; write access to the directory is itself code-execution authority. File watching/reload is convenient but makes directory ACLs and trust scope security-critical.
- The documented server binds to loopback with bearer token; loopback alone is not authentication. The repo explicitly treats `execute_code`, `menu_execute`, and defined-tool scripts as full Editor-privileged execution.

**Disposition:** ADAPT the job receipt, pre-start cancellation, result-size enforcement and unknown/failed outcome semantics; do not import its HTTP protocol code wholesale.

## 2. Funplay MCP — differentiated filesystem safety policy

Repository: [FunplayAI/funplay-unity-mcp](https://github.com/FunplayAI/funplay-unity-mcp).

Source reviewed:
- [ExecuteCodeSafetyPolicy.cs](https://github.com/FunplayAI/funplay-unity-mcp/blob/main/Editor/Tools/Builtins/ExecuteCodeSafetyPolicy.cs), blob `6f0cf0ea65128bce4e4f023388cc3234f670bd6e`.
- [ExecuteCodeSafetyPolicyTests.cs](https://github.com/FunplayAI/funplay-unity-mcp/blob/main/Tests/Editor/ExecuteCodeSafetyPolicyTests.cs), blob `9a00c88d0bf76042d7ea0e5f923b669008c7d060`.
- [MCPToolExportProfile.cs](https://github.com/FunplayAI/funplay-unity-mcp/blob/main/Editor/MCP/Server/MCPToolExportProfile.cs), blob `7e313d3a70452e3fe3c72dbfa51b8e0d0cc24420`.
- [MCPToolExporter.cs](https://github.com/FunplayAI/funplay-unity-mcp/blob/main/Editor/MCP/Server/MCPToolExporter.cs), blob `fa61d85c5aa59fef51a104833cb994abfb69bb91`.

It keeps familiar “base” blocklist rules for known dangerous operations and infinite-loop patterns, then adds an optional strict filesystem policy that blocks a broader list of file writes/moves, streams, absolute paths, user configuration paths and traversal strings. Tests explicitly distinguish the strict profile from the non-strict profile.

**Useful pattern:** make the policy's strength explicit instead of implying one vague safety mode fits every local workflow. Differentiate a restrictive profile, ordinary trusted-local development, and server-enforced remote/unattended policy.

**Important limitation:** regex checking on source text can be bypassed by aliases, reflection, dynamically constructed strings, alternate APIs, already-loaded helpers or code paths not covered by the rules. Even the strict profile is **not a C# sandbox**. It should be described as a conservative misuse guard, with authorization and trusted-client policy enforced outside the code text. A core tool profile that still exposes execute_code is also not safe merely because it contains fewer tools; tool visibility does not replace permissions.

**Disposition:** ADAPT the configuration/UX distinctions and regression-test structure; keep arbitrary C# disabled for untrusted remote/unattended access regardless of regex profile.

## 3. PerfAgent-Unity — profiler ownership and measurement integrity

Repository: [MothCyan/PerfAgent-Unity](https://github.com/MothCyan/PerfAgent-Unity).

Source reviewed:
- [ProfilerApi.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Editor/Core/ProfilerApi.cs), blob `ee425aff8856234b9843ff9807eb2d20788e5f9c`.
- [ProfilerOwnership.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Editor/Core/ProfilerOwnership.cs), blob `89f3906d33a6749cd94d092dcbf8cb41ad0c638e`.
- [PerfRuleEngine.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Editor/Analysis/PerfRuleEngine.cs), blob `6bcc309be7b59434000a340182c603e8927ebd13`.
- [PerfDataFidelityTests.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Tests~/Editor/PerfDataFidelityTests.cs), blob `18e95b4f7f9686f11d9515e3ad659f3932c252c1`.
- [PerfRuleEngineGoldStandardTests.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/Assets/PerfAgent/Tests~/Editor/PerfRuleEngineGoldStandardTests.cs), blob `b99ceff80a24241cd6486f93e770919c32553665`.
- [PerfAgentMcpTools.cs](https://github.com/MothCyan/PerfAgent-Unity/blob/main/PerfAgent.McpForUnity/Editor/PerfAgentMcpTools.cs), blob `dde05d3dea077757f4efd9c3a36064e2f2f49135`.

### Useful ideas

1. **Acquire and release Profiler settings as owned state.** It captures prior enabled/profileEditor/maxHistoryLength in SessionState, makes its desired history bound explicit (2,000 frames), restores prior values on release and uses InitializeOnLoadMethod to restore after a domain reload interrupted normal cleanup. It clears frame data only if it was the code that enabled capture, so it does not destroy a capture already owned by the user.
2. **Don't fabricate measurement precision.** Use the ProfilerRecorder counter as the primary managed-allocation metric where available; keep GC-memory-difference estimates separate as a cross-check. If the primary metric is unavailable, return NaN/unavailable with a note instead of silently substituting a smaller-looking value.
3. **Report insufficient or missing samples loudly.** The rule engine declines statistical conclusions for very short capture windows, and emits an explicit “zero frames captured; dynamic metrics unavailable” diagnostic instead of silently generating an empty report from resource-only checks.
4. **Golden-standard rule tests:** inject one known issue at a time, assert stable finding ID/category/severity, and ensure a clean snapshot does not trigger the known signatures.
5. **Read-only MCP boundary for diagnostics.** Its optional MCP integration exposes starting and polling a profiler capture and analyzing snapshots; it explicitly has no tool that applies one-click fixes. Fix plans require human confirmation in the Editor UI.

**Disposition:** ADAPT profiler resource-ownership, min-sample gates, source/metric fidelity metadata and read-only-by-default analysis. This is not necessarily a new MCP tool category; it is a correctness standard for the target's existing profiling and diagnostics surface.

## 4. Hatayama — direct-bridge CLI and diagnostic snapshots

Repository: [hatayama/unity-ai-cli-bridge](https://github.com/hatayama/unity-ai-cli-bridge).

Source reviewed:
- [README](https://github.com/hatayama/unity-ai-cli-bridge/blob/main/README.md), blob `966bba319b51e3fd8ebef1f13a004b9e9f7b09a5`.
- [Bridge v1 spec](https://github.com/hatayama/unity-ai-cli-bridge/blob/main/spec/unity-ai-cli-bridge-v1.md), blob `2464aa009ffc0fd71b10c49cdd2b88b411ea36bb`.
- [Go bridge client](https://github.com/hatayama/unity-ai-cli-bridge/blob/main/internal/unitybridge/client.go), blob `aabd1cdbe33f39d5f1795e42f51fcce666a3692d`.
- The repository tree has tests for catalog, diagnostics, discovery, end-to-end CLI, MCP server and direct bridge client.

Its CLI connects directly to Unity's built-in `UnityMCPBridge` using named pipes (Windows) or Unix sockets (macOS/Linux) and treats `com.unity.ai.assistant` as an immutable dependency. It provides `status`, `doctor`, `help`, `recipes`, `tools`, `describe`, `call`, `wait` and `mcp serve`; companion package snapshots write diagnostics and the tool catalog under the project's `Library/UnityAiCliBridge`.

**Useful pattern:** a diagnostic-first client can report discovery/heartbeat/approval issues without forcing the user to call the live tool list first. Recipe and tool help can be a developer-facing interface layered over the official bridge. The companion state is project-local/temporary under Library, not a modified file in Unity's PackageCache.

**Caveat:** this is specific to the Unity AI Assistant direct bridge and its IPC contract. It is not compatible with the CoplayDev HTTP plugin by merely copying the CLI. Adapt the status/doctor/catalog UX only if the target wants an independent CLI or more robust diagnostics; map to target's existing `unity-mcp` CLI before creating another CLI.

## 5. Yecats Unity MCP Toolkit — optional custom tooling for the official integration

Repository: [Yecats/unity-mcp-toolkit](https://github.com/Yecats/unity-mcp-toolkit).

Source reviewed:
- [README](https://github.com/Yecats/unity-mcp-toolkit/blob/main/README.md), blob `bf43240c4fd64d4166533ff8c79bf01fe505495c`.
- [Input Action editing tool](https://github.com/Yecats/unity-mcp-toolkit/blob/main/Editor/Tools/InputSystem/SetInputActionsTool.cs), blob `b884399034f7656a6cb06fef3ae6670dfa0154d1`.
- [Game View capture](https://github.com/Yecats/unity-mcp-toolkit/blob/main/Editor/Tools/General/GameViewCaptureTool.cs), blob `a6d1940488b8be8e890b3f21a906a13a98657caf`.

This is a custom-tool package for the official Unity AI Assistant MCP integration, with optional Input System and Recorder dependencies. Its Input Action modifier explicitly rejects changes while playing because edits would be lost on Play Mode exit; its capture tool downsamples images by default to keep payloads manageable and documents the required open Game View.

**Useful pattern:** dependency-gated tool groups, disabled-by-default mutation tools, output downscaling and clear “writes to disk / unavailable during Play Mode” constraints. This ecosystem is a different plugin/server stack from CoplayDev, and the README does not establish the breadth of automated tests in the 102-entry repo.

**Disposition:** source pattern only; no wholesale port.

## 6. nowsprinting Rider extension — compile/domain-reload race testing

Repository: [nowsprinting/mcp-extension-unity](https://github.com/nowsprinting/mcp-extension-unity).

Source reviewed:
- [RunUnityTestsTool.kt](https://github.com/nowsprinting/mcp-extension-unity/blob/master/src/main/kotlin/com/nowsprinting/mcp_extension_unity/RunUnityTestsTool.kt), blob `227a0e23619839fdec55c13a9ba3ad6c65ce5eef`.
- [Compilation-result race investigation](https://github.com/nowsprinting/mcp-extension-unity/blob/master/docs/plans/2026-05-16-fix-compilation-result-race-condition.md), blob `1228d008178535b24ed713773cd47c3914c98618`.

The issue report details a race where `AssetDatabase.Refresh()` triggers domain reload and cancels a Rider protocol call; the wait helper then immediately receives an old non-null Unity model because it was already notified with the stale value, and the next call is cancelled before Rider observes disconnect/reconnect. The plan proposes waiting for a **different model instance** after an operation that is known to trigger reconnect, and changing E2E tests so they call while compilation is actually in progress rather than waiting for compilation to finish first.

**Useful principle:** a non-null connection or fresh HTTP heartbeat is not enough to prove the bridge is at a new/current generation after reload. When refresh/compile causes reconnection, compare identity/generation and wait for a positive ready state. Realistic E2E race tests should exercise the operation during compilation/reload, not only after it has settled.

**Caveat:** this code uses JetBrains Rider's proprietary RD protocol and cannot be transplanted into the target. The test design and state-generation contract are reusable.

## Cross-repository acceptance themes

Across these independent codebases, the strongest recurring correctness patterns are:
- explicit job/result IDs and no blind re-execution after timeout;
- cancellation is effective only before a mutation begins;
- actual serialized result size is enforced instead of relying on a comment/schema declaration;
- unknown/unreadable metrics are not represented as zero/success;
- resources such as Profiler state are restored after normal stop, domain reload and failure;
- test fixtures include active compilation, reconnect, modal and timeout races rather than only the steady state;
- safety profiles are clearly differentiated but no regex check is called a general C# sandbox;
- optional integration modules are isolated behind explicit package/version dependencies.


## 7. Map these patterns against the actual target beta

The target already has useful job-specific machinery; avoid treating “async jobs” as wholly missing:

- `MCPForUnity/Editor/Services/TestJobManager.cs` (blob `b20b2f7cf05f8dd254200a94d49521a6630f85a6`) persists Test Runner job state across domain reload, throttles persistence, caps retained jobs at 10 and caps persisted failure details. `GetTestJob.cs` exposes polling. AssetGen, package and build workflows also have specialized job managers or handles.
- `MCPForUnity/Editor/Helpers/Response.cs` defines a generic `PendingResponse` contract, but that is not proof all long-running tools share one standardized pollable job ID/result lifecycle. Map each async operation before proposing a generic job registry.
- `TransportCommandDispatcher.cs` (blob `5b013154299f75052ea73449f85b36b2cbde1353`) already has cancellation-token registration, per-request cleanup and a check that a request cancelled before `ProcessCommand` begins will not execute. Once a handler is already running, cancellation does not forcibly undo/interupt its side effects. Preserve this distinction.
- In the inspected `ProcessQueue`, every not-yet-executing item in the global `Pending` dictionary is copied into a `ready` list and processed in one `EditorApplication.update` pass. The dictionary is keyed by request GUID and there is no explicit pending-count/serialized-byte admission cap in the inspected dispatcher. There is also no apparent per-update item/time budget across multiple short synchronous handlers; one individual handler can of course exceed any budget. That is a concrete queue-backpressure/fairness gap worth testing.
- The target tool registry and helpers already offer per-tool pagination/truncation in selected operations, and TestJobManager limits its own detailed history. However, source searches plus inspection of `Response.cs`, `WebSocketTransportClient.cs`, and `unity_response.py` did not reveal a single universal serialized-result limit enforced across the final transport boundary. Treat that as an audit finding to verify via a comprehensive response-path search before implementing a central layer—not an assertion that no individual output cap exists anywhere.

### Test implications

1. **Already started vs not started:** cancel before dispatch, cancel after the request was moved to the local ready list but before handler invocation, and cancel after handler starts; assert which side effects may occur.
2. **Queue pressure:** send many commands while a native modal blocks Editor updates; assert bounded queued command count and retained bytes, clear overload responses, queue-age diagnostics, and cleanup on disconnect/domain reload.
3. **Editor frame budget:** enqueue many quick commands and observe whether the dispatcher monopolizes one update. If adding time/item slicing, preserve ordering and ensure expensive tool work is not run concurrently.
4. **Job contract audit:** enumerate every operation returning a pending status, async handle, job ID or delayed completion. Verify ID lifetime/generation, terminal result retention, error status, cancellation, cross-project binding and no automatic mutation re-execution.
5. **Result cap audit:** find every serialization boundary for WebSocket, HTTP, stdio/CLI and job polling; test plain text, nested JSON, stack traces, images/base64 and tool-specific result types.

No target test or runtime benchmark was executed for this mapping.
