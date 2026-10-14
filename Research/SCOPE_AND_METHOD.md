# Scope and Evidence Method

**Snapshot:** 2026-10-14. See [README](README.md) for target and baseline identifiers.

## Scope

Research only improvements to the existing MCP-to-Unity path: protocol/tool contracts, the Python server, Unity C# Editor bridge, transport, project/Editor discovery and ownership, dispatch/readiness, C# script authoring, compilation and reload, deterministic scene generators, tests/builds, project assets, and targeted visual/profiling diagnostics.

### Explicitly excluded

Do not propose or implement integrations with separate asset/content generation or inference systems: Meshy, ComfyUI, Pinokio, Stable Diffusion, hosted image/3D/audio/video providers, SDKs/credentials, remote provider jobs, or provider artifact pipelines. Unity Editor APIs and MCP protocol handling are in scope; external generation orchestration is not.

### Script-first authoring rule

Generated scene state is owned by persistent project source. Edit the generator, validate and compile it, execute it through a controlled Editor entry point, check targeted assertions, then inspect visuals only as needed. Direct GameObject/component CRUD and prefab-override mutation remain valid product capabilities for other workflows, but they are not the intended agent's primary authoring strategy.

## Baseline discipline

- Primary target: `ApexForge-devz/unity-mcp`, default branch `beta`.
- Closest upstream: `CoplayDev/unity-mcp`, branch `beta`.
- At the 2026-10-14 snapshot, the target and upstream beta heads were observed to match at `142ea0af4890337f0ff6658df61db23982313717`. This is a historical comparison, not a guarantee about later heads.
- The reviewed upstream/main comparison showed that branch's history was older than beta at that snapshot; do not rebase onto an older branch merely because it is named `main`.
- Verify the current target code, tests, upstream issue/PR status and exact base before adopting any recommendation.

## Evidence labels

- **Observed in source:** behavior or structure directly visible in the inspected revision.
- **Covered by test source:** a regression test exists; this does not mean it ran in this research session.
- **CI/author-reported:** claims from a PR description or workflow, not independently reproduced here.
- **Inference / risk hypothesis:** plausible failure mode derived from control flow or contracts; reproduce it before calling it a confirmed defect.
- **Recommendation:** proposed work, not a description of current behavior.

Prefer a narrow, independently useful patch with a known failure mode over copying an entire fork. Record the repository, revision, relevant files/symbols, baseline difference, dependency/license constraints, and tests before implementation.

## Coverage limits

Fork discovery and descendant review are incomplete. GitHub search is not a complete fork census; selected comparisons were prioritized for substantive deltas. The review did not run a live Unity Editor, Unity compilation, EditMode/PlayMode tests, client compatibility matrix, or benchmark. See [Ecosystem Findings](ECOSYSTEM_FINDINGS.md) and [Validation Plan](VALIDATION_PLAN.md).
