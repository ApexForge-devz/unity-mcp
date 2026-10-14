# Upstream PR #1470 — Actionable Recovery Guidance for an Unavailable Unity Session

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1470](https://github.com/CoplayDev/unity-mcp/pull/1470)  
**Target baseline:** beta 142ea0af4890337f0ff6658df61db23982313717  
**Candidate head:** 9fcf0416c77b854afe6c4ab0c291f5f3a79923f5  
**Status:** Open, not merged  
**Decision:** **ADAPT as a small server-response improvement, with an in-repo regression test.**  
**Review type:** Source and PR evidence inspection; no tests were run by this review.

## Executive assessment

PR #1470 adds a human-readable recovery message for the existing PluginHub failure reason no_unity_session. The current error explains that the Unity session is unavailable and says to retry, but does not give the agent/user a concrete next step. The proposal explains that the MCP server is responding but the requested Unity session is not available, then suggests opening Unity, starting its MCP session, bringing the Editor forward and checking for pending dialogs.

This is a small improvement to error recoverability, not a runtime connection fix. It adds no tool, argument or transport behavior and leaves the machine-readable retry contract unchanged. It fits the MCP↔Unity scope because an agent can react more effectively to a structured failure instead of repeating the same command.

## 1. Existing behavior and what the PR changes

The target's [plugin_hub.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/transport/plugin_hub.py) has a shared _unavailable_retry_response(reason) helper. Before the proposal, it returns:

- success=false and the existing error text “Unity session not available; please retry”;
- hint=retry;
- data.reason and retry_after_ms=250;
- no actionable message field content.

PR #1470 fills the existing message field only when reason == no_unity_session. It preserves error, hint, data.reason and retry_after_ms, and does not change the stale_connection response.

This failure is returned after PluginHub cannot resolve an active session for the requested Unity instance. The target already waits for a bounded reconnection window—default 20 seconds, configurable via UNITY_MCP_SESSION_RESOLVE_MAX_WAIT_S with a 120-second maximum—before failing on the ordinary reconnect path. The guidance should therefore be read as a recovery hint after the existing wait, not as a replacement for or shortcut around the reconnect logic.

## 2. Useful recovery steps

The candidate message suggests opening Unity and starting its MCP session. The accompanying [troubleshooting guide change](https://github.com/CoplayDev/unity-mcp/pull/1470/files) also recommends confirming reload has completed, bringing a background Editor to the foreground, checking dialogs, and re-reading mcpforunity://instances.

These are reasonable actionable checks for common cases such as an Editor that has not registered, a session dropped during domain reload, or a background Editor waiting behind a dialog. The server can be reachable while no matching Unity session is registered, so this distinguishes server health from Unity-session availability.

### Required refinement: identify the requested instance

In a multi-instance workflow, “open Unity and start its MCP session” can be insufficient or misleading if the tool call is pinned to a stale/wrong project hash. Recovery guidance should explicitly tell the agent to inspect mcpforunity://instances, compare the requested Unity instance/project identity with the registered entries, and only then decide whether to start/reconnect that Editor or correct instance routing. This avoids repeatedly retrying a valid request against the wrong project.

The response should remain concise; the linked troubleshooting page can carry the longer explanation.

## 3. What it does not fix

The PR body explicitly notes that loss of a session while Unity is in the background and stalled compilation remain separate issues. Human-readable guidance makes those failures easier to diagnose but does not make PluginHub detect the exact Editor state, wake a stalled Unity Editor, repair incorrect routing or shorten the existing reconnect wait.

Do not treat the message as evidence that Unity is compiling, that a modal dialog is definitely open, or that a retry will succeed. The response should present those as checks, not asserted diagnoses.

## 4. Test gap and adoption conditions

The PR changes two files—PluginHub's response helper and the troubleshooting page—but does not add an automated test for the new message field. The PR author reports 1,575 Python tests passing with 18 skipped and describes manual verification against an isolated local HTTP server with no registered Unity sessions. This is author-reported evidence, not an independent run.

Before merging/adapting, add focused tests that prove:

1. reason=no_unity_session adds actionable message text.
2. reason=stale_connection leaves the message absent/unchanged.
3. success, error, hint, data.reason and retry_after_ms remain unchanged for both reasons.
4. The message does not assert a specific root cause such as “Unity is compiling” or “a dialog is open.”
5. The guidance mentions verifying the intended registered instance when appropriate.
6. The JSON response remains compatible with existing clients expecting the current machine-readable retry shape.

The current branch has existing PluginHub authentication/session tests, but this response shape deserves a direct unit test rather than relying only on a broad suite count or a manual HTTP smoke test.

## Recommended decision

**ADAPT, P2 / low implementation cost.** Add a concise human message only for no_unity_session while preserving all current retry fields and leaving stale_connection semantics unchanged. Extend the message/troubleshooting page with instance verification via mcpforunity://instances. Do not alter retry timing or add automatic retries as part of this small change.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| No Unity session resolves after bounded wait | Response has an actionable message plus unchanged error, hint, reason and retry interval. |
| Requested instance is wrong/unregistered | Guidance directs checking mcpforunity://instances and confirming the requested project identity. |
| Unity is still reloading | Guidance recommends waiting/rechecking state rather than claiming a specific diagnosis. |
| stale_connection reason | Existing message semantics are preserved; no_unity_session-specific text is not incorrectly applied. |
| Unity server is healthy but has zero registrations | Response clearly distinguishes server reachability from Unity session registration. |
| Existing client parses the response | Machine-readable keys and retry semantics remain backward-compatible. |
| No recovery action has yet been performed | Response remains guidance only; it does not claim a session was started or recovered. |

## Source references

- [PR #1470](https://github.com/CoplayDev/unity-mcp/pull/1470)
- Target [plugin_hub.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/transport/plugin_hub.py)
- Target [troubleshooting guide](https://github.com/ApexForge-devz/unity-mcp/blob/beta/website/docs/guides/troubleshooting.md)
- Candidate [response helper change](https://github.com/CoplayDev/unity-mcp/blob/9fcf0416c77b854afe6c4ab0c291f5f3a79923f5/Server/src/transport/plugin_hub.py)
- Candidate [troubleshooting guide change](https://github.com/CoplayDev/unity-mcp/blob/9fcf0416c77b854afe6c4ab0c291f5f3a79923f5/website/docs/guides/troubleshooting.md)
