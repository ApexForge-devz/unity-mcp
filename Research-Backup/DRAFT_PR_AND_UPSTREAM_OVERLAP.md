# Existing Draft PR and Upstream Proposal Overlap

**Snapshot:** 2026-10-14  
**Target repo:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp)  
**Purpose:** Reconcile this research with implementation work already present on the target repository and with closely related upstream PRs. This prevents duplicate recommendations and identifies where a draft needs additional protocol or runtime validation.

## 1. Existing target draft PR

[PR #1 — Unity 6 enhancements: dynamic tools, schema efficiency, modal recovery and screenshots](https://github.com/ApexForge-devz/unity-mcp/pull/1)

- State at inspection: **open**, draft, based on `beta`.
- Head SHA: `9480a79408422c95c17c08ed4efb0b66d24a1f24`.
- Scope: 89 files, 7,039 additions, 247 deletions.
- The PR body says regression tests were added, but that they were not executed locally. The connected GitHub Actions state on the inspected head provides additional CI evidence below.

### 1.1 CI status on head SHA

Observed workflow runs:
- [Python Tests — run 38190969623](https://github.com/ApexForge-devz/unity-mcp/actions/runs/38190969623): completed successfully.
- [Compile Check (no license) — run 38190969599](https://github.com/ApexForge-devz/unity-mcp/actions/runs/38190969599): completed successfully; C# compilation job passed for the configured Windows/macOS/Linux symbol matrix.
- [Docs Reference Drift Check — run 38190969592](https://github.com/ApexForge-devz/unity-mcp/actions/runs/38190969592): passed.
- [Docs Build & Deploy — run 38190969600](https://github.com/ApexForge-devz/unity-mcp/actions/runs/38190969600): passed.
- [E2E Bridge Smoke — run 38190969664](https://github.com/ApexForge-devz/unity-mcp/actions/runs/38190969664): workflow completed successfully but the `e2e-bridge` job was **skipped** because license secrets were unavailable.
- [Unity Tests — run 38190969593](https://github.com/ApexForge-devz/unity-mcp/actions/runs/38190969593): workflow completed successfully but the actual Unity test job was **skipped** because license secrets were unavailable.

Interpretation: Python tests, compile, docs-reference, and docs-build have passing workflow evidence. This is **not** a passing Unity EditMode or live bridge E2E run. The skipped jobs must not be counted as passed tests. The six run states were inspected through the GitHub Actions API on the PR head.

### 1.2 Features already in the draft

| Draft PR capability | Current source evidence | Relationship to this research | Remaining questions |
|---|---|---|---|
| Asset-search pagination/performance | `ManageAsset.SearchAssets` now counts matches and materializes metadata/previews only for the requested page; positive pagination validation; long start/count arithmetic; nonblank valid sanitized folder scope | Closely overlaps upstream PR #1459; do not build a separate optimization | Keep target-specific stricter path check and long counters; inspect its tests and recheck upstream PR state before implementation |
| Fixed Game View resolution | `GameViewSizeControl.cs`, readback of allocated render-target dimensions, size validation 10–8192 | New capability not present in the other reviewed open proposals | Uses reflected/undocumented Editor APIs; validate clamping, failure states, multi-monitor/scale behavior and supported Unity versions |
| Native Editor-window screenshots | C#, Python wrapper, tests, docs | The sampled files are **byte-for-byte identical** to the corresponding files in DelPariah's upstream-oriented PR #1417 | Preserve attribution/provenance in the PR, track the upstream PR and avoid a second implementation |
| Modal-dialog diagnosis/answer + main-thread liveness | `ModalDialogProbe.cs`, `EditorLivenessProbe.cs`, heartbeat, off-main-thread command routing, refresh scene guard, `answer_dialog` | Strong overlap with upstream PR #1350, but the target draft has material implementation differences | Highest risk: modal sampling and answer confirmation must be tested on an actual Windows Unity Editor; current PR CI skipped Unity and bridge E2E |
| Core-first tool catalog and schema compaction | Optional-group catalog, batch group activation, compact list descriptions, `get_tool_details`, schema benchmark | Directly overlaps progressive tool-discovery/context-efficiency recommendations | Current implementation uses FastMCP 3 per-session visibility; needs a modern MCP protocol strategy and task-success benchmark |
| Deterministic catalog / group updates | Registry order and activation changes are covered by new Python tests in the PR | Helpful groundwork for schema-cache correctness | Modern protocol tools/list behavior is not the same contract as legacy per-session list mutation |

### 1.3 Exact file overlap with native Editor-window screenshots PR

The following files in the target's `feature/unity6-enhancements` branch have exactly the same Git blob SHA as the corresponding files in [DelPariah/unity-mcp PR #1417](https://github.com/CoplayDev/unity-mcp/pull/1417):

| File | Shared blob SHA |
|---|---|
| `MCPForUnity/Editor/Tools/ManageEditorWindows.cs` | `5bb09de6cd5f957f36efbb1a45c1d24d48484906` |
| `MCPForUnity/Editor/Helpers/EditorWindowScreenshotUtility.cs` | `8c77897842079549002bf1ac35422e9d6fb56efe` |
| `Server/src/services/tools/manage_editor_windows.py` | `5939e7f8e441a284e1f377715fa18bd6dfe0a3a6` |
| `Server/tests/test_manage_editor_windows.py` | `b303a5911ecdf412fb0393b30cdf47327ae5f6aa` |
| `TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ManageEditorWindowsTests.cs` | `f433f6bbec55d51d71a728727e8e4a3a6da151d4` |
| `website/docs/guides/editor-window-screenshots.md` | `e58f7b19820e53989ec8b20f81df8df1f66b6a2f` |

This confirms exact content equality for these sampled files, not just feature similarity. It is reasonable to reuse work within an MIT-licensed codebase, but the PR should make source provenance clear and coordinate with upstream so future fixes are not lost. No claim about authorship intent is made.

## 2. Comparison with current upstream PRs

### PR #1459 — Asset search pagination/performance (open at snapshot)

[PR #1459](https://github.com/CoplayDev/unity-mcp/pull/1459) has the same core performance idea: count all matches but call asset metadata/preview retrieval only for requested-page items.

Target draft differences visible in source:
- checks nonblank `path` and validates the sanitized path as an actual folder;
- uses `long startIndex = ((long)pageNumber - 1L) * pageSize`;
- uses a `long totalFound` count and pre-sizes only the result list;
- target tests are similar but not blob-identical to the upstream PR tests.

The implementation overlap is strong enough to avoid duplicate design/implementation. Retain stricter target path semantics and overflow discipline while aligning with upstream review fixes.

### PR #1417 — Native Editor-window screenshots (open at snapshot)

[PR #1417](https://github.com/CoplayDev/unity-mcp/pull/1417) describes capture of Inspector, Console, custom tool windows and inactive docked tabs, with focus restoration, bounded image output and explicit file persistence. The target draft's sampled C#, Python, test and guide files are byte-for-byte identical to the PR branch. Track that PR rather than proposing this capability again.

### PR #1350 — Modal dialog diagnosis and answer (open at snapshot)

[PR #1350](https://github.com/CoplayDev/unity-mcp/pull/1350) identifies a main-thread modal stall as distinct from an ordinary busy Editor. It adds out-of-band liveness, modal title/body/button discovery, optional answer operation and a scene external-change guard.

Target draft source is materially different from KamilDev's PR branch:
- target `ModalDialogProbe.cs` is 17.5 KB versus 11.3 KB on the sampled upstream proposal;
- target bounds capture and answer scans, verifies that an answer click actually closed the original dialog, serializes competing answer attempts, and preserves a blocked signal when it cannot positively identify the modal owner;
- target liveness sampler uses a wake-up event and bounded join during shutdown/domain reload, rather than only setting a stop flag and sleeping;
- target dispatch has explicit cancellation/queue semantics beyond the sampled upstream implementation.

Those differences are potential correctness improvements, but complexity is also higher. With no actual Unity test or bridge E2E run on the PR head, do not assume the additional logic is safer merely because it is more extensive. Add deterministic tests for deadlines/cancellation/lifecycle and validate native dialogs in real Windows Unity. Retain fallback behavior for non-Windows platforms.

### Other upstream proposals that overlap planned work or expose high-value reliability gaps

| Upstream proposal | Status at snapshot | Implication for research |
|---|---|---|
| [#1465: keep Editor window focus refresh off the main thread](https://github.com/CoplayDev/unity-mcp/pull/1465) | Open | Independent of modal recovery: routine connection/path/executable checks can block UI responsiveness. Target draft does not fully replace this focus-refresh feature. |
| [#1462: list/apply/revert Prefab instance overrides](https://github.com/CoplayDev/unity-mcp/pull/1462) | Open | Useful workflow for diagnosing scene-vs-prefab state, with careful target scoping; review before designing a competing override interface. |
| [#1463: wait for Play Mode startup](https://github.com/CoplayDev/unity-mcp/pull/1463) | Open | Distinguishes entering Play Mode from scripts/scene initialization being ready. Complements, rather than replaces, general editor liveness. |
| [#1472: custom-tool required parameter order](https://github.com/CoplayDev/unity-mcp/pull/1472) | Open | Python 3.14 can fail registration when optional parameters precede required ones; add a regression to our supported-Python matrix and check whether the patch is merged. |
| [#1468: preserve custom stdio launchers during auto-configuration](https://github.com/CoplayDev/unity-mcp/pull/1468) | Open | Configuration writers should preserve user-managed commands/environment instead of clobbering settings. |
| [#1466: built AssetBundle explorer](https://github.com/CoplayDev/unity-mcp/pull/1466) | Open | Track before implementing bundle listing/info; distinct from asset dependency/reverse-reference analysis. |
| [#1461: append user instructions from a file](https://github.com/CoplayDev/unity-mcp/pull/1461) | Open | Project-specific workflows/rules may be better expressed as server instructions than a large generic prompt. Validate path/secret handling if adopted. |
| [#1460: test-run initialization/cleanup](https://github.com/CoplayDev/unity-mcp/pull/1460) | Open | Test orchestration must release throttling and clean up the Unity run/operation lock after timeout; a failed MCP job alone is not enough. |
| [#1286: session cleanup and deduplicated tool notifications](https://github.com/CoplayDev/unity-mcp/pull/1286) | Open | Relevant to listChanged notification fan-out and session lifecycle; recheck its branch/implementation before protocol migration. |
| [#1285: scope HTTP endpoint/server ownership per project](https://github.com/CoplayDev/unity-mcp/pull/1285) | Open | Supports project-isolation concerns; test EditorPrefs and endpoint ownership with multiple projects/worktrees. |
| [#1194: select Unity instance by launch directory](https://github.com/CoplayDev/unity-mcp/pull/1194) | Open | Useful current proposal for reducing wrong-instance selection; this is separate from explicit request identity safeguards. |

Open/closed status is only a point-in-time observation. Recheck current upstream status before porting.

## 3. Important protocol constraint for Core-first dynamic tool discovery

The target draft's `manage_tools` relies on FastMCP 3.x per-session visibility, changing which tools are advertised after a group activation/deactivation call. This works within the current legacy-oriented stack, but the [MCP 2026-07-28 Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) says the `tools/list` set must not vary per connection or as a side effect of other requests on that connection; it should be deterministic when the underlying set has not changed. The newer protocol is also designed to be stateless at the transport-session level.

Consequently:
- The schema compaction and `get_tool_details` pieces can still be useful in modern mode, as can stable typed tools.
- The group toggle/list mutation must be protocol-mode-aware or redesigned before claiming modern compatibility.
- Consider a stable modern catalog with invocation eligibility/policy checks, or separately configured stable tool surfaces for different profiles. Evaluate schema-cache behavior and client support in a contract test, not by assuming list-change notifications make any mutation legal.
- Do not switch entirely to one generic `call_any_tool` dispatcher just to reduce context; typed schemas and tool-level safety annotations remain important.
- Measure both schema token reduction and actual task success; compact descriptions can remove crucial preconditions or cautions even when the JSON schema is unchanged.

Full migration plan: [PROTOCOL_COMPATIBILITY.md](PROTOCOL_COMPATIBILITY.md).

## 4. Actions for the backlog

1. Mark asset search, native editor-window screenshots, and modal recovery as “in draft PR #1; review/validate, don't duplicate,” not as completed or merged.
2. Track PR #1459 and #1417, preserve the useful provenance and stricter target changes, and reconcile the overlapping implementations after upstream review.
3. Give modal recovery targeted Windows Editor tests and a live bridge E2E run before merge; compile success alone cannot prove that the non-main-thread answer path works.
4. Keep schema compaction a separate experiment, with before/after serialized schema bytes/tokens and task-quality tests.
5. Gate dynamic tool-list behavior against modern MCP requirements before FastMCP upgrade.
6. Recheck all upstream proposal statuses, since the state table is a snapshot.
