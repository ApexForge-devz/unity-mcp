# Deterministic Script-First Scene Authoring Workflow

**Established:** 2026-10-14  
**Applies to:** the target AI agent's Unity scene-authoring workflow  
**Status:** workflow constraint and source-mapped recommendation; no runtime code changed.

## 1. Core rule

The **persistent scene-generation/regeneration script is the source of truth**. To make a scene change, edit the script and execute it again. The agent does not build or repair the scene by issuing a long sequence of direct GameObject/component create/modify/delete calls, then reading a huge hierarchy to infer which change caused a problem.

The distinction is important: a generation script may itself call Unity APIs to create objects, attach components, set serialized fields, place assets, and save the scene. What is avoided is one-off agent-authored scene mutation outside that reproducible script. The generation program captures the intended state and can be reviewed, rerun, versioned and corrected deterministically.

## 2. Source mapping in the target repository

The following MCP tools were inspected on target `beta`:

| Existing tool | Appropriate role in this workflow | Boundary |
|---|---|---|
| `create_script` / `manage_script` | Create/read a persistent C# generator under the project-owned Assets tree. | Do not use an ephemeral method body as the only copy of the generation logic. Respect Editor-only assembly/folder conventions. |
| `script_apply_edits` / text edit helpers | Apply structured, narrow edits to the generator source. | Prefer method/anchor-based edits and validation to fragile whole-file rewrites. Confirm the intended source version before applying edits. |
| `validate_script` | Check script structure/compiler-related diagnostics where its supported level provides them. | Script-level validation is not a substitute for Unity's actual compilation/reload state. |
| `manage_scene` | Open/create/save the named scene, obtain loaded/active scene metadata, run available validation, control the Scene View or capture a scene frame. | Prefer lifecycle and targeted diagnostic actions. Do not rely on auto-repair or direct scene edits as a replacement for updating the generator. |
| `execute_menu_item` | Invoke a named, project-owned generator entry point, where that entry point is implemented as an Editor menu item. | Run only after source compilation/reload is confirmed and after validating the target Unity instance. Use a bounded operation ID/result where available. |
| `execute_code` | Run short diagnostics or bootstrap a narrowly scoped action when necessary. | The target wrapper runs code in memory; it does not persist the generator source. Do not make transient snippets the sole source of truth for a scene. |
| `find_gameobjects` / `manage_gameobject` / direct component-property tools | Narrow read-only diagnostics may answer a specific post-run assertion. | Direct CRUD or corrective mutation is not the agent's default scene-authoring strategy. |
| `manage_prefabs` and direct override operations | Only targeted inspection if a prefab mismatch is not already visible from generator output or asset references. | Direct apply/revert of scene overrides is not the normal way to update generated content. |

This mapping is source-level and limited to the inspected tool wrappers; verify the exact action contract again before implementing any workflow integration.

## 3. Recommended end-to-end loop

1. **Declare the output and ownership boundary.** Name the output scene by project-relative path. State whether the generator owns the whole scene or only a distinct generated root/region. A regeneration must not destroy unrelated hand-authored content.
2. **Create or edit the generator source.** Keep the persistent C# file in the project's expected Editor-only source/asmdef area, and store supporting config/data assets as appropriate. Use stable paths and script edit helpers.
3. **Validate the source and wait for Unity readiness.** Apply the edit once; then wait for compile/domain-reload completion using existing readiness/status behavior. Return source/compiler diagnostics on failure. Do not try to execute a partially compiled generator.
4. **Run the named generator once.** Target the correct Unity project/instance and output scene explicitly. Assign a request/run ID if the workflow supports it so a lost response can be reconciled without a blind second execution.
5. **Generate within a controlled boundary.** For whole-scene ownership, regenerate the intended scene in a staging or recoverable workflow. For partial ownership, remove/replace only the generator-owned root/region. Do not infer ownership from names alone when an explicit manifest/marker can be used.
6. **Validate through code, not full-scene reads.** The generator should check its required invariants and emit a concise result: scene path, generator revision/hash, status, counts by relevant categories, missing scripts/references, failed asset loads, unexpected duplicates, key component/property checks, warnings, save status and elapsed time.
7. **Commit/save only when validation passes.** Keep the last known-good saved scene recoverable. If generation fails, report the failure and do not silently patch individual scene objects to compensate. Update the source and rerun.
8. **Use targeted diagnosis for remaining issues.** Inspect only the named path/object/component or use a screenshot/Frame Debugger/profiler query directly relevant to the failure. Return a small diff or list of failed assertions rather than reading the entire scene.
9. **Test repeatability.** Run the same generator twice against the same inputs and compare semantic output. A second run must not accumulate duplicate objects, materials, components or references. If byte-for-byte scene YAML stability is an explicit requirement, test that separately; semantic determinism does not automatically guarantee stable serialized file IDs.

## 4. Determinism contract

The generator should make each of these explicit:

- **Inputs:** output scene path, generation version, data/config assets, random seed if randomness is intended, and relevant feature flags.
- **Ordering:** sort discovered assets/references and generation instructions where Unity APIs do not promise order. Do not depend on current selection, open scene order, unsaved editor state or arbitrary hierarchy enumeration.
- **Identity and ownership:** stable names and stable generated-root/section markers; explicit handling of existing content; do not delete content outside the declared boundary.
- **References:** resolve assets from GUIDs or validated project-relative paths where appropriate; report unresolved or ambiguous assets. Do not silently substitute a different asset when lookup fails.
- **State and save boundary:** define which scene is opened, whether it is additive, when it is saved, and what happens on validation or serialization failure.
- **Idempotence:** a rerun converges to the same intended semantic scene; it does not append duplicates or leave stale generator-owned content.
- **Reporting:** return generator ID/version/hash, scene path, counts, assertion failures/warnings, save result and duration. Keep full diagnostic detail opt-in.

## 5. Validation without dumping the whole scene

A generator can perform its own targeted assertions after constructing the scene. Recommended checks depend on the project but may include:

- Required scene path exists and is the scene just generated.
- Expected roots and counts of domain-specific item types/components are present.
- No duplicate generated IDs/names within the generator-owned region.
- Required asset references resolve; no missing scripts; required components and serialized values match the scene specification.
- Renderer, material, shader, layer/tag, collider, lighting, camera and navigation settings match explicitly configured requirements.
- Scene save succeeded and output hash/manifest was updated only on success.
- Build-settings registration is verified only where the scene is intended to be included in builds.

On success, return a summary. On failure, return only failed assertions with object paths/expected vs actual values and a short list of implicated generator sections. A full hierarchy export should be an explicit, targeted diagnostic, not the standard verification loop.

## 6. Failure and recovery contract

| Failure | Required behavior |
|---|---|
| Script edit/validation fails | Do not run the generator or mutate the scene. Return compiler/source diagnostics and the affected script path. |
| Domain reload/compile is still in progress | Wait through the existing readiness workflow or return a bounded pending state; do not resend the mutation. |
| Generator returns an error midway | Preserve the last known-good output where practical; report run ID, failure stage and whether the scene was saved. |
| Generator response is lost after it may have run | Reconcile the known run ID, generator version or output manifest before rerunning. Do not blindly double-execute. |
| Wrong Unity instance/output path | Reject before generation; show the resolved project/scene identity in the result. |
| Validation fails | Keep output marked failed/staged and do not promote it as the new known-good scene. |
| Re-run creates duplicates | Treat as an idempotence bug in generator logic; fix the generator rather than deleting duplicates through separate CRUD calls. |
| Generator-created scene differs visually | Capture a targeted screenshot or inspect the relevant rendering event/state, then revise the generator and rerun. |

A staging scene or a backup-and-promote pattern can protect the previous scene, but the exact Unity API sequence should be validated in the target's supported Editor versions before it becomes an automated default.

## 7. Acceptance tests for any integrated generator workflow

1. Create a scene from a blank starting point using the persistent generator source.
2. Run it twice with the same inputs; validate there are no duplicates and semantic output is stable.
3. Change one generator parameter and verify only the intended generated region/scene changes.
4. Place a hand-authored object outside the declared generated region and prove regeneration preserves it.
5. Inject a compile error and prove the current saved scene remains unchanged.
6. Inject a runtime-generation error and verify the result marks the run incomplete and accurately reports save/dirty state.
7. Simulate a lost response after generation may have run; verify run reconciliation prevents duplicate regeneration.
8. Verify all required asset/component references and domain-specific assertions without requesting a full scene hierarchy.
9. Use targeted screenshot/profiling/debugging for a known visual failure; the ordinary successful response remains concise.
10. Verify output scene identity and Unity instance routing when two projects are open.
11. Check semantic determinism separately from raw scene-file byte equality.
12. Record timing and allocations for edit→compile/reload→run→validate; distinguish editor compile time from generation and verification time.

## 8. Prioritization consequence

For this agent, direct `manage_gameobject` create/modify/delete calls, direct component/property writes and direct prefab override apply/revert belong in a deferred general-tool-parity category, not as recommended improvements to scene authoring. A new feature is higher priority when it improves source editing, compile/reload handling, safe generator execution, idempotence, targeted assertions, asset/reference checks, scene validation or useful visual diagnostics without requiring full-scene reads.

The general MCP product can retain its existing direct-mutation APIs for other clients; this workflow decision does not require deleting them.

## Sources inspected

- Target [`manage_script.py`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_script.py), including persistent C# script creation, source reads/structured text edits, validation and post-reload verification.
- Target [`script_apply_edits.py`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/script_apply_edits.py), including structured method/class/anchor editing.
- Target [`manage_scene.py`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_scene.py) and [`ManageScene.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageScene.cs), including scene lifecycle and validation actions.
- Target [`execute_code.py`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/execute_code.py) and [`ExecuteCode.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ExecuteCode.cs), which expose transient in-memory code execution rather than persistent generator storage.
