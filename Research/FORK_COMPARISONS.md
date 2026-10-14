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


## 10. The1Studio/unity-mcp — broad divergence with several high-value correctness patterns

Repository: [The1Studio/unity-mcp](https://github.com/The1Studio/unity-mcp), default branch `beta`.

**Compare result:** `The1Studio:beta` is **163 commits ahead and 529 behind** `CoplayDev:beta`, with 300 changed files in the compare response. It is therefore a broad, very stale downstream fork, not a candidate for wholesale merging. The most useful changes are specific fixes with a reproducible failure mode and focused tests.

### 10.1 Checked serialization writes: don't report success after narrowing to zero

Source evidence:
- [The1Studio ParamCoercion.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/ParamCoercion.cs), blob `5d160ca20f0c9cd949f8075138b55f4d6eef6d00`.
- [The1Studio ComponentOps.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/ComponentOps.cs), blob `d644297208790496e4735cef1ca41b9e36fc9d74`.
- [Original correctness fix commit](https://github.com/The1Studio/unity-mcp/commit/970ac29098b4b836cd6de596043f5f92aecf508f), August 11, 2026.

The fork adds `TryCoerceInt` / `TryCoerceLong`, returning success plus an actionable error instead of silently returning a default. Its integer `SerializedProperty` path uses these checked helpers before writing. The commit describes reproducible cases where a large LayerMask bit pattern was narrowed/defaulted to 0 and the MCP operation returned success; it also addresses boxed-struct write-back and verifies selected reflection writes by reading the field back.

The target's current `ComponentOps.SetSerializedPropertyRecursive` does an initial broad parse check and then calls `ParamCoercion.CoerceInt(value, 0)` / `CoerceLong(value, 0)`; the current target `ParamCoercion` returns the configured default when overflow or parse errors are caught. Its current component test paths cover native backing fields and UnityEvent behavior, but the sampled target tree has no standalone `TryCoerceInt` regression-test file. This is a **verified code-path difference**, not a claim that every integer assignment currently fails.

**Disposition: ADAPT (high priority).** Introduce checked coercion for write paths where success must mean the serialized value was applied, while retaining permissive/defaulting coercion for read-only/optional parameters where that behavior is intentional. Add boundary tests for `int.MinValue`, `int.MaxValue`, just-outside bounds, `long` bounds, very large JSON integers, invalid strings, and LayerMask bit patterns. For reflection paths with value-type intermediates, test write-back/readback instead of assuming `SetValue` mutated the original parent struct.

The upstream fork commit itself says it could not compile/run Unity locally; treat its regression tests as evidence of intended behavior and validate in the target CI/Editor before adopting.

### 10.2 Vector and struct SerializedProperty writes

[The1Studio commit #83](https://github.com/The1Studio/unity-mcp/commit/64279d6db40c46d2ffefebc1285b3fbbb5ddc59e) extends `ComponentOps.SetSerializedPropertyRecursive` for Vector2/3/4, Quaternion, Color, Rect, Bounds and AnimationCurve, using the shared vector parsing logic. It adds tests for object and array forms such as RectTransform `m_AnchorMin`, `m_AnchorMax`, `m_SizeDelta`, and `m_Pivot`.

The current target switch inspected in `ComponentOps.cs` handles Integer, Boolean, Float, String, Enum and ObjectReference before returning an unsupported-type error for other SerializedProperty leaves. The target already supports some component reflection/native-field writes, so this is specifically a **SerializedProperty fallback coverage gap**, not a missing component write system overall.

**Disposition: ADAPT.** Use one shared parser for values accepted through both reflection and SerializedProperty paths. Add tests for each supported value shape, unsupported values, round-trip persistence and unchanged values on failure. Review Unity-version support for each serialized type.

### 10.3 Input simulation as an optional Play Mode testing group

Unique files present in The1Studio's tree but absent from the target beta tree:
- [ManageInputSimulation.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageInputSimulation.cs), blob `1995f4b39cbbc674c3337f6cb6df2378d6201d7f`.
- [InputSimulationActions.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/InputSimulation/InputSimulationActions.cs), blob `237c2839f59968d572da01f2419b1c5945a9e0b1`.
- [InputTargetResolver.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/InputSimulation/InputTargetResolver.cs), blob `7c75076f7c7df448952151a6a83f81684c943348`.
- [InputSimulationFocusGuard.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/InputSimulation/InputSimulationFocusGuard.cs), blob `6b7c4a969ac27b6c0e58ef31465afffee8f22725`.
- [InputSimulationSequence.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/InputSimulation/InputSimulationSequence.cs), blob `162bcaad3befc32b7ec50649b5f640de940b8078`.
- Related runtime bridge and touch/discovery/focus helpers are in the fork tree, along with a `testing`-group tool registration.

The actions include UI/target discovery, coordinate/GameObject/UI-element/anchor targeting, click/double click, drag, scroll, key presses/combos, text entry, touch gestures, bounded sequences and status polling. This is a meaningful capability for automating game UI and Play Mode task validation.

A particularly useful correction in [commit #126](https://github.com/The1Studio/unity-mcp/commit/04424430a7b0116fe4f71f585f9f6130f37745b1) replaces pointer-position/scroll writes through `InputState.Change` with queued Input System state events. The commit explains that an Editor-update call could write to the Editor update buffer while the running game read the player buffer, causing interactions to land at (0,0). A companion focus guard warns when Unity or the Game View lacks focus rather than claiming that the game consumed the event.

**Disposition: INVESTIGATE as an optional testing capability.** Do not transplant the entire stale implementation. First decide which Input System versions must be supported, test old Input Manager vs new Input System behavior, inspect touch/sequence cancellation, verify focused/unfocused Game View behavior, and prove game-facing events are observed by a running project. The result should distinguish “event was queued” from “the application reacted.”

### 10.4 Run-in-background and Play Mode recovery

[RunInBackgroundPlayGuard.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Services/RunInBackgroundPlayGuard.cs), blob `bb44be1e1e5734e49c9475a4305a96420dfa3a3a`, is absent from the target beta tree. It temporarily enables `Application.runInBackground` on `manage_editor(action="play")` and restores the original value on the first `EnteredEditMode` transition, retaining the state via `SessionState` across normal play-mode domain reload.

The commit documents a material accepted gap: if the Editor crashes/quits while the guard remains active, `SessionState` is lost and no project-local recovery marker remains. Since `Application.runInBackground` writes through to PlayerSettings, abnormal exit can leave a tracked project setting changed.

**Disposition: INVESTIGATE, not unconditional adoption.** First reproduce the unfocused Game View symptom, then design durable cleanup and explicit opt-out/default behavior. A guard must preserve the prior project setting after normal stop, script-driven stop, domain reload and Editor shutdown; avoid hiding genuine application-lifecycle errors by permanently enabling background execution.

### 10.5 DOTS/ECS tools and the “unknown is not zero” result rule

The fork adds `ManageDots.cs` plus graphics/physics/subscene modules and Python/CLI wrappers. This exposes ECS worlds, entity/component queries, managed and unmanaged systems, archetypes, component writes and rendering/physics/subscene workflows. Those files are absent from the target beta tree; the package is a separate, specialized integration for projects that use Unity Entities/DOTS.

**Disposition: DEFER / OPTIONAL MODULE.** The feature area could be valuable for ECS-heavy game projects, but scope and compatibility costs are high. Keep it isolated and dependency-gated; add representative Unity.Entities version-matrix tests before porting.

More generally, [EntityCountSummary.cs](https://github.com/The1Studio/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/EntityCountSummary.cs), blob `2ad420b854752796880e4f0a65da14dd966bbe23d`, demonstrates a useful cross-domain contract: a failed component-type lookup must produce an unreadable/null count plus diagnostics, not a plausible `0`. Its tests explicitly distinguish unreadable from a genuine empty world. This principle applies to all MCP snapshots and validation outputs even if DOTS tools are not adopted.

### 10.6 Fork synchronization skill

[The1Studio fork-sync skill](https://github.com/The1Studio/unity-mcp/blob/beta/.claude/skills/fork-sync/SKILL.md), blob `0705ad4e5f0d9ab0fe7ffef0caae8408985fa8da`, formalizes clean-tree checks, upstream remote validation, fetched branch comparison, merge-base ahead/behind counts, overlapping changed-file lists, and explicit status/sync/diff/cherry-pick flows. It instructs contributors to stop rather than overwrite dirty work and keep studio-specific tools in separate files.

**Disposition: ADAPT as workflow documentation, not runtime code.** This is a strong practice for maintaining a long-lived fork and applies to the research branch's later feature-adoption phase.

### Summary judgment

The1Studio is one of the most useful upstream-derived candidates found so far despite being 529 commits behind. Its top transferable ideas are checked writes/readback verification, broader SerializedProperty type handling, optional deterministic Play Mode input simulation, and explicit unknown-vs-zero diagnostics. Server-sharing and editor shutdown ideas should be compared to the target's already-existing instance/auth/ownership controls before any port.
