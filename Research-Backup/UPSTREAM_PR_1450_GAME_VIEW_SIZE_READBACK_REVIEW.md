# Upstream PR #1450 — Fixed Game View Resolution with Render-Target Readback

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1450](https://github.com/CoplayDev/unity-mcp/pull/1450)  
**Target baseline:** beta 142ea0af4890337f0ff6658df61db23982313717  
**Candidate head:** 1596ea4069c8ae2db73563774c0e405800b5ebb4  
**Status:** Open, not merged  
**Decision:** **ADAPT selectively as an explicit visual-QA capability; not part of default regeneration.**  
**Review type:** Source and test inspection. No Unity Editor/tests were run independently.

## Executive assessment

PR #1450 adds `get_game_view_size` and `set_game_view_size` actions to `manage_editor`, with matching CLI support. The useful distinction is that the response reports the **selected preset**, Unity's computed target size and the **allocated RenderTexture dimensions** separately. A requested fixed resolution is not considered successful solely because Unity selected a preset with the desired label/dimensions.

For an agent validating a generated scene, a stable Game View resolution makes before/after screenshots more comparable, particularly for UI/layout, portrait/mobile composition and camera framing. The existing `manage_scene.screenshot` tool can capture screenshots but does not itself guarantee that the Game View's allocated target matches a requested width/height.

The feature should be an explicit opt-in step in visual QA. It is not necessary for ordinary scene generation, and should not run automatically for every regeneration because it changes shared Editor preset state.

## 1. What the candidate implements

The proposed actions are added to the existing `manage_editor` tool rather than creating another top-level MCP tool.

### Read and set contract

- `get_game_view_size` reports the selected preset, mode, platform group, Unity-computed target size, allocated render-target size, and whether the target/render sizes are settled.
- `set_game_view_size` requires integer width and height from 10 to 8192.
- A graphical Game View must already be open. If a Game View is focused, it is selected; if exactly one is open, the helper can use it. No Game View, multiple unfocused Game Views or batch mode produce explicit errors instead of opening a window or taking focus.
- The helper selects/reuses one MCP-owned preset per platform group. Ownership is recorded in EditorPrefs by exact name and dimensions. If the recorded preset was changed by the user, it is not overwritten merely because it shares a name/prefix; a new owned preset can be created.
- After selecting the preset, the helper waits for two settled Editor updates. It times out after five seconds, reports observed requested/target/render sizes and marks the preset as potentially changed.
- If the Editor reloads/quits, the selected view closes, another preset is selected, or the user edits the preset during the wait, the response is an explicit interrupted/changed result rather than a false success.
- Unity-version-dependent private Game View APIs are isolated in `GameViewSizeControl.GameViewApi`. Missing members produce an explicit unsupported/unavailable error instead of silent fallback.

## 2. Why this is useful for the script-first workflow

Use it only when resolution differences would confound a visual comparison:

1. Regenerate and save the scene using the authoritative generator script.
2. Open/focus the intended Game View and call `set_game_view_size` with a stable resolution used for the validation profile.
3. Inspect the returned `requested_size`, `target_size`, `render_size`, `settled` and `matches_requested`. Stop or label the capture inconclusive if the allocation does not match.
4. Capture using the existing `manage_scene.screenshot` pathway, and record the size/capture-source metadata with the screenshot.
5. Repeat the same resolution when comparing before/after output. Fix the generator, not generated objects, if visual assertions fail.

This is a deterministic **capture setup** aid, not a generator input and not a guarantee that the captured pixels are fresh. The helper verifies allocation dimensions; it does not read back or validate the actual screenshot pixels. Screenshot capture must still complete successfully, and the response must distinguish a composited Game View from a camera fallback.

## 3. Risks and adoption constraints

### 3.1 Undocumented internal API compatibility

The helper reflects into `UnityEditor.GameView`, `GameViewSizes`, `GameViewSizeGroup`, `GameViewSize`, `PlayModeView`, private fields and methods. Those are internal/undocumented APIs and can change across Unity versions.

The code resolves the full API before changing a preset and reports an explicit unsupported error when a required member is missing. Keep that isolation and add actual Editor validation for every Unity version the target promises to support. Compile checks against reference assemblies on several platforms are not equivalent to graphical Editor runtime coverage.

### 3.2 Shared persistent preset state

Unity's custom Game View presets are saved in shared Editor preferences, across projects. The helper deliberately persists an MCP-owned preset and its ownership metadata, and updates the owned preset when requested dimensions change. This is more careful than overwriting arbitrary presets, but it is still a persistent Editor preference change.

Do not automatically change the user's Game View resolution during normal generator runs. Make the action explicit, identify it as a persistent preset change in tool descriptions, preserve other/user-edited presets, and let the caller inspect readback after setting it. If the action times out or is interrupted, do not assume it reverted: the error explicitly says the preset may have changed.

### 3.3 Requested dimensions are not render-size guarantees

The candidate caps each dimension at 8192 and reads the actual allocated RenderTexture. GPU limits or Unity's allocation behavior may reduce the dimensions. The response correctly distinguishes `selected_size`, `target_size` and `render_size`, but callers must use `render_size` (plus `settled` and `matches_requested`) when determining whether a fixed-size visual comparison is valid.

The upper bound still permits an 8192 × 8192 target (~67 million pixels). At four bytes per pixel, one uncompressed color buffer is approximately 256 MiB before additional render buffers/intermediates. That can be undesirable on lower-VRAM machines. Preserve the hard bound and consider an optional stricter default/pixel budget if agents are likely to issue very large dimensions. A 2400 × 1080 or 1080 × 1920 baseline is usually sufficient for UI/layout checks.

### 3.4 Timeout leaves a possible partial state change

The preset is selected before the readback wait. If the Game View is hidden/unrepainted, the allocation may not settle before timeout; the selected preset may already have changed. This is an honest API contract and should not be papered over by blind retry.

After a timeout, read the current Game View size before issuing another set request; make the view visible and verify the intended target. Do not claim the previous setting was restored.

### 3.5 This is sizing, not pixel-readback or screenshot freshness

The PR title mentions “render readback,” but the helper's readback is of **render-target dimensions**, not a CPU readback/comparison of image pixels. It does not prove that a newly rendered frame has arrived or that the screenshot will contain updated scene content. Pair the API with the existing screenshot flow and use a separate readiness/frame-completion signal when pixel freshness matters.

## 4. Candidate tests and their limits

The candidate includes Python/CLI validation tests and graphical EditMode tests for:
- invalid dimension types/ranges failing before editor access;
- missing or ambiguous Game View failures;
- preset reuse/update without growing duplicates;
- preservation of built-in, unrelated and user-edited presets;
- unsupported internal API member errors;
- real render-target readback for landscape and portrait dimensions;
- no focus theft;
- GPU-clamped/readback mismatch and five-second timeout cases;
- user edits or view closure while waiting;
- cleanup/restoration of test fixture preferences and preset data.

The PR author reports 18 focused Game View EditMode tests passed in Unity 6000.3.24f1 on graphical macOS/Metal; 1,613 Python tests passed with 18 skipped; and compile checks against Windows/macOS/Linux symbols. These are author-reported results, not independently run in this review. The author explicitly notes there was no PlayMode test and that reference-assembly compilation is not runtime coverage across OSes/Unity versions.

## Recommended decision

**ADAPT selectively at P2 for explicit visual-QA workflows.** The readback contract is materially better than assuming a selected preset equals a valid render resolution. Keep setting a fixed resolution optional and visible to the agent/operator; never put it in the automatic scene-regeneration loop. Retain the target/readback metadata, timeout/partial-change semantics, ownership safeguards and focus preservation. Validate the internal API on the actual supported Unity Editor matrix before treating the feature as stable.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Resolution requested and allocation settles | Response reports requested/selected/target/render sizes and only marks match when actual allocation is verified. |
| GPU/Unity clamps render size | Response is explicit about mismatch; caller does not proceed as though resolution matched. |
| Hidden/unrepainted Game View times out | Response reports timeout and that the preset may have changed; no false success or assumed rollback. |
| Multiple Game Views without focus | Request is rejected as ambiguous; no window is opened or focused. |
| One Game View or a focused view | Correct window is selected without changing focus. |
| Existing user's preset matches dimensions/name but is not recorded as owned | Existing preset remains untouched; new owned preset is created. |
| User-edited MCP preset | The edit is preserved; the helper does not overwrite based on prefix alone. |
| Editor quits/reloads or selected view changes during wait | Wait terminates with explicit interrupted/changed status and releases the busy guard. |
| Unity internal API differs | Explicit unsupported error with Unity version; no silent fallback. |
| Very large dimensions | Input is bounded and workload is documented; optional lower pixel budget is considered for low-VRAM agents. |
| Screenshot follows fixed sizing | Screenshot capture source and completion are verified separately; size readback is not treated as pixel freshness. |
| Ordinary regeneration without visual QA | No Game View preset change is made implicitly. |

## Source references

- [PR #1450](https://github.com/CoplayDev/unity-mcp/pull/1450)
- Candidate [GameViewSizeControl.cs](https://github.com/CoplayDev/unity-mcp/blob/1596ea4069c8ae2db73563774c0e405800b5ebb4/MCPForUnity/Editor/Helpers/GameViewSizeControl.cs)
- Candidate [GameViewSizeControlTests.cs](https://github.com/CoplayDev/unity-mcp/blob/1596ea4069c8ae2db73563774c0e405800b5ebb4/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/GameViewSizeControlTests.cs)
- Candidate [ManageEditor.cs](https://github.com/CoplayDev/unity-mcp/blob/1596ea4069c8ae2db73563774c0e405800b5ebb4/MCPForUnity/Editor/Tools/ManageEditor.cs)
- Target [ManageScene.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageScene.cs)
- Target [ScreenshotUtility.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Runtime/Helpers/ScreenshotUtility.cs)
