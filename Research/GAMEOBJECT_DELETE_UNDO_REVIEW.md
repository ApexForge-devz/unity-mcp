# GameObject Deletion and Undo — Source Review

**Review date:** 2026-10-14  
**Target:** ApexForge-devz/unity-mcp, beta, commit 142ea0af4890337f0ff6658df61db23982313717  
**Upstream proposal:** [CoplayDev/unity-mcp PR #1135](https://github.com/CoplayDev/unity-mcp/pull/1135)  
**Observed PR state:** open, last updated 2026-07-04 in this snapshot.  
**Decision:** ADAPT the Undo-based deletion and regression test; expand validation for multi-object operations and state restoration.  
**Validation:** source/patch review only; Unity tests were not run in this session.

## 1. Executive conclusion

The current target deletes resolved GameObjects through Object.DestroyImmediate and explicitly comments that the action is not undoable. This is a concrete usability/safety gap for an Editor automation tool: an incorrect target match, misunderstood instruction or later workflow step can destroy scene objects without the normal Editor Undo recovery path.

Upstream PR #1135 changes the single call to Undo.DestroyObjectImmediate and adds an EditMode round-trip test: create an object, delete it through the MCP handler, verify it is gone, call Undo.PerformUndo and verify the object returns. This aligns with Unity's API contract: Undo.DestroyObjectImmediate destroys an object while recording the operation so it can be recreated; Undo.PerformUndo performs an Undo operation equivalent to the Edit → Undo command.

The patch is small and appropriate. It should be adopted after confirming the round-trip test passes in the project's supported Unity test matrix. The one-object test does not establish that multi-target deletion is grouped into one sensible Undo action, or that hierarchy, components, prefab-instance state and redo behavior are restored consistently.

## 2. Target source verified

Target [GameObjectDelete.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/GameObjects/GameObjectDelete.cs), blob **74382ef499f4f5bb31b6caf8124c6db40c2162e5**:

1. Finds target GameObjects through ManageGameObjectCommon.FindObjectsInternal.
2. Returns an error if no objects are found.
3. For each non-null target, records its name and instance ID, then calls Object.DestroyImmediate.
4. Returns a success result describing the deleted object(s).

The code comment says Undo.DestroyObjectImmediate did not work reliably in the project's test context and leaves a TODO to investigate Unity 2022+; this is why the source currently uses non-undoable destruction. The question is therefore not whether Undo is conceptually available—the official API says it records destroyed objects—but whether it behaves correctly through this actual MCP handler and the repository's test setup across supported Unity versions.

Target [ManageGameObjectDeleteTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ManageGameObjectDeleteTests.cs), blob **f3afc7d35df56b574cc1a84f025d3f103efe5388**, has tests for name/ID deletion, missing targets, inactive objects and duplicate names. The inspected baseline tests do not contain an Undo round-trip case.

## 3. What PR #1135 changes

The source diff replaces:

- Object.DestroyImmediate(targetGo)

with:

- Undo.DestroyObjectImmediate(targetGo)

The patch also adds an EditMode test in ManageGameObjectDeleteTests.cs. It creates a GameObject called UndoRoundtrip, invokes ManageGameObject.HandleCommand with action delete and searchMethod by_name, asserts the call succeeds and the object is no longer found, removes the destroyed reference from its cleanup list, performs Undo, verifies the object is restored, and adds the restored reference back to cleanup.

This directly addresses the stated baseline gap and uses the documented Unity workflow. It is stronger than merely checking that the code contains an Undo API call.

## 4. Official Unity API contract

- [Undo.DestroyObjectImmediate](https://docs.unity3d.com/ScriptReference/Undo.DestroyObjectImmediate.html) states that it destroys the object and records an Undo operation so it can be recreated, storing the destroyed object state in the Undo buffer.
- [Undo.PerformUndo](https://docs.unity3d.com/ScriptReference/Undo.PerformUndo.html) states that it performs an Undo operation equivalent to invoking Edit → Undo.
- Unity's [Undo API overview](https://docs.unity3d.com/ScriptReference/Undo.html) describes the Undo stack and says operations can be grouped automatically by Editor events; explicit grouping is available when needed.

These docs establish the intended semantic contract, but do not prove the target's handler works on every supported Unity version or for all GameObject forms. The repository's previous comment about test reliability is a real reason to run the round-trip rather than assume it.

## 5. Remaining test coverage gaps

### 5.1 Multiple targets and grouping

The target handler iterates a list of matching GameObjects and calls the Undo method once per target. The upstream test covers only one object. MCP may be asked to delete multiple objects in one tool operation; a user would usually expect a single Undo step to reverse the complete operation, not restore only the last object from the set.

Add a multi-target case that captures exact identities, deletes more than one object in one handler call, performs one Undo, and verifies every expected object is restored. Verify the resulting Undo group behavior; if one Undo does not restore the operation as a unit, explicitly group the mutation and give the group a useful name. Do not add explicit grouping without testing the actual Unity behavior first.

### 5.2 Restored state, not only object name

The upstream test verifies a root can be found by name. Extend the fixture with:
- a parent/child hierarchy;
- at least one component with a serialized value;
- a non-default transform and sibling/parent relationship;
- optionally a prefab instance where the supported test project can create one reproducibly.

After Undo, verify the hierarchy, component/value state and relationship, not only root existence. This makes sure the tool's destructive operation preserves enough state to be a practical recovery mechanism.

### 5.3 Redo and cleanup isolation

After confirming restoration, call Undo.PerformRedo and verify deletion is replayed. Then clean up the Undo stack and object fixture in an order that does not leave an unrelated action to affect later tests. Ensure failed lookup/failed deletion creates no unexpected Undo entry. Avoid broad Undo.ClearAll calls that could wipe another test's actions; scope/guard cleanup to the operations owned by the fixture.

### 5.4 Version and context matrix

The code comment specifically raises Unity 2022+ behavior. Run the same real handler → Undo round trip under the repository's supported Unity versions, both as a direct EditMode test and, if the integration suite supports it, as a client-triggered MCP operation. Distinguish:
- a pure API call proving a deleted object can be recreated;
- the actual handler path;
- end-to-end client routing and result reporting.

A static source check or passing unit test in one Editor version is not proof across the full matrix.

## 6. Correctness and product considerations

- The operation should still return the deleted object's pre-delete name and instance ID. After Undo, Unity may reconstruct an object with a new runtime instance ID; do not imply the old ID is guaranteed stable after restoration.
- Do not automatically make all project-file deletions undoable by analogy. This review concerns scene/GameObject destruction specifically; asset deletion has different AssetDatabase and file-system semantics.
- Record an Undo group name such as “Delete GameObject(s)” if grouping needs to be explicit and tested. The Undo group should represent one user-visible MCP operation.
- Preserve existing target matching behavior; Undo does not correct a wrong target selector. The MCP tool should continue to return clear target/result details.
- The change does not add confirmation prompts or authorization. Those controls remain separate decisions for destructive operations.

## 7. Recommended decision

**ADAPT / high-confidence candidate:** change deletion to Undo.DestroyObjectImmediate and merge only after the regression test runs successfully in Unity. Keep the patch narrow. Add multi-object grouping, hierarchy/component restoration and redo cases before asserting the operation is fully Undo-safe. At minimum, the one-object round-trip should pass on the supported Unity 2022+ and Unity 6 configurations.

## Acceptance matrix

| Scenario | Expected result |
|---|---|
| Delete one scene GameObject | Object disappears and MCP reports success. |
| Undo after deletion | Object is recreated with preserved expected name/state. |
| Delete multiple objects in one call | A single Undo restores the entire operation, or the product explicitly documents otherwise. |
| Hierarchy and components | Parent/child links, relevant serialized fields and transforms are restored. |
| Redo after Undo | Deleted state is reapplied. |
| Missing target | Error result; no new Undo deletion record. |
| Test cleanup | Restored objects are cleaned up and do not leak into later tests. |
| Supported Unity versions | Same handler-level assertions pass in each supported Editor; any known version limitation is documented. |
| MCP client invocation | Correct scene/Editor instance is mutated and reported result matches the actual post-state. |

## Sources

- Target [GameObjectDelete.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/GameObjects/GameObjectDelete.cs), blob **74382ef499f4f5bb31b6caf8124c6db40c2162e5**.
- Target [ManageGameObjectDeleteTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ManageGameObjectDeleteTests.cs), blob **f3afc7d35df56b574cc1a84f025d3f103efe5388**.
- Upstream [PR #1135](https://github.com/CoplayDev/unity-mcp/pull/1135).
- Unity [Undo.DestroyObjectImmediate](https://docs.unity3d.com/ScriptReference/Undo.DestroyObjectImmediate.html).
- Unity [Undo.PerformUndo](https://docs.unity3d.com/ScriptReference/Undo.PerformUndo.html).
- Unity [Undo API overview](https://docs.unity3d.com/ScriptReference/Undo.html).
