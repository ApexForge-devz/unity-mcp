# Independent Unity MCP Implementations

**Snapshot:** 2026-10-14  
**Purpose:** Extract reusable implementation patterns from independent repositories. These are not assumed to be forks of CoplayDev/unity-mcp.

## 1. AnkleBreaker Studio: progressive discovery, compact results, and bridge identity

Repositories:
- [Server](https://github.com/AnkleBreaker-Studio/unity-mcp-server)
- [Unity plugin](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin)

Source reviewed:
- [`src/tool-tiers.js`](https://github.com/AnkleBreaker-Studio/unity-mcp-server/blob/main/src/tool-tiers.js), blob `4273fd2136d6c7d86a7a964ccf174ade8a29337a`.
- [`src/response-format.js`](https://github.com/AnkleBreaker-Studio/unity-mcp-server/blob/main/src/response-format.js), blob `777067e4f163a468d3765b9e0d3f045c192bdff3`.
- [`docs/discovery.md`](https://github.com/AnkleBreaker-Studio/unity-mcp-server/blob/main/docs/discovery.md), blob `40dea06106d5c4b2d3b6dfca7f56a72a071a0907`.
- [Recent server commit](https://github.com/AnkleBreaker-Studio/unity-mcp-server/commit/9c84ff9fe22db1202bc003a56910f3add7a18d08), which includes maintenance, compatibility and response-size changes.

### Useful patterns

1. **Progressive tool discovery.** Keep common actions as individually typed tools but put the broad specialist catalog behind a summary/category/search/schema path. The project documents a change from a 9.7 KB discovery response to 0.7 KB for a no-argument summary and 12–22 KB to 3–6 KB for a category view. These are project-reported measurements, not independently reproduced here.
2. **Compact output at a single serialization seam.** `formatResult()` uses compact JSON by default instead of pretty-printing already serialized data. Human-readable formatting is a debug option.
3. **Translate logical failures into MCP tool errors.** The response helper recognizes multiple error shapes, including nested queue results, so HTTP 200 does not accidentally imply tool success.
4. **Validate editor identity before dispatch.** `docs/discovery.md` explains why a successful HTTP response is insufficient: a stale registry entry or unrelated local service could occupy a previous port. Require a recognizable Unity version and project identity from the ping payload.
5. **Enforce read-only rules at the final dispatch route.** A recent commit message reports rejecting non-read-only routes even through an advanced-tool proxy, validating operation names, preserving the job ID when follow-up polling fails, and reducing unnecessary poll/route requests.
6. **Use source-level compatibility tests across plugin versions.** The repo documents tests that exercise actual MCP processes and Unity editor connections, not only isolated function tests.

### Adaptation to this repository

- **ADAPT:** Progressive discovery for rarely used tools while keeping common and safety-critical tools individually typed.
- **ADAPT:** A single output-normalization seam that consistently maps logical failure to MCP error results.
- **ADAPT:** Explicit identity validation when discovering or routing to editor instances.
- **ADAPT:** Route-level safety policy even when requests arrive through aliases or generic dispatchers.
- **INVESTIGATE:** Whether compact JSON materially saves tokens for our common payloads; measure bytes/tokens and keep debugging output available.

### Caveat

A generic advanced tool can weaken schema clarity, per-tool approval, and discoverability if used indiscriminately. Do not replace the whole typed surface with a single dispatcher. Keep broad, infrequent operations lazy; preserve first-class schemas for frequent operations and destructive actions.

## 2. KitWright: output caps, spill handles, export profiles, and stall diagnostics

Repository: [kitwright/unity-mcp](https://github.com/kitwright/unity-mcp)

Source reviewed:
- [`Editor/MCP/Server/MCPRequestHandler.cs`](https://github.com/kitwright/unity-mcp/blob/main/Editor/MCP/Server/MCPRequestHandler.cs), blob `69ccbc1b4ecbff437bf7e85414e54af049bf3082`.
- [`Editor/MCP/Server/MCPToolExportPolicy.cs`](https://github.com/kitwright/unity-mcp/blob/main/Editor/MCP/Server/MCPToolExportPolicy.cs), blob `72e818d2fd173252ea097f2f610379c8a2520255`.
- [`Editor/Threading/EditorThreadHelper.cs`](https://github.com/kitwright/unity-mcp/blob/main/Editor/Threading/EditorThreadHelper.cs), blob `8629a1b215872ef75bd04d8cfc4a0598ce7c618f`.
- [Contributor validation guidance](https://github.com/kitwright/unity-mcp/blob/main/CONTRIBUTING.md).

### Useful patterns

1. **Hard inline text cap with optional spill-to-file.** `MCPRequestHandler` caps inline text at 64 KiB, returns a 4 KiB preview for spilled content, and retains a bounded number of output files. Its comments explicitly connect oversize tool results to clients rejecting calls. This is a concrete alternative to truncating arbitrarily: return a useful summary plus a stable way to retrieve the full result.
2. **Export profiles.** `Minimal`, `Core`, `Extended`, and `Full` profiles reduce the always-visible tool schema set. The target already has tool groups, so the useful delta is a curated profile policy, not a duplicate group system.
3. **Detect a stalled Editor main-thread pump separately from a slow command.** The helper tracks time since the last editor pump and only calls the pump stale beyond a defined threshold; its comment notes a slow awaited operation can still keep pumping. This can make timeout diagnostics actionable.
4. **Cancellation and queue discipline.** The thread helper tracks invocation completion through a concurrent queue, task completions and cancellation tokens.
5. **Explicit Undo for mutations.** The source uses `Undo.RecordObject` for serialized-object changes, and the code execution path can register modified objects. This validates that undo granularity is a reusable design pattern, although our repository already has Undo-aware operations.

### Adaptation to this repository

- **ADAPT (P1):** Central result-size budget with a brief, schema-shaped preview and explicit continuation/spill handle for oversized outputs. Cover nested data and image responses, not just plain text.
- **ADAPT (P1/P2):** Add an Editor-loop liveness watchdog that distinguishes "still pumping but slow" from "not pumping", plus in-flight command context.
- **INVESTIGATE:** Define named tool profiles layered on existing groups if current group activation leaves too much schema loaded for small-context clients.

### Caveat

The inspected `MCPRequestHandler` explicitly supports protocol versions only through `2025-06-18`; therefore, its output/threading patterns may be reusable, but its protocol handler is not a source of truth for current protocol compatibility. Protocol support must be evaluated separately.

## 3. Ozan Kasikci: worktree-aware instance discovery and diagnosability

Repository: [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp)

Source reviewed:
- [README and instance-discovery documentation](https://github.com/ozankasikci/unity-editor-mcp).
- [`mcp-server/src/core/server.js`](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/src/core/server.js), blob `d02babded5abec6b3666ae442cba12bece3fed42`.
- [`mcp-server/package.json`](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/package.json), blob `bbecffd79917eeb8e35be94c00ed79b11cb6d3fc`.

The repo exposes separate direct stdio and daemon/proxy entrypoints. Its README describes a project registry with process ID, Unity/package versions, workspace identity and heartbeat; selectors prefer explicit configuration, and related Git worktrees that do not match fail closed rather than silently choosing another editor.

### Adaptation

**ALREADY PRESENT / VERIFY:** This target already has multi-instance routing. The better follow-up is to ensure the safety properties are equally strict: project/worktree identity, stale registration handling, clear failure candidates, no accidental cross-project mutation, and a test that two open editors are never silently confused.

The separate daemon may be useful if many clients need to reuse an expensive editor connection; benchmark operational complexity and process lifecycle before adopting it.

## 4. Pushkar-Env: server-side permissions and approval for destructive work

Repository: [pushkar-env/unity-mcp](https://github.com/pushkar-env/unity-mcp)

Source reviewed:
- [`docs/architecture.md`](https://github.com/pushkar-env/unity-mcp/blob/main/docs/architecture.md), blob `8edcbd903ddf8f4cf07421e9e71378872f68f6b4`.
- [README](https://github.com/pushkar-env/unity-mcp).

The design separates a .NET MCP server (without Unity references) from the Unity Editor bridge and advertises READ / WRITE / DESTRUCTIVE permissions, optional approval prompts, structured validation and no arbitrary C# execution or shell access.

### Adaptation

**ADAPT (P0/P1):** Implement a server-enforced policy mode for remote/unattended sessions that can disable dynamic in-process C# execution and require approval for selected destructive mutations. Preserve trusted-local developer ergonomics as an explicit mode instead of removing the escape hatch from all contexts.

Policy must be enforced at the server boundary, not by adding advisory language or relying on a caller-set `safety_checks=false/true` value. Use audit logging and tests for all routes/aliases to the privileged operation.

### Trade-off

A strict structured surface is easier to secure and approve per tool, while arbitrary execution can compose workflows that otherwise take many calls. Support both through clear trust boundaries instead of treating either extreme as universally correct.

## 5. Ivan Murzak: capability composition and specialist workflows

Repository: [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)

Source reviewed: [README](https://github.com/IvanMurzak/Unity-MCP), blob `0e4abe8c2807bfd0c699aad82fad741e4c091bd9`, which describes the project as an extensible Unity skills/MCP system with a public package, Docker distribution, custom tool patterns and release workflows.

### Usefulness

This is a major independent implementation and should remain in the high-priority source-review set, especially for:
- Asset inspection and reference/dependency workflows.
- Prompt/resource-oriented common workflows.
- Custom project-specific tool registration and package integrations.
- Runtime/editor workflow boundaries.

### Status

README-level review only at this snapshot. The specific feature list is not treated as source-verified until handlers and tests are inspected. No recommendation to copy the whole architecture follows from scale or headline tool counts.

## 6. Mitch Christow: prompts, resources and worktree workflows

Repository: [mitchchristow/unity-mcp](https://github.com/mitchchristow/unity-mcp)

Source reviewed: [README](https://github.com/mitchchristow/unity-mcp), blob `9a5d5ef26bcad2883dfe158557a960ed5245de3e`, which documents tools/resources/prompts, an npm/Unity package setup and support for recent Unity 6 editor versions.

### Status and next action

README-level review only. Inspect its actual resources, prompts, operation/event history, worktree handling and tests before treating these as novel contributions. Similarity to popular upstream designs needs to be established by commit/tree evidence. **No feature has been accepted solely from the README.**

## 7. Overall takeaways from independent implementations

The strongest recurring patterns are not tool-count expansion:

1. **Discover on demand:** concise summary → category/filter → one full schema → call.
2. **Cap and normalize outputs centrally:** summaries, stable handles, bounded inline output and consistent error mapping.
3. **Make the editor bridge observable:** identity, in-flight work, last-pump time, cancellation/retry reason and command correlation.
4. **Enforce safety independently of the model:** server-side policies, final-route checks, approval tiers and identity/auth checks.
5. **Test full workflows and compatibility:** run tools against a real editor and protocol clients, including domain reload, missing editor, wrong port, oversized payload, cancellation and multiple projects.
6. **Treat package/CI maintenance as features:** version contracts, reproducible packaging, upstream sync and generated-document parity, while avoiding unnecessary scheduled builds.

See [Improvement Backlog](IMPROVEMENT_BACKLOG.md) for prioritized actions and acceptance criteria.


## 8. Specialist source review: memory profiling and asset dependency graph

### Sabri Boughanmi / Ivan Murzak Memory Profiler extension

Repository: [sabriboughanmi/Unity-MCP-MemoryProfiler](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler)

Unlike README-only candidates, source reviewed includes Tool_MemoryProfiler.cs, SnapshotQueryEngine.cs and SnapshotDiffEngine.cs. This package supports incremental snapshot opening with operation status/cancellation, a bounded maximum of two sessions, summary/metadata, sorted and paginated queries (default 50, maximum 500), snapshot diffs, object details, references and paths-to-roots. Its tree contains multiple EditMode suites around bridge behavior, query/diff engines and lifecycle; tests were not run during this review. It is MIT-licensed with an existing copyright notice for Ivan Murzak, which must be preserved if source is reused.

**Usefulness:** The target's current MemorySnapshotOps captures .snap files, lists them and compares file-level metadata; its own response notes object-level comparison must be performed in the Memory Profiler UI. Queryable snapshot analysis is therefore a meaningful optional capability gap, not just an alternate implementation of existing snapshot capture.

**Caution:** It depends on Unity Memory Profiler editor APIs/package and has its own integration framework. Port the domain pattern selectively and check compatibility against the target's Unity versions/package conventions.

### Ozan Kasikci asset dependency graph: evidence and limitations

Repository: [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp)

Source-reviewed Unity handler: [AssetDependencyHandler.cs](https://github.com/ozankasikci/unity-editor-mcp/blob/main/unity-editor-mcp/Editor/Handlers/AssetDependencyHandler.cs), blob 439239aec5f2562eb0866fd84fd5e3dafa2e668e.

It wires Unity AssetDatabase calls for direct/recursive dependencies, reverse dependents, size analysis, script circular-analysis and unused-asset candidates. This validates that the action family can be implemented in Unity, not just declared in the Node handler. The implementation should **not** be copied without revisions:
- recursive depth is simplified to 1 or 2, not a true graph depth;
- cycle detection is explicitly simplified and focuses on scripts;
- reverse dependency detection scans discovered assets and calls GetDependencies per asset, which can become expensive;
- “unused” is computed from serialized AssetDatabase dependency edges, so code-string loads, Resources paths, Addressables and external runtime/content workflows may cause false positives;
- the unit tests inspected at the MCP server layer use a mock Unity connection, so they do not establish correct AssetDatabase graph semantics.

**Usefulness:** Reuse the action vocabulary as a starting point, but implement conservative semantics and prove them against Unity EditMode fixtures; never automatically delete returned “unused” candidates.

Further source details and acceptance-test recommendations are in [Specialist Workflow Source Review](SPECIALIST_WORKFLOWS.md).


## 9. CoderGamester/mcp-unity: security/retry tests and the limits of Undo rollback

Repository: [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity), an independent implementation. Metadata snapshot: main branch; 1,930 stars, 244 forks, updated 2026-10-13; MIT license metadata. Source tree contained 245 entries.

Source reviewed:
- [McpUnitySecurityTests.cs](https://github.com/CoderGamester/mcp-unity/blob/main/Editor/Tests/McpUnitySecurityTests.cs), blob 62e5ecb199dc022d98cb641ac01f68a45fbf368a.
- [McpUnityServerRetryTests.cs](https://github.com/CoderGamester/mcp-unity/blob/main/Editor/Tests/McpUnityServerRetryTests.cs), blob 7423fb99e986861e6bd370dfafa7ae34d2318291.
- [PathHandlingTests.cs](https://github.com/CoderGamester/mcp-unity/blob/main/Editor/Tests/PathHandlingTests.cs), blob 7b00d664400a6e59c105a8899ba2cb8b93c5b61a.
- [BatchExecuteTool.cs](https://github.com/CoderGamester/mcp-unity/blob/main/Editor/Tools/BatchExecuteTool.cs), blob 3abef13ac4b1da11d757cbec4a07f8b55d7644cb.

### Useful patterns

1. **Security behavior is tested explicitly:** tests exercise Origin validation, auth token generation/persistence/rotation, malformed token state and denial of selected package-changing tools before parameter validation when disabled. Borrow the idea of security regression tests, not necessarily its exact Origin policy without reviewing our transport topology.
2. **Bounded retry/backoff and cleanup order:** startup retry delay is tested at boundary attempts, and failed-start cleanup is tested to stop a background tick before a null-server early return. This is a practical regression-test model for Editor/bridge lifecycle bugs.
3. **Path matrix:** path handling tests include spaces, nonexistent paths and valid package roots. The target's existing test surface should retain equivalent platform/spacing coverage.
4. **Batch rollback is Undo-group-based:** atomic=true starts an Undo group and calls Undo.RevertAllDownToGroup when a stop-on-error failure occurs. This is not a transaction across arbitrary tools: asset/file writes, server-side effects, external processes or actions that do not register appropriate Undo records may not roll back.

### Comparison against target coverage

The target already has:
- [SafeZipExtractorTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/SafeZipExtractorTests.cs), blob e27b742d50516aa86d352e0174c3f6641bcd27b1: normal extraction, disallowed extensions, traversal rejection.
- [ExecuteCodeTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ExecuteCodeTests.cs), blob dc3c4e360afe9c3273c5c39a1a8ff7d716c88464: execution, compiler/runtime errors, blocklist cases for file delete/process start/infinite loop, and a test showing safety_checks=false bypasses the blocklist.
- [BatchExecuteKeyPreservationTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/BatchExecuteKeyPreservationTests.cs), blob 3419dfed59655ef3b21d92e3f6c8a5a4c98e5363: verifies nested key preservation and representative batch operations.

Therefore do not add duplicate smoke tests that already exist. Instead add tests around the exact gaps:
- ZIP entry-count and expanded-byte quotas, per-entry streaming enforcement and cleanup after quota failure.
- A server policy test proving remote/unattended callers cannot disable privileged-operation guardrails by setting a tool argument.
- Undo rollback semantics only for a declared supported subset of operations. Every operation type must state whether it is Undo-recorded/rollback-capable; never label an arbitrary batch atomic.
- Reconnect/failed-start liveness behavior and bounded retry semantics where target implementation lacks equivalent tests.

**Disposition:** **ADAPT test design; do not port the atomic flag wholesale.** An optional Undo-group feature could be considered later only if operations expose accurate rollback capability annotations and non-Undo side effects are explicitly excluded from the guarantee.

## 10. Ozymandros/Unity-MCP-Server: structured contracts and validation pipeline

Repository: [Ozymandros/Unity-MCP-Server](https://github.com/Ozymandros/Unity-MCP-Server), an independent .NET MCP host and Unity bridge. Source/doc reviewed:
- [Tool contracts and observability](https://github.com/Ozymandros/Unity-MCP-Server/blob/master/Docs/tool-contracts-and-observability.md), blob 494384edaa58c68507c1c48e497271bd3571f312.
- [Validation pipeline and test strategy](https://github.com/Ozymandros/Unity-MCP-Server/blob/master/Docs/validation-pipeline-and-tests.md), blob aa755e39c51a4a1ad450a71e32dc826dfc40a032.
- [PathResolverTests.cs](https://github.com/Ozymandros/Unity-MCP-Server/blob/master/UnityMcp.Tests/Application/Tools/PathResolverTests.cs), blob 768f862978e368d61d7b01bdb5474085a3b70069.

Useful documented patterns:
- Shared error categories such as validation, IO, contract and external-tool failure, plus machine-readable codes and structured diagnostics.
- Path containment tests for URI schemes, traversal, out-of-project absolute paths, duplicate Assets segments and normal project-relative paths.
- Composable validation stages: static/schema/path checks, Unity import/compile validation, and scenario/e2e recipes.

This confirms that an explicit error taxonomy and layered validation contract can be documented even in an implementation with many tools. Our target already has structured per-tool responses and extensive tests, so the useful next step is normalizing cross-tool error codes and adding missing scenario tests—not moving the target to .NET or duplicating its validation framework. These docs are from an independent repo and do not establish runtime behavior beyond the cited source/tests.

## 11. PICO XR extension: useful architecture, not reusable source

Repository: [Pico-Developer/Unity-MCP-Extensions](https://github.com/Pico-Developer/Unity-MCP-Extensions). Source reviewed: [PXR_MCP_Tools.cs](https://github.com/Pico-Developer/Unity-MCP-Extensions/blob/main/Editor/Tools/PXR_MCP_Tools.cs), blob 09eed0509a670932687e60d157447a643316ee3; [haptics EditMode tests](https://github.com/Pico-Developer/Unity-MCP-Extensions/blob/main/Tests/Editor/PXR_MCP_HapticsEditorTests.cs), blob 90f1d8c95f8a8b7f98ce2cd66a1adc72614890ee.

It uses action-based typed tools, small feature-block operations and a result envelope. Its haptics tests explicitly assert idempotence: attaching the same configuration twice does not create duplicate managers, and upserting effects preserves one manager.

**Usefulness:** For optional XR provider integrations, test idempotent attach/upsert operations and represent unsupported runtime capability explicitly.

**Hard restriction:** its C# source carries a proprietary PICO Technology notice stating reproduction is forbidden without prior written permission. Treat as reference-only; do not copy source code into Unity-MCP. Reimplement generic ideas independently and only integrate with appropriate licensed SDK/dependencies.
