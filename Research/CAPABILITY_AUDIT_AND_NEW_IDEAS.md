# Second-Pass Capability Audit and Improvement Opportunities

**Audit date:** 2026-10-15  
**Target baseline:** `ApexForge-devz/unity-mcp` `beta`, observed HEAD `142ea0af4890337f0ff6658df61db23982313717`  
**Research branch:** `research/exhaustive-unity-mcp-2026-10`  
**Method:** source/tree inspection, registry/manifests, consolidated research and upstream open-PR status check. Not a runtime validation report.

## Executive assessment

Unity-MCP is already a broad MCP-to-Unity bridge. It can manage scripts, scenes, assets, GameObjects/components, materials/shaders/textures, cameras, graphics/rendering, physics, UI, animation, VFX, builds, tests and profiling. It also has read-only resources, script validation/edit helpers, async test/build patterns, client setup, project/Editor discovery, transports, logging, package operations and a substantial test suite.

The highest-value next step is **not to add a larger set of isolated tools**. Improve the guarantees that connect existing tools into reliable workflows:

1. Enforce a real trust policy for in-process C# execution and destructive operations.
2. Provide consistent bounded results, explicit operation status and safe recovery after timeouts.
3. Make script-edit → actual compile result → generator invocation → receipt/assertions → save/visual check a reliable pipeline.
4. Validate modern/legacy MCP compatibility and predictable tool discovery.
5. Add a first-class read-only Unity project health/preflight report.
6. Improve asset search efficiency and add a conservative asset dependency/reference graph.

This audit excludes new integrations with external or local generation systems, hosted providers, generation SDKs, credentials, remote generation jobs and provider-specific artifact pipelines.

## 1. What exists in the reviewed beta

The Unity package declares `com.coplaydev.unity-mcp` version `10.3.1-beta.7`, Unity `2021.3+`; the Python server manifest declares `mcpforunityserver` `10.3.0` and `fastmcp>=3.0.2,<4`. The different version strings may be intentional independent release tracks; expose and verify both in diagnostics instead of assuming they must be equal.

### 1.1 Tool groups and availability

The registry defines these groups; **Core is the only group enabled by default**. Additional tools are opt-in through `manage_tools`. Actual client-visible availability depends on group state, transport/client behavior and Editor package compatibility.

| Group | Existing capability surface |
|---|---|
| `core` (default) | Scene/asset/script operations; Editor state/lifecycle; GameObject/component work; cameras; materials, shaders and textures; packages; rendering/graphics and physics; builds; batch execution; console/refresh/preflight utilities; menu execution and in-process C# execution. |
| `docs` | Unity documentation lookup and reflection/type introspection. |
| `vfx` | VFX Graph assets and operations, particles, lines and trails. |
| `animation` | Animator reads/control, controller creation, layers/blend trees, AnimationClip creation/presets and 2D animation helpers. |
| `ui` | UI Toolkit/UXML/USS/UIDocument work. |
| `scripting_ext` | ScriptableObject creation, inspection and modification. |
| `testing` | Asynchronous EditMode/PlayMode test-run start and job polling. |
| `probuilder` | ProBuilder mesh authoring helpers; requires the package. |
| `profiling` | Profiler sessions/areas, frame timing/counters, object memory queries, Memory Profiler snapshot capture/list/file comparison, and Frame Debugger events. |
| `asset_gen` | Existing provider-oriented image/model/audio generation and import-related surface. This audit does not recommend expanding it. |

Sources: [tool registry](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/registry/tool_registry.py), [tool-group manager](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_tools.py), [server tool modules](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/).

### 1.2 Capabilities already present

**Scripts and compile workflow:** persistent script create/read/update/delete; script validation; precise text edits with optional SHA-256 preconditions; structured/anchor-based edits through `script_apply_edits`; Editor refresh/readiness preflight; menu-item execution; and in-memory `execute_code` with history/replay functions. In-memory execution is an escape hatch, not a durable generator source or sandbox.

**Scenes and runtime objects:** create/load/save/close scenes, multi-scene operations, active/loaded scene reads, hierarchy queries with paging/caps, Scene View framing and scene validation; GameObject/component create/find/modify/delete; prefab-stage/prefab operations; Editor Undo/Redo. Persistent source-driven generation can be built using these pieces, but compile success and generation completion are not yet one correlated workflow receipt.

**Assets and presentation:** asset import/create/modify/delete/duplicate/move/rename/search/info/folder and component metadata operations; material/shader/texture tools; camera controls/capture; UI Toolkit, sprite, animation and VFX tools. Graphics actions include render-pipeline/quality/settings, volumes/post-processing, skybox/environment, lighting bake/probes, renderer features and rendering stats. Physics includes settings/collision matrix/materials/joints, physics queries, rigidbody configuration, force application and validation.

**Build, testing and diagnostics:** player builds across targets; build settings/scenes/profiles, batch builds, status and cancellation; asynchronous EditMode/PlayMode test jobs; console reads, refresh/state checks, Unity docs/reflection; profiler sessions, timing/counters, snapshots and Frame Debugger event listing.

**Integration/operations:** stdio and local HTTP transports; remote-hosted HTTP mode with API-key validation and per-user isolation support; Editor-side client setup/configuration; bridge/server management; multiple Unity instance discovery/routing; custom-tool execution. Read-only resources expose Editor/project/instance state, selection, cameras, GameObjects, prefabs/prefab stage, windows, menu items, tags/layers, renderer features, volumes, rendering stats, tool groups and tests.

Sources: [server README](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/README.md), [Unity plugin README](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/README.md), [package manifest](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/package.json), [profiler tool](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_profiler.py), [graphics tool](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_graphics.py), [physics tool](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_physics.py).

### 1.3 Important distinction: code vs default-visible

A module's existence does not mean every MCP client sees it by default; most modules are group-tagged. The source includes an optional `asset_gen` group and provider adapters, including Meshy-related code, as well as a Blender bridge. Per project requirements, **do not add, extend, recommend or prioritize external-generation integrations**. This research-only change does not remove existing functionality; hiding/removing it would be a separate scope decision.

## 2. Research proposal disposition

| Item | Current beta status | Disposition |
|---|---|---|
| Server-enforced policy for `execute_code` and destructive operations | In-process C# execution exists; caller-controlled text checks are not a security boundary | **P0: implement.** Enforce policy at server/final dispatch, including aliases, proxy and batch paths; make remote/unattended defaults restrictive. |
| Truthful batch contract | Python exposes parallel-related arguments; Unity-side calls execute sequentially on the Editor main thread | **P0: correct contract.** Preserve order and partial results; state non-transactional, sequential semantics. |
| Universal result-size/context budget | Some tools paginate, cap or downscale, but no universal budget is established | **P0/P1: centralize.** Bound serialized bytes; preserve errors, truncation metadata and a continuation/retrieval handle; apply the same contract to async status results. |
| Modern/legacy MCP compatibility | FastMCP is capped below 4; group selection relies on FastMCP 3 session-visibility operations | **P1: compatibility spike.** Do not upgrade blindly; test the protocol/client matrix and redesign modern tool discovery if needed. |
| Warning-preserving schema compaction | Target draft PR #1 proposes Core-first visibility/schema compaction; blind prefix truncation can drop tail warnings/preconditions | **P1: harden and benchmark** before relying on it. Curate summaries, mark truncation and preserve full definitions. |
| Project/Editor identity and shared-server ownership | Auth/routing exist; archived source review found lifecycle ambiguity in persistent server ownership metadata | **P1: verify/repair.** Test two-Editor/project cases and launcher/borrower exit orders. Unknown status must not trigger destructive stop. |
| JSON-encoded scalar argument normalization | Research identifies action paths where JSON-encoded arguments can fail normalization | **P1: targeted fix/tests.** Normalize at a defined boundary, then validate against schema. |
| Asset-search pagination | Paging exists; research identified eager hydration and search-scope validation concerns | **P1: correct/benchmark.** Materialize previews/metadata only for the requested page while preserving totals/filter semantics. |
| Compile diagnostics and generator receipts | Readiness/refresh, script validation, menu execution and async test jobs exist; compile success and generation completion aren't a correlated contract | **P1: implement.** CompilationPipeline diagnostics + persisted cycle summary + run-ID receipt. |
| Asset dependency/reverse-reference graph | Asset search/info exists; inspected asset data path has a dependency-metadata TODO | **P2: implement conservatively.** GUID-based references, cache invalidation tests and “potentially unused” suggestions; never auto-delete. |
| Frame Debugger consistency | Frame Debugger/event paging exists; a specialist derivative suggests capture/event stability fixes | **P2: targeted correctness work** after source diff and reproducer. |
| Object-level Memory Profiler querying | Snapshot capture/list/file comparison exist; deeper object queries remain in the UI | **P2: optional/package-gated** after API/version validation. |
| Screenshot resource bounds | Multiple capture modes exist | **P2: harden existing tools.** Bound pixels, supersize, orbit count and inline bytes; report capture source/fallback truthfully. |
| Play Mode startup, test cleanup, Editor refresh, server instructions, Console filtering, Game View sizing, AssetBundle explorer, custom-tool parameter-order fixes | Several have upstream open PRs as of the current check | **Do not duplicate blindly.** Review the current patch/CI/merge state first. |
| Direct GameObject/property/prefab edits as the agent's main scene-authoring loop | APIs exist and remain useful for other workflows | **Defer as primary workflow.** Generated scenes should change through persistent generator source; use object queries for targeted diagnosis. |

The detailed source reviews are archived under [Research-Backup](../Research-Backup/README.md); the main synthesis is in [Priority Roadmap](PRIORITY_ROADMAP.md), [Script-First Workflow](SCRIPT_FIRST_WORKFLOW.md), [Protocol and Context](PROTOCOL_AND_CONTEXT.md) and [Reliability and Safety](RELIABILITY_AND_SAFETY.md).

## 3. Upstream overlap to check before implementation

At the GitHub check on 2026-10-15, the following proposals were listed as open; recheck before patching:
- [#1459 — asset search loads only the requested page and validates folders](https://github.com/CoplayDev/unity-mcp/pull/1459)
- [#1469 — JSON-encoded Console/menu arguments](https://github.com/CoplayDev/unity-mcp/pull/1469)
- [#1463 — wait for Play Mode startup before running code](https://github.com/CoplayDev/unity-mcp/pull/1463)
- [#1465 — move Editor-window focus refresh off the main thread](https://github.com/CoplayDev/unity-mcp/pull/1465)
- [#1460 — native test cleanup after timeouts](https://github.com/CoplayDev/unity-mcp/pull/1460)
- [#1461 — custom server instructions from a file](https://github.com/CoplayDev/unity-mcp/pull/1461)
- [#1466 — read-only AssetBundle inventory](https://github.com/CoplayDev/unity-mcp/pull/1466)
- [#1470 — no-Unity-session recovery guidance](https://github.com/CoplayDev/unity-mcp/pull/1470)
- [#1472 — required custom-tool parameters after optional parameters](https://github.com/CoplayDev/unity-mcp/pull/1472)

The target's own [draft PR #1](https://github.com/ApexForge-devz/unity-mcp/pull/1) is on `feature/unity6-enhancements`, not baseline `beta`. It proposes asset-search changes, fixed Game View sizing/readback, native Editor-window screenshots, modal handling, and Core-first discovery/schema compaction. The PR description did not claim local Unity test execution. Treat this as unmerged draft code, not as available beta functionality or proven behavior.

## 4. Additional ideas from this second-pass analysis

### A. Project Doctor: one read-only, evidence-backed health report
**Value: very high.** Add a single `project_doctor`/preflight operation composing existing read-only checks:
- actual Editor/package/server versions and compatibility/capability matrix;
- compile/reload state and compiler errors;
- missing scripts and unresolved references in a specified scene/scope;
- invalid build-scene paths, missing packages and unsupported package-dependent tools;
- active render pipeline and requirements for requested diagnostics;
- dirty/unsaved scenes, active project/Editor identity and test/build readiness.

Return error/warning/info/unknown separately, with evidence locations and next steps. Do not silently auto-repair or scan every asset on each call. Build from existing resources and validators first.

### B. Standard task/run contract across asynchronous operations
**Value: very high.** Test/build jobs already establish a pattern, while compilation, generators, light baking and captures can have different completion/recovery semantics. Standardize long/mutating runs around a `run_id`, target project/Editor, states (accepted/queued/running/waiting-for-reload/completed/failed/cancelled/outcome-unknown), timestamps, progress, bounded diagnostics and final structured result. Persist receipts for work crossing domain reload. Cancellation must mean only what the underlying operation can guarantee. Do not turn short reads into jobs unnecessarily.

### C. Change-set preview and mutation journal
**Value: high, moderate cost.** For high-impact work, optionally preview intended scene/assets/paths/settings/destructive effects. Afterward record source hashes, files/assets/scenes touched, validation evidence, warnings and save/build result. Categorize actions as undoable, compensatable or irreversible; Unity Undo cannot reverse every file/import/build/package side effect, so never promise a transaction where none exists.

### D. Capability manifest and version-aware dispatch
**Value: high, moderate cost.** Return a machine-readable manifest of server/package/Editor versions, selected instance, protocol/transport, enabled groups, installed optional packages, render pipeline and support state per feature. Use `supported`, `unsupported`, or `unknown` with reasons. This can prevent avoidable calls to Memory Profiler, ProBuilder, URP-only or newer-Unity features. Generate it from actual runtime registration/capability probes rather than manually maintained claims.

### E. Golden workflow evaluation suite
**Value: high, low-to-moderate cost.** Alongside unit/EditMode tests, curate end-to-end tasks: edit script with preconditions → successful compile → run deterministic generator once → validate invariants → capture a truthful bounded screenshot → report a build/test failure with actionable evidence. Track success rate, invalid calls, round-trips, bytes/tokens, retry/duplicate mutation rate, elapsed time and recovery success. Use this to decide between schema compaction, operation catalogs and new tools empirically.

### F. Semantic project navigation and impact analysis
**Value: medium-high, moderate-to-high cost.** Go beyond raw text search/reflection to answer “which project scripts reference this API/field?” and “which scenes/assets depend on this asset?” Start with bounded text/symbol search plus asmdef/namespace relationships; add Roslyn semantic references/rename only if Unity compilation context can be represented reliably. Results should include file/line and asset GUIDs; invalidate incrementally on edits/import/reload. Do not build a heavyweight index before benchmarking.

### G. Semantic repeatability checks for generators
**Value: medium-high.** Provide a standard way to run a generator twice and compare generated-root identity, object/component counts, asset GUIDs, key serialized fields and missing references. Byte-identical scene YAML should be optional because stable serialized IDs and ordering are a separate concern. This tests idempotence and detects duplicate accumulation.

### H. Visual regression over Unity-produced captures—not image generation
**Value: medium, project-dependent.** Add opt-in comparison of screenshots produced by Unity itself, recording scene/camera, resolution, quality/pipeline settings, capture source, time/seed and difference metrics. Flag sensitivity to temporal effects, random particles, exposure and dynamic resolution. Reuse current capture infrastructure; do not add external image-generation/provider integrations.

### I. Better compile and source diagnostics
**Value: medium-high.** Correlate compiler output with the edited source hash and run ID; return concise diagnostics by default and details on demand. Unity's `CompilationPipeline.assemblyCompilationFinished` provides an assembly path and `CompilerMessage[]` on the main thread. Treat “Editor ready” or Console scraping alone as insufficient proof of successful compilation; validate API behavior on supported Unity versions. [Unity 6.0 API reference](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/compilation/compilationpipeline/assemblycompilationfinished).

## 5. Suggested implementation order

| Order | Work | Rationale |
|---|---|---|
| 1 | Recheck upstream PRs and draft PR #1 | Avoid duplicate work; inspect test/CI evidence. |
| 2 | Add focused baseline tests for batch contracts, argument normalization, asset pagination and compile/readiness | Establish current behavior before cross-cutting changes. |
| 3 | Implement final-route privilege policy and central response budget | Highest safety/context return; affects future work. |
| 4 | Add compilation-cycle diagnostics and durable run receipts | Makes script-first generation recoverable and debuggable. |
| 5 | Add Project Doctor and capability manifest | High value with a small visible surface; improve version-aware diagnosis. |
| 6 | Fix/test shared-server ownership, queue/liveness and exact-modal answering | High impact failure cases across multiple Editors. |
| 7 | Add asset dependency graph and semantic repeatability checks | Valuable maintenance capabilities requiring careful fixtures/cache semantics. |
| 8 | Benchmark schema approaches; then consider visual regression or semantic indexing | These add complexity and should earn adoption through measured improvements. |
| 9 | Add object-level memory snapshot querying or other specialist modules only when demanded | Useful for specific workflows, not prerequisites for a reliable core bridge. |

## 6. Acceptance principles

- Presence of a tool is not evidence it is correct or useful for a target workflow.
- A test file or PR claim is not evidence tests passed in the current checkout.
- Readiness/heartbeat is not compile success or generator completion.
- Transport frame size is not an LLM response budget.
- A regex blocklist is not a sandbox for in-process C#.
- A timeout is not proof a mutation stopped; never blindly replay it.
- A screenshot request is not proof the final Game View image is ready or faithful.
- A “potentially unused” asset is not safe to delete.
- Fork popularity or feature count is not evidence for adoption.
- Improve an existing feature only when source/tests establish a gap; new features need a clear acceptance gate.

## Sources
- [Tool registry](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/registry/tool_registry.py)
- [Server tool modules](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/)
- [Server transport/configuration documentation](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/README.md)
- [Unity plugin setup/Editor UI](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/README.md)
- [Async test workflow](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/run_tests.py)
- [Asset operations](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_asset.py)
- [Script authoring/validation](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_script.py)
- [Profiler surface](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_profiler.py)
- [Graphics surface](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_graphics.py)
- [Physics surface](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_physics.py)
- [MCP 2026-07-28 tools specification](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx)

This is the integrated capability and product-direction assessment, not a claim that proposed changes have been implemented.
