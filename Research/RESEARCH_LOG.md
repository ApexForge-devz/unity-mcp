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
| f31d327d7ac50a41affb906c42054166960055b4 | Detailed MCP/FastMCP protocol compatibility plan |
| dd1c02c723fd6fda2c2c2af57e7605fb45cfd3fc | README links candidate register and specialist review |
| 81040d1d2d518304b53735fb5c6ac0804f5028eb | Expanded dated source research, fork count, and protocol notes |

## Remaining work, ordered

1. Get a complete paginated direct-fork list through a source that permits access to the forks endpoint. Do not infer completeness from repository search.
2. For every fork with nontrivial ahead commits, compare changed source/tests to the common ancestor and target baseline; classify all discovered direct forks, including stale/mirror/archived states.
3. Continue source review of candidate implementations with meaningful unique code, including current Unity API compatibility, tests and licenses.
4. Verify exact 2026-07-28 tools/list/discovery protocol requirements directly against the current official spec and formulate a client compatibility matrix.
5. No Unity runtime/build/test/benchmark has been executed in this session; retain those as explicit validation tasks.


## 2026-10-14 — Branch, tests, and active-upstream consistency pass

### Branch relationship, corrected and source-mapped

- Target/upstream beta HEAD: `142ea0af4890337f0ff6658df61db23982313717`.
- Upstream main HEAD: `aa5fc638d623f56178d50329d9ad8c541a57fe66`.
- With `beta` as compare base and `main` as compare head, GitHub reports `main` as 115 commits behind beta. The research branch is correctly based on beta. This direction was initially misstated in a progress update and corrected before finalizing these documents.
- Fetched the key P0/P1 source paths from main as a second check. The reviewed batch wrapper, Unity batch handler, tool-group manager, SafeZipExtractor, ManageAsset and FrameDebuggerOps have the same blob SHA as beta. FastMCP remains bounded below v4. ExecuteCode has three assembly-loading compatibility call-site differences, but those do not resolve the caller-controlled safety_checks trust-boundary issue.
- Full branch/issue/PR status and file SHAs are recorded in [UPSTREAM_BRANCH_COMPARISON.md](UPSTREAM_BRANCH_COMPARISON.md).

### Active upstream proposals and status snapshot

- [PR #1466: read-only built AssetBundle explorer](https://github.com/CoplayDev/unity-mcp/pull/1466) was open at the time of inspection. Its proposed path-restricted, paginated bundle list/info actions should be tracked before implementing the same feature. AssetBundle introspection is not the same as an AssetDatabase reference graph.
- [Issue #1438: path allowlist for execute_code file deletion](https://github.com/CoplayDev/unity-mcp/issues/1438) was open. It is a narrower user request and does not by itself provide a server-enforced trust boundary for arbitrary C#.
- [Issue #709: disabled tools reappear/list](https://github.com/CoplayDev/unity-mcp/issues/709) is a closed historical report against v9.4.0, not proof of an unresolved current bug.
- [PR #1470: actionable guidance when no Unity session is available](https://github.com/CoplayDev/unity-mcp/pull/1470) was open and based on the reviewed beta SHA. Its body reports Python test results, but they have not been independently run during this research session.
- These statuses can change; check current merge/closure state immediately before implementation.

### Mapped source recommendations to existing tests

- [SafeZipExtractorTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/SafeZipExtractorTests.cs), blob `e27b742d50516aa86d352e0174c3f6641bcd27b1`, already checks normal extraction, blocked extensions and traversal rejection. It lacks expanded-byte and entry-count quota coverage.
- [ExecuteCodeTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ExecuteCodeTests.cs), blob `dc3c4e360afe9c3273c5c39a1a8ff7d716c88464`, already covers routine execution and errors, blocks file deletion/process-start/infinite-loop patterns when checks are enabled, and tests that `safety_checks=false` bypasses the blocklist. New work should enforce a server policy and test aliases/remote paths, not duplicate the local blocklist tests.
- [BatchExecuteKeyPreservationTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/BatchExecuteKeyPreservationTests.cs), blob `3419dfed59655ef3b21d92e3f6c8a5a4c98e5363`, checks key preservation and representative batch behavior. Add contract and output-budget cases around it.
- CoderGamester's `BatchExecuteTool.cs` uses Unity Undo groups and `RevertAllDownToGroup` for its atomic option. This is not a transaction across external/file/server side effects or commands that do not register Undo; the target should not transplant an unrestricted atomic promise.
- Reviewed CoderGamester security/retry/path tests and Ozymandros path/validation test designs. Reviewed PICO XR source carries a proprietary notice that prohibits reproduction without permission, so it remains reference-only.

### New documents and commits in this tranche

- [PROTOCOL_COMPATIBILITY.md](PROTOCOL_COMPATIBILITY.md): modern MCP tools/list contract and FastMCP 4 compatibility spike plan.
- [UPSTREAM_BRANCH_COMPARISON.md](UPSTREAM_BRANCH_COMPARISON.md): beta/main source snapshot, issue/PR status and overlap rules.
- [SPECIALIST_WORKFLOWS.md](SPECIALIST_WORKFLOWS.md): Frame Debugger, Memory Profiler and AssetDatabase dependency analysis.
- [ALTERNATIVES.md](ALTERNATIVES.md): extended with CoderGamester, Ozymandros and PICO patterns.
- [FORK_COMPARISONS.md](FORK_COMPARISONS.md): expanded compare outcomes including the very stale Puerts/TypeScript fork.
- [FORK_CANDIDATE_REGISTER.md](FORK_CANDIDATE_REGISTER.md): expanded search terms/candidates and explicit coverage limits.
- [IMPROVEMENT_BACKLOG.md](IMPROVEMENT_BACKLOG.md): incorporates existing-test coverage and active upstream proposals.

Selected source commits include:
- `1eca549253e72309225982b866d0205ea924010a` — upstream branch/issue/PR status.
- `aa42a913046879a96167ead38f5b998413b18060` — backlog cross-check against upstream proposals.
- `17b1eef0e70f223a99558129045fb4b96e3d1f50` — further fork comparison results.
- `2c66d2405add5a446ebee3c88617c1ed34c00ca7` — security/lifecycle and specialist source patterns.
- `b44351d4d546646c4b8174e5fa07a42d554be8f4` — align recommendations with existing target tests.
- `6b90032a093551518a6c0bf845fee5c26b41a248` — source-review status in repository inventory, if this commit remains present on current branch history.

## Hard stop before describing the work as exhaustive

This branch is a substantial source-informed research tranche, **not yet an exhaustive inventory of all direct forks**. The upstream repo metadata returned 1,553 forks. Repository-search pages and candidate-by-candidate compares cannot prove coverage of all 1,553. The available GitHub connection rejects the canonical forks-list endpoint, the public forks page is not retrievable through the browsing path, and direct network DNS is unavailable in the working runtime. Do not mark the fork phase complete until a full paginated fork inventory (or a demonstrably equivalent complete source) can be read, normalized, and classified.

No Unity Editor session, Unity package build, Python test suite, CI workflow, live MCP client round-trip, or performance benchmark was executed by this research pass. Those are validation steps, not completed checks.


## Continued research — target draft PR and upstream overlap (2026-10-14)

### Target PR #1 and branch review

Inspected [ApexForge-devz/unity-mcp PR #1](https://github.com/ApexForge-devz/unity-mcp/pull/1), an open draft against beta:
- Head SHA: `9480a79408422c95c17c08ed4efb0b66d24a1f24`.
- 89 changed files, 7,039 additions, 247 deletions.
- Scope: lazy asset-search page materialization, fixed Game View resolution/readback, native Editor-window screenshots, modal liveness/answering and scene-change guard, Core-first tool discovery and description/schema compaction.
- Added [DRAFT_PR_AND_UPSTREAM_OVERLAP.md](DRAFT_PR_AND_UPSTREAM_OVERLAP.md) with the source/file comparison and risk status.

### PR-head CI is partly green, but Unity runtime is still unverified

GitHub Actions run results on head `9480a794...` were inspected individually:
- Python tests: success.
- C# compile check for configured Windows/macOS/Linux symbols: success.
- Documentation reference drift, docs build: success.
- Unity Tests workflow: workflow conclusion success, but actual Unity test job skipped (license secret unavailable).
- E2E Bridge Smoke workflow: workflow conclusion success, but actual bridge smoke job skipped (license secret unavailable).

Therefore, CI does establish Python, compile-only and docs evidence; it does **not** establish that the draft's Unity EditMode tests or live bridge smoke ran. Those remain mandatory before trusting the modal/transport lifecycle paths.

### Duplicate/overlap findings

- The sampled Editor-window screenshot tool, helper, Python wrapper, tests and guide in PR #1 have exactly the same blob SHA as the corresponding files in DelPariah's [upstream screenshot PR #1417](https://github.com/CoplayDev/unity-mcp/pull/1417). The research records this as source equality and a coordination/provenance item, without inferring intent.
- The target draft's `ManageAsset.SearchAssets` lazy-loads only page rows, validates page inputs and uses `long` page offset/count arithmetic. Upstream [PR #1459](https://github.com/CoplayDev/unity-mcp/pull/1459) applies a similar algorithm but its source/tests are not identical; target specifically rejects blank/invalid sanitized folders before calling AssetDatabase. This is one overlapping implementation to reconcile.
- Target modal probe code is materially longer/different than KamilDev's [PR #1350](https://github.com/CoplayDev/unity-mcp/pull/1350): it adds bounded capture/answer deadlines, owner-chain checks for a disabled parent, unknown-modal fail-safe state, answer serialization and verifies the original dialog closed, plus explicit sampler stop signaling and a bounded join. These additions are potential reliability improvements, but need actual Windows Editor/E2E tests.
- The draft's schema compaction copies tool definitions and shortens descriptions while preserving parameter structure, with `get_tool_details` exposing the full registered definition. That is not equivalent to centralizing/capping **tool results**. Its dynamic group activation still relies on FastMCP 3 per-session visibility; the modern MCP tools/list contract needs a separate mode-aware design.

### Additional upstream proposal list

Fetched the current first page of open upstream PRs and added it to [UPSTREAM_BRANCH_COMPARISON.md](UPSTREAM_BRANCH_COMPARISON.md), including #1472 custom-tool registration, #1468 user config preservation, #1465 Editor focus-refresh threading, #1463 Play Mode readiness, #1462 prefab overrides, #1461 custom instructions, #1460 test cleanup, and earlier #1459/#1417/#1350 overlap. Each item remains a snapshot and must be rechecked before implementation.

