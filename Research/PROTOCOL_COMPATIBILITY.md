# MCP 2026-07-28 and FastMCP 4 Compatibility Review

**Reviewed:** 2026-10-14  
**Target:** ApexForge-devz/unity-mcp `beta`  
**Target HEAD at review:** `142ea0af4890337f0ff6658df61db23982313717`  
**Decision:** Do a controlled protocol-mode spike; do not upgrade the pinned dependency on version number alone.  
**Validation state:** Protocol text and target source inspected; no integration tests or Unity runtime tests run.

## Executive finding

The key incompatibility is now concrete, not hypothetical. The published [MCP 2026-07-28 tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) permits the available tool set to change over time and to vary by authorization presented on each request. It also says the set **must not vary per connection or as a side effect of other requests on that connection** and recommends deterministic order.

The target's `manage_tools` implementation explicitly enables/disables registered components through FastMCP 3 per-session visibility rules, and `tools/list` is expected to change after those calls. The target pins `fastmcp>=3.0.2,<4`. FastMCP 4 was announced GA on 2026-08-31 and states it supports the 2026-07-28 protocol while retaining compatibility with older clients. That makes a controlled migration possible in principle, but it does not establish that the target's existing session-visibility API, private context API, tests and transports work unchanged.

**Recommendation:** separate protocol compatibility from tool-catalog/context design. Keep the existing pin until a modern/legacy matrix passes. Modern mode must not depend on mutable transport-session visibility.

## 1. Current target behavior confirmed from source

### Dependency boundary

Target [`Server/pyproject.toml`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml), blob `83800f55c65984102b61a035613a2ca40459eb91`, pins `fastmcp>=3.0.2,<4` and `mcp>=1.16.0`.

### Session-dependent group visibility

Target [`manage_tools.py`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py), blob `b8875e4d5fd3673c26063c26681b31fb43703fd3`, declares that tool groups can be activated/deactivated for the current session and says this uses FastMCP 3.x native per-session visibility. Its implementation calls:

- `ctx.enable_components(...)` on activation.
- `ctx.disable_components(...)` on deactivation.
- `ctx.reset_visibility()` on reset.
- `ctx._get_visibility_rules()` to reconstruct visible group state.

The final call is a private API, making API compatibility an explicit migration check. This behavior is coupled to both the protocol/session model and FastMCP internals, not only to the `FastMCP` constructor or import path.

### Consequence

A blind change to `fastmcp<4` → `fastmcp>=4` could expose incompatibilities in at least three layers: negotiated protocol behavior, session/tool visibility, and private context APIs. Tests that only import the package or register a basic tool are insufficient.

## 2. Protocol-mode compatibility matrix

| Concern | Legacy mode (2025-11-25 family) | Modern mode (2026-07-28) | Required target behavior |
|---|---|---|---|
| Protocol lifecycle | `initialize` / `initialized` handshake and protocol session header are part of the older flow. | The release retires the handshake and `Mcp-Session-Id`; each request is self-describing. | Negotiate by client mode; do not require a legacy session before handling modern requests. |
| Request identity | Session state may be associated with the negotiated legacy connection. | Protocol-level session state cannot be the only carrier of application state; use request metadata, authenticated identity or explicit application handles. | Project and Unity-instance identity must remain unambiguous across reconnects and independent HTTP requests. |
| `tools/list` catalog | Existing session visibility behavior may be preserved when the client negotiated a legacy mode and the SDK still supports it. | List contents must not vary per connection or because an earlier request changed visibility. Differences based on per-request authorization are allowed. | Keep the legacy path tested; use a stable catalog or fixed configured server surface for modern mode. |
| Catalog order and caching | Preserve existing client-compatible ordering. | List results support `ttlMs` and `cacheScope`; order should be deterministic. | Stable ordering; cache scope must never share project/user-private lists. Use zero TTL/private scope until stability and identity boundaries are demonstrated. |
| HTTP headers | Existing legacy transport contract and clients must continue to work. | Streamable HTTP modern requests use protocol metadata plus `Mcp-Method` and `Mcp-Name` routing headers. | Verify both directions: accepted modern requests include required headers; header/body disagreement and missing required headers produce defined errors. Do not break stdio. |
| Discovery | Legacy clients typically discover through the existing initialization/tool-list flow. | `server/discover` is available but optional. | No normal tool call should depend on a prior optional discovery call. |
| Tool-list change notifications | Preserve behavior expected by supported clients. | Current spec uses modern subscription/list-change semantics; list invalidation and cache lifetime need explicit tests. | Test per mode, not with one assumed notification mechanism. |
| Long-lived application work | Existing job manager/status handles remain useful. | The protocol is stateless, but Unity Editor state and background jobs are still stateful resources. | Continue using explicit job/snapshot/capture/result handles with ownership, expiry and stale-ID checks. Do not confuse protocol statelessness with an actually stateless Unity Editor. |
| Security | Existing trusted-local assumptions and server policy remain. | Per-request authorization may justify a request-specific catalog; a previous tool call's arguments do not constitute credentials. | Never use group selection as authorization, and never let hidden/visible status bypass final-route permission checks. |

Sources: [MCP 2026-07-28 announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/), [current tools specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [FastMCP 4 GA notes](https://blog.gofastmcp.com/3mufbh2vcv22o).

## 3. Catalog design choices for modern mode

The protocol requires a stable list relative to connection/session side effects, not a specific user interface or catalog architecture. Evaluate the following choices against measured task completion and tool-context cost.

### Option A — Stable catalog, invocation policy separate from visibility

Modern `tools/list` returns the same deterministic catalog. A group toggle can explain groups, show status, or affect a documented capability/configuration state, but cannot mutate the list as a connection-side effect. Every tool call still checks final authorization and whether the underlying Editor capability is actually supported.

**Advantages:** simplest protocol contract and avoids session-visibility dependence.  
**Costs:** disables some current schema/context savings unless a separate discovery/catalog approach is added.

### Option B — Fixed, curated server endpoints/profiles

Configure different logical server endpoints or registrations with distinct, fixed catalog profiles. A profile's tool list is stable because the endpoint is configured for that catalog, rather than having the same connection mutate its list.

**Advantages:** preserves bounded tool sets and clearer context budgets.  
**Costs:** additional configuration and maintenance; endpoint and project identities must not cross. Each server/profile needs deterministic list tests and clear naming.

### Option C — Request-authorization-based catalog filtering

Only use a different list when the request's validated authorization actually differs and defines the available capability set. The 2026-07-28 specification permits this. Do not simulate it with a hidden mutable group variable, a client ID alone, or ordinary tool-call arguments. The target's local configurations must not invent authorization where none exists.

### Option D — Single generic `call_any_tool` facade

Do not adopt by default. It can shrink the listed tool catalog but moves typed schemas into a wrapper argument, weakens client discovery and makes per-operation annotations/review harder. Benchmark it against the current catalog and existing core-first draft before considering it.

## 4. FastMCP 4 migration gate

FastMCP's [GA announcement](https://blog.gofastmcp.com/3mufbh2vcv22o) (2026-08-31) reports support for the 2026-07-28 protocol and older clients. Treat this as SDK capability evidence, not proof of target compatibility.

Before modifying the dependency range, audit:

1. `Context` APIs: `enable_components`, `disable_components`, `reset_visibility`, and especially private `_get_visibility_rules`.
2. Tool/resource/prompt registration wrappers and visibility filtering.
3. Middleware and context access in both HTTP and stdio paths.
4. Session-state assumptions in `manage_tools`, `unity_instance_middleware`, connection selection, and tool/resource registries.
5. Modern headers and protocol metadata; errors when the header set is invalid or mismatches the body.
6. Tool result serialization, annotations/schema parity, list caching hints and change notifications.
7. Python 3.10+ supported range, Windows/macOS/Linux packaging, lockfile and CLI startup.

No dependency-range relaxation until a specific FastMCP 4 version is locked and those API checks plus the protocol tests pass.

## 5. Required compatibility test matrix

| Test | Setup | Pass condition |
|---|---|---|
| Stable catalog | Modern client calls `tools/list`, invokes `manage_tools`, then lists again. | Tool names/order do not change because the preceding tool call mutated connection/session visibility. |
| Legacy groups | Client forced/pinned to legacy protocol. | Existing activate/deactivate/reset behavior works if preserved for legacy mode. |
| Concurrent callers | Two clients call group and regular tools concurrently. | No cross-client or cross-project visibility leakage. |
| Request authorization | Two validated authorization contexts with different allowed capability sets, if auth is supported. | Catalog differences follow only the authoritative request credentials. |
| Reconnect/stateless request | Modern HTTP calls are sent without a protocol session, including a simulated route to another server process. | Every call routes to the intended project/Unity instance using explicit identity; no hidden connection affinity is required. |
| Required HTTP metadata | Modern Streamable HTTP requests with correct, missing and inconsistent standard routing/version headers. | Correct requests succeed; unsupported/invalid cases fail with deterministic diagnostics. |
| Cache hints | Stable and changing lists, private/project-specific catalog. | Stable deterministic order; TTL and cache scope match actual stability/privacy; one project's catalog is not reused for another. |
| Schema parity | Enumerate registered tools and compare visible names, input schemas, required/default fields and annotations with the registry. | No drift, missing required arguments, or exposure of injected server context. |
| Change notification | Change the catalog through any supported reconfiguration path and connect modern/legacy listeners. | Only supported semantics are used; cached lists expire or invalidate correctly. |
| Existing workflows | Tool groups, test polling, scene/resource reads, custom tools, auth/CLI/client setup on all supported transports. | No behavior regression after protocol negotiation or SDK migration. |

Record protocol version, FastMCP version, transport, client identity and result for every case. Existing Python tests, a real protocol client test, and live Unity Editor integration tests are separate layers; one cannot substitute for the others.

## 6. Decision

**Status: P1 — investigate; do not yet implement a dependency upgrade.**

The current target's per-session component visibility is the specific modern-protocol conflict. The leading next step is an isolated prototype that runs FastMCP 4 against the existing tests, makes the modern catalog stable, and preserves legacy group behavior only where the negotiated protocol supports it. Compare the stable catalog and fixed-profile endpoint options using schema token cost, operation discoverability, additional round-trips, isolation and end-to-end Unity task success.

## Sources

- [MCP 2026-07-28 Tools specification — capabilities, stable lists, deterministic order and cache hints](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)
- [MCP 2026-07-28 specification announcement — stateless protocol and headers](https://blog.modelcontextprotocol.io/posts/2026-07-28/)
- [FastMCP 4 GA announcement — dual modern/legacy support and breaking changes](https://blog.gofastmcp.com/3mufbh2vcv22o)
- [FastMCP v4.0.0 release notes](https://github.com/PrefectHQ/fastmcp/releases/tag/v4.0.0)
- [Target dependency manifest](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/pyproject.toml)
- [Target session-based tool group manager](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_tools.py)
