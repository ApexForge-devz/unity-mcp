# Upstream PR #1472 — Custom Tool Required/Optional Parameter Ordering

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1472](https://github.com/CoplayDev/unity-mcp/pull/1472)  
**Target baseline:** beta 142ea0af4890337f0ff6658df61db23982313717  
**Candidate head:** becf6787317673650a2ac51292153ed7fcf6498f  
**Status:** Open, not merged  
**Decision:** **ADAPT; high-confidence, low-cost server fix.**  
**Method:** Source and test inspection. No tests were run independently.

## Executive assessment

PR #1472 fixes a real custom-tool registration failure. The target dynamically builds a Python inspect.Signature from project-defined tool parameters. It currently marks every user parameter as POSITIONAL_OR_KEYWORD. In Python, a required positional-or-keyword parameter cannot follow an optional positional-or-keyword parameter in the signature. A schema ordered as optional limit=10 followed by required action therefore raises ValueError while constructing the signature, before FastMCP can register or expose the custom tool.

The PR changes custom parameters to KEYWORD_ONLY while keeping the injected Context parameter unchanged. This matches the custom handler's _handler(ctx, **kwargs) calling convention and MCP's named-argument object, so required and optional parameter declarations can appear in either order without changing their names, type annotations, defaults or requiredness. It is a narrow compatibility fix rather than a tool-surface expansion.

## 1. Source-verified cause and fix

Target [custom_tool_service.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/custom_tool_service.py) builds the signature in _build_signature:
- The first parameter is the injected Context parameter.
- Each custom parameter gets a mapped Python annotation and either inspect._empty (required) or a coerced default (optional).
- All custom parameters are currently POSITIONAL_OR_KEYWORD.
- The resulting inspect.Signature validates ordering, which rejects an optional-first / required-later definition.

The candidate [fix](https://github.com/CoplayDev/unity-mcp/blob/becf6787317673650a2ac51292153ed7fcf6498f/Server/src/services/custom_tool_service.py) sets each custom parameter kind to KEYWORD_ONLY. The injected Context remains positional-or-keyword, so the Python signature is valid while public custom arguments remain named. This aligns with the handler body, which receives custom values in kwargs and forwards them to Unity as a JSON parameter object.

This matters to project-defined MCP tools: one ordering mistake in a user's custom-tool parameter list can prevent that tool from registering. No Unity C# changes are required.

## 2. Test evidence and gap

The PR adds a parameter-order regression test that registers a global custom tool with optional-first and required-first ordering. It asserts that registration succeeds in both orders, required action has no default, annotations preserve string/integer types, signature order remains the declared order, a typed default is retained, and binding without the required action fails.

The PR author also reports a smoke test with the actual locked FastMCP SDK: the tool was listed, action was marked required, limit retained integer default 10, and injected Context was excluded from the public schema. The PR author reports 1,577 Python tests passing with 18 skipped; the Unity compile attempt was unavailable and Editor tests were not run. This review did not independently run those tests.

The checked-in new test uses a mock MCP registration decorator to inspect the signature. Add or preserve a small real-FastMCP schema test in CI so the test verifies the exposed JSON inputSchema, not only the Python signature. It should assert required fields, defaults, types, and that Context remains an injected non-public parameter.

## 3. Compatibility constraints

- Use keyword-only for generated custom parameters, not for the injected Context parameter.
- Preserve declared parameter order where the FastMCP schema allows ordering.
- Preserve typed optional defaults through the existing coercion logic.
- Missing required arguments must remain rejected; do not substitute empty values.
- No runtime Unity feature or new MCP tool name is needed.

## Recommended decision

**ADAPT.** This is a small, high-confidence fix to the existing custom-tool capability. The target implementation reproduces the cause directly in source. Apply the one-line parameter-kind change with the regression test, then verify the public FastMCP schema under the pinned dependency. It fits P2 custom-tool reliability and does not compete with the script-first scene-authoring contract.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Optional parameter precedes required parameter | Signature constructs and tool registers. |
| Required parameter precedes optional parameter | Existing valid definitions remain valid. |
| Required parameter omitted | Schema marks it required and invocation validation fails. |
| Optional value has a typed default | Public schema and handler preserve type/default. |
| Context parameter is present | Context remains injected and is not exposed as a tool argument. |
| Parameter names retain author order | Signature/schema ordering is retained where supported. |
| Custom tool registration | Tool is discoverable with expected schema. |
| Custom-tool scoping/routing | Existing global/project-scoped semantics are unchanged. |
| Existing custom-tool tests | User scoping, annotations and routing remain unchanged. |

## Source references

- [PR #1472](https://github.com/CoplayDev/unity-mcp/pull/1472)
- Target [custom_tool_service.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/custom_tool_service.py)
- Candidate [custom_tool_service.py](https://github.com/CoplayDev/unity-mcp/blob/becf6787317673650a2ac51292153ed7fcf6498f/Server/src/services/custom_tool_service.py)
- Candidate [test_custom_tool_parameter_order.py](https://github.com/CoplayDev/unity-mcp/blob/becf6787317673650a2ac51292153ed7fcf6498f/Server/tests/test_custom_tool_parameter_order.py)
- Target [custom-tool scoping tests](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/tests/test_custom_tool_service_user_scope.py)
