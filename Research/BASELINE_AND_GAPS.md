# Baseline and Gaps

**Baseline snapshot:** target `beta`, observed 2026-10-14 at `142ea0af4890337f0ff6658df61db23982313717`. This is source inspection, not a full re-audit of every file.

## Existing architecture and capabilities

The repository already has a Python FastMCP server and Unity C# Editor bridge, centralized tool registration/groups, resource-backed state, transport/authentication and multi-Editor routing, async test-job patterns, script creation/structured editing/validation, scene lifecycle operations, screenshots, profiling tools, and Python/C# test infrastructure. Improve missing guarantees at these existing seams instead of adding parallel subsystems.

Source landmarks at the reviewed baseline:
- [Tool registry](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/registry/tool_registry.py)
- [Tool-group management](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_tools.py)
- [Batch wrapper](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/batch_execute.py)
- [Unity batch handler](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/Editor/Tools/BatchExecute.cs)
- [Execute Code handler](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/Editor/Tools/ExecuteCode.cs)
- [Asset manager](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/Editor/Tools/ManageAsset.cs)
- [Scene manager](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/MCPForUnity/Editor/Tools/ManageScene.cs)

## Material gaps and design mismatches

### 1. Security policy is not the same as source filtering

`ExecuteCode` compiles and runs supplied C# inside the Unity Editor process. A caller-controlled `safety_checks` flag and blocked-pattern inspection are not a process sandbox. A remote caller must not be able to bypass trust policy by setting that flag or reaching the handler through an alias, proxy or batch route.

### 2. Batch schema overstates concurrency

The Python interface exposes parallel-related parameters, while Unity operations are executed sequentially on the Editor main thread. The correct work is contract clarity, deterministic partial-result semantics and bounded output—not a new batch engine.

### 3. Output limits are fragmented

Some handlers paginate, truncate or downscale output, but there is no verified universal byte/context budget across all return paths. Nested batch results, errors, image payloads and async job polling must obey the same budget and preserve a recoverable path to omitted detail.

### 4. Tool visibility is coupled to a framework-specific design

The server dependency is constrained to `fastmcp>=3.0.2,<4`; `manage_tools` uses FastMCP 3 session-visibility operations. This deserves a protocol compatibility spike before any dependency upgrade. Do not assume that keeping session-dependent `tools/list` behavior will be valid for every modern stateless client.

### 5. Editor readiness is not compile success or generator completion

Current readiness and compile-start signals help the caller survive reload, but a ready Editor does not prove that compilation succeeded or the scene generator completed. The lifecycle needs compilation diagnostics and a request-correlated generator receipt.

### 6. Shared-server ownership needs explicit lifecycle semantics

The reviewed PID/token implementation has project-local PID files but also uses global EditorPrefs state for some last-server metadata. Shared-server shutdown must distinguish launcher ownership from permission to stop a process used by other Editors. Unknown or timed-out liveness results should not cause a destructive stop.

### 7. Asset search and dependency graph are separate gaps

Asset search should avoid hydrating previews/metadata before pagination has selected the requested page. Separately, `ManageAsset.GetAssetData` still contains a dependency-metadata TODO; a dependency/reverse-reference graph is not already present. Dependency analysis needs conservative semantics and fixture-backed cache invalidation.

### 8. Visual validation exists; its resource bounds need review

`manage_scene` already supports screenshot/capture modes and contact sheets. Do not add a second capture system first. Review supersize pixel allocation, orbit-count limits, payload budgets, async file readiness, and explicit fallback-source metadata.

## Not a priority for the script-first agent

Do not elevate direct GameObject/component CRUD, direct serialized property changes, prefab override apply/revert, or Undo convenience to the primary scene-authoring roadmap. Retain targeted read-only queries and diagnostics. See [Script-First Workflow](SCRIPT_FIRST_WORKFLOW.md).

## What is not experimentally established

No runtime performance gains, token savings, memory bounds, Editor responsiveness, protocol interoperability, or end-to-end task success should be claimed until measured. Static source review identifies candidate work; it does not validate behavior in a live Editor.
