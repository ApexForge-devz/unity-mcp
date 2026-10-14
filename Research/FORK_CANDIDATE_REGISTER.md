# Fork Candidate Register — Search Snapshot

**Snapshot date:** 2026-10-14  
**Status:** Candidate register only; not a complete inventory of all direct forks.

## Scale and retrieval limitations

GitHub repository metadata for [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) returned 1,553 forks, 14,923 stars, default branch beta, updated_at 2026-10-14T04:58:04Z, and MIT license metadata.

The connected GitHub integration can read repository metadata, files, trees and commit comparisons, but rejects the public REST endpoint ending in /repos/CoplayDev/unity-mcp/forks. Fetching the GitHub forks page is disabled, and the working runtime has no external DNS/network. GitHub search is therefore being used to discover candidates, not to claim a complete parent-child listing.

Searches on 2026-10-14:
- CoplayDev unity-mcp fork:true: 23 results on page 1, none on page 2.
- CoplayDev fork:true: 25 results on page 1.
- unity-mcp fork:true: pages 1–4 sampled; each page had 100 results. Many are independent tools, project-specific demos, learning repositories or forks of other Unity MCP projects. They are not automatically direct forks of CoplayDev.
- unity editor mcp and unity-mcp-server: used for independent implementation discovery.

A 100-result search page cannot stand in for 1,553 direct forks. Search is relevance-ranked and capped. Continue with broad search, commit ancestry, branch comparisons and source evidence. Use a complete paginated forks collection when an authorized endpoint becomes available.

## A. Upstream-derived candidates to check

The following were returned by CoplayDev-focused search or have README/source evidence of being derived from an upstream Unity MCP. Parent relation may still need verification.

| Candidate | Evidence today | Classification today | Review priority |
|---|---|---|---|
| [VRCLearn/unity-mcp](https://github.com/VRCLearn/unity-mcp) | Compare: 11 commits ahead, 115 behind, 31 changed files; VPM workflows and localization source | Confirmed useful divergent downstream fork | High |
| [TomLeeLive/CoplayDev-unity-mcp](https://github.com/TomLeeLive/CoplayDev-unity-mcp) | Compared beta is 749 commits behind; no ahead commits/files reported | Stale snapshot on compared branch | Low |
| [IgorOffline/CoplayDev-unity-mcp](https://github.com/IgorOffline/CoplayDev-unity-mcp) | Compared beta is 415 commits behind; no ahead commits/files reported | Stale snapshot on compared branch | Low |
| [aj1126/-forked-CoplayDev-unity-mcp](https://github.com/aj1126/-forked-CoplayDev-unity-mcp) | Compared beta is 199 commits behind; no ahead commits/files reported | Stale snapshot on compared branch | Low |
| [JossueMac/unity-mcp](https://github.com/JossueMac/unity-mcp) | Public, default branch pinned-v10.0.0; README identifies project; compare returned 404 | Release pin candidate; unique code delta unproven | Medium |
| [iflow-mcp/coplaydev-unity-mcp](https://github.com/iflow-mcp/coplaydev-unity-mcp) | Public; README resembles upstream distribution | Mirror/packaging candidate; tree delta unknown | Medium-low |
| [BaBy-Metal/unity-mcp](https://github.com/BaBy-Metal/unity-mcp) | Public; README blob SHA equals dawnli sample | Copy/mirror candidate; repository-level identity unproven | Low-medium |
| [dawnli/unity-mcp](https://github.com/dawnli/unity-mcp) | Public; sampled README matches BaBy-Metal | Copy/mirror candidate; repository-level identity unproven | Low-medium |
| [OverTM/CoplayDev.unity-mcp](https://github.com/OverTM/CoplayDev.unity-mcp) | CoplayDev-focused search result | Candidate only | Medium-low |
| [heshuimu/CoplayDev.unity-mcp](https://github.com/heshuimu/CoplayDev.unity-mcp) | CoplayDev-focused search result | Candidate only | Medium-low |
| [neurmostudio0409/CoplayDev-unity-mcp](https://github.com/neurmostudio0409/CoplayDev-unity-mcp) | CoplayDev-focused search result | Candidate only | Medium-low |
| [buianhduc-ai/unity-mcp-CoplayDev](https://github.com/buianhduc-ai/unity-mcp-CoplayDev) | CoplayDev-focused search result | Candidate only | Medium-low |
| [Bon-Games/Unity-MCP-CoplayDev](https://github.com/Bon-Games/Unity-MCP-CoplayDev) | CoplayDev-focused search result | Candidate only | Medium-low |
| [adrnsyhsdq-debug/unity-mcp](https://github.com/adrnsyhsdq-debug/unity-mcp) | CoplayDev-focused search result | Candidate only | Medium-low |
| [KasuganoSorakog/UnityMCP](https://github.com/KasuganoSorakog/UnityMCP) | CoplayDev-focused and broad search result | Candidate only | Medium-low |
| [ShibaPapaMikami/unity-mcp-template-coplay](https://github.com/ShibaPapaMikami/unity-mcp-template-coplay) | CoplayDev-focused search result | Template/customization candidate | Medium |
| [haroon911/unity-mcp-vpm](https://github.com/haroon911/unity-mcp-vpm) | CoplayDev-focused search result | VPM packaging candidate | Medium |
| [ibelongedtoyou/unity-mcp-2019](https://github.com/ibelongedtoyou/unity-mcp-2019) | CoplayDev-focused search result | Old fork-history candidate | Low pending delta |
| [devoffeed/UN-mcp-server](https://github.com/devoffeed/UN-mcp-server) | CoplayDev-focused search result | Possible renamed distribution | Medium-low |
| [Amarok121/unity-platform-integration-sample](https://github.com/Amarok121/unity-platform-integration-sample) | CoplayDev-focused search result | Possible sample/template | Low pending ancestry |
| [Ridil86/claude-unity-bootstrap](https://github.com/Ridil86/claude-unity-bootstrap) | CoplayDev-focused search result | Possible bootstrap/wrapper | Low pending ancestry |
| [pashara/Unity-MCP-CoplayDev-FrameDebugger](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger) | Commit describes port from IvanMurzak Frame Debugger extension to CoplayDev MCP 9.3.2 | Specialized derivative, not a simple mirror | High |
| [sabriboughanmi/Unity-MCP-FrameDebugger](https://github.com/sabriboughanmi/Unity-MCP-FrameDebugger) | README says it is built on IvanMurzak/Unity-MCP | Specialist implementation/ancestor | High for rendering workflows |

### Useful findings so far

1. **VRCLearn:** editor UI localization with English fallback and separation from MCP protocol names/schemas; VPM packaging, validation, release and upstream-sync workflows. Adapt release hygiene only when relevant; do not copy stale core code or hourly sync cadence blindly.
2. **Pashara/SabriBoughanmi Frame Debugger:** deeper capture and render-event inspection than the target’s current three Frame Debugger actions; event-index verification and bounded event-tree rereading aim to prevent stale/misattributed render data.
3. **JossueMac:** release-pinned branch is worth assessing as reproducibility practice, but no code-level unique improvement is proven.
4. **Stale copies:** TomLeeLive, IgorOffline and aj1126 have no ahead commits on the refs compared. This is evidence against porting from those refs, not a declaration that every branch is useless.

## B. Broad-search implementation candidates (not automatically direct forks)

The broad unity-mcp fork search contains hundreds of adjacent projects. They should be reviewed for their own implementations but not counted as direct forks without ancestry evidence.

- [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp) — independent server with asset dependency analysis and multi-instance diagnostics.
- [pushkar-env/unity-mcp](https://github.com/pushkar-env/unity-mcp) — independent .NET bridge and READ/WRITE/DESTRUCTIVE permissions.
- [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) — independent Unity skills/MCP system and source base for an original Frame Debugger extension.
- [AnkleBreaker-Studio/unity-mcp-server](https://github.com/AnkleBreaker-Studio/unity-mcp-server) — progressive discovery, compact formatting and final-route guards.
- [kitwright/unity-mcp](https://github.com/kitwright/unity-mcp) — inline output caps, export profiles and Editor pump diagnostics.
- [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) — independent/earlier implementation candidate.
- [mitchchristow/unity-mcp](https://github.com/mitchchristow/unity-mcp) — independent editor integration candidate.
- [MothCyan/PerfAgent-Unity](https://github.com/MothCyan/PerfAgent-Unity) — performance workflow candidate.
- [Savidyaf/Unity-VFX-Graph-MCP-Tool](https://github.com/Savidyaf/Unity-VFX-Graph-MCP-Tool) — specialist VFX Graph candidate.
- [Pico-Developer/Unity-MCP-Extensions](https://github.com/Pico-Developer/Unity-MCP-Extensions) and [meta-quest/Unity-MCP-Extensions](https://github.com/meta-quest/Unity-MCP-Extensions) — specialist extension candidates.
- [sabriboughanmi/Unity-MCP-FrameDebugger](https://github.com/sabriboughanmi/Unity-MCP-FrameDebugger) — rendering-analysis extension.

The broad search also returns demos, toy apps, tutorials, workflow repositories and forks of other upstream projects. Names alone do not establish ancestry or usefulness.

## C. Path toward a complete inventory

1. Obtain the upstream fork collection through a tool/API that can read it and page until all 1,553 records are exhausted.
2. Store a normalized table of repository ID, owner/name, parent/source, default branch, pushed date, archive status, stars and observed SHA.
3. Group identical upstream snapshots while preserving inventory rows.
4. Compare every plausible fork with ahead-of-upstream commits to the nearest common ancestor; inspect substantive files and tests.
5. Review every candidate with meaningful ahead commits rather than sampling only popular repos.
6. Record a decision and source evidence in FORK_COMPARISONS.md and RESEARCH_LOG.md.
7. Keep this research labelled incomplete until the coverage criteria are met.


## D. Additional source-checked candidates discovered in later search passes

These were found by additional searches for MCPForUnity, frame debugger, memory profiler and related package terms.

| Repository | Evidence reviewed | Candidate judgment |
|---|---|---|
| [czmomocha/unity-coplayDev-mcp](https://github.com/czmomocha/unity-coplayDev-mcp) | Compare: 8 ahead, 1,541 behind, 32 files changed. New TypeScript/TSX management for Puerts; custom-tool guide; Unity 2020 compatibility notes. | Useful specialist contribution for Puerts projects; forward-port selectively because branch is extremely stale. See FORK_COMPARISONS.md. |
| [Kayungko/McpForUnityChinese](https://github.com/Kayungko/McpForUnityChinese) | README describes a source-level Chinese UI patch applied to resolved com.coplaydev.unity-mcp files, with backups/rollback and reapplication after package resolution. | Useful evidence that localization is wanted by some users, but patching package-resolved files is fragile. Prefer a maintained localization layer like VRCLearn's source-level UI localization if localization is product scope. |
| [JakeLee-057/MCPForUnity](https://github.com/JakeLee-057/MCPForUnity) | Package JSON reports com.coplaydev.unity-mcp 9.3.1; README is primarily an Editor plugin setup/troubleshooting guide. Compare endpoint returned 404. | Legacy docs/package snapshot; no unique code change demonstrated. |
| [yunuscan/MCPForUnity](https://github.com/yunuscan/MCPForUnity) | Small repository with a separate package ID com.yunuscan.unitymcp version 0.1.0 and a README describing an early bridge. | Alternative/experimental implementation, not evidence of a feature missing from target without deeper source comparison. |
| [sabriboughanmi/Unity-MCP-MemoryProfiler](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler) | Source-reviewed snapshot query/diff tools, bounded sessions and multiple EditMode suites. | Useful specialist workflow. Current target only captures/lists/compares snapshot file metadata; detailed query capability is a separate improvement. See SPECIALIST_WORKFLOWS.md. |
| [Ozymandros/Unity-MCP-Server](https://github.com/Ozymandros/Unity-MCP-Server) | README describes a .NET server v3.1.0 with CI, CodeQL, release automation, NuGet/Docker distribution, and MCP 2025-11-25 support. | Independent alternative for typed deployment, hardening/packaging and CI patterns; only README reviewed so far. Protocol badge is older than the 2026 spec; inspect actual handler before drawing compatibility conclusions. |
| [Pico-Developer/Unity-MCP-Extensions](https://github.com/Pico-Developer/Unity-MCP-Extensions) | README describes PICO XR 6.1.x blocks (VST/passthrough, controller, haptics, locomotion, spatial mesh and plane detection) and claims idempotent/non-destructive/ambiguity-aware operations. | Specialist SDK extension, not a generic MCP server replacement. Could be a pattern for optional XR capability modules if a project has a matching SDK. |
| [Savidyaf/Unity-VFX-Graph-MCP-Tool](https://github.com/Savidyaf/Unity-VFX-Graph-MCP-Tool) | README labels it work-in-progress, states basic functionality was tested, and says its original direct-YAML approach was abandoned in favor of training AI to build the YAML editor. | **Do not adopt as-is.** Its own stated direction and limited validation are insufficient evidence of a maintained production-ready tool. It is a reminder to validate Graph serialization/editor APIs and Unity-version compatibility. |
| [Ozymandros/unity-mcp-server-plugin](https://github.com/Ozymandros/unity-mcp-server-plugin) | README describes an independent Python/Semantic Kernel wrapper using stdio, process management, retries/health monitoring, input validation, log sanitization and dynamically discovered tools. | Potentially useful client/plugin lifecycle pattern; not a Unity editor feature gap. README-level. |
| [czmomocha/unity-coplayDev-mcp](https://github.com/czmomocha/unity-coplayDev-mcp) | Latest commit history and source for manage_ts_script.py; typed file extension and allowed-root/path traversal checks are present. | In Puerts/TypeScript use cases, an optional scripted workflow. Don't enable by default in core for Unity C#-only projects. |

### Search expansion coverage

Later searches included:
- com.coplaydev.unity-mcp fork:true
- MCPForUnity fork:true (plus page 2)
- Unity AI Game Developer fork:true
- CoplayDev Unity fork:true
- coplaydev-unity-mcp fork:true
- frame debugger unity-mcp
- memory profiler unity-mcp
- unity-mcp fork:true pages 5–10

The MCPForUnity-focused query surfaced 7 candidates. The frame-debugger query returned two direct specialist project names; the memory-profiler query returned one specialist project name. Broad pages 5–10 added 600 more result rows across the same relevance-ranked broad search, several of which are unrelated to direct upstream forkage.

Do not sum query totals as unique forks: searches overlap heavily and may return the same repo more than once. Continue to distinguish confirmed comparisons from README-only candidates.


## F. Further fork-only and facade review pass (2026-10-14)

Additional GitHub repository searches used:
- `unity-mcp fork:only`, pages 1–3 (100 results per page);
- `MCPForUnity fork:only`, pages 1–2 (no results for that exact query);
- `CoplayDev fork:only`, page 1 (15 results);
- `MCP for Unity fork:only`, page 1;
- `unity-mcp in:readme CoplayDev fork:true`, page 1.

These filters describe GitHub search results and do not guarantee that every row is a direct child of CoplayDev; parentage and source ancestry still need repository-level verification.

| Repository | Branch / evidence | Classification | What was useful |
|---|---|---|---|
| [The1Studio/unity-mcp](https://github.com/The1Studio/unity-mcp) | beta; compare against CoplayDev beta: 163 ahead, 529 behind, 300 changed files | Broad, very stale upstream-derived fork | Checked integer write coercion, component SerializedProperty vector support, Play Mode input simulation, RunInBackground guard, optional DOTS/ECS suite and fork-sync workflow. Detailed review in [FORK_COMPARISONS.md](FORK_COMPARISONS.md). |
| [Vangardo/unity-mcp-efficient](https://github.com/Vangardo/unity-mcp-efficient) | main; latest visible commit on 2026-08-25; 53-entry tree; README and source/test files inspected | Independent facade/companion, not a replacement Editor plugin | Six stable tools, operation-level catalog, result compaction, TTL/size-bounded retrieval handles, sequential request-id mutation suppression and bounded declarative workflow. See [ALTERNATIVES.md](ALTERNATIVES.md). Its published efficiency figures are against upstream v10.1.0 on 2026-08-24 and use character-count/4 token estimates plus a synthetic hierarchy. |
| [Scriptwonder/unity-mcp](https://github.com/Scriptwonder/unity-mcp) | beta compares identical to CoplayDev beta | Exact mirror on compared ref | No unique runtime code contribution on that ref. |
| [TigerHix/unity-mcp](https://github.com/TigerHix/unity-mcp) | beta is 674 commits behind; no ahead changes reported | Stale snapshot | No unique code contribution found on compared ref. |
| [chanqueenieolivia-ui/unity-mcp](https://github.com/chanqueenieolivia-ui/unity-mcp) | beta is 529 commits behind; no ahead changes reported | Stale snapshot | No unique code contribution found on compared ref. |
| [nathanandrewturner/unity-mcp](https://github.com/nathanandrewturner/unity-mcp) | master is 685 commits behind; no ahead changes reported | Stale snapshot | No unique code contribution found on compared ref. |
| [RealAgentMoulder/unity-mcp-fork](https://github.com/RealAgentMoulder/unity-mcp-fork) | beta is 345 commits behind; no ahead changes reported | Stale snapshot | No unique code contribution found on compared ref. |
| [paopaolaowang/unity-mcp-fork](https://github.com/paopaolaowang/unity-mcp-fork) | beta is 1,385 commits behind; no ahead changes reported | Very stale snapshot | No unique code contribution found on compared ref. |
| [ZhangJiaLong90524/unity-mcp-fork](https://github.com/ZhangJiaLong90524/unity-mcp-fork) | beta compares identical to CoplayDev beta | Exact mirror on compared ref | No unique runtime code contribution on that ref. |
| [RasputinKaiser/Fork-unity-mcp](https://github.com/RasputinKaiser/Fork-unity-mcp) | master is 1,969 commits behind; no ahead changes reported | Very stale snapshot | No unique code contribution found on compared ref. |

Other cross-owner compare calls returned 404 for a few candidates (including a-nom-ali, MrDonut12, TND23 and Vangardo). These are recorded as unavailable comparisons, not evidence of absent repositories or useless code. Vangardo was subsequently reviewed directly as a separate facade.

### New reusable patterns, with caveats

- The1Studio's checked integer/long coercion is a real data-correctness difference in the inspected serialized write path. See its commit [970ac290](https://github.com/The1Studio/unity-mcp/commit/970ac29098b4b836cd6de596043f5f92aecf508f).
- The1Studio's input simulator is a promising optional Play Mode test capability, but game-facing input must be proven to reach the player loop and the focused Game View; a queued device event alone is not end-to-end success.
- The1Studio's RunInBackground guard explicitly documents an unresolved crash/quit restore gap, so it is a candidate to investigate, not a no-risk patch.
- Vangardo's facade demonstrates a possible stable, typed operation catalog and recoverable compact output, but the full feature runs as an additional MCP server and its schema/action allowlists may drift. Benchmark against the current upstream package before choosing a facade versus internal integration.


## G. Small-delta fork checks: bgr and xOrfe

| Repository/ref | Compare result | Unique contribution | Decision |
|---|---|---|---|
| [bgr/unity-mcp:bgr-patches](https://github.com/bgr/unity-mcp/tree/bgr-patches) | 2 ahead, 1,311 behind | Preflight refresh uses compile=none to avoid a domain reload; connection section adds auto-reconnect when the local server is reachable but no session is active | Investigate narrowly. Compile-none needs test-before-adoption; auto-reconnect needs a manual-disconnect override and lifecycle tests. |
| [xOrfe/unity-mcp:beta](https://github.com/xOrfe/unity-mcp/tree/beta) | 2 ahead, 635 behind | Antigravity Windows/Linux config path changes | Already superseded: target beta now uses Antigravity 2.0 path ~/.gemini/config/mcp_config.json for Windows/macOS/Linux. |
