# Fork and Derivative Comparison Findings

**Snapshot:** 2026-10-14  
**Comparison base:** [CoplayDev/unity-mcp `beta`](https://github.com/CoplayDev/unity-mcp/tree/beta), observed HEAD `142ea0af4890337f0ff6658df61db23982313717`  
**Target:** [ApexForge-devz/unity-mcp `beta`](https://github.com/ApexForge-devz/unity-mcp/tree/beta), observed HEAD `142ea0af4890337f0ff6658df61db23982313717`.

This is an initial comparison, not a complete scan of all 1,500+ upstream forks. Candidates were discovered through GitHub search; source ancestry, compare results, commit messages, and selected source files were then checked. Repositories with no meaningful delta are still logged to avoid rediscovery.

## 1. Direct baseline comparison

### ApexForge-devz/unity-mcp

GitHub compare returned identical for `ApexForge-devz/unity-mcp:beta` and `CoplayDev/unity-mcp:beta`, and recent-commit searches reported the same latest commit SHA `142ea0af4890337f0ff6658df61db23982313717`.

**Finding:** The public target's `beta` branch is an upstream mirror at this snapshot. There is no ApexForge-only code delta in that branch to inspect. Research recommendations should be easy to upstream or should document why they intentionally diverge.

## 2. Fork candidates checked by GitHub compare

| Repository / ref | Compare result against upstream `beta` | Finding | Usefulness to our implementation |
|---|---|---|---|
| [TomLeeLive/CoplayDev-unity-mcp `beta`](https://github.com/TomLeeLive/CoplayDev-unity-mcp/tree/beta) | Behind by 749 commits; no ahead commits/files reported | Stale upstream snapshot, not a novel implementation on this ref | No feature to port from the default ref. Keep as a stale mirror in inventory. |
| [IgorOffline/CoplayDev-unity-mcp `beta`](https://github.com/IgorOffline/CoplayDev-unity-mcp/tree/beta) | Behind by 415 commits; no ahead commits/files reported | Stale upstream snapshot, not a novel implementation on this ref | No feature to port from the default ref. |
| [aj1126/-forked-CoplayDev-unity-mcp `beta`](https://github.com/aj1126/-forked-CoplayDev-unity-mcp/tree/beta) | Behind by 199 commits; no ahead commits/files reported | Stale upstream snapshot, not a novel implementation on this ref | No feature to port from the default ref. |
| [VRCLearn/unity-mcp `main`](https://github.com/VRCLearn/unity-mcp/tree/main) | Diverged: 11 commits ahead, 115 behind; 31 changed files reported | Meaningful downstream customization | Reusable maintenance/distribution ideas, not a wholesale core-code replacement. See below. |
| [JossueMac/unity-mcp `pinned-v10.0.0`](https://github.com/JossueMac/unity-mcp/tree/pinned-v10.0.0) | Cross-fork compare request returned 404 | Repo and branch were separately confirmed public; ancestry/compare endpoint could not establish delta | README indicates a release-pinned distribution workflow. No novel code is claimed without a source diff. |
| [pashara/Unity-MCP-CoplayDev-FrameDebugger `main`](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/tree/main) | Cross-fork compare request returned 404 | Repo is a specialized derivative/port, not a simple source mirror | High-value specialist candidate because its latest commit describes correctness fixes to Frame Debugger capture and tests. See below. |

A GitHub compare 404 is recorded as **comparison unavailable**, not as proof a repository is missing, private, or irrelevant. The latter two repositories were readable directly.

## 3. Useful fork: VRCLearn/unity-mcp

**Observed delta:** Upstream comparison reports 11 commits ahead and 115 behind, 31 changed files. Latest commit found during the snapshot: `4868739e149fca206d8665b652213a48d6596081` (“Correct the Launcher package ID in VPM documentation”).

Source inspected:
- [EditorLocalization.cs](https://github.com/VRCLearn/unity-mcp/blob/main/MCPForUnity/Editor/Helpers/EditorLocalization.cs), blob `94f9034d61db220af99dcd4af34fc8742778ca5a`.
- [VPM upstream-sync workflow](https://github.com/VRCLearn/unity-mcp/blob/main/.github/workflows/vpm-sync-upstream.yml), blob `997f171134148d6af71acdf587557aaf3e4be02b`.
- Compare-listed additions include VPM package build/validation/release workflows and localization files.

### What is useful

1. **Editor UI localization with English fallback.** The localization layer explicitly preserves tool names, schemas and protocol data, using English source strings as lookup keys/fallbacks. This clean separation is a suitable pattern if localization becomes a product requirement.
2. **Reproducible package distribution.** The fork has dedicated VPM build, validation, failure-reporting and release automation. This is useful if we distribute a VPM/VRChat-specific package or want package validation in CI.
3. **Upstream sync as an explicit workflow.** The workflow records a base SHA and upstream SHA and creates a named sync branch, rather than concealing sync behavior in a manual process.

### Caveats

- The fork is 115 commits behind the current upstream `beta` comparison base, so its custom code must not be copied without forward-porting and current-branch validation.
- Its workflow schedules hourly and runs on pushes. Do **not** copy the cadence blindly: excessive sync builds and CI triggers conflict with a low-noise maintenance strategy. Prefer manual/weekly sync unless a demonstrated need justifies higher frequency.
- Localization and VPM distribution are downstream product concerns. They are not evidence of a core MCP capability missing from our server.

**Disposition:** **ADAPT** packaging/sync workflow ideas for future distribution needs; **DEFER** UI localization unless product scope justifies it; do not cherry-pick its old core code.

## 4. Useful derivative: pashara/Unity-MCP-CoplayDev-FrameDebugger

The latest commit returned by GitHub search is `bfcc3207537bc90827d8fd2b2d8a4e6f2c9c1ef3`, titled “Port Frame Debugger to MCP for Unity (CoplayDev).” The commit description identifies it as a port of a specialized Frame Debugger package to CoplayDev MCP for Unity 9.3.2, under a new package ID `com.pashara.coplaydev.unity-mcp.frame-debugger`.

The commit message reports these concrete changes:
- Consolidation of 13 `framedebugger-*` operations into one `frame_debugger` action-based tool.
- Live-scene capture that replays/repaints the paused frame instead of advancing simulation per event.
- Event payload handling that checks `m_FrameEventIndex`, limits the frame to event N+1, and reads event payloads once.
- Explicit `Unavailable` values and logging when event payloads fail to refresh rather than attributing stale data to the wrong event.
- Re-read the event tree up to three times when it changes.
- Four new EditMode tests, with 34 total tests reported in that commit.
- Installer behavior revised to add a dependency without overwriting existing manifest entries.

### Why this matters

A correct frame-debugger result is not just a list of tool methods: render-event identity and state can change while an animated scene is being inspected. Explicitly refusing to associate a stale payload with the wrong event is a valuable correctness principle for any editor snapshot API.

### Important baseline check before adoption

Our tool registry already includes a `profiling` group described as including Profiler counters, memory snapshots and Frame Debugger. Therefore, the feature itself may overlap current coverage. The concrete follow-up is to inspect our current profiler/frame-debugger handlers and tests, then compare the derivative's capture lifecycle and stale-event safeguards. Do not add a second Frame Debugger subsystem until that mapping proves a gap.

The listed 34 tests and behavior are author-commit claims; they were not run independently in this research session.

**Disposition:** **INVESTIGATE / ADAPT** the event-index/capture-stability strategy and installer non-overwrite behavior. First map it against existing profiling handlers; likely an integration/correctness improvement rather than a new capability category.

## 5. Candidates that are not yet useful evidence

- **JossueMac/unity-mcp:** The `pinned-v10.0.0` branch and README confirm a release-pinned snapshot/distribution direction. That is useful deployment practice, but no branch-delta evidence currently supports claiming original code improvements.
- **iflow-mcp/coplaydev-unity-mcp, BaBy-Metal/unity-mcp, dawnli/unity-mcp:** Current README content looks very close to upstream branding/docs. `BaBy-Metal` and `dawnli` returned identical README blob SHA `eb077506c15dffd5694e95ca8ad03a2dd762104a` at snapshot, a strong indication the sampled README is copied exactly. Full repository identity and tree-level differences remain to be checked.
- **TomLeeLive, IgorOffline and aj1126:** Their compared default refs are simply behind upstream; an old fork is not a feature source by itself.

## 6. Next fork review actions

1. Finish page-by-page candidate inventory for direct forks; the available connector does not expose a dedicated forks-list tool, so search-based discovery must be labeled as partial unless a complete paginated source can be obtained.
2. Check candidates such as `pashara` at source-file level and inspect release/CI claims.
3. Inspect other forks with actual ahead commits or unique directories; ignore redundant README-only variations until a tree/diff shows substance.
4. Keep the upstream compare base current before any cherry-pick/port decision.


## 7. Useful but very stale derivative: czmomocha/unity-coplayDev-mcp

**GitHub compare:** current CoplayDev beta versus czmomocha main reports 8 commits ahead, 1,541 behind and 32 changed files. Latest observed fork commits were read through GitHub commit search on 2026-10-14.

Source reviewed:
- [TypeScript/TSX script management tool](https://github.com/czmomocha/unity-coplayDev-mcp/blob/main/MCPForUnity/UnityMcpServer~/src/tools/manage_ts_script.py), blob 7ff281ced10598998ea0c4e6c57a3e89c17a214b.
- [Custom tool authoring guide](https://github.com/czmomocha/unity-coplayDev-mcp/blob/main/MCP_Custom_Tool_Guide.md), blob 72680fd9310d00dea7df535c426c0f15758f8703.
- [Unity 2020 compatibility notes](https://github.com/czmomocha/unity-coplayDev-mcp/blob/main/Unity2020_%E5%85%BC%E5%AE%B9%E6%80%A7%E4%BF%AE%E5%A4%8D%E6%80%BB%E7%BB%93.md), blob f7fb9377bc9d3cf4546e156e837369466995e4f9.
- [Modified ManageGameObject.cs](https://github.com/czmomocha/unity-coplayDev-mcp/blob/main/MCPForUnity/Editor/Tools/ManageGameObject.cs), blob c3623b9b5e9b77dd84fe40f040ade3f6c3add755.

### Distinct contributions

- A TypeScript/TSX management tool for Puerts-based Unity projects. It defines allowed roots (Assets and DevCommon/TSProject), chooses likely tsconfig candidates, rejects path-traversal segments and validates paths against allowed roots. This fills a specialist scripting workflow for projects that keep gameplay TypeScript outside Unity Assets.
- Commit messages describe added TypeScript create/read/update/delete operations, fixes for prefab stage automation and consistent GameObject/prefab addressing, and a custom-tool build/validation guide.
- The Unity 2020 note records older API/language compatibility work such as PrefabStage namespace changes, avoiding ZipFile APIs not available in the target runtime, replacing newer C# range syntax, and older object-find APIs.

### What to borrow

**INVESTIGATE / ADAPT only for matching requirements:**
- If first-party support for Puerts/TypeScript workflows becomes a goal, use the typed TS operation surface and allowed-root/path validation as an example for an optional extension or custom-tool package.
- The custom-tool guide is a useful source of process documentation, although the target already has custom tool extensibility.
- Preserve a tested Unity version compatibility matrix. Legacy support should be a deliberate product requirement rather than adopting old workarounds without checking the current minimum version.

### Why not merge it

The branch is approximately 1,541 commits behind current upstream and is based on an old v7-era baseline. Its large ManageGameObject and EditorWindow edits predate extensive upstream evolution. A feature that is still missing should be reimplemented/forward-ported onto the current baseline with tests; do not cherry-pick the old branch wholesale.

Commit evidence:
- [TypeScript support commit](https://github.com/czmomocha/unity-coplayDev-mcp/commit/70df486b7dd787d0a840ed10b5ca629600d3599d)
- [TypeScript outside Assets commit](https://github.com/czmomocha/unity-coplayDev-mcp/commit/b49fe7131b8824b01970611752e7730b7d477ba6)
- [Unity 2020 compatibility adaptation](https://github.com/czmomocha/unity-coplayDev-mcp/commit/2e2e3e024fb2dcdcaae36638aa238860aab288da)


## 8. Additional fork-focused compare results

A second compare pass tested additional names discovered by the CoplayDev-focused searches. Results below refer to the reported default branch/ref at the time. The connector does not always return a common ancestor for downstream repositories; a 404 is not treated as evidence that the repository is absent or has no changes.

| Candidate | Compare against CoplayDev beta | What the ahead delta contains / conclusion |
|---|---|---|
| [OverTM/CoplayDev.unity-mcp](https://github.com/OverTM/CoplayDev.unity-mcp) main | Behind by 1,542 commits; 0 ahead commits/files | Sampled README is upstream-branded. Stale mirror on this ref, no code contribution identified. |
| [heshuimu/CoplayDev.unity-mcp](https://github.com/heshuimu/CoplayDev.unity-mcp) main | Behind by 1,492 commits; 0 ahead commits/files | Sampled README resembles the upstream README. Stale snapshot; no port recommendation. |
| [neurmostudio0409/CoplayDev-unity-mcp](https://github.com/neurmostudio0409/CoplayDev-unity-mcp) beta | Behind by 524 commits; 0 ahead commits/files | Upstream-branded snapshot; no unique source contribution identified in this compare. |
| [buianhduc-ai/unity-mcp-CoplayDev](https://github.com/buianhduc-ai/unity-mcp-CoplayDev) beta | Behind by 199 commits; 0 ahead commits/files | Snapshot of an older upstream branch; no delta from older ancestry beyond being behind. |
| [Bon-Games/Unity-MCP-CoplayDev](https://github.com/Bon-Games/Unity-MCP-CoplayDev) beta | Behind by 682 commits; 0 ahead commits/files | Older upstream-style snapshot; no unique contribution identified. |
| [adrnsyhsdq-debug/unity-mcp](https://github.com/adrnsyhsdq-debug/unity-mcp) beta | Ahead by 16, behind by 426; 16 changed files | All 16 ahead files are added personal fork notes and one-line setup logs; no implementation source change. The fork's own [FORK_NOTES.md](https://github.com/adrnsyhsdq-debug/unity-mcp/blob/beta/FORK_NOTES.md) states it is for personal experiments. |
| [czmomocha/unity-coplayDev-mcp](https://github.com/czmomocha/unity-coplayDev-mcp) main | Ahead by 8, behind by 1,541; 32 changed files | The non-upstream contribution is substantive: TypeScript/TSX management for Puerts projects, custom-tool guide, old Unity compatibility notes. See Section 7. |
| [VRCLearn/unity-mcp](https://github.com/VRCLearn/unity-mcp) main | Ahead by 11, behind by 115; 31 changed files | Dedicated localization and VPM distribution/validation/release automation. See Section 3. |

Compare failures/no-ancestry responses were also returned for these public candidates:
- [dawnli/unity-mcp](https://github.com/dawnli/unity-mcp)
- [ShibaPapaMikami/unity-mcp-template-coplay](https://github.com/ShibaPapaMikami/unity-mcp-template-coplay)
- [iflow-mcp/coplaydev-unity-mcp](https://github.com/iflow-mcp/coplaydev-unity-mcp)
- [KasuganoSorakog/UnityMCP](https://github.com/KasuganoSorakog/UnityMCP)
- [BaBy-Metal/unity-mcp](https://github.com/BaBy-Metal/unity-mcp)
- [haroon911/unity-mcp-vpm](https://github.com/haroon911/unity-mcp-vpm)
- [iflow-mcp/coplaydev-coplay-unity-plugin](https://github.com/iflow-mcp/coplaydev-coplay-unity-plugin)
- [Amarok121/unity-platform-integration-sample](https://github.com/Amarok121/unity-platform-integration-sample)
- [devoffeed/UN-mcp-server](https://github.com/devoffeed/UN-mcp-server)
- [Ridil86/claude-unity-bootstrap](https://github.com/Ridil86/claude-unity-bootstrap)
- [JossueMac/unity-mcp](https://github.com/JossueMac/unity-mcp)

For this group, the compare endpoint's 404 often reflects absent merge-base/source ancestry or unsupported cross-owner ref resolution. A positive source review would require fetching its changed code/tree or locating the actual common ancestor. No unique code contribution is claimed for these candidates merely from the error.

## 9. Fork-pass conclusion

In the CoplayDev-focused candidates whose history could be compared successfully, there are only two currently substantiated code/customization leads from this search set:
1. VRCLearn: localization and packaging/sync automation.
2. czmomocha: Puerts/TypeScript tooling and old-version compatibility.

The third repo with a nonzero ahead count, adrnsyhsdq-debug, adds only personal notes and setup-log files. Multiple other repos are stale mirrors. Pashara's Frame Debugger and SabriBoughanmi's Memory Profiler are valuable specialist derivatives, but they are not counted as direct forks of the current target absent ancestry evidence.

This is a meaningful source comparison of the available candidate set, **not exhaustive coverage of all 1,553 upstream forks**. The inaccessible complete fork-list endpoint remains the main coverage blocker.
