# Ecosystem Findings — What to Reuse, Not What to Copy

**Snapshot:** 2026-10-14. The branch review is a selective comparison; direct-fork and fork-of-fork discovery is not complete. Recheck each upstream PR/repository before implementation.

## Core conclusion

At the snapshot, target `beta` matched `CoplayDev/unity-mcp` beta. Most sampled forks were stale mirrors or had non-code changes. The strongest evidence supports selective adaptation of narrow correctness, context-budget, identity and lifecycle patterns—not wholesale porting and not a new mandatory server.

## High-signal candidates

| Source | Useful pattern | Decision / caveat |
|---|---|---|
| [AnkleBreaker Studio](https://github.com/AnkleBreaker-Studio/unity-mcp-server) | Progressive discovery, compact output, final-route permissions and Editor identity checks | Adapt principles at existing target seams; benchmark context gains |
| [KitWright](https://github.com/kitwright/unity-mcp) | Serialized output caps, spill/continuation handles, export profiles and stall diagnostics | Adapt a universal result budget; test storage scope, expiry and oversized raw payloads |
| [Pushkar-Env](https://github.com/pushkar-env/unity-mcp) / [Funplay](https://github.com/funplay-ai/unity-mcp) | Explicit trust tiers and broader filesystem-policy cases | Use as policy/test design references only; source filtering is not a C# sandbox |
| [Ozan Kasikci](https://github.com/ozankasikci/unity-editor-mcp) | Project/worktree-aware Editor discovery and clearer routing diagnostics | Verify against the target's existing multi-instance routing |
| [Vangardo efficient facade](https://github.com/Vangardo/unity-mcp-efficient) | Operation catalog, compact responses and result recovery store | Benchmark only before architecture change; adds a separate server and catalog drift/identity/idempotency risks |
| [Pashara Frame Debugger derivative](https://github.com/pashara/Unity-MCP-CoplayDev-FrameDebugger) | Capture/event-index consistency and non-overwriting installation behavior | Map against current profiler implementation before adopting; specialized derivative, not a broad replacement |
| [The1Studio fork](https://github.com/The1Studio/unity-mcp) | Checked serialized-value writes, shared parsing and write-back/readback tests | Selective correctness patterns only; branch is heavily diverged and stale; no wholesale cherry-pick |
| [VRCLearn fork](https://github.com/VRCLearn/unity-mcp) | VPM distribution/release validation and localization | Packaging workflow may be useful if distribution requires it; localization is product-dependent |
| [czmomocha derivative](https://github.com/czmomocha/unity-coplayDev-mcp) | Puerts/TypeScript project management and custom-tool guidance | Highly stale and project-specific; only adopt if that workflow becomes an explicit requirement |
| [Memory Profiler extension](https://github.com/SabriBoughanmi/Unity-MemoryProfiler-MCP) | Queryable object-level snapshot analysis | Meaningful optional gap beyond current capture/list/file-level comparison; API/package/version validation required |

Candidate names and comparisons are preserved in [Research-Backup/FORK_CANDIDATE_REGISTER.md](../Research-Backup/FORK_CANDIDATE_REGISTER.md), [FORK_COMPARISONS.md](../Research-Backup/FORK_COMPARISONS.md) and [ALTERNATIVES.md](../Research-Backup/ALTERNATIVES.md).

## Fork-specific cautions

- **The1Studio:** broad divergence; checked numeric coercion and serialized struct/vector write-back are promising narrow patterns. Input simulation and run-in-background recovery need version-specific tests. DOTS/ECS tooling is optional/deferred.
- **bgr:** a small ahead delta includes reconnection/preflight behavior, but the fork is far behind upstream. Reproduce the relevant lifecycle problem before porting.
- **xOrfe:** the inspected configuration-path changes appeared already superseded in target beta; no port justified from the reviewed delta.
- **VRCLearn:** its meaningful delta is mainly distribution/localization, not a core MCP bridge replacement.
- Many sampled mirrors were simply behind upstream or had personal notes only; age/ahead counts alone do not indicate usable improvements.

## Upstream proposals already reviewed

The 2026-10-14 snapshot included these overlapping proposals; check current merge state and exact patch before planning duplicates:
- [#1448](https://github.com/CoplayDev/unity-mcp/pull/1448) — filter MCP bridge diagnostics from project Console queries.
- [#1450](https://github.com/CoplayDev/unity-mcp/pull/1450) — fixed Game View size/readback.
- [#1461](https://github.com/CoplayDev/unity-mcp/pull/1461) — custom MCP server instructions.
- [#1463](https://github.com/CoplayDev/unity-mcp/pull/1463) — Play Mode startup readiness.
- [#1465](https://github.com/CoplayDev/unity-mcp/pull/1465) — nonblocking Editor window refresh.
- [#1466](https://github.com/CoplayDev/unity-mcp/pull/1466) — read-only built AssetBundle explorer.
- [#1470](https://github.com/CoplayDev/unity-mcp/pull/1470) — actionable no-session recovery guidance.
- [#1472](https://github.com/CoplayDev/unity-mcp/pull/1472) — custom-tool parameter-order registration regression.

Also review [target draft PR #1](https://github.com/ApexForge-devz/unity-mcp/pull/1) as one package: Core-first discovery/schema compaction, modal liveness and dialog answering, scene-change guard, plus additional Unity 6 enhancements. Its existence is not validation; the snapshot PR description said local tests/Unity Editor tests had not been run, and live/CI outcomes must be checked directly.

## Specialist notes

- Asset dependency graphs are useful only with proven direct/transitive reference semantics, reverse-cache invalidation, package/folder handling and conservative “potentially unused” labels. Never auto-delete results.
- Memory snapshot query tools extend beyond the target's current file-level capture/list/compare; isolate behind a version/package capability check.
- Undo/rollback and “atomic batch” flags must not promise rollback for non-Undo side effects.
- Respect licenses: some specialist implementations contain proprietary source. Learn generic concepts without copying incompatible code.

## Evidence boundary

The full working register, individual source reviews, timestamps and unresolved candidates live in [Research-Backup](../Research-Backup/README.md). No branch census or runtime behavior should be inferred from this selective sample.
