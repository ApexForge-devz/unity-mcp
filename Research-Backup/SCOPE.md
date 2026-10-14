# Research Scope — MCP to Unity Only

**Effective:** 2026-10-14  
**Applies to:** all documents under `Research/`, candidate reviews, conclusions, and implementation backlog entries.

## Objective

Improve how the MCP server communicates with, discovers, directs, and safely operates Unity. Research should yield practical changes to the existing Python MCP server and Unity C# Editor bridge, with evidence from source, tests, and measurable acceptance criteria.

## In scope

- **MCP protocol and client interoperability:** tool/resource/prompt contracts, transport behavior, errors, capability discovery, stable schemas, annotations, and modern/legacy protocol compatibility.
- **MCP-to-Unity bridge:** connection/readiness state, project and Editor-instance identity, ownership/routing, request correlation, dispatch, timeouts, cancellation, backpressure, main-thread scheduling, domain reload/reconnect, and accurate unknown-outcome semantics.
- **Tool ergonomics and context efficiency:** progressive or operation-level discovery, schema quality, tool grouping, bounded/structured outputs, pagination/continuation handles, batch semantics, useful diagnostics, and clear errors.
- **Unity Editor and project operations:** deterministic script-driven scene generation/regeneration; persistent C# source creation and structured edits; compilation/reload handling; targeted post-generation validation; scene loading/saving; project assets/references; EditMode/PlayMode tests; Play Mode control; builds; screenshots; profiling; Frame Debugger; Memory Profiler; and project-local automation. Existing direct GameObject/component/prefab mutation tools may be inspected for generic MCP correctness, but they are not the primary authoring path for the AI-agent workflow described below.
- **Reliability, security, and quality:** authorization for privileged operations, safe file/path handling for in-scope local workflows, mutation retry/idempotency policy, resource bounds, logging without secrets, tests, CI, packaging, documentation, client configuration, and reproducible benchmarks.
- **Research sources:** upstream, direct forks, forks-of-forks, and independent MCP/Unity tools may be investigated. Only their independently useful, in-scope MCP↔Unity patterns can become recommendations.

## Agent workflow constraint — deterministic scene authoring

For the target AI-agent workflow, **the persistent scene-generation/regeneration script is the source of truth**. The agent creates or edits that script, runs it through a controlled Unity Editor entry point, checks a compact validation report, and fixes problems by editing the script and regenerating. It does not build scenes through a long sequence of one-off MCP calls that create, mutate or delete individual GameObjects/components, and it should not repair a generated scene by ad hoc direct edits that are absent from the generator source.

Consequences for research priorities:
- Prioritize script creation/editing, safe structured edits, compile diagnostics, readiness/reload handling, reliable generator invocation, idempotent regeneration, validation assertions, result summaries, and minimal diffs.
- Preserve targeted read-only inspection and visual/profiling diagnostics when they answer a specific question without dumping an entire scene/hierarchy.
- Treat direct GameObject/component CRUD, per-property scene writes, direct prefab-override apply/revert, and Undo support for manual scene edits as **deferred/general-tool parity**, not agent-workflow feature priorities.
- This constraint does not mean those tools should be deleted from the product or that they are useless to every client. It means they should not be proposed as the normal scene-authoring mechanism for this agent.
- Scene lifecycle operations (open/create/save a scene) remain relevant when the generation script uses them as part of a controlled regeneration pipeline.

## Explicitly out of scope

Do **not** recommend, implement, or prioritize integrations with external or separate generation/inference systems. Examples include:

- Meshy and other hosted 2D/3D/model/texture/image/audio/video generation APIs.
- ComfyUI, Pinokio, Stable Diffusion or other separate AI-generation workflow/runtime orchestration.
- Third-party provider SDKs, credentials/API keys, account management, billing, provider-specific model options or request builders.
- Remote generation-job submission, polling, recovery/resume, and provider-specific artifact downloads/import pipelines.
- Features whose primary purpose is invoking or managing a generator/inference endpoint rather than operating Unity through MCP.

MCP protocol communication itself, ordinary GitHub/source research, and Unity's own Editor/runtime APIs are not excluded by this rule. The exclusion is about adding or advocating extra generator/provider integrations as product features.

## How to handle findings in forks

1. Do not promote a fork's advertised feature list directly into this repository's backlog.
2. If a fork includes an out-of-scope generator, ignore that feature for implementation purposes. Record it only when necessary to explain the fork comparison or preserve historical provenance.
3. A general engineering principle (for example, never blindly retry an uncertain mutation) may be adapted only if its value is clear for an existing in-scope MCP↔Unity operation and the design does not require the excluded provider.
4. Mark provider-only reviews and recommendations `OUT OF SCOPE`. Keep them separate from active backlog priorities.
5. Do not broaden scope because a fork already has an implementation, because an API is available, or because a generator feature seems impressive.

## Change control and validation

This scope applies to research and documentation; it does not by itself remove or alter existing runtime features in the baseline. Any future runtime implementation must be separately scoped, tested, and reviewed. The active recommendation list is [IMPROVEMENT_BACKLOG.md](IMPROVEMENT_BACKLOG.md); if a proposal conflicts with this document, this scope takes precedence.
