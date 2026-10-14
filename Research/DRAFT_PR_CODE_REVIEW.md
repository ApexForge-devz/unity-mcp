# Draft PR #1 — Source-Level Review of Core-First Discovery and Schema Compaction

**Target PR:** [ApexForge-devz/unity-mcp #1](https://github.com/ApexForge-devz/unity-mcp/pull/1)  
**Reviewed head:** 9480a79408422c95c17c08ed4efb0b66d24a1f24 (feature/unity6-enhancements)  
**Base:** beta at 142ea0af4890337f0ff6658df61db23982313717  
**Review date:** 2026-10-14  
**Scope:** Static source/test review of schema compaction, on-demand tool details and group catalog. This is not an implementation approval or a completed runtime benchmark.

## 1. Implementation reviewed

Source files:
- [tool_schema_optimizer.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/tool_schema_optimizer.py), blob 1a701f2846a6f05bb4f1bd82b9cfb7f37ab20a07.
- [test_tool_schema_optimizer.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/tests/test_tool_schema_optimizer.py), blob 4fb58ebf9c0fe7b13685ff7a04fe22d5de9a45c8.
- [get_tool_details.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/tools/get_tool_details.py), blob 23078ce245fd4f3ad8a8b3fecf19cfccc1f0f943.
- [manage_tools.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/tools/manage_tools.py), blob 58fc655a7448417e1d69090412103bd0e87c0da8.
- [tool_registry.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/registry/tool_registry.py), blob 39245e43766e23dd493d41ab4b9ca71184055c76.
- [tool_registry metadata tests](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/tests/test_tool_registry_metadata.py), blob 31e437140ba691ec402877f9ce5ec6a9525b825e.

### What the implementation does correctly

- The schema optimizer is only called from FastMCP's tool-list middleware. It creates a compact copy rather than editing a registered tool in place.
- It shortens prose descriptions; it does not rewrite parameter names, types, required lists, enum values, defaults, bounds, or additionalProperties.
- It shortens descriptions in both parameter schemas and output_schema where present.
- get_tool_details returns the full registered definition (description, parameters, output schema, annotations and tags) on demand, without executing that tool.
- The optional-group catalog is generated from the current tool registry, sorted by group and tool name, and uses a short first-sentence summary. Core is omitted from the optional list; server-only meta tools are always visible.
- Group activation accepts multiple groups in one request and validates all names before applying a visibility rule; the intended goal is one list-change notification for a batch of group changes.
- Existing unit tests check no mutation of the original tool/schema, preserved validation constraints and tags, output schema compaction, and group-catalog generation from registered tools.

These are useful safety properties for a schema-shortening layer.

## 2. Important limitations to resolve before merge

### 2.1 A length cap can remove semantic warnings or preconditions

The implementation normalizes whitespace and cuts text at a word boundary to 220 characters for the tool description and 140 for schema description fields. It does not distinguish “background explanation” from safety-critical text.

A description can put a destructive-operation warning, project-root constraint, required refresh step, version caveat, supported action list, or recovery instruction at the end. If that sentence is removed, the JSON schema may be structurally identical while its semantics become less safe. The full tool definition remains available via get_tool_details, but the model may not know it needs to call that tool before performing the operation.

**Recommendation:** compact from curated summaries where available, rather than a blind prefix. Preserve full prose for destructive/high-risk operations and for descriptions explicitly marked as containing warnings/preconditions. At minimum, make the compact form contain a stable “details/preconditions available via get_tool_details” note and mark whether truncation occurred. Do not rely on the model to infer that a tail was removed.

### 2.2 The tests are unit fixtures, not a whole-catalog quality evaluation

The test suite uses small FakeTool definitions and verifies schema preservation. It does not measure:
- serialized bytes/token count of the actual complete beta tool catalog;
- how many tools/actions lose essential warnings under compaction;
- whether a model can still choose and correctly parameterize operations from compact descriptions;
- real lookup rate, extra round trips or task-success change;
- differences between tools with long legal/diagnostic constraints and ordinary CRUD tools.

The broader PR workflow has Python test and compile/doc evidence, but the live Unity test and bridge E2E jobs were skipped for missing license secrets. The schema experiment has no independently reproduced task-level benchmark in this research pass.

**Recommendation:** add a deterministic whole-registry before/after report and a curated evaluation set where compact-list calls are paired with original full definitions. Measure actual target-model tokenization when possible; character-count/4 may be reported as an estimate only. Include adversarial descriptions with warnings at the beginning, middle and end, non-English prose, long inline code/paths, and descriptions with multiple sentence abbreviations.

### 2.3 Core-first group activation is not the same as operation-level schema discovery

The draft keeps optional operations behind group activation. This is a practical context reduction, but activating a group makes its tool set visible together; it still does not give an operation-level action catalog and action-specific parameter schema for tools with generic manager/action inputs.

The independent [Vangardo/unity-mcp-efficient review](ALTERNATIVES.md) describes a different trade-off: stable six-tool façade and an on-demand catalog of individual actions. That approach can lower the visible schema surface further, but it introduces action allowlists/schema drift, a second server process, generic-call annotations and result-store isolation risks.

**Recommendation:** first benchmark the current Core-first solution, then compare a minimal internal operation catalog on the same target beta tool schemas. Don't require a separate facade without evidence. Keep exact named actions, per-operation annotations and full-schema retrieval; fuzzy search should suggest candidates, never auto-execute a mutation.

### 2.4 MCP protocol compatibility remains unresolved

The draft uses FastMCP 3.x per-session visibility: activating/deactivating a group changes the tools listed to that session. The published MCP 2026-07-28 Tools specification says tools/list must not vary per connection or as a side effect of another request on that connection. The protocol now has a stateless transport core, although application resources may still be stateful behind explicit IDs.

Consequently, these are two distinct concerns:
1. Description compaction and on-demand full schema retrieval can remain useful.
2. Changing tools/list after a prior group-toggle call needs a protocol-mode-aware design before the branch can claim compatibility with the new spec.

**Recommendation:** keep the draft's FastMCP 3 behavior as a legacy-mode candidate, but include it in the modern/legacy compatibility spike in [PROTOCOL_COMPATIBILITY.md](PROTOCOL_COMPATIBILITY.md) before upgrading dependency bounds or presenting the feature as protocol-current.

### 2.5 get_tool_details should stay a metadata route, not an alternate dispatch surface

The current implementation returns a tool's full registered definition, including annotations, but not a way to invoke a hidden tool. That's the right separation. Maintain these constraints:
- exact tool name is required; ambiguous search results must not be resolved inside this endpoint;
- its response should never imply that a hidden tool is currently callable or enabled;
- optional-group tool metadata should include whether the group is currently enabled if the client can obtain that status consistently;
- authorizations for listing/invoking high-risk tools remain at final execution dispatch, not at metadata lookup.

A disabled tool's schema may be discoverable without silently re-enabling it. Test that getting details leaves visibility rules unchanged.

## 3. Required validation before calling the schema approach successful

1. **Actual catalog size:** serialize the full registered tools/list on beta and draft branches using the same serializer, report tool count, total chars, actual tokenizer tokens for relevant clients, and size by descriptions vs parameters vs output schemas.
2. **Description safety review:** identify any compacted description that loses a constraint, warning, allowed action, required workflow step, or error-recovery hint. Use a curated registry-wide review rather than a random handful of tools.
3. **Capability lookup:** create 50–100 realistic task phrases across assets, scene editing, scripts, tests, shaders/VFX, profiling, packages, and destructive workflows. Measure top-1/top-3 choice accuracy, wrong-action rate, and how often a full schema lookup is needed. Include multilingual/typo cases only if they are product requirements.
4. **End-to-end tasks:** compare beta, draft Core-first and (if implemented) operation-catalog variants on the same fixed Unity tasks. Record calls, round trips, tokens, failures and task success. Run Unity EditMode tests and the bridge smoke job; skipped jobs are not pass results.
5. **Protocol matrix:** test legacy FastMCP 3 visibility separately from the current stateless MCP tools/list behavior. Verify tool-list order/cache stability and explicit invocation denial for hidden groups.
6. **Safety annotations:** confirm compact schema output preserves output schema/annotations and that destructive operations retain the warnings/metadata required by clients for approvals.

## 4. Interim conclusion

**Assessment: useful foundation, not yet evidence of production-level schema efficiency or safety equivalence.** Keep the copy-on-list and schema-preservation design. Improve warning preservation, measure the actual target catalog, and test task outcomes. Resolve modern MCP visibility compatibility independently from description length. Schema compaction only addresses tool-definition context; it does not cap oversized tool results, prevent duplicate mutations after timeout, or provide result handles. Those remain separate backlog items.
