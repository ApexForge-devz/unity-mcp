# Execute Code Diagnostics — Wrapper Aliases, Line Mapping and Compiler Hints

**Review date:** 2026-10-14  
**Target:** ApexForge-devz/unity-mcp, beta, commit 142ea0af4890337f0ff6658df61db23982313717  
**Upstream proposal:** [CoplayDev/unity-mcp PR #1455](https://github.com/CoplayDev/unity-mcp/pull/1455)  
**Observed PR state:** open, last updated 2026-10-10 in this snapshot.  
**Decision:** ADAPT the type aliases and regression tests; tighten the wording/classification of CodeDom hints before claiming they diagnose a specific cause.  
**Validation:** source/patch review only; no Python or Unity tests were executed.

## 1. Executive conclusion

MCP-driven C# execution is more useful when users can write ordinary Unity code and receive compiler diagnostics that point to their own snippet instead of a generated wrapper. The current target has three relevant characteristics:

1. The generated method wrapper imports both System and UnityEngine, making unqualified Object and Random ambiguous.
2. Wrapper line counts are tracked by a constant, and compiler-specific paths subtract that number to translate diagnostics back to the user snippet. Adding two using-alias lines requires the offset to increase by exactly two.
3. CodeDom is the legacy C# 6 path, while auto selects Roslyn if it is available and otherwise uses CodeDom. The upstream proposal gives failed CodeDom compilation a Roslyn/older-syntax hint, but applies that hint to every CodeDom compilation failure—not only diagnostics caused by unsupported syntax.

The alias and line-mapping changes are straightforward and useful. The main caution is diagnostic precision: a string-to-int mismatch, missing type, invalid method call or reference-resolution problem can fail under CodeDom without being caused by modern syntax. The hint should be phrased as an option (“If this uses C# 8+ syntax…”) or driven by diagnostic classification, not read as a proven explanation for the failure.

## 2. Target source review

Primary target: [ExecuteCode.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs), blob **10325861e52def46cc72fe7cb2d5d4e28ffbb828**. Python wrapper: [execute_code.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/execute_code.py), blob **3cc44e260db2011a7b459d6ebe4059e38596f0ea**.

### 2.1 Generated wrapper and type names

The baseline wrapper emits six using directives, then the class declaration, class brace, Execute method declaration and method brace. User code therefore begins on generated source line 11. The target constant is WrapperLineOffset = 10, and the CodeDom and Roslyn diagnostic paths use it when translating generated lines to user lines.

The imports include System and UnityEngine:
- System.Object conflicts with UnityEngine.Object for unqualified Object.
- System.Random conflicts with UnityEngine.Random for unqualified Random.

This is a predictable usability problem in snippets like Object.DestroyImmediate(go) or Random.Range(0, 1), not a request to change C# semantics generally. Aliasing these two types to UnityEngine defaults is a reasonable product convention if it is documented and explicit System.Random remains accessible with its fully qualified name.

### 2.2 Compiler paths and error locations

The target uses:
- explicit roslyn: only the Roslyn compiler, with a clear unavailable error when Microsoft.CodeAnalysis is not installed;
- explicit codedom: the legacy CSharpCodeProvider path;
- auto: Roslyn when available, otherwise CodeDom.

Compiler-specific paths translate source locations before returning an ErrorResponse. CodeDom subtracts WrapperLineOffset from the one-based compiler error line; the Roslyn reflection path adds one to Roslyn's zero-based line then subtracts the same offset. OffsetErrors itself currently returns the already-adjusted strings unchanged, so the true contract lives in both compiler-specific conversion sites.

The current test suite already exercises basic execution, the CodeDom backend, assembly reference handling and cache behavior, but the inspected baseline tests do not exercise a bare Object/Random ambiguity or a compiler error on a known user line across both compiler routes.

### 2.3 Python-facing contract

The Python execute_code tool documents auto/roslyn/codedom and warns that safety_checks is not a sandbox. It returns the Unity handler's success/message/data envelope. This review is about compile diagnostics and developer usability only; it does not weaken the separate authorization/trust-policy concern for arbitrary in-process C# execution.

## 3. What upstream PR #1455 changes

The reviewed patch:
- adds using aliases for Object = UnityEngine.Object and Random = UnityEngine.Random;
- increases WrapperLineOffset from 10 to 12 to account for two inserted using lines;
- documents that bare Object/Random refer to UnityEngine types and that System.Random remains explicit;
- adds CodeDom hint strings explaining that the legacy compiler does not support modern C# syntax and recommending Roslyn install or compiler='auto' where appropriate;
- adds new ExecuteCode tests for UnityEngine Object/Random aliases, preserving lowercase object and System.Random, user-line mapping, and a modern switch-expression failure with a Roslyn hint;
- updates the skill/tool reference documentation.

The patch's key correctness test uses a type error deliberately on user line 3 and asserts that the reported error starts with “Line 3:”. This catches both a wrapper offset that is too small and one that is too large for the tested wrapper.

## 4. Review findings and caveats

### 4.1 Good: aliases are explicit and local to the wrapper

The aliases solve a common ambiguity while preserving the ability to write System.Random explicitly. The tests should stay on both CodeDom and auto (where the runtime/compiler availability determines which backend is exercised). Keep the tool description and reference docs aligned with the generated wrapper behavior.

Do not assume that unqualified Object now means System.Object; users can write object or System.Object when they need the .NET type.

### 4.2 Good: wrapper-offset regression test

Changing the wrapper constant is necessary because source lines are generated dynamically. The current test's third-line error is a stronger guard than simply asserting the error exists.

The offset remains a maintenance seam: if another using, attribute or namespace line is inserted later, a separately updated constant can drift from the actual prelude length. Either retain the constant with a mandatory line-mapping regression test, or create the wrapper prelude from a single list/string whose line count supplies the offset. Do not maintain one independently edited constant without test coverage.

### 4.3 Caveat: CodeDom hint is unconditional for compilation errors

In the PR, explicit compiler='codedom' returns the modern-syntax hint whenever compilation fails, without checking the compiler error category. When auto uses CodeDom because Roslyn is unavailable, a CodeDom failure also receives the installation hint. This may help for switch expressions or other unsupported C# 8+ syntax, but the same path also handles ordinary errors such as:
- assigning a string to an int;
- misspelling a type or method;
- missing assembly/type references;
- a bad overload or inaccessible member;
- other errors unrelated to language-version support.

The result can make an unrelated compile error look like a compiler-version problem. The new PR test only verifies the hint for a modern switch expression; it does not constrain the message for an ordinary semantic/type error.

**Recommendation:** keep the hint as conditional guidance—“If this snippet uses C# 8+ syntax, try Roslyn; otherwise inspect the compiler errors”—or retain ErrorNumber/severity and classify only well-known language-version diagnostics. The current error serialization drops CodeDom error numbers, so accurate classification would require preserving diagnostic IDs instead of emitting only line plus ErrorText. Do not promise that installing Roslyn will resolve arbitrary compilation errors.

### 4.4 Caveat: auto mode already chooses Roslyn when available

The current auto branch returns the Roslyn diagnostics directly when Roslyn is available but the compilation fails; it does not retry CodeDom after a Roslyn failure. The upstream hint is therefore appropriate only for a failure actually produced by CodeDom. Tests and result payloads should keep the reported compiler field aligned with the backend that actually ran.

### 4.5 Runtime exceptions are a separate path

The target's runtime-error response includes the exception's StackTrace and compiler name. The compiler offset logic only adjusts compile diagnostics. Do not imply that the PR maps runtime stack-trace line numbers to the snippet as well. If runtime source-line usability is a later requirement, add a separate test and normalization design; do not silently apply a compiler offset to arbitrary stack-trace strings.

## 5. Recommended implementation/test decision

**ADAPT**, with these conditions:

1. Keep the UnityEngine Object/Random aliases and clarify the visible tool description.
2. Update wrapper line mapping and retain a test whose compilation error is on a known non-first user line for CodeDom and auto; run a forced Roslyn case when installed.
3. Make compiler hints conditional or clearly advisory. A normal semantic error must not be described as definitively caused by a C# language-version limitation.
4. Keep diagnostic metadata (compiler backend, line, message; ideally diagnostic code) structured so clients can reason about failure causes. Do not parse an opaque message at the Python layer if the Unity handler can provide the classification.
5. Preserve current CodeDom assembly response-file safeguards, compiled-snippet cache behavior, safety warnings and execution authorization policy. This proposal should be a focused usability patch, not a rewrite of the execution subsystem.

## 6. Acceptance matrix

| Scenario | Expected behavior |
|---|---|
| Bare Object references a UnityEngine GameObject/Object API | Compiles and behaves as UnityEngine.Object under CodeDom and auto when those backends are available. |
| Bare Random.Range | Resolves to UnityEngine.Random. |
| Lowercase object and explicit System.Random | Continue to compile as before. |
| Compile error on user line 3 | Result reports Line 3 under the tested backend. |
| Insert another wrapper using/attribute later | Offset test fails until the wrapper metadata and offset are updated consistently. |
| Switch expression under CodeDom without Roslyn | Compile fails with a useful optional Roslyn/older-syntax suggestion. |
| Ordinary type mismatch under CodeDom | Shows the actual diagnostic and does not confidently identify a language-version failure. |
| Missing assembly/type under CodeDom | Error remains a reference/type-resolution diagnosis; the hint is advisory only. |
| Explicit roslyn selected but unavailable | Clear unavailable error points to CodeDom alternative; no misleading CodeDom error data. |
| auto with Roslyn installed and a normal compiler error | Reports Roslyn diagnostics/backend, not a CodeDom fallback message. |
| Runtime exception from successful compilation | Runtime stack trace stays a runtime error; no claim that compile-line mapping normalized it. |

## 7. Validation limits

This review inspected the target source, existing EditMode test inventory, and the upstream #1455 patch. It did not run CodeDom/Roslyn tests, did not exercise all Unity versions, and did not verify the hint against a matrix of semantic versus language-version diagnostics. Upstream test additions are evidence in the patch, not independently passing test results in this session.

## Sources

- Target [ExecuteCode.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs), blob **10325861e52def46cc72fe7cb2d5d4e28ffbb828**.
- Target [ExecuteCodeTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ExecuteCodeTests.cs), blob **dc3c4e360afe9c3273c5c39a1a8ff7d716c88464**.
- Target [execute_code.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/execute_code.py), blob **3cc44e260db2011a7b459d6ebe4059e38596f0ea**.
- Upstream [PR #1455](https://github.com/CoplayDev/unity-mcp/pull/1455), patch title “alias Object and Random to UnityEngine; hint at Roslyn when CodeDom fails.”
