# Unity-MCP Ecosystem Research

**Status:** Active research; fork enumeration and runtime validation remain incomplete.  
**Research branch:** research/exhaustive-unity-mcp-2026-10  
**Target repository:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp)  
**Baseline branch:** beta  
**Started:** 2026-10-14

## Purpose

Systematically evaluate the MCP protocol/server and Unity Editor bridge, their forks and derivative implementations, and independent Unity-editor agent bridges. For scene authoring, use the agent's script-first pattern: persistent generator source is edited and rerun deterministically; direct GameObject/component CRUD is not the primary workflow. Recommend MCP↔Unity improvements that make that path reliable—script edits, compile/reload, generator invocation, targeted validation, diagnostics, safe results, transport and lifecycle handling. See [SCOPE.md](SCOPE.md) and [DETERMINISTIC_SCENE_AUTHORING.md](DETERMINISTIC_SCENE_AUTHORING.md).

This folder is a living research record. Findings must be updated as source revisions change. It is not authorization to implement every proposed idea.

## Current status

- [x] Create a dedicated research branch and durable research folder.
- [x] Record methodology, evidence standards, and scope boundaries.
- [x] Start an ecosystem inventory using several GitHub repository searches.
- [x] Compare the target beta head against upstream beta; both matched the observed HEAD SHA at the snapshot date.
- [x] Compare additional candidate fork heads, including The1Studio, bgr and xOrfe; classify code-level deltas and superseded/stale snapshots.
- [x] Review source samples from selected independent implementations.
- [x] Produce an initial source-linked ranked improvement backlog.
- [x] Expand the candidate register with additional fork searches and specialist repositories.
- [x] Deepen source-level checks of Frame Debugger, Memory Profiler, asset dependency analysis, and The1Studio's divergent fork.
- [x] Re-check Memory Profiler incremental-load budgets, cancellation status polling, and caller-supplied capture/export path boundaries.
- [x] Review an independent operation-level facade's catalog, compaction, idempotency, and result-store source/tests.
- [x] Record remaining limitations, commits and next actions in a dated research log.
- [x] Cross-check the target's open draft PR against relevant upstream PRs and current GitHub Actions results.
- [x] Review the schema compaction middleware and tests for warning retention, measurement quality and protocol-compatibility gaps.
- [x] Inspect the native modal-answering, liveness sampler, dispatcher queue and scene-change guard paths and identify live-Editor test gates.
- [x] Compare external job/result-budget patterns against the target's existing test-job manager, pending-response contract and dispatcher queue.
- [x] Inspect additional upstream lifecycle/configuration PR patches and add source-informed backlog entries for startup readiness, test cleanup, nonblocking UI refresh, config preservation, and prefab overrides.
- [x] Patch-review additional in-scope upstream proposals for asset pagination, argument validation, C# wrapper diagnostics, console filtering, Game View sizing, Undo, package-version safety, and multi-Editor server lifetime.
- [x] Deep-review the shared-server ownership proposal against target PID/EditorPrefs code and shutdown tests; record lifecycle handoff risk.
- [x] Source-review JSON-encoded scalar argument normalization for Read Console and menu execution, including schema validation order and blacklist coverage.
- [x] Deep-review asset search pagination against target ManageAsset source and the upstream PR's asset-load regression tests.
- [x] Source-review C# wrapper aliases, compiler-specific error-line translation, and the accuracy of CodeDom/Roslyn hints.
- [x] Review GameObject deletion's Undo gap against Unity's documented Undo contract and the upstream round-trip regression test; later reclassify it as deferred for the script-first agent workflow.
- [x] Incorporate the agent's deterministic scene-generation/regeneration workflow as the default authoring assumption and downgrade direct scene CRUD recommendations.
- [x] Deep-audit script edit guards, compile/reload readiness, menu-item invocation semantics and missing generator completion receipts.
- [x] Inspect the underlying RefreshUnity, EditorStateCache, ReadConsole and ManageScene C# contracts to separate compile-start/readiness from compile success.
- [x] Compare generator-run receipt requirements with the existing asynchronous Test Runner job/status pattern and its reload-persistence limits.
- [x] Verify a direct Unity CompilationPipeline event path for per-assembly compiler diagnostics and compilation-cycle correlation.
- [ ] Enumerate and classify direct forks of the target/upstream using a reproducible complete listing.
- [ ] Systematically review meaningful forks-of-forks and all fork candidates with ahead-of-upstream code.
- [ ] Diff the high-value candidates file-by-file and map changes to all corresponding target handlers/tests.
- [ ] Complete source review of selected independent implementation subsystems, including Unity-side implementations and tests.
- [ ] Finish mapping every recommendation against the current source and CI tests.
- [x] Complete the MCP modern/legacy compatibility specification matrix from authoritative current sources; retain live client/runtime tests as pending.
- [ ] Run repository tests/builds/benchmarks for any implementation work. No Unity Editor runtime test or performance benchmark has been executed as part of the research recorded here.

## Documents

- [Scope boundary — MCP to Unity only](SCOPE.md)
- [Methodology and evidence standard](METHODOLOGY.md)
- [Repository and fork inventory](REPOSITORY_INVENTORY.md)
- [Current architecture baseline](CURRENT_BASELINE.md)
- [Fork comparison findings](FORK_COMPARISONS.md)
- [Independent implementation comparisons](ALTERNATIVES.md)
- [Consolidated improvement backlog](IMPROVEMENT_BACKLOG.md)
- [Research log and coverage gaps](RESEARCH_LOG.md)
- [Fork candidate register and search coverage](FORK_CANDIDATE_REGISTER.md)
- [Specialist workflow source review](SPECIALIST_WORKFLOWS.md)
- [Asset dependency/reference analysis review](ASSET_DEPENDENCY_ANALYSIS_REVIEW.md)
- [Shared HTTP server lifecycle review](SHARED_HTTP_SERVER_LIFECYCLE_REVIEW.md)
- [JSON-encoded scalar argument normalization review](ARGUMENT_NORMALIZATION_REVIEW.md)
- [Asset search pagination source review](ASSET_SEARCH_PAGINATION_REVIEW.md)
- [Execute Code diagnostics review](EXECUTE_CODE_DIAGNOSTICS_REVIEW.md)
- [GameObject deletion and Undo review — deferred for this agent workflow](GAMEOBJECT_DELETE_UNDO_REVIEW.md)
- [Deterministic script-first scene authoring workflow](DETERMINISTIC_SCENE_AUTHORING.md)
- [Runtime startup readiness proposal review](UPSTREAM_PR_1463_PLAY_STARTUP_REVIEW.md)
- [Asynchronous Editor window refresh proposal review](UPSTREAM_PR_1465_ASYNC_WINDOW_REFRESH_REVIEW.md)
- [Read-only AssetBundle explorer proposal review](UPSTREAM_PR_1466_ASSETBUNDLE_EXPLORER_REVIEW.md)
- [Deterministic scene-generator lifecycle source audit](DETERMINISTIC_GENERATOR_LIFECYCLE_REVIEW.md)
- [Existing scene visual validation capability audit](SCENE_VISUAL_VALIDATION_AUDIT.md)
- [MCP modern/legacy protocol compatibility plan](PROTOCOL_COMPATIBILITY.md)
- [Recent in-scope upstream proposal review (2026-10-14)](RECENT_UPSTREAM_PR_REVIEW_2026-10-14.md)
- [Upstream branch, issue and PR tracking](UPSTREAM_BRANCH_COMPARISON.md)
- [Existing draft PR and upstream overlap](DRAFT_PR_AND_UPSTREAM_OVERLAP.md)
- [Draft PR schema-compaction code review](DRAFT_PR_CODE_REVIEW.md)
- [Draft PR modal liveness and scene-guard review](DRAFT_PR_MODAL_LIVENESS_REVIEW.md)
- [onlyfeng test-status cache review](FORK_ONLYFENG_TEST_STATUS_CACHE.md)
- [Pashara Frame Debugger specialist fork review](FORK_PASHARA_FRAME_DEBUGGER_REVIEW.md)
- [Memory Profiler specialist fork review](FORK_MEMORY_PROFILER_REVIEW.md)
- [Upstream PR #1460 native test cleanup review](UPSTREAM_PR_1460_NATIVE_TEST_CLEANUP_REVIEW.md)
- [Upstream PR #1463 Play Mode startup review](UPSTREAM_PR_1463_PLAY_STARTUP_REVIEW.md)
- [Serraya profiler expansion review](FORK_SERRAYA_PROFILER_REVIEW.md)
- [Cross-repository operational reliability patterns](OPERATIONAL_RELIABILITY_PATTERNS.md)

### Historical references — excluded from active scope

These source reviews are retained only to preserve the research trail. They are **not** implementation recommendations and must not expand the active backlog into external generator/provider integrations.

- [Historical AssetGen download/security review — out of scope](FORK_YKH09242_ASSETGEN_SECURITY.md)
- [Historical Meshy recovery/options review — out of scope](FORK_HAOHAO_MESHY_RECOVERY.md)

## Important interpretation rules

1. A repository returned by a name/description search is a candidate, not necessarily a fork of CoplayDev/unity-mcp.
2. A fork README and advertised tool count are discovery signals only; recommendations need source or test evidence.
3. Code copied from upstream without meaningful divergent commits is not counted as a novel implementation.
4. “Useful” means a concrete pattern plausibly improves this repo; it does not imply the code is safe or suitable to copy.
5. Distinguish facts observed in source, claims made by a project, analysis/inference, and untested hypotheses.
6. This review is point-in-time. Record branch, commit SHA, retrieval date, and relevant file path whenever possible.
7. Do not call the work exhaustive until the direct-fork inventory is complete to a stated API/query boundary and the high-value candidates have been diffed.
8. GitHub compare endpoint errors do not prove a repository is missing or has no useful changes.
9. Static source review and project-reported test counts do not mean this research independently ran the tests.

## Preliminary high-priority questions

- Can the sequential Unity-main-thread batch API have a more honest and compact contract?
- What changes are required to support modern MCP protocol semantics without breaking supported legacy clients?
- How should tool discovery reduce schema/context overhead while retaining typed schemas for common actions?
- Can every tool result be governed by one configurable output budget, including structured content and images?
- How should arbitrary in-process C# execution be gated for local trusted use versus remote/unattended use?
- Are editor-loop liveness diagnostics, mutation retries, idempotency, and local project-file operations sufficiently robust?
- What asset dependency/reference analysis is missing, and how can results be verified without unsafe auto-deletion?
- Does the existing Frame Debugger profiling implementation need event identity/capture-stability fixes like those documented in a specialized derivative?
- Which MCP↔Unity designs improve deterministic script-driven scene generation/regeneration, compilation, validation, diagnostics, and reproducibility—without requiring one-off direct GameObject/component mutation calls?
- External generator, inference, and provider API integrations are excluded even when found in a fork; assess only independently useful MCP/Unity-side patterns.

## Existing internal research

Two earlier local research documents were found on 2026-10-14:
- unity_mcp_optimization_review.md
- unity_mcp_source_audit_and_implementation_backlog.md

They provide a strong starting baseline but do not demonstrate exhaustive enumeration of the fork ecosystem or runtime benchmark execution. The new research records verify and extend those conclusions, without treating them as final.
