# Existing Scene Visual Validation — Source Audit

**Reviewed:** 2026-10-14  
**Target:** `ApexForge-devz/unity-mcp`, branch `beta`  
**Decision:** Use existing `manage_scene` visual-capture actions in the deterministic generator workflow; do not add a parallel screenshot tool before testing these paths.  
**Method:** Source and test inspection only. No Unity Editor or test suite was run during this review.

## Executive conclusion

The repository already has a fairly capable visual-validation surface. `manage_scene` supports inline screenshots, Scene View viewport capture, camera-positioned screenshots, six-angle surround contact sheets and configurable orbit contact sheets. It can frame the Scene View on an explicit target and return dimensions/capture metadata. `manage_camera.screenshot` and `screenshot_multiview` delegate to the same infrastructure.

This means the script-first workflow does not need a new screenshot integration as its first step. After regeneration, open/activate the intended output scene, capture one inline image, then request a small multi-angle set only when composition/occlusion needs inspection. If the scene visually fails, fix the generator and regenerate; do not mutate generated objects to patch the image.

The audit did identify request-bound gaps worth fixing before exposing these actions to unconstrained high-resolution or large-orbit requests.

## 1. Current capability map

### `manage_scene.screenshot`

The dispatch/schema in [`ManageScene.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageScene.cs) supports:

- `capture_source: "game_view"` (default) or `"scene_view"`.
- `include_image: true` for an inline base64 PNG, and `max_resolution` to reduce the inline payload's longest edge (default utility fallback is 640 pixels).
- A specific camera by name/path/instance ID.
- `view_target`, `view_position` and `view_rotation` for a temporary positioned camera.
- `batch: "surround"` for six viewpoints, or `batch: "orbit"` for a configurable contact sheet.
- `output_folder` for saved screenshots, with project-root-constrained folder resolution.

Scene View capture is a viewport screenshot: it requires a non-batch Editor with an open Scene View. It can frame a target before capture, but does not support a custom camera, `super_size > 1`, batch modes, or explicit view position/rotation. The separate `scene_view_frame` action can frame the Scene View on a target object or aggregate scene renderer bounds.

### Capture modes are not interchangeable

- **Game View, Play Mode, `include_image: true`:** requests a composited capture after the end of frame. This is the best first check for actual on-screen output when it succeeds.
- **Camera-selected / Edit Mode capture:** renders through the selected or fallback camera. Useful for inspecting generated geometry/material composition when the game is not running, but it may not include Screen Space Overlay canvases or UI Toolkit overlays.
- **Reported camera fallback:** if the composited capture cannot obtain a frame (e.g. batch mode, paused/unfocused Editor), the utility can return a camera render instead. The response includes `fallbackReason` and `captureSource: "camera_fallback"`; that image must not be mistaken for a faithful composited Game View.
- **Scene View capture:** shows the current Editor viewport rather than final player output. Use it for layout/composition inspection; use Game View/real Play Mode when judging gameplay presentation.
- **Surround/orbit batches:** return a contact-sheet image as inline base64 with per-view metadata; the reviewed methods do not save that contact sheet to disk, even though they also return an output-folder field. Treat the inline image as the result, not that field as proof a contact-sheet file exists.

The target [`ScreenshotUtility.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Runtime/Helpers/ScreenshotUtility.cs) constrains screenshot output folders to the project root and sanitizes filenames. For output under `Assets/`, the Editor imports the captured file.

## 2. Recommended generator-validation sequence

1. Confirm the intended output scene is open/active. Target-object framing resolves against the active scene.
2. Run generator-owned assertions and return the concise result before visual inspection.
3. Request one image first. For final in-game presentation, enter Play Mode and request `manage_scene.screenshot` with `capture_source: "game_view"` and `include_image: true`; inspect capture-source/fallback metadata before trusting the result.
4. For Edit Mode layout checks, use `capture_source: "scene_view"` with `view_target`, or use a positioned camera / `batch: "surround"` for a compact contact sheet.
5. If the failure is specific—wrong shader/pass, render target, odd clear/blit, missing rendered object—use a targeted query or the existing `manage_profiler` Frame Debugger actions. See [`FORK_PASHARA_FRAME_DEBUGGER_REVIEW.md`](FORK_PASHARA_FRAME_DEBUGGER_REVIEW.md) only for capture-consistency features the target lacks.
6. Correct the generator source and regenerate. Do not use per-object/component mutations to patch an incorrect generated image.

**Important:** the default non-inline `ScreenCapture.CaptureScreenshot` path is asynchronous and may return `isAsync: true` before the file is ready. If the next step requires image inspection, request `include_image: true` or use a bounded file-readiness check. A successful “screenshot requested” response is not proof that image bytes are already available.

## 3. Request-bound and correctness gaps

### 3.1 `super_size` has no clear upper bound in the inspected source

`PrepareCaptureResult` normalizes `super_size` with `Mathf.Max(1, superSize)`, but does not impose an upper maximum. Camera capture multiplies source width/height by this value before allocating a RenderTexture and Texture2D. `max_resolution` constrains the inline downscaled preview; it does not bound the full-resolution screenshot render/write. Very large requests can cause huge allocations, integer overflow, GPU allocation failure or Editor memory pressure.

**Recommendation:** use checked dimension arithmetic before allocations and reject captures exceeding a documented max pixel/byte budget. If high-resolution captures are supported, clamp dimensions or downsample in bounded steps rather than allocating the full supersize source by default.

### 3.2 Orbit capture has unbounded elevation count / total shots

The orbit code clamps `orbit_angles` to 1–36 and clamps FOV, but accepts the caller-supplied `orbit_elevations` array without a corresponding maximum count. Total tile count is azimuth count multiplied by elevation count. Contact-sheet composition then allocates a combined Texture2D and a full `Color32[sheetWidth * sheetHeight]` buffer. An excessive array can cause large synchronous render and memory work; an empty elevation list reaches the compositor's “no tiles” error only after setup.

**Recommendation:** cap total shots and contact-sheet pixels; reject empty, oversized and non-finite elevation arrays; validate `orbit_distance` / dimensions before creating the temporary camera or tile textures. Keep errors structured and reject invalid requests early.

### 3.3 Inline preview size is not a full memory budget

Downscaling base64 keeps the response smaller, but camera capture may still render and encode the larger source image for disk before downscaling the inline copy. Separate (a) maximum source dimensions/pixels, (b) disk-output policy and (c) inline response size. Bound base64 response bytes as well as GPU/CPU allocations.

### 3.4 Contact-sheet response contract is ambiguous

The surround/orbit methods say the contact sheet is not saved to disk but return `imageBase64` plus `outputFolder` metadata. That folder value must not imply that the contact sheet was written there. Remove/rename the unused field, or add an explicit opt-in save operation using the ordinary project's safe path and unique-filename rules.

## 4. Existing tests

- [`ScreenshotCapturerTests.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Helpers/ScreenshotCapturerTests.cs) verifies that a no-end-of-frame timeout and external destruction complete the pending callback without leaking the hidden capturer. Its batch-mode case expects immediate camera fallback plus a reported reason when graphics are available.
- [`ScreenshotUtilityDownscaleTests.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ScreenshotUtilityDownscaleTests.cs) covers linear/sRGB downscale fidelity and the no-upscale rule.
- The camera-render path restores the camera's previous `targetTexture`, the previously active RenderTexture and temporary texture resources.

These tests exercise useful regressions and lifecycle failures, but do not by themselves validate visual parity across the project's real URP pipeline, every Editor focus state, or the extreme request sizes identified above. No tests were run during this research pass.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Inline Game View capture in Play Mode | Returns image bytes, dimensions and capture source; camera fallback is distinguishable from composited capture. |
| Paused/unfocused Editor or batch mode | Returns bounded fallback or actionable failure with explicit fallback reason. |
| Scene View capture with target | Frames the intended active-scene target and reports viewport dimensions/source. |
| Wrong active scene | Target mismatch is rejected or clearly surfaced rather than silently inspecting a different scene. |
| Excessive `super_size` | Rejected/clamped before large allocations using checked dimensions and a pixel budget. |
| Orbit elevation array empty / huge / non-finite | Rejected before camera creation or tile rendering. |
| Maximum orbit workload | Total images, contact-sheet pixels and output/base64 bytes stay below documented limits. |
| Repeated capture / render exception | Temporary camera, textures and capturer objects are cleaned; previous camera/RenderTexture state is restored. |
| Contact-sheet metadata | No disk-file claim unless a file is actually written and verified. |
| Screenshot saved under Assets | Project-relative path is correct and AssetDatabase import is complete or reported pending. |

## Source references

- Target [`ManageScene.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageScene.cs)
- Target [`ScreenshotUtility.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Runtime/Helpers/ScreenshotUtility.cs)
- Target [`EditorWindowScreenshotUtility.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Helpers/EditorWindowScreenshotUtility.cs)
- Target [`ScreenshotCapturerTests.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Helpers/ScreenshotCapturerTests.cs)
- Target [`ScreenshotUtilityDownscaleTests.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ScreenshotUtilityDownscaleTests.cs)
