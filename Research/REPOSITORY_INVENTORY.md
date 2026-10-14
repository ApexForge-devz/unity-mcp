# Repository and Fork Inventory

**Snapshot date:** 2026-10-14  
**Classification:** Preliminary inventory; direct-fork enumeration is not yet complete.

## 1. Primary repositories to inspect

| Repository | Relationship / review priority | Why it is in scope | Initial status |
|---|---|---|---|
| [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp) | Target implementation | The code this research is intended to improve | Baseline source sampled on `beta`; deeper file-by-file mapping in progress |
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | Closest upstream | Likely canonical source of the target's architecture and current fixes | README + current source references reviewed; must recheck exact head / compare |
| [AnkleBreaker-Studio/unity-mcp-server](https://github.com/AnkleBreaker-Studio/unity-mcp-server) | Independent server paired with plugin | Progressive operation/schema discovery, multi-agent scheduling, integrations, operation history | README inspected; source review in progress |
| [AnkleBreaker-Studio/unity-mcp-plugin](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin) | Companion Unity package | Main-thread dispatch, tool routes, status/dashboard, agent scheduling | README inspected; inspect dispatcher and tests next |
| [kitwright/unity-mcp](https://github.com/kitwright/unity-mcp) | Independent Unity Editor implementation | Tool export profiles, bounded output, stable IDs, editor-thread diagnostics, Undo-aware operations | README inspected; selected source paths known; inspect source next |
| [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) | Independent implementation | Asset workflows, custom tool registration, profiling, prompts/resources and package integrations | README inspected; source review in progress |
| [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp) | Independent implementation | Editor operations, multi-instance discovery, worktree-aware routing, screenshot and Play Mode workflows | README inspected; source review in progress |
| [mitchchristow/unity-mcp](https://github.com/mitchchristow/unity-mcp) | Independent implementation / possible derivative ancestry to verify | Resources, prompts, event/history-oriented workflows and current Unity support | README inspected; ancestry and source differences unverified |
| [pushkar-env/unity-mcp](https://github.com/pushkar-env/unity-mcp) | Independent implementation | .NET server/editor bridge with READ/WRITE/DESTRUCTIVE permissions and optional approval prompts | README inspected; verify repo identity and inspect enforcement/tests |
| [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) | Independent/earlier implementation | Alternate protocol/server architecture and established workflow surface | Candidate; not yet source-reviewed |
| [isuzu-shiranui/UnityMCP](https://github.com/isuzu-shiranui/UnityMCP) | Independent/derivative candidate | Potential useful divergent capabilities | Candidate; relationship and source not reviewed |
| [FunplayAI/funplay-unity-mcp](https://github.com/FunplayAI/funplay-unity-mcp) | Independent/derivative candidate | Potential newer multi-tool Unity workflow design | Candidate; relationship and source not reviewed |
| [akiojin/unity-mcp-server](https://github.com/akiojin/unity-mcp-server) | Independent/derivative candidate | May include server architecture useful for comparison | Candidate; relationship and source not reviewed |
| [killop/puerts-unity-mcp](https://github.com/killop/puerts-unity-mcp) | Specialist integration | Large codebase, likely relevant to scripting/JS bridge integration | Candidate; specialist relevance to baseline not yet established |
| [meta-quest/Unity-MCP-Extensions](https://github.com/meta-quest/Unity-MCP-Extensions) | Specialist extension | Unity/Quest ecosystem and possible extension patterns | Candidate; inspect if it has reusable MCP/editor integration patterns |
| [mitchchristow/unity-mcp](https://github.com/mitchchristow/unity-mcp) | Independent implementation | Potentially useful resources/prompts/event history | README sampled; further source audit required |

## 2. Search methods used so far

GitHub repository search queries run on 2026-10-14:
- `unity-mcp fork:true`
- `CoplayDev unity-mcp fork:true`
- `unity editor mcp`
- `unity-mcp-server`
- `unity mcp fork:true`

Results:
- The first query returned a capped page of 100 results; it includes many different MCP implementations and does **not** establish that every result is a fork of CoplayDev/unity-mcp.
- The more specific `CoplayDev unity-mcp fork:true` query returned 23 candidates. It surfaced renamed fork candidates such as `JossueMac/unity-mcp` (pinned-v10.0.0), `pashara/Unity-MCP-CoplayDev-FrameDebugger`, `TomLeeLive/CoplayDev-unity-mcp`, `iflow-mcp/coplaydev-unity-mcp`, `IgorOffline/CoplayDev-unity-mcp`, `BaBy-Metal/unity-mcp`, `VRCLearn/unity-mcp`, and others.
- These are **search results**, not an authoritative inventory returned by the direct-forks API. Parentage and current divergence must be verified per repository.
- GitHub search also returned a range of adjacent projects, old experiments, plugins, Unity-related MCPs and false positives. Keep them classified instead of silently treating all as fork candidates.

## 3. High-value fork candidates to diff

| Candidate | Reason to inspect | Status |
|---|---|---|
| [JossueMac/unity-mcp](https://github.com/JossueMac/unity-mcp) | Branch is named `pinned-v10.0.0`; compare release pinning/package stability with current beta | Not diffed |
| [pashara/Unity-MCP-CoplayDev-FrameDebugger](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger) | Name suggests Frame Debugger workflow work; verify actual code and source lineage before crediting it | Not diffed |
| [TomLeeLive/CoplayDev-unity-mcp](https://github.com/TomLeeLive/CoplayDev-unity-mcp) | Recent fork candidate; inspect changed commits and whether changes are upstreamed | Not diffed |
| [iflow-mcp/coplaydev-unity-mcp](https://github.com/iflow-mcp/coplaydev-unity-mcp) | Organization fork/packaging candidate; check how it handles distribution or integrations | Not diffed |
| [IgorOffline/CoplayDev-unity-mcp](https://github.com/IgorOffline/CoplayDev-unity-mcp) | Fork candidate; inspect divergence and maintenance | Not diffed |
| [BaBy-Metal/unity-mcp](https://github.com/BaBy-Metal/unity-mcp) | Fork candidate | Not diffed |
| [VRCLearn/unity-mcp](https://github.com/VRCLearn/unity-mcp) | Potential XR/community-specific adaptation; verify whether any useful code differs | Not diffed |
| [aj1126/-forked-CoplayDev-unity-mcp](https://github.com/aj1126/-forked-CoplayDev-unity-mcp) | Explicit fork candidate; inspect for meaningful delta | Not diffed |
| [ibelongedtoyou/unity-mcp-2019](https://github.com/ibelongedtoyou/unity-mcp-2019) | Old fork naming suggests history/compatibility relevance only if source delta is meaningful | Not diffed |

## 4. Discovery gaps

- Direct-fork pagination of the CoplayDev repository has not yet been completed.
- Direct-fork pagination of the target's ApexForge repository has not yet been completed.
- Forks of forks and renamed forks have not yet been enumerated systematically.
- Search is relevance-ranked and may omit repositories with generic names; search totals must not be mistaken for all public projects.
- This document will be updated with repo IDs, branch/commit snapshots, parent links when verifiable, activity status, source-review status, and a reason for exclusion when a candidate is not reviewed in depth.
