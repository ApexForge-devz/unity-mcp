# Repository and Fork Inventory

**Snapshot date:** 2026-10-14  
**Classification:** Preliminary inventory; direct-fork enumeration is not yet complete.

## 1. Primary repositories to inspect

| Repository | Relationship / review priority | Why it is in scope | Initial status |
|---|---|---|---|
| [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp) | Target implementation | The code this research is intended to improve | Baseline source sampled on `beta`; deeper file-by-file mapping in progress |
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | Closest upstream | Likely canonical source of the target's architecture and current fixes | README + current source references reviewed; must recheck exact head / compare |
| [AnkleBreaker-Studio/unity-mcp-server](https://github.com/AnkleBreaker-Studio/unity-mcp-server) | Independent server paired with plugin | Progressive operation/schema discovery, compact JSON results, logical-error normalization, final-route guardrails, editor identity validation | Selected source files reviewed; reported token savings remain unverified here |
| [AnkleBreaker-Studio/unity-mcp-plugin](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin) | Companion Unity package | Main-thread dispatch, tool routes, status/dashboard, agent scheduling | README inspected; inspect dispatcher and tests next |
| [kitwright/unity-mcp](https://github.com/kitwright/unity-mcp) | Independent Unity Editor implementation | Tool export profiles, bounded output, spill handles and Editor-pump diagnostics | Selected source files reviewed; protocol implementation inspected only as an older-protocol reference |
| [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) | Independent implementation | Broad tools/skills ecosystem; upstream for a specialist Frame Debugger extension examined separately | README-level review in this pass; deeper source review remains open |
| [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp) | Independent implementation | Worktree-aware instance identity, AssetDatabase dependencies/reverse dependents and reference diagnostics | Server source plus dependency handler/tests sampled; simplified graph and unused-asset semantics documented |
| [mitchchristow/unity-mcp](https://github.com/mitchchristow/unity-mcp) | Independent implementation / possible derivative ancestry to verify | Resources, prompts, event/history-oriented workflows and current Unity support | README inspected; ancestry and source differences unverified |
| [pushkar-env/unity-mcp](https://github.com/pushkar-env/unity-mcp) | Independent implementation | .NET server/editor bridge with READ/WRITE/DESTRUCTIVE permissions and optional approval prompts | Architecture docs and README reviewed; actual enforcement tests remain to inspect |
| [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) | Independent implementation | Security/auth regression tests, bounded retry backoff, startup cleanup checks and batch Undo-group pattern | Source and test files reviewed; atomic rollback claim has important side-effect limits |
| [isuzu-shiranui/UnityMCP](https://github.com/isuzu-shiranui/UnityMCP) | Independent/derivative candidate | Potential useful divergent capabilities | Candidate; relationship and source not reviewed |
| [FunplayAI/funplay-unity-mcp](https://github.com/FunplayAI/funplay-unity-mcp) | Independent/derivative candidate | Potential newer multi-tool Unity workflow design | Candidate; relationship and source not reviewed |
| [akiojin/unity-mcp-server](https://github.com/akiojin/unity-mcp-server) | Independent/derivative candidate | May include server architecture useful for comparison | Candidate; relationship and source not reviewed |
| [killop/puerts-unity-mcp](https://github.com/killop/puerts-unity-mcp) | Specialist integration | Large codebase, likely relevant to scripting/JS bridge integration | Candidate; specialist relevance to baseline not yet established |
| [meta-quest/Unity-MCP-Extensions](https://github.com/meta-quest/Unity-MCP-Extensions) | Specialist extension | Unity/Quest ecosystem and possible extension patterns | Candidate; inspect if it has reusable MCP/editor integration patterns |

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


## 5. Snapshot metadata and expanded search coverage

GitHub repository metadata for [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) returned:
- Fork count: **1,553** (metadata observed 2026-10-14T04:58:04Z)
- Default branch: beta
- Stars: 14,923
- Repository license metadata: MIT
- Repository not archived at the time of inspection.

A full fork-by-fork inventory cannot be derived from relevance-ranked search pages. The GitHub connector rejected the public forks-list endpoint, so the inventory remains partial pending a complete paginated source. See [FORK_CANDIDATE_REGISTER.md](FORK_CANDIDATE_REGISTER.md) for query details, candidates and per-repository status; see [RESEARCH_LOG.md](RESEARCH_LOG.md) for the evolving coverage ledger.

Expanded searches performed on 2026-10-14:
- CoplayDev Unity fork:true (25 results)
- coplaydev-unity-mcp fork:true (24 results)
- MCPForUnity fork:true (7 results; page 2 empty)
- Unity AI Game Developer fork:true (100-result page)
- frame debugger unity-mcp (2 specialist candidates)
- memory profiler unity-mcp (1 specialist candidate)
- general unity-mcp fork:true pages 1–10 (100 results per populated page in the sampled pages)

Searches overlap and contain non-forks, derivative packages and unrelated projects. Counts are not additive and do not imply 1,000 unique repositories.


## 6. Source-review status update

As of the latest pass, detailed files have been inspected for:
- AnkleBreaker Studio (progressive tool tiers, result normalization, discovery and bridge identity);
- KitWright (request handler, tool export profiles, editor-thread helper);
- Ozan Kasikci (server instance registry and AssetDatabase dependency handler/tests);
- CoderGamester (batch implementation, auth/security tests, retry/cleanup tests and path handling tests);
- Ozymandros (tool-contract/observability docs, staged validation docs and path resolver tests);
- Pasha/Sabri Frame Debugger and Memory Profiler specialist extensions;
- VRCLearn and czmomocha upstream-derived branches.

Ivan Murzak and Mitch Christow remain at README-level in this pass. Several search-derived fork candidates remain metadata/README/compare-only. The coverage blocker is not the absence of useful leads; it is inability to retrieve/paginate the authoritative 1,553-item upstream fork listing with the currently exposed connector.
