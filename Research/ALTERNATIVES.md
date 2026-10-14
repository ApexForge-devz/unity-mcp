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
