# JSON-Encoded Scalar Arguments — Validation and Dispatch Review

**Review date:** 2026-10-14  
**Target:** ApexForge-devz/unity-mcp, branch beta, commit 142ea0af4890337f0ff6658df61db23982313717  
**Upstream proposal:** [CoplayDev/unity-mcp PR #1469](https://github.com/CoplayDev/unity-mcp/pull/1469)  
**Decision:** **ADAPT** the narrow one-layer boundary decoder and tests; avoid generic recursive coercion.  
**Validation:** target and PR source reviewed; tests were not executed in this research session.

## 1. Executive finding

The target MCP server defines the Read Console action and format as Literal-backed parameters, and its Unity handler normalizes these values with lower-case conversion. Some clients have compatibility trouble when enum-like scalar strings arrive JSON-encoded as a string (for example, the string value includes a surrounding JSON string representation). A narrowly scoped, one-layer decoder can accept that input shape without weakening the visible MCP schema.

The second affected path is more safety-sensitive: **execute_menu_item** accepts a menu path string, and the Unity-side handler uses a blacklist for disruptive entries. When normalizing menu paths, decoding must occur **before** the blacklist check. Otherwise a normalized encoded form can be treated differently by the safety gate than by the eventual executor.

PR #1469 is directionally sound: it introduces opt-in single-layer decoding at the MCP server and Unity bridge boundaries, adds enum validation after normalization, and tests malformed/nested values. It should be adopted only with the same boundary discipline and end-to-end tests. The useful rule is: **decode at most one JSON string layer for explicitly opted-in fields, then validate the normalized value before any dispatch or side effect**.

## 2. Target source and existing contract

### 2.1 Python tool schemas

- Target [read_console.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/read_console.py), blob **6a642dcec9b83ef6a5915de5b76585bfbc977d4d**, uses typed Literal choices for action (**get / clear**) and format (**plain / detailed / json**). It already parses the separate **types** argument when that field is supplied as a JSON string and applies validation to its resulting list. Action and format are lower-cased in the tool body, but the typed schema can reject a JSON-string-encoded enum value before the function body sees it.
- Target [execute_menu_item.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/execute_menu_item.py), blob **af6d46b86182500f2ac98c1a862bb2477a1e7819**, declares the menu path as an ordinary string and forwards it as **menuPath** to Unity. That preserves schema flexibility but leaves final normalization and safety decisions to the Unity handler.

### 2.2 Unity handlers

- Target [ReadConsole.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ReadConsole.cs), blob **b4abb5882d5726964c312b945137c794f0b19aa3**, reads the action and format from the command payload and uses lower-case normalization. The reviewed handler does not contain a dedicated JSON-string decoder for those scalar fields.
- Target [ExecuteMenuItem.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteMenuItem.cs), blob **f606f774f1e8f60f23bc706725e9608196a3044d**, reads menu_path/menuPath, validates that it is nonempty, checks a case-insensitive blacklist, then executes the menu path. Any compatibility decoder belongs **before** the blacklist check and execution.

### 2.3 Existing test coverage

- [ReadConsoleTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ReadConsoleTests.cs), blob **7ba0fbc2c09dba2fb18a5007dbfe57cf2660ceb9**, covers ordinary actions, log retrieval and formatting but the inspected baseline tests do not include a JSON-encoded scalar action or format.
- [ExecuteMenuItemTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ExecuteMenuItemTests.cs), blob **51cbd4cfd2fd2c5aafbf4bcf1eaf70944f7014a2**, checks missing path, a plain File/Quit blacklist case and the immediate result shape, but does not test encoded or nested-encoded paths.

## 3. What PR #1469 changes

Source-reviewed patch [#1469](https://github.com/CoplayDev/unity-mcp/pull/1469) makes the following changes:

1. A Python helper decodes one layer only if a string begins and ends with a quote and JSON parsing returns a string. Non-strings, malformed JSON, JSON objects/numbers/booleans and ordinary text remain unchanged.
2. The MCP server uses a Pydantic BeforeValidator for Read Console action and format, allowing the encoded scalar form to be decoded before Literal validation. The tool body normalizes and explicitly validates the final supported action/format before dispatch.
3. The Unity bridge adds a corresponding helper in ParamCoercion and calls it for Read Console action/format and menu path.
4. The menu path is decoded before the existing blacklist is checked, keeping the safety gate aligned with the value that Unity will execute.
5. Tests cover real MCP schema validation in a fresh Python interpreter, ordinary strings, encoded action/format/menu path, malformed and nested-encoded action/path values, preservation of quoted filter text, and an encoded File/Quit path that must still be blocked.

This is a narrowly scoped fix, not a reason to recursively JSON-decode arbitrary tool arguments.

## 4. Required invariants

### 4.1 One layer only, per opted-in field

- Ordinary values remain unchanged.
- A JSON-encoded string is decoded once only for fields explicitly designated as compatible with this representation.
- Nested-encoded strings are not recursively decoded. They should be rejected by typed validation or remain an invalid path that causes no side effect.
- JSON strings that encode non-string values are not coerced into booleans, numbers, lists or objects by this helper.
- Malformed strings should remain unchanged so normal type/value validation returns a predictable error; do not silently substitute a default.

### 4.2 Validate before dispatch and side effects

For Read Console, validate the normalized action against **get/clear** and format against **plain/detailed/json** on the server side before invoking Unity. Keep the typed MCP schema so clients still discover the allowed options; BeforeValidator is an input compatibility layer, not a schema broadening.

For Execute Menu Item, decode at the Unity boundary **before** path validation and blacklist matching. Preserve case-insensitive comparison for the blacklist. Do not decode the path a second time after the guard. A JSON-encoded **File/Quit** path must remain blocked and must never cause a menu invocation.

### 4.3 Do not apply to every string

This decoder should not be globally installed for all string parameters, especially project paths, filters, source code, search terms and arbitrary serialized property values. A legitimate user string may begin/end with quotes; broad decoding could change its meaning. Keep compatibility behavior opt-in, field-specific and tested.

### 4.4 Avoid locale-sensitive normalization

Use invariant case normalization for protocol/action keywords, not machine-culture lower casing. Keep normalization and allowed-value checks explicit so an unknown action never falls through to an unintended handler.

## 5. Recommended implementation and test decision

**Recommendation: ADAPT.** Add one-layer decoding for the two specific boundaries in PR #1469 and preserve its high-value tests, subject to compatibility and side-effect coverage:

- Server-side Read Console uses a BeforeValidator to decode the one supported representation before the Literal schema validates the final enum.
- Read Console validates action/format again in the callable body, covering direct Python callers that bypass Pydantic.
- Unity-side Read Console remains defensive for direct/batch callers that bypass the Python tool.
- Unity Execute Menu Item decodes before nonempty/path rules and blacklist checks. Never put the decoder after the guard.
- Add server integration tests using the actual supported FastMCP SDK and Unity EditMode tests; stub-only tool invocations are not sufficient evidence of schema behavior.

## 6. Acceptance matrix

| Input class | Read Console action/format | Execute Menu Item path |
|---|---|---|
| Normal valid string | Accepted, forwarded unchanged except documented case normalization | Accepted; existing blacklist behavior applies |
| Single JSON-encoded valid string | Decode once, then Literal/allowlist validation; forwarded as canonical value | Decode once before blacklist and execution |
| Ordinary string that contains quote characters inside | Preserve exactly; do not strip internal quotes | Preserve unless it is explicitly the outer JSON string representation |
| Malformed leading/trailing quotes | Do not guess or trim; return ordinary validation/error result | Do not guess or trim; path fails normally with no unintended execution |
| JSON-encoded unknown action | Reject before Unity dispatch | N/A |
| Nested-encoded action/path | No recursive decoding; reject or fail path lookup; no side effect | Must not execute a menu item |
| JSON-encoded non-string (e.g. number/object) | No coercion; typed schema rejects | No coercion; path validation rejects |
| JSON-encoded File/Quit | N/A | Still blocked by blacklist; menu side-effect counter remains zero |
| Direct Unity/batch call bypassing Python | Unity handler still performs one-layer decode and validates action/format | Unity handler still decodes before blacklist |
| Invalid values through aliases/batch | Same validation, no alternate dispatch route | Same blacklist and validation path |

## 7. Validation limitations

This review inspected the target handler/schema/tests and PR patch. It did not execute the proposed Python child-process MCP test or Unity EditMode tests. The proposed changes should not be described as verified until the integration tests run on the actual pinned FastMCP version and the Unity test project. Also re-check all alternate handler routes that can reach the same C# functions before implementation, including batch execution and aliases.

## Sources

- Target [read_console.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/read_console.py), blob **6a642dcec9b83ef6a5915de5b76585bfbc977d4d**.
- Target [execute_menu_item.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/execute_menu_item.py), blob **af6d46b86182500f2ac98c1a862bb2477a1e7819**.
- Target [ReadConsole.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ReadConsole.cs), blob **b4abb5882d5726964c312b945137c794f0b19aa3**.
- Target [ExecuteMenuItem.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteMenuItem.cs), blob **f606f774f1e8f60f23bc706725e9608196a3044d**.
- Target [ReadConsoleTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ReadConsoleTests.cs), blob **7ba0fbc2c09dba2fb18a5007dbfe57cf2660ceb9**.
- Target [ExecuteMenuItemTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ExecuteMenuItemTests.cs), blob **51cbd4cfd2fd2c5aafbf4bcf1eaf70944f7014a2**.
- Upstream [PR #1469 patch and regression tests](https://github.com/CoplayDev/unity-mcp/pull/1469).
