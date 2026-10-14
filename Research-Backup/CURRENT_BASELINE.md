# Current Unity-MCP Baseline

**Observed:** 2026-10-14  
**Repository:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp)  
**Branch reviewed:** `beta`  
**Baseline freshness:** Some files were directly fetched from `beta` on 2026-10-14; this is not a full line-by-line review of the repository.

## 1. Repository relationship

A GitHub compare of `ApexForge-devz/unity-mcp` `beta` to `CoplayDev/unity-mcp` `beta` returned **identical** on 2026-10-14 (zero ahead/behind commits). That confirms the public target branch matched the closest upstream branch for that comparison snapshot; any unpublished/local working tree is outside this evidence.

The repo metadata reports `beta` as the default branch. The target retains upstream package identity and support links:
- Unity package uses the upstream identity (see `MCPForUnity/package.json`).
- `Server/pyproject.toml` describes Python package `mcpforunityserver` version `10.3.0`.
- Server URLs still reference `CoplayDev/unity-mcp`.
- Dependency declaration currently includes `fastmcp>=3.0.2,<4` and `mcp>=1.16.0`.

Source: [Server/pyproject.toml at beta](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml), blob observed as `83800f55c65984102b61a035613a2ca40459eb91`.

**Implication:** Research changes should generally be upstreamable or be kept as a clearly differentiated fork. Do not rename package identity merely for branding; consider update compatibility and package-manager effects first.

## 2. Major layers actually present

- **MCP server:** Python server using FastMCP, a tool registry/decorator, grouped tools, resources, transports, instance routing, authentication/remote support, tests and release automation.
- **Unity Editor bridge:** C# Editor package that dispatches commands into Unity APIs, plus runtime helpers.
- **Optional custom code tooling:** Roslyn/custom tool support and dynamic in-process C# compilation/execution.
- **Tool organization:** `Server/src/services/registry/tool_registry.py` defines a central registry and tool groups including core, docs, VFX, animation, UI, scripting extensions, testing, ProBuilder, profiling and asset generation. The default enabled group is `core`.
- **Testing and delivery:** Existing repository tests and CI are extensive; improvements should extend them rather than duplicate the framework.

Relevant source:
- [Tool registry](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/registry/tool_registry.py)
- [Tool-group meta-tool](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py)
- [Python batch tool](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/batch_execute.py)
- [Unity batch handler](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/BatchExecute.cs)
- [Unity execute_code implementation](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs)

## 3. Verified design issues and key nuances

### 3.1 Batch execution has an inaccurate interface

The Python `batch_execute` input schema exposes `parallel`, `fail_fast`, and `max_parallelism`. In Unity, `BatchExecute.HandleCommand` explicitly executes commands sequentially on the Editor main thread and logs a warning if parallel mode is requested. Unity API safety justifies sequential Unity-side execution, but the public contract should not imply that setting `parallel=true` makes Unity API calls concurrent.

Already present: a batch-size setting, default 25 commands, hard limit 100, per-command outcome records, and fail-fast behavior. The missing work is **contract clarity, response limits, and tests for the semantics**, not a new batching subsystem.

Sources:
- [Python batch wrapper](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/batch_execute.py) — blob `2c717d9c45cc25b2be878bda7eb76db580becdfb`
- [C# batch handler](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/BatchExecute.cs) — blob `aca4a1994a4dd2857e132b9f068146bc4d8960ff`

### 3.2 Tool-group visibility is coupled to FastMCP 3 behavior

The `manage_tools.py` module description says it depends on FastMCP 3.x native per-session visibility. The dependency is pinned below major 4. That design must be evaluated against the newer MCP protocol's tool-list invariants before any upgrade. A major-version bump alone is not a sufficient migration plan; decide how group selection and tool exposure should work across modern stateless HTTP and supported legacy clients.

Source: [manage_tools.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py).

### 3.3 Dynamic C# execution is powerful but not a process boundary

The Unity `ExecuteCode.HandleExecute` path accepts user code up to 50,000 characters, has a `safety_checks` input defaulting to true, checks blocked patterns when enabled, then compiles and executes the code inside Unity Editor. Since the caller can disable checks, and source-pattern filtering cannot provide a general C# sandbox, safe use depends on who can invoke the tool and under which server policy.

This does not mean the tool should automatically be removed: it is a valuable orchestration escape hatch for trusted local development. The recommended design direction is server-enforced policy/permissions, especially for remote and unattended clients, not a claim that a regex can sandbox arbitrary C#.

Source: [ExecuteCode.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs) — blob `10325861e52def46cc72fe7cb2d5d4e28ffbb828`.

### 3.4 Context efficiency is partly implemented, but not governed globally

The repo already groups tools, has tool descriptions and resource-backed state, and some individual paths bound or paginate output. Do not infer that all outputs are budgeted centrally. Candidate follow-up: a consistent output contract (small summary by default, selected fields, capped items/bytes, continuation handles, opt-in large artifacts) across tool handlers and transports.

### 3.5 Asset coverage is broad, but dependency analysis needs specific validation

The gap has now been checked against source: target **ManageAsset.GetAssetData** still has an explicit dependency-metadata TODO. The current manage_asset metadata path does not expose a dependency/reverse-reference graph. The alternative's Unity implementation is useful evidence but overstates depth, uses a costly all-assets reverse scan, and treats serialized references too confidently. See [ASSET_DEPENDENCY_ANALYSIS_REVIEW.md](ASSET_DEPENDENCY_ANALYSIS_REVIEW.md). Before implementation, test direct/transitive references, real graph depths, cycles, folders, GUID moves, package boundaries, reverse-cache invalidation and conservative potentially-unused results. Report candidates, not automatic deletion.

## 4. Existing capabilities not to rebuild

Previous source audit identified the following as already present in some form; test refinements may still be warranted:
- Undo-aware operations.
- Scene and hierarchy management.
- Screenshots/capture workflows.
- Pagination and some result truncation.
- Async test jobs.
- Scene/editor state and readiness snapshots.
- Multi-instance routing.
- Tool grouping / activation.
- C# script creation, structured editing and script validation; plus scene lifecycle, screenshots and read-only profiling/diagnostic primitives.
- Authentication and remote-server building blocks.

Confirm each item against its current handler/tests before making a fine-grained claim that it is complete. The architectural rule is: improve contracts and gaps before adding duplicate subsystems.

### Agent-specific scene-authoring constraint (2026-10-14)

The agent's scene authoring is script-first: a persistent generator/regeneration script owns intended scene state. Modify that source, compile/reload, run the named generator and perform targeted assertions; do not treat direct GameObject/component CRUD, direct property writes, or individual prefab-override mutations as the normal workflow. Those APIs remain for other clients and may be inspected for general tool correctness, but are not active priorities for this agent. See [DETERMINISTIC_SCENE_AUTHORING.md](DETERMINISTIC_SCENE_AUTHORING.md).

## 5. Not yet verified experimentally

No Unity Editor was launched from this research session. The following remain **unverified** until measured:
- Runtime latency/throughput improvement.
- Actual LLM token cost under target clients.
- Frame or editor responsiveness during large batches.
- Memory impact of long sessions or code execution.
- MCP compatibility against the full client/protocol matrix.
- End-to-end task success after any proposed feature.
