# MCP 2026-07-28 and FastMCP 4 Compatibility Review

**Reviewed:** 2026-10-14  
**Target:** ApexForge-devz/unity-mcp beta  
**Purpose:** Turn the protocol migration concern into testable design requirements. This document is based on the current published MCP tools specification, the MCP release announcement, FastMCP v4 release notes and protocol notes, plus the target's current dependency/tool-group source. No protocol integration test has yet been executed.

## 1. Authoritative protocol requirements relevant to this repo

### 1.1 Tool list stability

The [MCP 2026-07-28 Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) states:
- tools/list responds with the set of tools currently available to the requesting client.
- The set may be empty and may change over time.
- **It must not vary per connection or as a side effect of other requests on that connection.**
- It may vary with the authorization presented on the individual request, because credentials are request-level inputs.
- Servers should return tools in deterministic order when the underlying set has not changed.
- listChanged notifications are recommended when availability changes for listening clients.

This has a direct design consequence for the existing manage_tools behavior: session-specific list mutation is a legacy-era design assumption that needs an explicit modern-mode strategy. A new client should not receive different tools/list contents solely because it opened a different connection or invoked a tool-group toggle. Do not infer that all dynamic tools are forbidden: time-varying availability and request-authorization-based filtering are allowed, subject to notification/cache rules. The exact contract and client caching behavior need tests.

Source: [Tools capability and list behavior](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) (the published page's Capabilities section, especially the listChanged text).

### 1.2 The modern protocol is sessionless

The [MCP 2026-07-28 announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/) says the new revision retires the initialize/initialized handshake and Mcp-Session-Id. Requests carry protocol version and client metadata in _meta; clients may call server/discover, but it is optional. Each request can land on any server replica. Application state may still exist, but should be surfaced through explicit handles passed in tool arguments rather than hidden transport-session dependence.

For Unity-MCP, this suggests:
- do not use the transport session as the only source of project, tool-group or operation state in modern mode;
- use explicit Unity-instance, job, capture, snapshot or continuation handles where continuity is necessary;
- preserve the identity/project routing and auth boundaries while supporting stateless requests;
- distinguish protocol statelessness from actual stateless application behavior: the Editor, jobs and captured snapshots are naturally stateful resources and should use explicit resource IDs and validated lifetimes.

### 1.3 FastMCP v4 is a migration candidate, not a blind bump

[FastMCP v4.0.0 release notes](https://github.com/PrefectHQ/fastmcp/releases/tag/v4.0.0) say v4 was released on August 31, 2026 and supports both the 2026-07-28 stateless protocol and legacy handshake-era clients, auto-detecting per connection. They also list breaking/API changes that still require an application audit.

[FastMCP's 2026 protocol notes](https://github.com/PrefectHQ/fastmcp/blob/main/dev-docs/v4-notes/protocol-2026.md) describe modern server/discover support, legacy initialize support, cache hints, resource path security and mode negotiation.

Target [Server/pyproject.toml](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml) constrains FastMCP to >=3.0.2,<4, and target [manage_tools.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py) is written around FastMCP 3 per-session visibility. Therefore v4 is a sensible candidate for a controlled spike, not yet a proven drop-in replacement for every target workflow.

## 2. Options to evaluate (do not decide by guesswork)

### Option A — Keep one stable tool catalog in modern mode

Publish a deterministic catalog independent of calls on a transport connection. Tool-group selection could influence invocation eligibility, returned explanations or caller-requested detail, rather than changing tools/list based on earlier calls. If filtering is based on request authentication, follow authorization and cache-scope rules; do not pretend the model's ordinary tool call arguments are credentials.

**Pros:** closest to the new tools-list contract; stable schemas and better prompt-cache behavior.

**Risk:** client UX may expect hidden/disabled tools to be absent entirely; tool-count/context gains may be smaller unless discovery and schema filtering are designed carefully.

### Option B — Use distinct logical servers/configurations for distinct curated surfaces

Expose curated, stable tool catalogs through separate server identities/configurations or independently configured endpoint surfaces. Each client's available list remains stable for that configured endpoint.

**Pros:** context budget and tool set are explicit at setup time; authorization boundaries can be cleaner.

**Risks:** more configuration, duplication, routing and release complexity; ensure users do not get accidental cross-project tools.

### Option C — Rely on the newer discovery/capability mechanisms

Evaluate server/discover, cache hints, pagination and modern extension surfaces before inventing a custom discovery protocol. Keep common high-frequency actions as individual typed tools; broad specialist schemas may be discoverable or curated on demand only if the resulting client workflow remains compatible with the spec and actual MCP clients.

**Pros:** more likely to use protocol-native behavior than a bespoke dispatch layer.

**Risks:** exact client support varies; the optional discovery call is not mandatory and should not become a hidden precondition for every other operation.

**Do not default to one generic call_any_tool dispatcher.** That may reduce schema tokens but gives up typed schemas, discoverability, per-operation safety annotations and approval clarity.

## 3. Compatibility spike plan

Create an isolated branch or environment before updating the production dependency range. Do not modify this research-only branch's runtime code as part of this report.

### Phase 1 — Inspect actual FastMCP use

Audit imports, decorators, context/session access, auth middleware, transports, tool visibility, resources, prompts, tasks/jobs, test fixtures and CLI. Search for every FastMCP 3-specific API and ctx.session_id/per-session registry assumption. Document each v4 API replacement or confirmed compatible behavior.

### Phase 2 — Lock down behavior in existing tests

Add tests before migration:
- tool manifest matches actual registered tools;
- stable tool ordering;
- repeated tools/list does not change because of a preceding manage_tools call on modern mode;
- authorized request-level filtering behaves correctly and does not leak between users/projects;
- tool-group state cannot silently grant permission to privileged calls;
- all resources/prompts/auth/transport routes keep correct instance isolation;
- operation/job/snapshot handles function without a transport session and reject stale/foreign handles;
- response caching cannot reuse private/project-specific results across principals or Unity instances.

### Phase 3 — Protocol matrix

Run tests with:
1. FastMCP 4 client in modern auto mode;
2. FastMCP 4 client pinned to legacy mode;
3. existing supported legacy clients/transports;
4. stateless modern HTTP with more than one server replica or a simulation of requests reaching different instances;
5. stdio and the project's supported HTTP/auth configurations;
6. tool calls that need extra input/approval, if elicitation/input-required flows are used;
7. capability changes and tool-list subscriptions where clients support them.

### Phase 4 — Measure behavior, not just green unit tests

Record:
- initialization/discovery and first-call latency;
- tool-list bytes/tokens and whether order/hashes stay stable;
- context footprint after group selection compared with legacy mode;
- auth failures, cross-project/tenant isolation and retries;
- job/capture continuation after reconnect or alternate server replica;
- test/CI duration and environment compatibility.

### Phase 5 — Make the migration decision

Only update pyproject.toml/lock after the matrix passes. If v4 exposes incompatible behavior, decide whether to shim legacy mode or defer release; don't leave a broad >=3,<5 range without tested semantics. Record the exact FastMCP version and lockfile state in the release evidence.

## 4. Recommended conclusion for now

**Decision: INVESTIGATE; prioritize as P1.**

A modern stateless protocol strategy is needed because of a concrete mismatch between target per-session tool visibility and the new tools/list rule. FastMCP v4 is the leading candidate because it supports both modern and legacy eras, but the application migration surface remains untested. Keep the existing dependency pin until a controlled compatibility spike has a passing test matrix.

### Source references

- [MCP 2026-07-28 Tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)
- [MCP 2026-07-28 announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/)
- [FastMCP v4.0.0 release notes](https://github.com/PrefectHQ/fastmcp/releases/tag/v4.0.0)
- [FastMCP v4 protocol support notes](https://github.com/PrefectHQ/fastmcp/blob/main/dev-docs/v4-notes/protocol-2026.md)
- [Target dependency manifest](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml)
- [Target tool-group manager](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py)
