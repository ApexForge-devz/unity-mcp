# Research Methodology

**Last updated:** 2026-10-14  
**State:** Initial methodology; coverage is in progress.

## 1. Research questions

1. What is the current architecture and capability surface of this repository's `beta` baseline?
2. Which direct forks and meaningful derivatives contain changes absent from the baseline?
3. Which independent Unity MCP implementations provide demonstrably useful design patterns?
4. Which proposed features are already present, incomplete, or missing in this repo?
5. What is the safest, smallest implementation plan to improve real agent success, safety, maintainability, latency, and context efficiency?

## 2. Target and baseline discipline

Primary target: [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp), default branch `beta`.

Closest upstream: [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp), default branch `beta`.

A previous audit observed both heads at `142ea0af4890337f0ff6658df61db23982313717` (2026-10-08). This is a **historical observation**, not proof the branches remain equal. Recheck branch heads/compare results before using that claim for current recommendations.

For every source-level observation, aim to capture:
- Full repository name and URL.
- Branch/ref and exact commit SHA or observed revision date.
- File path(s), relevant symbol(s), and line links when available.
- Whether the finding is directly observed, README-described, inferred, or an unverified hypothesis.
- Difference from the target baseline.
- Adaptation cost, dependencies, risks, licensing implications, and validation required.

## 3. Fork and candidate discovery

Use multiple complementary methods; no single repository-name search is complete.

1. Search GitHub repository metadata for `unity-mcp`, `UnityMCP`, `unity-editor-mcp`, `unity-mcp-server`, and relevant Unity MCP terms.
2. Query the target upstream's direct forks from GitHub's forks API when the available connector/API permits it. Paginate and record the retrieval timestamp and API limits.
3. Search for renamed forks and descendant forks using repository descriptions, README links, commit ancestry, fork metadata, and exact-source search.
4. Record all discovered candidates before filtering.
5. Deduplicate by repository identity and source ancestry; do not discard a fork only because it has little activity.
6. Bucket candidates: exact/near mirror, meaningful fork, renamed or packaged distribution, independent implementation, archived/stale, unrelated false positive, or unable to classify.
7. For meaningful candidates, compare current source against the nearest known ancestor/baseline; inspect changed commits, not just default branch README text.

**Completeness boundary:** GitHub search can rank and limit results; search results alone do not prove a complete fork inventory. If the full fork API is unavailable, rate-limited, or not paginated to completion, explicitly mark discovery incomplete. A large fork count cannot be represented honestly by sampling a few prominent entries and calling it exhaustive.

## 4. Source review rubric

Score candidate contributions across these areas:

- MCP protocol/version compatibility and transport correctness.
- Tool discovery, schemas, annotations, grouping, prompts, resources, and context/token consumption.
- Bounded/structured results, pagination, stable handles, image payloads, and continuation semantics.
- Unity main-thread scheduling, responsiveness, cancellation, retries, idempotency, and multi-instance isolation.
- Scene/GameObject/component/prefab operations and Undo behavior.
- C# authoring/editing, compile validation, tests, Play Mode, and build workflows.
- Asset search, metadata, dependency/reference graphs, prefab usage, and safe cleanup analysis.
- Profiling, diagnostics, logs, screenshots and repeatable visual iteration.
- Security policy for remote transport, authentication, arbitrary C#/shell execution, destructive actions, untrusted archives, and path validation.
- Tests, CI matrix, packaging/release, version compatibility, docs, license/provenance, and maintainer activity.

Do not prioritize feature count. Prefer evidence of correctness, task success, safety, testability, bounded resource usage, and maintainability.

## 5. Recommendation classifications

- **ADOPT:** High confidence that a pattern fits, with implementation/test plan.
- **ADAPT:** Useful design requiring changes for this architecture.
- **INVESTIGATE:** Plausible benefit but important source/benchmark/compatibility questions remain.
- **DEFER:** Potentially useful but lower leverage or costly.
- **REJECT:** Duplicate, unsafe, misleading, unmaintainable, or not supported by evidence.
- **ALREADY PRESENT:** Existing functionality; possibly needs verification/refinement rather than a new subsystem.

## 6. Experimental discipline

Static source inspection does not establish runtime performance. Do not claim latency, memory, reliability, or task-success improvements without measurements. For any implementation, define before/after tests:
- Contract/unit tests.
- Existing Python and Unity EditMode/PlayMode regression suites.
- Unity version/client/protocol compatibility matrix.
- Latency p50/p95, response bytes/tokens, schema bytes/tokens, allocations/peak memory where relevant.
- Main-thread block time, retries, duplicate mutations, and failure-recovery behavior.
- Representative end-to-end Unity tasks with recorded success/failure.

Research-only recommendations will include an acceptance-test proposal rather than implying tests were run.

## 7. Reporting and commits

Commit logical research increments on `research/exhaustive-unity-mcp-2026-10`. Keep inventory, observations, and the recommendation backlog separate so that a later researcher can update one area without rewriting everything. Each research update should state newly covered repos, changed findings, unresolved gaps, and its source snapshot date.

The research branch must not change runtime code unless a separately scoped implementation task is explicitly started.
