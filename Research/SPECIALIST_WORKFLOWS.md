# Specialist Workflow Source Review

**Snapshot:** 2026-10-14  
**Purpose:** Record deeper source-level review of specialist extensions and the exact gap between their behavior and the current target. This is not an endorsement to copy entire packages.

## 1. Frame Debugger: current target versus specialist extension

### Current target source

Target source:
- [ManageProfiler.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/ManageProfiler.cs), blob 606b53abd68ec5bb5a94fd59d70510f691393023.
- [FrameDebuggerOps.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs), blob 0876fdb121755c4bd4d71b1f6219a00f4bf199b8.
- [Profiler action test](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/tests/test_manage_profiler.py), blob fcb652d62926a7a31fd6daa84ba9d372bb1cb861.

The target already has the manage_profiler action tool and tests action registration/forwarding. Its Frame Debugger surface supports:
- frame_debugger_enable
- frame_debugger_disable
- frame_debugger_get_events, with page_size/cursor pagination

The C# handler uses reflection for Unity's internal FrameDebuggerUtility because namespace/type signatures vary by Unity version. It reads selected event descriptor details (name/type/GameObject ID) and attempts to extract shader/pass/render target dimensions/vertex/index/instance counts/mesh name. Exceptions for individual names/details are caught and skipped; there is no explicit per-field status in this output. Enabling the debugger opens its Editor menu and requires Play Mode to be paused if currently playing.

The memory-profiler surface in the target is also intentionally shallow:
- memory_take_snapshot
- memory_list_snapshots
- memory_compare_snapshots

MemorySnapshotOps compares file-level metadata and explicitly advises that object-level comparison must be done in the Memory Profiler window. It can take a snapshot by reflection across some Unity API variants and waits for a callback with a 30-second timeout. It does not open the snapshot file into a queryable object graph in the inspected implementation.

### Frame Debugger specialist extension

Projects:
- [Pashara's CoplayDev port](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger)
- [SabriBoughanmi original extension](https://github.com/sabriboughanmi/Unity-MCP-FrameDebugger), based on IvanMurzak/Unity-MCP.

Source reviewed:
- [FrameDebuggerMcpTool.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameDebuggerMcpTool.cs), blob f9370986c0d0d5924d7ec03b7574f32ce8ca8f52.
- [Tool_FrameDebugger.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/Tool_FrameDebugger.cs), blob 3755ed565d0b65659f47c7e48e7ea2171f07aa47.
- [FrameCaptureOperation.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/FrameCaptureOperation.cs), blob e657594df03feb89069d69752467bb6dac4aaa63.
- [UnityFrameDebuggerReflectionBackend.cs](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Editor/Scripts/UnityFrameDebuggerReflectionBackend.cs), blob 7cbc66175d1b1f05008647bbb593629a6af3ce1d.
- [EditMode tests](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.frame-debugger/Tests/Editor/FrameDebuggerTests.cs), blob e036bb782f8d9325bc6c24dc755ce5bf0da5a0b3.
- [MIT license](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger/blob/main/LICENSE), copyright 2026 Sabri Boughanmi.

Its tool surface includes capabilities/contract diagnostics, async begin_capture and operation status, cancellation, capture sessions, paged event search, event details, summary metrics, diagnostics, export/diff profiles, and async render-target previews. The code has backend abstractions, query limits and explicit capture lifecycle states, including saving/restoring Editor state, replay, event-tree verification and cleanup.

The commit description says the previous approach stepped the Editor per event, changing the live animated frame. The port instead repaints/replays the paused frame, checks the payload's own m_FrameEventIndex before filing details, and retries a changing event tree up to three times. If event payloads never refresh, it reports Unavailable rather than silently attributing a different event's data. It reports 34 EditMode tests, but the workflow was described as manual/not run on an automated runner in that same commit; these tests were **not** run during this research.

**Assessment:** This is a real specialist capability lead. Prefer a focused integration or selective reimplementation using target conventions over wholesale transplantation. The port targets CoplayDev MCP 9.3.2 in its described commit; compatibility with today's 10.3.1-beta.7 package and current internal APIs must be validated. Both extension LICENSE files are MIT, but retain required copyright notices and review provenance when copying.

**Recommended decision:** INVESTIGATE / ADAPT. First add target characterization tests for animated/live-scene consistency, payload/event identity, API reflection mismatch, and restoration after cancellation/failure. Then choose an MVP: stable frame capture session + paginated event query + summary/detail; add profile diff and render-target previews only when those workflows justify complexity.

## 2. Memory Profiler: snapshot exploration missing in target

Project: [SabriBoughanmi Unity-MCP-MemoryProfiler](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler), a specialist extension built on the IvanMurzak MCP plugin ecosystem.

Source reviewed:
- [README](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/README.md), blob 115db43cc0f2f942968d6ea2fcac625362323aab.
- [Tool_MemoryProfiler.cs](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Editor/Scripts/Tool_MemoryProfiler.cs), blob dd1c6a0ef6416f93c198ef1094a9005049b73e58.
- [SnapshotQueryEngine.cs](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotQueryEngine.cs), blob ea94501ce5d560d9e2144b2d53a930b8aaea23b9.
- [SnapshotDiffEngine.cs](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotDiffEngine.cs), blob b7ea6eda36fbbcc9bf1331c5a641b7a9ef20dab8.
- Its tree contains bridge/query/diff/object/path-to-root/reference engines and multiple EditMode suites (BridgeTests, DiffEngineTests, QueryEngineTests, SnapshotLifecycleTests and stage-specific tests).
- [MIT license](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/LICENSE), copyright 2025 Ivan Murzak.

The source has asynchronous/incremental snapshot loading with operation status/cancellation, a cap of two loaded sessions, snapshot metadata and category summaries, paginated query/search engines with default limit 50 and maximum 500, sort/filter controls, diff engines, object details, references and paths-to-roots. The code uses Unity Memory Profiler editor types and is therefore sensitive to dependency/version compatibility.

**Gap vs target:** target can take, list and compare snapshot *file metadata*, but its own compare result notes that object-level comparison requires the UI. Querying the snapshot and explaining which managed/native allocations grew is a materially different ability from merely capturing a .snap file.

**Recommended decision:** INVESTIGATE / ADAPT as a specialist profile, likely optional and gated on the official Unity Memory Profiler package. A first-party integration should return compact summary results by default, paginate rows, explicitly close sessions, cap concurrent snapshots, and state API/package availability. Avoid copying the plugin framework or depending on IvanMurzak types; port the useful domain logic and test it against the target bridge.

**Acceptance tests:** open/cancel/close lifecycle; bounded concurrent sessions; summary and paginated object queries; sort/filter limits; paths-to-roots and references; snapshot-to-snapshot diffs with positive and negative deltas; malformed or incompatible .snap inputs; package absent/older-version errors; disposal of native resources and domain reload handling.

## 3. Asset dependency analysis: useful API shape, weak semantics to avoid copying

Project: [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp).

Source reviewed:
- [AssetDependencyHandler.cs](https://github.com/ozankasikci/unity-editor-mcp/blob/main/unity-editor-mcp/Editor/Handlers/AssetDependencyHandler.cs), blob 439239aec5f2562eb0866fd84fd5e3dafa2e668e.
- [AssetDependencyToolHandler.js](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/src/handlers/asset/AssetDependencyToolHandler.js), blob 2c94da8b497c7c98f7a4b79a2c2fc6e3ec7d922.
- [Handler unit tests](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/tests/unit/handlers/AssetDependencyToolHandler.test.js), blob 9c938bdf4c7009d8768f219b6402064d26aa37e6.

The source establishes the action vocabulary and a working Unity-side handler for direct dependencies and reverse-dependency scans, size estimates, circular analysis and “unused” reports. However, a close read found notable limits:

1. Recursive dependency depth is simplified: a transitive dependency may be labelled depth 2 rather than calculating its full depth.
2. Circular analysis is described in the source as simplified and focuses on scripts rather than a fully general asset graph.
3. get_dependents scans all found assets and calls AssetDatabase.GetDependencies for each candidate, which can become expensive for large projects.
4. find_unused defines “used” from serialized AssetDatabase dependency edges. An unreferenced asset can still be loaded by addressable key, Resources path, code string, external mod/content pipeline, build setting or user workflow. Treating it as unused is a review candidate, not a safe deletion signal.
5. The Node handler unit test uses a mock Unity connection, so those tests prove request validation/forwarding, not correctness of the Unity graph analysis itself.

**Recommended decision:** ADAPT the action design but not the simplifying assumptions. Use Unity AssetDatabase APIs for graph edges; report exact vs inferred fields, cap and page large outputs, include scan scope/build configuration, and make “potentially unreferenced” explicitly conservative. Add Unity EditMode fixtures with known dependency graphs; do not auto-delete.

## 4. Specialist integration principles

- Check current target capability and tests before adding the category: target already has profiling, memory snapshots, VFX groups and extensibility.
- Treat extension packages as evidence of useful workflows, not as mandatory new first-party tool packs.
- Verify package ID/dependencies, supported Unity versions, licenses, code ownership and upstream compatibility before porting.
- Prefer action-based typed tools with a bounded result contract and session/operation IDs for expensive operations.
- Test live Unity behavior, not only mocked transport or synthetic fake-backend tests.
- Keep expensive specialist functionality optional; don't load every detail schema into the default model context.
