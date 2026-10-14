# Upstream PR #1463 — Play Mode Startup Readiness and One-Shot Post-Start Action

**Reviewed:** 2026-10-14  
**PR:** [CoplayDev/unity-mcp #1463](https://github.com/CoplayDev/unity-mcp/pull/1463)  
**Target baseline:** beta at 142ea0af4890337f0ff6658df61db23982313717  
**PR head inspected:** 3eaa4b23fed490a159d67e5bc564b59636cc344f  
**Status at review time:** Open, not merged  
**Decision:** **ADAPT the readiness milestone after focused validation; keep one-shot code strictly secondary to persistent generator scripts.**

## Executive assessment

The current target's ordinary manage_editor(play) action requests Play Mode and returns an acknowledgement; that does not guarantee the scene has loaded, Start methods have run, or a first frame has completed. PR #1463 adds an opt-in wait milestone and a project-owned job record so follow-up validation does not race startup or blindly re-execute a request after a lost reply.

This is relevant to the script-first scene workflow in one narrow way: after the authoritative scene-generation script has produced and saved the intended scene, the agent can enter Play Mode, wait for first-frame startup, and only then perform targeted verification or capture the actual Game View. That avoids treating “Play Mode entered” as proof of runtime readiness.

The optional on_ready_code hook is **not** a replacement for the persistent scene-generation script. It runs a transient C# body through the existing Execute Code path. For this agent workflow, use the wait-only mode by default; if on-ready code is used at all, restrict it to bounded, read-only assertions/diagnostics. Do not move scene authoring or fixes into an ephemeral hook.

## 1. Target gap and proposal

The current beta ManageEditor handler sets EditorApplication.isPlaying = true and returns immediately. With the default domain-reload path, static state set before entering Play Mode can be reset; Start and first-frame initialization may also still be pending when the next MCP command arrives.

PR #1463 extends play with:
- wait_until = scene_loaded or first_frame;
- optional on_ready_code;
- wait_timeout (default 30 seconds, range 1–300);
- get_play_mode_startup(job_id) to inspect a previously created job;
- a Unity-side request/job ID persisted across domain reload;
- no automatic resubmission of the initial side-effecting request after a timeout/lost response;
- same-Unity-project routing for later status polls once the initial response provides the project hash.

The normal play request without startup options retains its existing fast acknowledgement behavior.

## 2. Useful behaviors worth retaining

### 2.1 Readiness is an explicit milestone

scene_loaded means the active scene is loaded, not that all Start callbacks or a complete frame have run. first_frame waits for an entered-play frame marker and later frame progress. The new Unity EditMode tests include a Play Mode probe verifying Start was called and LateUpdate has executed before the one-shot code runs.

For scene validation, this gives a practical sequence:
1. Run the persistent generator and validate its own postconditions.
2. Enter Play Mode with first_frame readiness.
3. Capture an inline composited Game View image or perform narrowly scoped read-only checks.
4. If something is wrong, edit the generator source and regenerate.

Do not add an arbitrary delay/sleep in the agent prompt; use the explicit readiness contract.

### 2.2 Lost response is not permission to repeat the action

The Python helper creates a request UUID before initial dispatch and disables transport retry for that startup call. It later polls get_play_mode_startup using the same job ID instead of submitting play/on_ready_code again. This is correct for a one-shot side effect.

On the Unity side, the service persists the job state before executing on_ready_code. If a domain reload interrupts code already marked executing, it marks the request failed/interrupted and does not execute it again. This is an **at-most-once attempt**, not a guarantee that the side effect and result are atomic: an effect may have occurred before the reload while the final result was not persisted. Callers must treat that failure state as potentially indeterminate, not automatically retry the body.

### 2.3 Instance routing is mostly well considered

After a successful initial reply, the Python helper pins subsequent polls to the Unity project hash returned by the Unity-side job. The local REST tests cover a websocket-session change while the helper continues to use the same project hash. The helper also has tests for a lost initial response that returns the pre-generated job ID without resubmitting the action.

There is a recovery-edge weakness: if the initial reply is lost, the fallback result contains the guessed job ID and status=unknown but does not include the selected Unity instance/project identity. The response tells the client to poll the same instance, but with multiple Editors a later status call can be misrouted if the original selection is not retained. The existing unit test checks no resubmission and a returned ID; it does not verify status polling after a lost first response while multiple Editors are available.

**Recommendation:** include the originally selected instance identifier in the unknown-start response (while keeping server-side project-hash pinning whenever it becomes known), and test lost-initial-reply recovery with two open Unity projects. If a project hash is not yet available, the client must still have an explicit stable route target rather than falling back to a possibly different default instance.

## 3. Source-review findings and adoption gates

### 3.1 Unbounded request-ID tombstones in SessionState

PlayModeStartupService trims its job list to MaxJobs = 16, and documentation says completed results are retained for the latest 16 jobs in the Editor session. However, it separately persists a Requests HashSet under SessionState and adds every startup request ID to it. When old Jobs are evicted, their IDs remain in Requests so a repeated stale ID cannot silently run again. That means the request registry grows for the entire Editor session even though job results are capped at 16. Persist() serializes the full registry on state transitions, so the stored value and serialization work both grow without a bound.

This is a real state-growth defect, even though retaining tombstones has an idempotency purpose.

**Required correction:** define a bounded replay-protection policy. For example, use a capped, age-bounded tombstone history and explicitly document its retention horizon; or stop accepting stale IDs after a documented expiry and rely on the server-generated request ID plus no-retry transport. Do not claim indefinite no-replay semantics while storing an unbounded ledger. Add tests proving the registry is bounded after many more than 16 jobs and that behavior after expiry is explicit.

### 3.2 Unknown initial response lacks a pinned route handle

The Python wrapper generates the request ID, but does not attach the selected instance identifier to the fallback last_data. A lost response can therefore return a useful ID without enough routing metadata to guarantee a follow-up polls the same project when multiple Unity instances are connected.

**Required correction:** preserve route identity from the first dispatch and return it in the unknown-start state; test both Python direct transport and local REST behavior when the first response is lost. Status reads must never start a new job.

### 3.3 on_ready_code outcome is not atomic with domain reload

The Unity service changes status to executing and persists before calling ExecuteCode. If a reload takes place during that invocation, the next static initialization changes executing jobs to failed with the message that the code was interrupted and will not be repeated. This prevents duplicate execution, but cannot prove whether partial side effects occurred.

**Required gate:** expose the phase and interruption semantics clearly. For mutation-prone bodies, require idempotent operations and explicit postcondition checks; for this user's agent, avoid mutating scene state in this hook entirely. Keep authoritative scene mutations inside the persistent generator script.

### 3.4 Timeout is an observation timeout, not a Play Mode stop

The service's wait timeout fails the pending startup job but deliberately does not stop Play Mode. Pending code is cleared so it won't fire later if the Editor resumes, and the tests cover a paused wait timing out without executing the hook after resume. A code body already executing can still finish.

That is a reasonable boundary, but clients must not infer that Play Mode stopped or that an already-running call was cancelled. After timeout, poll the existing job and inspect current Editor state before issuing any follow-up action.

### 3.5 Bound and validate job result size

The job count is capped to 16, but each job keeps the on_ready_code result as a JSON object from ExecuteCode. The audit did not independently establish that every possible ExecuteCode return object is size-bounded tightly enough for retention inside the serialized job list. Validate or cap the stored result summary before persisting it; preserve essential diagnostics but do not persist arbitrary large result payloads.

## 4. Test evidence and caveats

The PR was open/unmerged on 2026-10-14 and uses the same beta base SHA recorded above. The PR author reports:
- 1,613 Python tests passed, 18 skipped;
- 13 new Unity EditMode tests passed;
- runtime validation on Unity 6000.3.20f1 with Test Framework 1.6.0;
- actual Play Mode checks for Start/LateUpdate completion, request-ID resubmission not repeating a call, stop/timeout cancellation, reload-interrupted code, and preserving Enter Play Mode settings;
- C# compilation on Unity 6000.0.75f1 across Windows/macOS/Linux compile defines;
- four other Unity-version checks skipped because those Editors were unavailable, and licence-dependent Unity/E2E CI jobs skipped.

These are author-reported results, not independently rerun by this review. The added test files are useful, but the current submitted tests do not cover unbounded Requests growth or initial lost-response routing with multiple Editors.

## 5. Recommended integration

1. Add the opt-in scene_loaded/first_frame readiness gate to the existing manage_editor action; no new top-level MCP tool is needed.
2. Keep the no-options play action unchanged.
3. Persist a bounded job record through domain reload and poll by its original job ID; never blindly re-send the initial request.
4. Bound the request tombstone registry and stored result payloads.
5. Include stable route identity in unknown-start responses; keep later polls pinned to the project hash once observed.
6. Treat interrupted on-ready execution as indeterminate/at-most-once and require postcondition reads rather than automatic rerun.
7. For this agent's workflow, prefer wait_until=first_frame without on_ready_code. Use the generator script for any scene-generation change and use screenshots/targeted read-only assertions for validation.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Start Play Mode with first_frame | Completion is returned only after the tested startup/frame milestone. |
| scene_loaded with Start not yet complete | Status accurately reports scene_loaded while first_frame_complete remains false. |
| Wait times out while Play Mode is paused | Job fails, Play Mode remains active, and pending code does not execute after resume. |
| Same startup request ID is replayed | Existing/range-retained outcome is returned or explicit expired-ID behavior is reported; code is not silently duplicated within the documented retention window. |
| More than 16 jobs created in one Editor session | Job results and ID tombstones stay within their documented bounds. |
| Lost initial response with two Unity Editors | Returned recovery handle allows polling the original project, not a different default instance; start request is not resent. |
| Status polling during reconnect/domain reload | Poll uses the same project identity and does not create a new startup job. |
| Domain reload while code is executing | No replay; status conveys interrupted/indeterminate outcome and postconditions determine what actually happened. |
| on_ready_code returns a large value | Persisted response is capped with explicit truncation while critical diagnostics remain available. |
| on_ready_code attempts scene mutation | The script-first agent workflow rejects/deprioritizes it in favor of editing/regenerating its persistent source of truth. |
| Older Unity package lacks startup support | Client receives an explicit unsupported response and does not claim readiness. |

## Source references

- [PR #1463](https://github.com/CoplayDev/unity-mcp/pull/1463) (open at review time)
- [PR issue #1437](https://github.com/CoplayDev/unity-mcp/issues/1437)
- Target [ManageEditor.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageEditor.cs)
- PR-head [PlayModeStartupService.cs](https://github.com/CoplayDev/unity-mcp/blob/3eaa4b23fed490a159d67e5bc564b59636cc344f/MCPForUnity/Editor/Services/PlayModeStartupService.cs)
- PR-head [Python play-mode startup coordinator](https://github.com/CoplayDev/unity-mcp/blob/3eaa4b23fed490a159d67e5bc564b59636cc344f/Server/src/services/play_mode_startup.py)
- PR-head [startup unit tests](https://github.com/CoplayDev/unity-mcp/blob/3eaa4b23fed490a159d67e5bc564b59636cc344f/Server/tests/test_play_mode_startup.py)
- PR-head [startup REST tests](https://github.com/CoplayDev/unity-mcp/blob/3eaa4b23fed490a159d67e5bc564b59636cc344f/Server/tests/test_play_startup_rest.py)
- PR-head [ManageEditorStartupTests.cs](https://github.com/CoplayDev/unity-mcp/blob/3eaa4b23fed490a159d67e5bc564b59636cc344f/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ManageEditorStartupTests.cs)
