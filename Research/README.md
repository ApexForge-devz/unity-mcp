# Unity-MCP Ecosystem Research

**Status:** Active research; not yet exhaustive or implementation-approved  
**Research branch:** `research/exhaustive-unity-mcp-2026-10`  
**Target repository:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp)  
**Baseline branch:** `beta`  
**Started:** 2026-10-14

## Purpose

Systematically evaluate the upstream Unity MCP implementation, its forks and derivative implementations, plus independent Unity-editor agent bridges. Identify concrete improvements that can be adapted to this repository with source evidence, compatibility analysis, risk, and measurable acceptance criteria.

This folder is a living research record. Findings must be updated as source revisions change. It is not authorization to implement every proposed idea.

## Current status

- [x] Create a dedicated research branch and durable research folder.
- [x] Record methodology, evidence standards, and scope boundaries.
- [x] Start an ecosystem inventory from GitHub repository searches.
- [ ] Enumerate and classify direct forks of the target upstream with a reproducible complete listing.
- [ ] Identify and review meaningful forks-of-forks and renamed forks where discoverable.
- [ ] Compare high-value fork diffs with the current `beta` baseline.
- [ ] Review relevant independent implementations at source level.
- [ ] Map every candidate improvement against what this repository already implements.
- [ ] Produce a source-linked, ranked adoption backlog.
- [ ] Validate recommendations against latest MCP and dependency specifications.
- [ ] Run repository tests/builds/benchmarks for any implementation work (research-only findings remain hypotheses until tested).

## Documents

- [Methodology and evidence standard](METHODOLOGY.md)
- [Repository and fork inventory](REPOSITORY_INVENTORY.md)
- [Current architecture baseline](CURRENT_BASELINE.md)
- [Fork comparison findings](FORK_COMPARISONS.md)
- [Independent implementation comparisons](ALTERNATIVES.md)
- [Consolidated improvement backlog](IMPROVEMENT_BACKLOG.md)
- [Research log and coverage gaps](RESEARCH_LOG.md)

Documents listed but not yet present are deliberate work items. Do not interpret a placeholder in the checklist as a completed review.

## Important interpretation rules

1. A repository returned by a name/description search is a **candidate**, not necessarily a fork of CoplayDev/unity-mcp.
2. A fork's README and advertised tool count are discovery signals only; recommendations need source or test evidence.
3. Code copied from upstream without meaningful divergent commits is not counted as a novel implementation.
4. “Useful” means a concrete pattern plausibly improves this repo; it does not imply the code is safe or suitable to copy.
5. Distinguish facts observed in source, claims made by a project, analysis/inference, and untested hypotheses.
6. This review is point-in-time. Record branch, commit SHA, retrieval date, and relevant file path whenever possible.
7. Do not call the work exhaustive until the direct-fork inventory is complete to a clearly stated API/query boundary and the high-value candidates have been diffed.

## Preliminary high-priority questions

- Can the current sequential Unity-main-thread batch API have a more honest and compact contract?
- What changes are required to support modern MCP protocol semantics without breaking supported legacy clients?
- How should tool discovery reduce schema/context overhead while retaining typed schemas for common actions?
- Can every tool result be governed by one configurable output budget, including structured content and images?
- How should arbitrary in-process C# execution be gated for local trusted use versus remote/unattended use?
- Are archive limits, editor-loop liveness diagnostics, mutation retries, and idempotency sufficiently robust?
- What asset dependency/reference analysis is missing, and how can results be verified without unsafe auto-deletion?
- Which alternative designs measurably improve real tasks rather than merely increasing tool count?

## Existing internal research

Two earlier local research documents were found on 2026-10-14:
- `unity_mcp_optimization_review.md`
- `unity_mcp_source_audit_and_implementation_backlog.md`

They provide a strong starting baseline but do **not** demonstrate exhaustive enumeration of the fork ecosystem or runtime benchmark execution. The new research records will verify, update, and extend their conclusions rather than treating them as final.
