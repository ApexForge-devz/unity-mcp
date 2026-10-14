# Unity-MCP Research — Decision Synthesis

**Snapshot:** 2026-10-14  
**Target:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp)  
**Research branch:** `research/exhaustive-unity-mcp-2026-10`  
**Reviewed baseline:** `beta` at `142ea0af4890337f0ff6658df61db23982313717`  
**Purpose:** Turn the source, upstream, and selected-fork review into actionable design decisions. This is a synthesis of the existing research, not a claim that implementation or runtime validation is complete.

## Executive position

Keep the existing Python MCP server and Unity C# Editor bridge. Improve their contracts and lifecycle before adding more capability surface. The highest-value work is: (1) enforce a real trust policy for in-process C# execution; (2) make protocol/tool discovery behavior compatible across modern and legacy MCP clients; (3) impose consistent, recoverable result budgets; and (4) make script-first scene generation reliable from source edit through compile, execution, assertions, save and visual review.

**Authoring constraint:** persistent generator source is the source of truth for generated scenes. Change the generator and rerun it; do not build or repair the intended scene through long sequences of one-off GameObject/component mutations.

**Scope constraint:** Unity MCP ↔ Unity Editor/project handling only. Do not add integrations with external or local model/asset-generation systems (including Meshy, ComfyUI, Pinokio, Stable Diffusion, hosted generators or provider APIs).

## Read the synthesis

| Document | Use |
|---|---|
| [Baseline and Gaps](BASELINE_AND_GAPS.md) | What already exists, what is missing, and what must not be rebuilt |
| [Priority Roadmap](PRIORITY_ROADMAP.md) | Ordered recommendations, acceptance gates and deferred work |
| [Script-First Workflow](SCRIPT_FIRST_WORKFLOW.md) | Recommended deterministic generator lifecycle |
| [Protocol and Context](PROTOCOL_AND_CONTEXT.md) | MCP compatibility, tool discovery, schemas and result budgets |
| [Reliability and Safety](RELIABILITY_AND_SAFETY.md) | Trust policy, identity, lifecycle, retries and queue limits |
| [Ecosystem Findings](ECOSYSTEM_FINDINGS.md) | High-signal upstream/fork patterns and their limitations |
| [Validation Plan](VALIDATION_PLAN.md) | Tests, measurements and evidence needed before adoption |
| [Scope and Method](SCOPE_AND_METHOD.md) | Boundaries, evidence labels and coverage limitations |

## Evidence boundary

Findings are based on source/test inspection and the dated repository/PR snapshot. They are not proof of live Unity behavior. No Unity Editor compilation, EditMode/PlayMode run or end-to-end performance benchmark was executed as part of this research pass. Some fork discovery and descendant-diff coverage remains incomplete; treat the ecosystem review as a filtered, source-informed comparison—not an exhaustive census.

The original 42 research files are preserved verbatim under [Research-Backup](../Research-Backup/README.md). That archive is for detailed evidence, source-path notes and historical working analysis; the documents above are the primary decision layer.
