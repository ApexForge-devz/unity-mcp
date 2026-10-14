# Shared Local HTTP Server Lifecycle — Cross-Editor Ownership Review

**Review date:** 2026-10-14  
**Target baseline:** ApexForge-devz/unity-mcp, beta, commit 142ea0af4890337f0ff6658df61db23982313717  
**Upstream proposal:** [CoplayDev/unity-mcp PR #1380](https://github.com/CoplayDev/unity-mcp/pull/1380)  
**Decision:** Fix the target ownership boundary; do **not** port PR #1380 unchanged until the handoff/orphan case is resolved.  
**Validation:** source and patch review only; no live two-Editor reproduction or process-lifecycle integration test ran.

## 1. Executive conclusion

There are two related problems, and the proposed fix addresses only part of the lifecycle.

1. **Verified source-level risk in target beta:** the PID file itself is project-local, but the handshake and last-server tracking are stored under unscoped, machine-global EditorPrefs keys. A different Unity Editor can read and act on the most recently stored handshake. The PID and instance-token checks protect against unrelated processes, but they do not establish that the Editor requesting termination was the Editor that launched the valid matching process. The stop path can therefore operate on another project's correctly identified server. Starting the server also attempts a stop before checking whether the configured port is occupied, using the same shared handshake state.
2. **Important handoff gap in upstream PR #1380:** it improves scoping and checks whether other Unity projects are connected before stopping a process, but the launch marker is held in SessionState. Unity documents that SessionState is cleared when Unity exits. If the launching Editor exits while a second Editor is connected, it correctly leaves the server running, then loses its launch marker as its process exits. The remaining Editor did not launch the server and therefore will not stop it when it later becomes the last connected instance. The managed headless process can be orphaned.
3. **Race and identity caveats:** the instance-count probe and subsequent process termination are separate operations, so a new Editor may attach between them. The proposed count filters by project hash, which can also conflate two sessions if multiple live sessions share the same project hash unless the product explicitly prevents that configuration.

The right objective is not simply “only let the original launcher stop the server.” It is: **a managed server must never be stopped while another live Unity client depends on it, must never be confused with an unrelated/external process, and must eventually be reclaimed after the final client exits—even if the original launcher exited first or crashed.**

## 2. Target implementation verified

### 2.1 PID file is local to the project; tracking keys are not

Target [PidFileManager.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/Server/PidFileManager.cs), blob **eca60ee2745a465c026999c92f273550601e5037**, stores its PID file below the project root:

- Directory: project root / Library / MCPForUnity / RunState
- File name: mcp_http_{port}.pid

But the current implementation reads and writes global keys in EditorPrefs such as LastLocalHttpServerPidFilePath, LastLocalHttpServerInstanceToken, LastLocalHttpServerPid, LastLocalHttpServerPort, LastLocalHttpServerStartedUtc and LastLocalHttpServerPidArgsHash. StoreHandshake writes the last PID-file path and instance token without a project/port namespace; TryGetHandshake reads those same global values; ClearTracking deletes the shared keys.

### 2.2 Shutdown trusts global ownership state

Target [ServerManagementService.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/ServerManagementService.cs), blob **7a2c546cc33b5138a76474de16e09470f1658490**:

- StartLocalHttpServer calls StopLocalHttpServerInternal(quiet: true) before checking whether the configured port remains occupied.
- StopManagedLocalHttpServer reads the global handshake, derives the target port from its PID-file path (falling back to the current URL only if path parsing fails), then calls StopLocalHttpServerInternal with that port override.
- StopLocalHttpServerInternal checks the PID file, whether that PID is listening on the port, whether its command line contains the launch token, and fallback process signatures when token inspection is unavailable.
- On successful stop, it deletes the PID file and clears the shared EditorPrefs tracking values.

Those process/token checks are useful safeguards against PID reuse or killing an unrelated listener. They do **not** solve ownership isolation: a second Editor that reads the first Editor's valid shared handshake can still pass the same identity checks for the first Editor's managed server. This is a source-level defect candidate; it has not been reproduced with two live Editor processes in this session.

### 2.3 Existing tests do not cover multiple-Editor ownership

The current [McpEditorShutdownCleanupTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Services/McpEditorShutdownCleanupTests.cs), blob **74bf851bd8adf94f494a64b19d91cee9a914375b**, primarily tests the batch-mode cleanup guard. [PidFileManagerTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Services/Server/PidFileManagerTests.cs), blob **8b0d5350884fab1acc21bead9466a9240db3f6c4**, tests PID-file paths/reads and EditorPrefs state but does not establish isolation across two project roots and editor processes.

The tests therefore do not currently prove that project A cannot stop project B's server, that a borrowed server survives a non-owner quit, or that the managed server is eventually cleaned up after the original owner is gone.

## 3. What PR #1380 improves

Upstream [PR #1380](https://github.com/CoplayDev/unity-mcp/pull/1380), open in the 2026-10-14 snapshot (head was source-reviewed), proposes these changes:

- Key PID/handshake/argument-hash values by project hash and port, avoiding the current globally shared tracking slot.
- Put a “this Editor process launched this server on port X” marker in SessionState, which survives domain reload but is cleared on Unity exit.
- Add TryGetLaunchedLocalHttpServerPort and make StopManagedLocalHttpServer return without resolving a server unless this Editor has a launch marker.
- Before stopping that server on quit, GET /api/instances with a 500 ms budget and count other connected Unity instances. Stop only when the count is exactly zero; if the response is malformed or unavailable, leave the process running.
- Add unit coverage for a zero/positive/unknown instance count and parser/port-scoped PID-file operations.

The source endpoint already exists in target [Server/src/main.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/main.py#L534-L547), blob **b057f3d96c0632692fae01dd21b0be71534c087e**, and returns a list of connected Unity instances with project, hash, Unity version and connected-at metadata.

Per-project/per-port keys and fail-safe “unknown means leave it running” are valuable. The following gaps prevent wholesale adoption.

## 4. Critical handoff gap in the proposed lifecycle

Scenario:

1. Editor A launches a managed HTTP server. A's launch port is kept in SessionState.
2. Editor B attaches to A's server but did not launch it.
3. A quits. Its shutdown code sees B as another connected instance and correctly leaves the server running.
4. A exits. Unity clears A's SessionState, including the only launch marker.
5. B quits later. Its own SessionState has no launch marker, so the proposed TryGetLaunchedLocalHttpServerPort returns false and B does not attempt the last-instance probe or stop the server.
6. No Editor is left to clean up the managed headless process; it remains until manually stopped or otherwise terminated.

Unity's [SessionState documentation](https://docs.unity3d.com/ScriptReference/SessionState.html) explicitly says this store survives assembly reloads but is cleared when Unity exits. This makes the handoff issue a lifecycle consequence of the proposed design, not merely a speculative race.

**Required correction:** use a lifecycle/lease model that can transfer cleanup responsibility or lets a managed server reclaim itself when its connected-instance count remains zero for a bounded grace period. Any adopter must test “owner exits first, borrower exits last” and prove eventual process cleanup. A per-process launch marker is useful to prevent an Editor from accidentally resolving an unrelated server; by itself, it is not a complete shared-server ownership model.

## 5. Additional edge cases to resolve

### 5.1 Non-atomic last-instance check

The probe returns a snapshot, then the Editor terminates the PID. A second Editor could attach after the zero count response but before process termination. The 500 ms timeout limits how long quit blocks; it does not make the check/stop sequence atomic.

Prefer server-side drain/shutdown coordination or a managed-server lease with a bounded transition to “closing,” rejecting new connections while stopping only after existing clients have detached. If that is too invasive, at least define the race window and stress-test attach-during-shutdown. A count-only unit test does not cover it.

### 5.2 Project hash is not necessarily a process/session identity

The proposed helper counts instances whose project hash differs from the quitting project's hash. That is adequate only if the runtime guarantees at most one live Unity bridge session per project hash. If a user can run two connected Editor processes for one project identity, the same hash can cause one to be treated as the quitting Editor even when it is another live session. Either enforce and test the one-editor-per-project invariant or count distinct live sessions using an identity that can exclude exactly the quitting session.

### 5.3 Startup has the same ownership problem

Because StartLocalHttpServer stops the previously tracked process before it checks the current port, project-scoping must be applied to **start, stop, cleanup and stale-state clearing**, not only to the quit hook. A project/port namespace prevents one project's cleanup from clearing another project's metadata, but the start path still needs explicit behavior when its port is occupied: refuse to take over, deliberately reuse a shared server, or request controlled shutdown after proving it has no other users. It should never silently terminate a valid server just because another Editor asks to start on the same port.

### 5.4 External servers and probe errors

An externally launched MCP server or a server not marked as Unity-managed must never be terminated through this cleanup path. Invalid JSON, unsupported endpoint, timeout, malformed instance records, or unavailable identity should be treated as **unknown**, not zero. Preserve the good fail-safe policy from PR #1380: uncertain count means leave the process running and emit a useful diagnostic without logging credentials/tokens.

## 6. Recommended lifecycle design

Prioritize in the following order:

1. **Isolate bookkeeping:** namespace persistent server metadata by project identity and port; use unambiguous project-root identity, not only project display name. Clear only the slot that belongs to the stopped server.
2. **Preserve process ownership evidence:** retain a PID + launch-token/argument fingerprint for a server verified as Unity-managed; never kill based only on “something is listening.”
3. **Make shared-user state explicit:** query connected Unity session identity and distinguish the current session from other sessions. Do not treat project hash as a unique process ID unless the system proves that invariant.
4. **Transfer cleanup responsibility:** if the original launcher leaves while another instance is connected, the managed server or a new elected owner must retain an eventual cleanup path. A server-side idle shutdown for Unity-managed launches is one option; a persisted adoptable lease with validated handoff is another.
5. **Guard the transition:** make shutdown/draining resistant to new attaches, or explicitly define and test the acceptable race behavior.
6. **Keep shutdown bounded:** preserve short timeouts; probing or lifecycle failure must not hang Unity quit.

Do not duplicate existing server/pidfile services if a small contract change and ownership-state correction suffice.

## 7. Required regression matrix

| Scenario | Expected result |
|---|---|
| A launches server; A is the only client and quits | Managed server stops; PID file and only A's state are cleared. |
| A launches; B attaches; B quits first | Server remains running; A can still stop it later. |
| A launches; B attaches; A quits first; then B quits | Server remains while B is connected and is eventually stopped after B is gone. This is the critical handoff test. |
| A and B use different ports/projects | Starting or quitting B cannot alter A's PID, token, PID file or process. |
| A and B share same port/server | Neither stops it while the other is actively connected. |
| An editor connects during the last-instance transition | New connection is either admitted with the server left running or rejected by an explicit drain state; no connected client is torn down unexpectedly. |
| Probe times out or returns malformed JSON | Leave the process running; emit a bounded diagnostic. |
| PID is reused or token does not match | Never terminate based solely on PID/port. |
| External/non-managed server occupies port | Do not kill it; report port conflict and preserve existing service. |
| Project path moves or same display name exists in another worktree | Ownership remains project-identity-safe; no cross-project state reuse. |
| Editor domain reload while server is alive | Launch marker survives reload and the server is not terminated. |
| Original launcher crashes | A managed cleanup path still exists; test the documented recovery behavior. |

Validation should include isolated pure/unit tests, local HTTP integration against the actual /api/instances route, and a two-Editor Unity test. Tests that only exercise ShouldStopManagedServer(0/1/null) validate a decision helper, not the ownership lifecycle.

## 8. Decision

**PR #1380: ADAPT, not cherry-pick as-is.** Its project/port-scoped keys and fail-safe probe are improvements, but the last-one-out ownership gap means its advertised lifecycle is incomplete. Update backlog P1.6 to include owner handoff/eventual cleanup, and require the regression matrix above before merging a fix.

## Sources

- Target [McpEditorShutdownCleanup.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/McpEditorShutdownCleanup.cs), blob **e757b86ce4045790a3f7d6412c73e9c899639393**.
- Target [ServerManagementService.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/ServerManagementService.cs), blob **7a2c546cc33b5138a76474de16e09470f1658490**.
- Target [PidFileManager.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/Server/PidFileManager.cs), blob **eca60ee2745a465c026999c92f273550601e5037**.
- Target [McpEditorShutdownCleanupTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Services/McpEditorShutdownCleanupTests.cs), blob **74bf851bd8adf94f494a64b19d91cee9a914375b**.
- Target [PidFileManagerTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Services/Server/PidFileManagerTests.cs), blob **8b0d5350884fab1acc21bead9466a9240db3f6c4**.
- Target [instances endpoint](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/main.py#L534-L547), blob **b057f3d96c0632692fae01dd21b0be71534c087e**.
- Upstream [PR #1380 patch and tests](https://github.com/CoplayDev/unity-mcp/pull/1380).
- Unity [SessionState documentation](https://docs.unity3d.com/ScriptReference/SessionState.html).
