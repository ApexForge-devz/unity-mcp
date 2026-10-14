# MCP Protocol, Tool Discovery and Context Budgets

## Compatibility comes before dependency upgrades

At the reviewed snapshot, `Server/pyproject.toml` constrains FastMCP to `>=3.0.2,<4`, while `manage_tools` depends on FastMCP 3 session-visibility operations. The research reviewed the MCP 2026-07-28 tools specification and FastMCP 4 protocol notes, which introduce compatibility constraints for modern stateless clients, including deterministic tool listing and restrictions on per-connection/session-dependent list variation.

Do not treat a major-version bump as a migration plan. First re-check the current specification and library implementation, then build a compatibility spike.

### Candidate designs to test

1. **Stable modern catalog with invocation gating:** fixed `tools/list` surface; group activation affects which operations may execute, not a connection-specific schema list.
2. **Separately configured catalogs/endpoints:** server configurations expose a fixed, intentional tool set.
3. **Authorization-based filtering:** only where every list request carries authoritative caller identity and authorization is evaluated consistently.
4. **Legacy compatibility mode:** keep existing session visibility only for a negotiated/tested legacy path.

Never use a caller-controlled tool argument or opaque session value as authorization. Keep legacy clients deliberately supported; don't assume the newest protocol replaces them.

## Schema/context strategy

The existing Core-first groups and draft schema compaction are useful directions, not proven wins. Blind prefix truncation can remove destructive-action warnings, path constraints, prerequisites or recovery instructions even when the JSON schema remains structurally valid.

Prefer:
- curated short summaries for high-risk and frequently used tools;
- full tool details through the existing details-retrieval mechanism;
- a clear “description shortened / details available” marker where text was abbreviated;
- deterministic group/tool ordering and schema-parity tests;
- whole-catalog byte/token measurement plus actual task-selection evaluation.

Do not assume “six tools versus many tools” wins without measuring lookup accuracy and extra round-trips. An operation-level façade is a benchmark comparator, not the default architecture.

## One result contract across tools

Some tools currently paginate/truncate/downscale, but a universal output budget was not established. Introduce a central boundary that applies to direct calls, nested batch results, exceptions and async job status/retrieval:
- concise, schema-shaped summary by default;
- hard serialized-byte budget and explicit truncation state;
- critical error details, counts, continuation cursor or stable result handle;
- optional detail fields/pages/full result when requested;
- TTL/eviction and project/principal scoping for stored results;
- the original tool's output budget reused during later async retrieval;
- opt-in image payloads where visual evidence isn't needed.

A transport/frame limit is not the same as an LLM-context budget. Avoid duplicating the same large data under both text and structured content. Where feasible, cap before fully materializing an enormous response.

## Batch semantics

The target's Python batch wrapper exposes parallel-related parameters while Unity command handlers are serialized on the Editor main thread. Public metadata should tell the truth: deterministic sequence, partial results and fail-fast semantics; not a transaction; not concurrent Unity API execution. A disconnected caller must not automatically cause a non-idempotent operation to be repeated.

## Acceptance tests

- Modern and legacy protocol negotiation, transports, tools/resources/prompts, auth/errors/cancellation.
- Deterministic tools/list output before and after group selection for each protocol mode.
- Warning/precondition retention in compact schema and retrieval of the original schema.
- Stable ordering, schema parity against all registered tools and framework-version matrix.
- Bounded direct, nested, async, exception and image outputs with recoverable continuation.
- Cross-session/project/principal rejection of expired or foreign result handles.
- Whole-registry serialized-size and task-success benchmark against current beta without compaction and a minimal internal operation-catalog comparator.

References: [MCP tools specification, 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [FastMCP v4 notes](https://github.com/PrefectHQ/fastmcp/blob/main/dev-docs/v4-notes/protocol-2026.md), [server dependency](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/pyproject.toml), [group management](https://github.com/ApexForge-devz/unity-mcp/blob/142ea0af4890337f0ff6658df61db23982313717/Server/src/services/tools/manage_tools.py). Re-verify versioned specifications before implementation.
