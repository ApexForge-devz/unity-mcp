# Research Log and Coverage Ledger

**Last updated:** 2026-10-14  
**Branch:** research/exhaustive-unity-mcp-2026-10  
**Important:** Active research. This is not the final exhaustive report.

## 2026-10-14 — Branch and initial baseline

- Created research branch from beta: research/exhaustive-unity-mcp-2026-10.
- Added Research/README.md and Research/METHODOLOGY.md defining scope, source evidence rules, classifications, and the limitation that a search-ranked sample is not a complete fork enumeration.
- GitHub metadata confirms ApexForge-devz/unity-mcp is public, default branch beta, with push permission in the connected session.
- GitHub recent-commit search reports the target and CoplayDev upstream at HEAD 142ea0af4890337f0ff6658df61db23982313717. Compare returned identical for ApexForge beta versus CoplayDev beta.
- Fetched target server/package source files and confirmed batch, tool-group, arbitrary C# execution, ZIP extraction and asset metadata facts; captured exact source links and blob SHAs in CURRENT_BASELINE.md.
- Added initial repository discovery inventory and candidate-fork status.

## 2026-10-14 — Fork and derivative comparisons

Reviewed candidates and evidence:

| Repository | Review depth today | Observed finding | Next action |
|---|---|---|---|
| ApexForge-devz/unity-mcp | Source paths + branch comparison | beta identical to CoplayDev beta | Keep as target baseline |
| CoplayDev/unity-mcp | Recent commit + source baseline | Closest upstream; same observed beta HEAD | Recheck when research snapshot is refreshed |
| TomLeeLive/CoplayDev-unity-mcp | Branch comparison + README fetch | beta is 749 commits behind; zero ahead commits reported | Class as stale snapshot unless non-default refs reveal delta |
| IgorOffline/CoplayDev-unity-mcp | Branch comparison + README fetch | beta is 415 commits behind; zero ahead commits reported | Class as stale snapshot unless non-default refs reveal delta |
| aj1126/-forked-CoplayDev-unity-mcp | Branch comparison + README fetch | beta is 199 commits behind; zero ahead commits reported | Class as stale snapshot unless non-default refs reveal delta |
| VRCLearn/unity-mcp | Branch comparison + localization/workflow source | 11 commits ahead, 115 behind, 31 changed files; localization and VPM/sync automation | Review actual diffs and keep distribution ideas separate from core behavior |
| JossueMac/unity-mcp | Repo metadata + README | public pinned-v10.0.0 branch | Compare API returned 404 across owner refs; delta not proven |
| pashara/Unity-MCP-CoplayDev-FrameDebugger | README + latest commit/diff narrative | Specialized Frame Debugger port; claims capture-consistency fixes and additional EditMode tests | Map fixes against target's current profiling implementation before adoption |
| iflow-mcp/coplaydev-unity-mcp | Repo metadata + README | upstream-branded copy candidate | Tree/diff not yet compared |
| BaBy-Metal/unity-mcp | Repo metadata + README | README blob matches dawnli sampled README blob | Tree/diff needed before classifying entire repo as duplicate |
| dawnli/unity-mcp | Repo metadata + README | README blob matches BaBy-Metal sampled README blob | Tree/diff needed |
| ibelongedtoyou/unity-mcp-2019 | Search result only | old-named fork candidate | Metadata/source ancestry needed |
| KasuganoSorakog/UnityMCP | Search result only | fork candidate | Metadata/source ancestry needed |
| ShibaPapaMikami/unity-mcp-template-coplay | Search result only | fork/template candidate | Metadata/source ancestry needed |
| haroon911/unity-mcp-vpm | Search result only | packaging candidate | Metadata/source ancestry needed |
| OverTM/CoplayDev.unity-mcp | Search result only | fork candidate | Metadata/source ancestry needed |
| heshuimu/CoplayDev.unity-mcp | Search result only | fork candidate | Metadata/source ancestry needed |
| Bon-Games/Unity-MCP-CoplayDev | Search result only | fork candidate | Metadata/source ancestry needed |
| neurmostudio0409/CoplayDev-unity-mcp | Search result only | fork candidate | Metadata/source ancestry needed |
| buianhduc-ai/unity-mcp-CoplayDev | Search result only | fork candidate | Metadata/source ancestry needed |
| adrnsyhsdq-debug/unity-mcp | Search result only | fork candidate | Metadata/source ancestry needed |
| devoffeed/UN-mcp-server | Search result only | possible renamed distribution | Verify ancestry |
| VRCLearn/unity-mcp | Source-reviewed partial fork | VPM workflows/localization | Continue if needed |
| pashara/Unity-MCP-CoplayDev-FrameDebugger | Specialized source derivative | Captured-frame/event-index correctness is the strongest novel use case found so far | Inspect implementation/tests and map overlap |

**Discovery scope caveat:** GitHub repository-search queries on this date included unity-mcp fork:true, CoplayDev unity-mcp fork:true, unity editor mcp, unity-mcp-server and unity mcp fork:true. The first broad query returned a capped 100-item page and the specific CoplayDev query surfaced 23 candidates. The connected GitHub fetch action rejected the upstream /forks endpoint, and web fetch of the GitHub forks page was disabled. Therefore these searches do **not** prove a complete inventory of the upstream’s approximately 1,500 forks. The repository inventory must remain marked partial until fork enumeration can be obtained through a complete, paginated source or another reproducible method.

The GitHub compare endpoint returned 404 for several cross-owner head references. These outcomes are recorded as “comparison unavailable,” not as evidence that a repo is missing or has no useful changes. Continue with per-repo source and ancestry evidence where possible.

## 2026-10-14 — Independent implementation source samples

| Repository | Source inspected | Useful idea | Confidence/limitation |
|---|---|---|---|
| AnkleBreaker-Studio/unity-mcp-server | src/tool-tiers.js, src/response-format.js, docs/discovery.md, recent commit | Progressive discovery, compact JSON, logical-error normalization, final-route read-only checks, editor identity validation | Source sampled; reported token/byte gains are project-reported, not reproduced |
| AnkleBreaker-Studio/unity-mcp-plugin | README | Companion Editor bridge, route scheduling and multiple-agent use cases | README-level for now |
| kitwright/unity-mcp | MCPRequestHandler.cs, MCPToolExportPolicy.cs, EditorThreadHelper.cs | Inline output cap/spill handles, named tool profiles, main-thread pump stall detection | Source sampled; protocol handler itself only supports older versions, so not a source for modern protocol migration |
| ozankasikci/unity-editor-mcp | server.js, package.json, AssetDependencyToolHandler.js | Instance identity/worktree selection; asset dependency/dependent/cycle/reference/size analysis contract | Source samples inspected; dependency tool's Unity implementation still needs review |
| pushkar-env/unity-mcp | README, docs/architecture.md | Separate MCP server and Unity bridge; READ/WRITE/DESTRUCTIVE permissions and approval | Architecture/docs-level so far |
| IvanMurzak/Unity-MCP | README | Mature independent implementation, custom tools/prompts/assets/profiler workflows | README-level; do not accept specific features without handler/tests review |
| mitchchristow/unity-mcp | README | Modern Unity support, resources/prompts, worktree-oriented documentation | README-level; no unique feature accepted yet |

## 2026-10-14 — Backlog synthesis

The ranked backlog now documents:
- P0: correct batch semantics and bounded responses; server-enforced arbitrary-C# policy; streamed ZIP expansion quotas.
- P1: FastMCP 4/MCP 2026-07-28 compatibility spike; central result output governor/continuation; verify final-route permissions and Editor identity.
- P2: editor-pump liveness and safe retries; AssetDatabase dependency/reference analysis; Frame Debugger consistency gap check; package/version/release alignment.
- P3: visual diffs, event/history resources and other specialist coverage only after benchmarked task evidence.

These are recommendations. No Unity project was launched, no Python test suite or CI run was triggered, no real editor round-trip was executed, and no latency or task-success benchmark was performed in this research session.

## Research commits so far

| Commit | Contents |
|---|---|
| 4f61789a53ae4e0cac124641303285bfdeaf996a | Research workspace and status README |
| a2168cc1a26a19d4d91a9e594f571aac24554c3b | Methodology and evidence discipline |
| 3856c68c5a82275913b175c88ce98e426fc98fae | Repository and fork inventory |
| cde48679f82b43f01d68a3a7e49f502593c02aa8 | Current implementation baseline |
| e8696e8109fbbb4953df7ecebd3c30e0d86f6681 | Fork/derivative comparison findings |
| 614fb380cf9183228425dd8e780d9dd2309e4c5f | Independent implementation comparisons |
| 854ad7777dfb26dd1b072c050ba66f48561dd735 | Consolidated improvement backlog |

## Next actions (not yet complete)

1. Obtain a fully paginated direct-fork inventory for CoplayDev/unity-mcp and ApexForge-devz/unity-mcp; preserve retrieval date and classify duplicate/stale/active forks.
2. Compare each plausible fork head against the upstream beta/base ancestry and review commits/files for nontrivial ahead-of-upstream changes; expand to forks-of-forks if ancestry indicates meaningful descendants.
3. Review fork-specific additions to C#, Python, tests, package and workflow code—not just README. Record per-candidate decision.
4. Inspect target profiling/Frame Debugger implementation against pashara's capture-consistency issues.
5. Inspect Unity-side implementation/tests of Ozan's dependency analyzer, then compare semantics to target code.
6. Inspect protocol changes with authoritative MCP spec/FastMCP release references and design a compatibility matrix, especially modern static discovery versus legacy group visibility.
7. Update this log, README coverage checklist, and backlog after each stage. Do not mark research complete until the stated inventory boundary and candidate-review criteria are met.


## 2026-10-14 — Expanded fork discovery and specialist deep dives

### Fork universe and search boundary

- GitHub repo metadata for [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) reports **1,553 forks** at 2026-10-14T04:58:04Z, default branch beta, 14,923 stars and MIT license metadata.
- Additional queries covered `CoplayDev Unity fork:true`, `coplaydev-unity-mcp fork:true`, `MCPForUnity fork:true`, `Unity AI Game Developer fork:true`, `frame debugger unity-mcp`, `memory profiler unity-mcp`, and broad `unity-mcp fork:true` pages 1–10. Broad result pages overlap and include unrelated/independent implementations, so they must not be summed as unique direct forks.
- The connected GitHub interface can fetch repo metadata/tree/source and compare refs, but the canonical `/repos/CoplayDev/unity-mcp/forks` endpoint is rejected; the public HTML forks listing was not retrievable. The `parent:` repository filter did not return results. Complete direct-fork enumeration is therefore still blocked by retrieval capability.
- Expanded the register with additional candidates, including `czmomocha/unity-coplayDev-mcp`, `Kayungko/McpForUnityChinese`, `sabriboughanmi/Unity-MCP-MemoryProfiler`, `Ozymandros/Unity-MCP-Server`, `Pico-Developer/Unity-MCP-Extensions`, `Savidyaf/Unity-VFX-Graph-MCP-Tool`, and others. Statuses distinguish source-reviewed candidates, README-only projects and candidates needing parent/diff verification.

### Additional fork comparison

- [`czmomocha/unity-coplayDev-mcp`](https://github.com/czmomocha/unity-coplayDev-mcp): compare reports 8 commits ahead, 1,541 behind and 32 changed files relative to upstream beta. Reviewed `manage_ts_script.py`, which allows TypeScript/TSX under `Assets` and `DevCommon/TSProject` with path traversal checks, plus its custom-tool guide and Unity 2020 compatibility summary. Useful only for Puerts/TypeScript use cases; not safe to merge wholesale because the branch is very stale.
- The [target Frame Debugger handler](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs) exposes enable/disable/paged event listing, while the Pashara derivative has async capture sessions, event query/detail, summaries, diffs and render-target previews. The useful delta is a substantial optional rendering-inspection workflow plus capture-consistency safeguards, not just tool renaming.
- The [Memory Profiler extension](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler) source contains incremental snapshot-open lifecycle, bounded sessions, paginated query and diff engines. Target snapshot tools only take/list/compare file metadata, so queryable snapshot analysis is a plausible optional capability gap.
- The Ozan asset dependency handler was read beyond its MCP wrapper. It calls Unity's AssetDatabase for reverse-dependency and unused-candidate scans but has simplified recursive depth/cycle detection, potentially expensive full-project scans, and false-positive risks when assets are loaded via Addressables, Resources, code strings or external pipelines. Reuse action ideas, not its heuristic conclusions.
- The current target FrameDebuggerOps and MemorySnapshotOps sources were inspected directly, reducing the risk of recommending a duplicate subsystem without knowing current coverage.

### Current protocol reference verification

- The MCP 2026-07-28 announcement describes a stateless protocol core, removal of `initialize`/`initialized` and `Mcp-Session-Id`, a new `server/discover` mechanism, deterministic/cached discovery, and authentication changes.
- FastMCP 4.0 release notes and protocol notes describe support for both the newer 2026-07-28 protocol era and legacy handshake-era clients in one server. This supports a **controlled compatibility migration** recommendation, not a blind dependency bump.
- Reference links: [MCP 2026-07-28 announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/), [Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [FastMCP v4 protocol notes](https://github.com/PrefectHQ/fastmcp/blob/main/dev-docs/v4-notes/protocol-2026.md), [FastMCP v4.0 release](https://github.com/PrefectHQ/fastmcp/releases/tag/v4.0.0).
- These protocol findings still require an explicit contract matrix for modern and supported legacy clients before changing the FastMCP upper bound or how `manage_tools` interacts with tool visibility.

## Latest research commit sequence (selected)

| Commit | Contents |
|---|---|
| 4f61789a53ae4e0cac124641303285bfdeaf996a | Initial Research workspace README |
| a2168cc1a26a19d4d91a9e594f571aac24554c3b | Methodology and evidence standard |
| 3856c68c5a82275913b175c88ce98e426fc98fae | Preliminary repository/fork inventory |
| cde48679f82b43f01d68a3a7e49f502593c02aa8 | Current target baseline |
| e8696e8109fbbb4953df7ecebd3c30e0d86f6681 | Initial fork comparisons |
| 614fb380cf9183228425dd8e780d9dd2309e4c5f | Independent implementation comparisons |
| 854ad7777dfb26dd1b072c050ba66f48561dd735 | Initial consolidated improvement backlog |
| 5052d5fdb691d5d35ae5d21fb5012b8e9a8622a7 | Initial coverage/research log |
| 6ffa5100534269ce5e2e2b453e1cb8a593c925df | Frame Debugger gap expanded |
| 86b976f0c76e6ee6a91b4dbd0b7de905ef3fa3ae | Memory Profiler backlog addition |
| 13d5b8afa219a43b3dd4611645813e7cf5ede6c0 | Asset and memory specialist-source review |
| 972c79cf2f69155ec36eb1833f4968ecbd0859eb | Stale Puerts/TypeScript fork findings |
| d98bc62ca87c9202460c9a595322950feeff8514 | Expanded candidate register |
| df88766bb853e91ca8d4e774957c084533ee6f50 | Dedicated specialist workflow review |
| undefined | Current log version before this update |
| undefined | (placeholder for this append's preceding file SHA; next commit supersedes prior log) |

## Remaining work, ordered

1. Get a complete paginated direct-fork list through a source that permits access to the forks endpoint. Do not infer completeness from repository search.
2. For every fork with nontrivial ahead commits, compare changed source/tests to the common ancestor and target baseline; classify all discovered direct forks, including stale/mirror/archived states.
3. Continue source review of candidate implementations with meaningful unique code, including current Unity API compatibility, tests and licenses.
4. Verify exact 2026-07-28 tools/list/discovery protocol requirements directly against the current official spec and formulate a client compatibility matrix.
5. No Unity runtime/build/test/benchmark has been executed in this session; retain those as explicit validation tasks.
