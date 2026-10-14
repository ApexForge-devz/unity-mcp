# Deterministic Scene-Generator Lifecycle — Source Audit

**Observed:** 2026-10-14  
**Target source:** `ApexForge-devz/unity-mcp`, `beta`  
**Purpose:** Establish which parts of a script-first scene-generation loop already exist, and where the operation contract is still incomplete.  
**Status:** Source inspection only. No Unity Editor was launched and no tests were run.

## Executive conclusion

The target already has useful primitives for persistent C# source, structured edits, source validation, compilation requests, readiness polling, scene lifecycle and Editor menu invocation. The problem is not the absence of basic tools. The problem is that these pieces do not yet form one end-to-end **generator operation contract**.

Three distinctions must stay explicit:

1. **The source change was applied** does not prove Unity compiled it successfully.
2. **Unity is ready for tools again** does not prove the generator completed successfully or saved the expected scene.
3. **A menu item was invoked** does not prove the generator's intended postconditions passed.

The research priority should therefore be to compose and test the existing tools first, then add only the narrow missing operation/result contract. It should not be to add more direct GameObject/component CRUD calls.

## Source-verified capability map

| Stage | Existing target behavior | Remaining contract gap |
|---|---|---|
| Persistent generator source | `manage_script` creates and reads files under Assets; `script_apply_edits` supports structured/source edits. | Keep one authoritative generator source and make the intended output scene explicit. |
| Stale-edit protection | Script edits use a SHA-256 precondition on the Unity side; stale edits return a mismatch instead of overwriting newer source. | Keep the precondition on every source update and preserve the generated diff/updated hash in the run record. |
| Source validation | `validate_script` calls the Unity-side source validation path; the C# implementation supports different validation levels and optional Roslyn-based checks. | This is source-level validation. The public wrapper's basic/standard result is not, by itself, proof that Unity's actual project compilation succeeded. |
| Compile and reload | `refresh_unity` can request script compilation and wait for the canonical Editor state to become ready. Script mutation helpers also wait for Editor readiness after reload-related disconnects. | Readiness is not the same as compiler success. The current readiness result does not itself include the actual compiler error set or a generator-specific result. |
| Editor state | `mcpforunity://editor/state` exposes compile/reload flags and timestamps, refresh state, instance identity, active-scene metadata and readiness advice. | It exposes state/timing, not a durable scene-generator operation ID, run receipt or scene postcondition report. |
| Generator invocation | `execute_menu_item` sends a menu path; the C# handler calls `EditorApplication.ExecuteMenuItem`. | The handler reports that execution was attempted and points to logs; it does not return the generator's final result, save status or validation report. The wrapper does not declare a generator run ID. |
| Scene lifecycle | `manage_scene` supports scene create/load/save and general scene validation. | These actions are separate from generator invocation. They do not automatically prove that the scene saved by a particular run is the intended output or that its custom invariants passed. |
| Transient C# | `execute_code` compiles and executes a method body in memory. | Useful for limited diagnostics/bootstrap tasks, but unsuitable as the only stored representation of the scene-generation program. |

### Primary source references

- [manage_script.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_script.py) — script operations and source-validation wrapper.
- [ManageScript.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageScript.cs) — validation, SHA-guarded text edits, atomic source writes and refresh scheduling.
- [script_apply_edits.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/script_apply_edits.py) — structured edit routing and mutation/reload recovery.
- [refresh_unity.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/refresh_unity.py) — compile request, readiness wait and disconnect recovery.
- [editor_state.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/resources/editor_state.py) — compilation flags/timestamps and readiness advice.
- [execute_menu_item.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/execute_menu_item.py) and [ExecuteMenuItem.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteMenuItem.cs) — menu dispatch and its current result wording.
- [unity_transport.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/transport/unity_transport.py) and [unity_connection.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/transport/legacy/unity_connection.py) — default retry/readiness transport behavior.
- [manage_scene.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_scene.py) and [ManageScene.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageScene.cs) — scene lifecycle and general validation.

## Detailed findings

### 1. Script edits are already protected against some stale-write and reload cases

The C# text-edit handler requires a precondition SHA-256, compares it with the current source, and returns a structured stale-file response on mismatch. The Python edit wrapper also routes mutations through a helper that avoids replaying immediately when Unity says it is reloading, waits for readiness and can verify the source hash after a connection loss.

This is a sound foundation. Do not replace it with blind whole-file rewrites or a duplicate editing subsystem. Keep the source hash as part of the generation input record.

**Important limit:** the post-disconnect SHA check proves only that the source file changed as expected. It does not prove the modified script compiled, that its menu item was registered after reload, or that a scene-generation run succeeded.

### 2. Readiness is not compile-success evidence

The C# `RefreshUnity` handler has more nuance than a simple refresh call: when compilation is requested it reports whether the compile-start edge was observed (`compile_started`) and the resulting high-level state. On Unity 6+, it returns after the compile has started rather than waiting through the domain reload; the Python wrapper then polls `mcpforunity://editor/state` until the Editor is ready or times out. The editor-state cache retains compile/reload flags and timestamps across the relevant reload boundary using SessionState.

That establishes a useful lifecycle signal, but not a successful-build result. `RefreshUnity` does not return the compiler error set or a compile-success boolean, and the editor-state schema does not carry compiler diagnostics. Source-level `validate_script` is helpful before the compile, but should not be described as equivalent to the project's actual Unity compilation result.

**A direct Unity compiler API exists, so Console scraping should be a fallback rather than the design target.** Unity documents `CompilationPipeline.assemblyCompilationFinished` as an Editor-main-thread event supplying the output assembly path and `CompilerMessage[]`; an assembly fails if the event includes build errors. `CompilerMessage` exposes file, line, column, message and type. `CompilationPipeline.compilationFinished` fires after the last per-assembly-finished event, and its context can be matched by reference to the corresponding `compilationStarted` context. This supports one aggregated diagnostic summary per compilation cycle, without parsing the global Console.

Suggested design seam: aggregate bounded per-assembly diagnostics under the compilation context; at cycle finish, persist a compact summary (run/cycle identifier, timestamps, assembly/error/warning counts and capped diagnostics with source locations) in `SessionState` before any domain reload. Expose it through the editor-state/compile-status contract. The next ready-state poll can then distinguish “the pipeline completed” from “the pipeline completed without errors.” Preserve assembly/cycle attribution and clear or supersede stale diagnostics for the next compile. Do not assume the context object's representation is stable or stringifiable; treat it as an in-memory correlation key. Do not start another compilation from a compilation callback.

**Official API references:** [Unity 6.0 `assemblyCompilationFinished`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Compilation.CompilationPipeline-assemblyCompilationFinished.html), [Unity 7.0 `compilationFinished`](https://docs.unity.com/en-us/engine/7000.0/script-reference/unityeditor/compilation/compilationpipeline/compilationfinished), [Unity 7.0 `CompilerMessage`](https://docs.unity.com/en-us/engine/7000.0/script-reference/unityeditor/compilation/compilermessage).


The existing `read_console` tool can filter error/warning messages and page through entries, but its contract has no timestamp, “since compile started” cursor, or generator/run correlation ID. Taking a before/after log snapshot is a possible interim workaround, but repeated messages, parallel Editor activity or Console changes make that less reliable than a compiler-result delta tied to the compile operation.

**Recommendation:** after a script change, distinguish (a) source validation, (b) compile-start acknowledgement, (c) Unity compile/reload completion, and (d) absence of blocking compiler errors. Prefer a structured compiler-result snapshot correlated to the compile request. If using Console logs as an interim check, capture a baseline and explicitly report its weaker correlation guarantee. Do not infer compile success from a successful refresh response or a ready Editor alone.

### 3. Menu invocation lacks a completion receipt

The C# menu handler calls `EditorApplication.ExecuteMenuItem(menuPath)`. If Unity accepts the path, the success message says the item was attempted and directs the caller to Unity logs for confirmation/errors. It cannot report whether a project-owned scene generator finished, which scene it changed, whether saving completed, or whether custom postconditions passed.

The Python wrapper also uses the normal transport call without explicitly choosing uncertain-outcome behavior for a side-effectful generator action. The transport defaults `retry_on_reload` to true unless overridden. This does **not** prove that every invocation will be duplicated; it means the wrapper's public contract does not give this workflow an explicit, tested distinction between a command rejected before execution and a response lost after execution may have begun.

**Recommendation:** do not use a generic menu-item success response as proof of scene-generation success. Give the generator invocation a stable request/run ID and a receipt that can be queried after reconnect. A result should say whether the run was accepted, running, succeeded, failed or uncertain, and should carry the same run ID across retries/status reads. If the first response is lost, reconcile the existing run before starting another one.

### 4. Scene validation is not the generator's acceptance test

The current `manage_scene.validate` implementation is narrower than a generator acceptance test, and it is not a targeted query: it gets the active scene, traverses every root and descendant, and checks missing MonoBehaviours and prefab instances whose assets are missing. It caps the detailed issue list at 200. With `auto_repair=true`, it removes missing scripts and marks the active scene dirty. This action neither accepts the generator's output scene path nor checks custom expectations such as item counts, component settings, material/shader configuration, asset identity or visual correctness.

Consequently, it can scan a large hierarchy the agent would otherwise avoid reading, validate the wrong scene if another scene is active, and mutate scene state through auto-repair. It remains useful as an explicitly requested broad integrity check after confirming active-scene identity, but should not be the default post-generator validator.

**Recommendation:** put project-specific assertions beside the generator and have it emit a concise validation report. The MCP layer should capture/retrieve that report and expose actionable failures. Prefer assertions scoped to the generated scene/region; do not turn successful generation verification into a full hierarchy walk or an automatic scene repair.

## Recommended end-to-end operation contract

The default loop should be:

1. **Resolve and record identity:** target Unity instance/project, generator source path, source SHA-256, output scene path and declared ownership boundary.
2. **Edit once:** use the existing SHA-guarded structured edit path; return the updated SHA and a concise diff.
3. **Validate source:** return diagnostics from source validation. On errors, stop before requesting generator execution.
4. **Compile and wait:** request the Unity compile/reload path and wait for Editor readiness. Separately confirm that the actual Unity compilation has no blocking errors. Prefer `CompilationPipeline` compiler messages aggregated by compilation context over Console before/after diffs. Do not treat readiness alone as success.
5. **Invoke once:** run the named, project-owned generator against the declared target. Give the operation a unique run ID; do not rely on a generic menu-invocation acknowledgement as the completion result.
6. **Track completion:** reconcile run ID/status after disconnects or timeouts instead of blindly resending the start command. Distinguish failed-to-start, running, succeeded, failed and unknown outcome.
7. **Validate the generated scene:** assertions run in the generator or a dedicated read-only validator. Verify scene path/identity, required scene roots and domain-specific counts, missing scripts, asset references, required component/property values, save result and relevant visual/rendering invariants.
8. **Promote only on success:** keep a recoverable last-known-good scene or stage the output where practical. Report whether output was saved/promoted, left dirty, or marked failed.
9. **Return a compact report:** run ID, target project/scene, generator source hash/version, status, elapsed time, assertion totals, actionable failures/warnings, save state and optional screenshot/report handles. Keep the full diagnostic payload opt-in.
10. **Fix the source and rerun:** if assertions fail, make the correction in the generator source and regenerate. Do not repair generated objects through separate direct mutation calls.

A dedicated `run_scene_generator` action may be warranted if the existing menu path cannot carry a run ID or return/retrieve a bounded completion report. Prefer a narrow operation contract backed by the existing transport, editor-state and scene tools over a broad new orchestration framework. Before adding that action, test whether a project-owned generator can publish and retrieve the same receipt through existing mechanisms with equivalent reliability.

## Existing async test jobs: pattern to adapt, not an API to reuse

The target already has an established asynchronous-job design in `TestJobManager`, `RunTests` and `get_test_job`:

- A start action returns a generated job ID and `running` status immediately; callers poll that same ID for progress and terminal state.
- The Editor-side manager retains a bounded set of recent jobs (10), timestamps and progress/failure summaries, and persists lightweight state in `SessionState` so a job can be restored across an assembly/domain reload during the same Editor session.
- Polling may return `running`, `succeeded` or `failed`, with timestamps, progress and bounded failure details. The Python wrapper can long-poll for a bounded time instead of forcing tight client polling.
- The implementation is test-specific. It prevents overlapping test runs and its status endpoint speaks the test-job schema; it is not a general-purpose scene-operation service.
- Crucially, persisted job metadata deliberately omits the full `TestRunResult` to keep `SessionState` bounded. After reload, a terminal status can be restored without the original detailed result payload. `SessionState` also does not promise persistence across closing/restarting Unity.

Use this as an internal design precedent for scene generation, not by routing generation through `get_test_job` or expanding `TestJobManager` into an unrelated generic framework. If the existing menu action cannot return/retrieve its own completion result, a narrow generator-run record should include a generated run ID, target Unity project/instance, source hash, output scene path, start/finish timestamps, state, concise assertion summary and save/promote result. Persist only bounded summary metadata through domain reload; store full reports as project-owned artifacts only when needed. State whether run records survive only domain reload/reconnect or also full Editor restart. A status read must never start the generator.

In a multi-Editor setup, scope server-side receipts to the selected Unity instance and authenticated principal where applicable; a locally generated UUID alone is not the routing/authorization policy.

**Existing implementation references:** [TestJobManager.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Services/TestJobManager.cs), [RunTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/RunTests.cs), [run_tests.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/run_tests.py), [test_run_tests_async.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/tests/integration/test_run_tests_async.py).

## Acceptance tests

| Test | Required assertion |
|---|---|
| Valid generator source | Source validation passes, Unity compile/reload completes, and only then can invocation start. |
| Source has a compiler error | The run is rejected with actionable compiler diagnostics; generator is not invoked and the output scene is unchanged. |
| Diagnostics span assemblies | Errors/warnings are attributed to the correct assembly and compilation cycle; only bounded diagnostics are returned and stale errors do not contaminate a later successful cycle. |
| Reload disconnect after edit | Source SHA is reconciled, Editor readiness returns, and no duplicate source mutation occurs. |
| Menu invocation returns but generator later fails | Final result is failed, not successful merely because the menu command was accepted. |
| Response lost after invocation | Querying the same run ID returns the existing state; the start is not blindly duplicated. |
| Wrong Unity project or output path | Invocation is rejected before modifying output. |
| Generator throws before save | Failure stage and output/save state are reported; last-known-good output remains recoverable where the staging strategy promises it. |
| Postcondition fails | Only the concise failed assertions are returned, including expected/actual values and relevant generator section/path. |
| Same input run twice | Semantic scene output converges; duplicate generated content does not accumulate. |
| Change one generator input | Only the declared generated scene/region is changed; unrelated hand-authored content is preserved. |
| Asset/reference problem | Missing or ambiguous references are explicit errors, not silent substitutions. |
| Normal successful run | Default response is bounded; no full scene hierarchy is emitted. |
| Two Unity Editors connected | The run receipt and output scene belong to the intended instance/project. |
| Run receipt across reload | A running/terminal receipt survives domain reload within the Editor session, stays bounded, and does not falsely promise full detail or Editor-restart persistence unless explicitly implemented. |

## Priority decision

Keep this under **P1.11 — make script-first deterministic scene generation the primary authoring contract**. The source audit sharpens P1.11 rather than justifying another set of CRUD tools. The most important implementation seam is **compile-success evidence plus a reload-surviving generator-run receipt**. Treat it as an integration gap to test, not a claim that all existing primitives are unusable.

No runtime source or tests were changed during this audit.
