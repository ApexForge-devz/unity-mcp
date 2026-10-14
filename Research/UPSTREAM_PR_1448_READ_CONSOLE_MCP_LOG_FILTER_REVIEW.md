# Upstream PR #1448 — Exclude MCP Diagnostics from Project Console Reads

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1448](https://github.com/CoplayDev/unity-mcp/pull/1448)  
**Target baseline:** beta 142ea0af4890337f0ff6658df61db23982313717  
**Candidate head:** 789ca9efe5c3097b3cf50b2791c73378b6444670  
**Status:** Open, not merged  
**Decision:** **ADAPT as an opt-in filter; retain inclusion by default.**  
**Review type:** Source and test inspection. No tests were run independently.

## Executive assessment

PR #1448 adds `include_mcp_logs` to the existing `read_console` MCP tool and a matching `--no-mcp-logs` CLI option. It allows a caller doing project diagnostics to exclude the bridge's own console messages, which otherwise can consume a small count/page limit before project warnings or errors appear.

This is useful for the deterministic scene workflow: after script compilation or scene regeneration, the agent often wants project warnings/errors rather than repeated transport/debug messages. However, MCP's own logs remain important while troubleshooting the bridge itself. The candidate therefore leaves the default unchanged (include MCP logs) and only filters them when explicitly asked.

## 1. Source-verified behavior

### Exact prefix classification

The candidate adds `McpLog.IsMcpLogMessage`, which checks for the logger's actual prefixes:
- Rich-text info prefix: `<b><color=#2EA3FF>MCP-FOR-UNITY</color></b>:`
- Rich-text debug prefix: `<b><color=#6AA84F>MCP-FOR-UNITY</color></b>:`
- Rich-text warning prefix: `<b><color=#cc7a00>MCP-FOR-UNITY</color></b>:`
- Rich-text error prefix: `<b><color=#cc3333>MCP-FOR-UNITY</color></b>:`
- Plain fallback prefix: `MCP-FOR-UNITY:`

The matching is at the beginning of the message, not a broad substring check. That preserves ordinary project messages that happen to mention “MCP for Unity” later in their text. Do not widen the rule to hide every message containing “MCP”, “Unity MCP” or a bridge-related word.

### Filtering occurs before count and paging

In the C# `ReadConsole.GetConsoleEntries` loop, the candidate excludes matching MCP messages immediately after reading each non-empty log entry and before classifying severity, applying `filterText`, incrementing `totalMatches`, enforcing `count`, or advancing page/cursor results.

This ordering is important. If MCP messages were removed after paging, they would still consume the entry budget and the user might get a short or empty page despite matching project messages being present later in the Console. The candidate's tests cover both limited count and page/cursor flows.

### The default and transport contract remain compatible

- `includeMcpLogs` defaults to true in the C# handler.
- The Python `read_console` wrapper exposes `include_mcp_logs`, supports boolean string coercion, and forwards it as `includeMcpLogs`.
- The CLI adds `--mcp-logs/--no-mcp-logs`, defaulting to inclusion.
- Existing MCP and CLI behavior remains unchanged when the option is omitted.
- The option controls reads only; it does not clear, suppress, or alter entries in Unity's actual Console buffer.

## 2. Candidate regression coverage

The PR adds tests for:
- Exclusion before a non-paged count limit.
- Exclusion before cursor/pagination limits, while keeping the subsequent project message reachable.
- Inclusion by default and when `includeMcpLogs=true`.
- Keeping a normal project message that merely includes the plain MCP prefix later in the message.
- The four rich-text logger prefixes and the plain prefix.
- Python wrapper forwarding, including default/true/false and string values.
- CLI defaults and explicit `--no-mcp-logs` / `--mcp-logs` behavior.

The PR author reports 33 ReadConsole EditMode tests passed on Unity 6000.3.24f1/macOS and 1,583 Python tests passed with 18 skipped; the author notes the configured Unity-version matrix was unavailable, and Windows/live transport/PlayMode weren't tested. These are author-reported results, not an independent run.

## 3. Adoption caveats

1. **Do not change the default to exclude MCP logs.** Bridge logs are critical for connection, reload, transport and registration troubleshooting.
2. Treat the filter as a presentation/query option, not a deletion/suppression mechanism; all log entries remain in Unity's normal Console/log file.
3. Keep classification tightly tied to the logger's explicit prefixes so project messages with similar text are not accidentally hidden. Add a test whenever the first-party log prefix format changes.
4. Ensure the filter applies consistently to all output formats and before count/page accounting, as the current proposal does.
5. A message from the bridge that lacks the standard prefix may remain visible. That is a conservative false negative, preferable to a broad heuristic that hides project errors.

## Recommended decision

**ADAPT, P2 / small diagnostic-quality improvement.** This makes project console reads more useful during compile/generation validation without changing default bridge troubleshooting behavior. Expose it in the existing read_console action and CLI; do not add a separate MCP tool. Accept only after the count/page behavior and prefix tests are present in target.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Option omitted | MCP logs remain included, preserving old behavior. |
| Exclusion requested | Recognized MCP-prefixed entries do not consume count/page slots. |
| Rich-text info/debug/warning/error prefix | Each recognized logger prefix is excluded. |
| Plain fallback prefix | The known plain MCP prefix is excluded. |
| Ordinary project text mentions MCP later in the message | It remains visible. |
| Excluded log precedes a project warning on the first page | The project warning fills the page/count slot. |
| Every candidate entry is excluded | The response has an accurate empty/truncated/next-cursor result. |
| Detailed/plain/json output | Filtering is consistent across supported formats. |
| CLI flag omitted / `--mcp-logs` | Bridge logs remain included. |
| CLI `--no-mcp-logs` | Option reaches the same C# filter as MCP. |
| Clear action | Existing behavior is unchanged; the option does not alter clear semantics. |

## Source references

- [PR #1448](https://github.com/CoplayDev/unity-mcp/pull/1448)
- Candidate [McpLog.cs](https://github.com/CoplayDev/unity-mcp/blob/789ca9efe5c3097b3cf50b2791c73378b6444670/MCPForUnity/Editor/Helpers/McpLog.cs)
- Candidate [ReadConsole.cs](https://github.com/CoplayDev/unity-mcp/blob/789ca9efe5c3097b3cf50b2791c73378b6444670/MCPForUnity/Editor/Tools/ReadConsole.cs)
- Candidate [read_console.py](https://github.com/CoplayDev/unity-mcp/blob/789ca9efe5c3097b3cf50b2791c73378b6444670/Server/src/services/tools/read_console.py)
- Candidate [ReadConsoleTests.cs](https://github.com/CoplayDev/unity-mcp/blob/789ca9efe5c3097b3cf50b2791c73378b6444670/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ReadConsoleTests.cs)
- Target [ReadConsole.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ReadConsole.cs)
