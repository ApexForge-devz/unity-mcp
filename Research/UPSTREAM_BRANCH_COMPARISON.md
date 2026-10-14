# Upstream Branch, Issue and Pull Request Status

**Snapshot:** 2026-10-14  
**Repository:** [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)  
**Research branch base:** ApexForge-devz/unity-mcp beta at 142ea0af4890337f0ff6658df61db23982313717.

## 1. Branch relationship — direction clarified

- Target/default branch is beta, with HEAD 142ea0af4890337f0ff6658df61db23982313717 (merge PR #1452; Unity package 10.3.1-beta.7, committed 2026-10-08).
- Upstream main HEAD at this snapshot is aa5fc638d623f56178d50329d9ad8c541a57fe66 (merge PR #1425; v10.3.0 release, committed 2026-10-04).
- GitHub compare with base=beta, head=main reports status behind, 0 ahead and 115 behind. In this direction that means **upstream main is 115 commits behind beta**. The research branch correctly starts from beta; this is not an instruction to rebase it onto main.
- The target beta and upstream beta had identical HEAD at this snapshot. The target is a source-identical upstream mirror on its public beta branch.

Useful refs:
- [Target beta HEAD](https://github.com/ApexForge-devz/unity-mcp/commit/142ea0af4890337f0ff6658df61db23982313717)
- [Upstream main HEAD](https://github.com/CoplayDev/unity-mcp/commit/aa5fc638d623f56178d50329d9ad8c541a57fe66)
- [Upstream release PR #1425](https://github.com/CoplayDev/unity-mcp/pull/1425)

## 2. Cross-branch source comparison for the current backlog

The following paths were fetched from upstream main. For the critical files below, the blob SHAs match the reviewed beta versions unless noted.

| File | main blob SHA | Current assessment |
|---|---|---|
| Server/pyproject.toml | 83800f55c65984102b61a035613a2ca40459eb91 | Same FastMCP upper bound <4 as beta; no sign of the protocol migration in this dependency file |
| Server/src/services/tools/batch_execute.py | 2c717d9c45cc25b2be878bda7eb76db580becdfb | Same batch schema, including parallel/max_parallelism inputs |
| Server/src/services/tools/manage_tools.py | b8875e4d5fd3673c26063c26681b31fb43703fd3 | Same per-session tool visibility design |
| MCPForUnity/Editor/Tools/BatchExecute.cs | aca4a1994a4dd2857e132b9f068146bc4d8960ff | Same sequential Unity main-thread behavior |
| MCPForUnity/Editor/Services/AssetGen/Import/SafeZipExtractor.cs | b5ef2b3c00e412c124d0bf76e6524d1b2e52d2dd | Same inspected path containment/extension filtering; no archive expansion quotas evident |
| MCPForUnity/Editor/Tools/ManageAsset.cs | 69aaf53cc93a0b638d5a10139f4751dc42c13787 | Same GetAssetData dependencies metadata TODO |
| MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs | 0876fdb121755c4bd4d71b1f6219a00f4bf199b8 | Same three Frame Debugger actions in the reviewed handler |
| MCPForUnity/Editor/Tools/ExecuteCode.cs | main: bef08d49a7e661181780e04694be466fd2a2a5e7; beta: 10325861e52def46cc72fe7cb2d5d4e28ffbb828 | Three assembly-load compatibility call sites differ. This does not resolve the caller-controlled safety_checks or establish a general sandbox |

This mapping means the high-priority contract/security/resource-limit issues described in the backlog are not obviously fixed simply by comparing the current main tree to beta. The reviewed high-impact source files remain materially identical; ExecuteCode differs only in the selected assembly load/path compatibility calls in the inspected line comparison.

## 3. Upstream open proposals and related issue history

### PR #1466 — AssetBundle explorer (open at snapshot)

[feat(asset): explore built AssetBundle contents](https://github.com/CoplayDev/unity-mcp/pull/1466), created 2026-10-11 and last updated 2026-10-13, was open at the time of inspection.

The proposal adds read-only manage_asset actions:
- list_asset_bundles, to enumerate likely built AssetBundle files/directories without loading bundles;
- get_asset_bundle_info, to inspect name, size, asset/scene counts and contained names, and unload only the bundle it opened.

The PR body says paths are project-restricted, symbolic links are not followed, results are paginated (default 25, max 200), common non-file entries are excluded, and optional AssetDatabase updates are avoided. The PR author marks Python tests and Unity 6000.3.20f1 tests as tested. These claims were not independently rerun here.

**Backlog implication:** track this proposal before designing another AssetBundle explorer. Do not conflate it with the separate AssetDatabase dependency/reference graph idea: listing a built bundle and finding reverse dependencies of project assets are different operations. Re-check merge state before implementing either.

### Issue #1438 — execute_code file-delete safety granularity (open)

[Path allowlist for execute_code destructive file ops](https://github.com/CoplayDev/unity-mcp/issues/1438), opened 2026-10-05, says the current safety check blocks System.IO.File.Delete outright and offers either a project-path allowlist for selected generated folders or clearer guidance around the all-or-nothing safety_checks=false switch.

**Backlog implication:** this is adjacent to the broader trust-boundary concern in P0.2 but not a complete security-policy solution. A path allowlist may improve routine build cleanup while retaining restrictions on Assets/out-of-project paths; it does not sandbox arbitrary in-process C# or fix remote/unattended authorization. Evaluate the issue/proposed UX before designing overlapping settings. The issue is a user request, not proof a patch is implemented.

### Issue #709 — disabled tools visible in clients (closed historical issue)

[Disabled Tools Reappear and Still Show Up in Client Lists](https://github.com/CoplayDev/unity-mcp/issues/709) was opened 2026-02-10 and closed 2026-02-12 against v9.4.0. It reports disabled tools returning after restart and still appearing in client tool lists.

**Backlog implication:** this is historical, and it must not be reported as an unresolved bug in the current v10.3.1-beta.7 branch without reproduction. It demonstrates that tool visibility/persistence has been a real compatibility area before; the 2026 protocol change presents a new, standards-level constraint that requires its own tests.

### PR #1470 — no-Unity-session recovery message (open)

[Recovery guidance when no Unity session is available](https://github.com/CoplayDev/unity-mcp/pull/1470) was open at snapshot and is based on the 142ea0af beta commit. It improves the error message/troubleshooting guidance without changing the underlying routing lifecycle. Its PR body states 1,575 Python tests passed and 18 were skipped, but this is author-reported evidence and was not independently run here.

**Backlog implication:** editor-liveness work should complement actionable state/error categories, not replace existing no-session responses and recovery guidance. Re-check whether this PR is merged before editing the same response path.

## 4. Research rule for upstream proposals

Before implementing any feature in this research branch:
1. Check whether it is already in target beta.
2. Check upstream open/merged PRs and recent commits after the current beta base.
3. If a PR exists, assess its behavior, test coverage and risks rather than duplicating it blindly.
4. Keep upstream PR test claims labeled as claims unless CI logs or local test execution independently verify them.
