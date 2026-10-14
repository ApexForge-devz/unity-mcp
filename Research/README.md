# Unity-MCP Ecosystem Research

**Status:** Active research; fork enumeration and runtime validation remain incomplete.  
**Research branch:** research/exhaustive-unity-mcp-2026-10  
**Target repository:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp)  
**Baseline branch:** beta  
**Started:** 2026-10-14

## Purpose

Systematically evaluate the upstream Unity MCP implementation, its forks and derivative implementations, plus independent Unity-editor agent bridges. Identify concrete improvements that can be adapted to this repository with source evidence, compatibility analysis, risk, and measurable acceptance criteria.

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
- [x] Review an independent operation-level facade's catalog, compaction, idempotency, and result-store source/tests.
- [x] Record remaining limitations, commits and next actions in a dated research log.
- [x] Cross-check the target's open draft PR against relevant upstream PRs and current GitHub Actions results.
- [x] Review the schema compaction middleware and tests for warning retention, measurement quality and protocol-compatibility gaps.
- [x] Inspect the native modal-answering, liveness sampler, dispatcher queue and scene-change guard paths and identify live-Editor test gates.
- [x] Compare external job/result-budget patterns against the target's existing test-job manager, pending-response contract and dispatcher queue.
- [x] Inspect additional upstream lifecycle/configuration PR patches and add source-informed backlog entries for startup readiness, test cleanup, nonblocking UI refresh, config preservation, and prefab overrides.
- [ ] Enumerate and classify direct forks of the target/upstream using a reproducible complete listing.
- [ ] Systematically review meaningful forks-of-forks and all fork candidates with ahead-of-upstream code.
- [ ] Diff the high-value candidates file-by-file and map changes to all corresponding target handlers/tests.
- [ ] Complete source review of selected independent implementation subsystems, including Unity-side implementations and tests.
- [ ] Finish mapping every recommendation against the current source and CI tests.
- [ ] Complete the MCP modern/legacy compatibility specification matrix from authoritative current sources.
- [ ] Run repository tests/builds/benchmarks for any implementation work. No Unity Editor runtime test or performance benchmark has been executed as part of the research recorded here.

## Documents

- [Methodology and evidence standard](METHODOLOGY.md)
- [Repository and fork inventory](REPOSITORY_INVENTORY.md)
- [Current architecture baseline](CURRENT_BASELINE.md)
- [Fork comparison findings](FORK_COMPARISONS.md)
- [Independent implementation comparisons](ALTERNATIVES.md)
- [Consolidated improvement backlog](IMPROVEMENT_BACKLOG.md)
- [Research log and coverage gaps](RESEARCH_LOG.md)
- [Fork candidate register and search coverage](FORK_CANDIDATE_REGISTER.md)
- [Specialist workflow source review](SPECIALIST_WORKFLOWS.md)
- [MCP modern/legacy protocol compatibility plan](PROTOCOL_COMPATIBILITY.md)
- [Upstream branch, issue and PR tracking](UPSTREAM_BRANCH_COMPARISON.md)
- [Existing draft PR and upstream overlap](DRAFT_PR_AND_UPSTREAM_OVERLAP.md)
- [Draft PR schema-compaction code review](DRAFT_PR_CODE_REVIEW.md)
- [Draft PR modal liveness and scene-guard review](DRAFT_PR_MODAL_LIVENESS_REVIEW.md)
- [ykh09242 AssetGen download and ZIP security review](FORK_YKH09242_ASSETGEN_SECURITY.md)
- [haohao-0407 Meshy recovery review](FORK_HAOHAO_MESHY_RECOVERY.md)
- [onlyfeng test-status cache review](FORK_ONLYFENG_TEST_STATUS_CACHE.md)
- [Cross-repository operational reliability patterns](OPERATIONAL_RELIABILITY_PATTERNS.md)

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
- Are archive expansion limits, editor-loop liveness diagnostics, mutation retries, and idempotency sufficiently robust?
- What asset dependency/reference analysis is missing, and how can results be verified without unsafe auto-deletion?
- Does the existing Frame Debugger profiling implementation need event identity/capture-stability fixes like those documented in a specialized derivative?
- Which alternative designs measurably improve real tasks rather than merely increasing tool count?

## Existing internal research

Two earlier local research documents were found on 2026-10-14:
- unity_mcp_optimization_review.md
- unity_mcp_source_audit_and_implementation_backlog.md

They provide a strong starting baseline but do not demonstrate exhaustive enumeration of the fork ecosystem or runtime benchmark execution. The new research records verify and extend those conclusions, without treating them as final.
