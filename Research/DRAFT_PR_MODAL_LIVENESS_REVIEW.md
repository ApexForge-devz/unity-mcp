# Draft PR #1 — Modal Liveness, Answering and Scene Guard Review

**Target PR:** [ApexForge-devz/unity-mcp #1](https://github.com/ApexForge-devz/unity-mcp/pull/1)  
**Reviewed head:** 9480a79408422c95c17c08ed4efb0b66d24a1f24  
**Review date:** 2026-10-14  
**Scope:** Static C# and Python source/test review. This is not a live Windows Unity test or security sign-off.

## 1. Source reviewed

- [ModalDialogProbe.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Helpers/ModalDialogProbe.cs), blob 0155932c087a341a21d1b47ce55e4c4a64d5f6c0.
- [MainThreadHeartbeat.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Services/MainThreadHeartbeat.cs), blob 3d600ebd3d06da513afbf2dd95c7db66a11ba737.
- [EditorLivenessProbe.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Services/EditorLivenessProbe.cs), blob fc658eedc16f587cd4f29343a5db93b0bd38a32f.
- [TransportCommandDispatcher.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Services/Transport/TransportCommandDispatcher.cs), blob 3873e88d94cd81f4d3526b4a4d074465d690b367.
- [SceneExternalChangeGuard.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/MCPForUnity/Editor/Helpers/SceneExternalChangeGuard.cs), blob 0775f2d5e5db8f278b124cd784edcb6434c893bd.
- [answer_dialog.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/src/services/tools/answer_dialog.py), blob d8408def22f83ae724fa19498a97bfabc94ea62c.
- Tests: [ModalDialogLivenessTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/TestProjects/UnityMCPTests/Assets/Tests/EditMode/ModalDialogLivenessTests.cs), [SceneExternalChangeGuardTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Helpers/SceneExternalChangeGuardTests.cs), [TransportCommandDispatcherQueueTests.cs](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/TestProjects/UnityMCPTests/Assets/Tests/EditMode/TransportCommandDispatcherQueueTests.cs), and [test_modal_dialog_detection.py](https://github.com/ApexForge-devz/unity-mcp/blob/feature/unity6-enhancements/Server/tests/test_modal_dialog_detection.py).

## 2. Sound design choices

1. **Liveness reads do not wait behind the ordinary main-thread queue.** The heartbeat is only written from EditorApplication.update, so a background response does not falsely prove the Editor thread is alive.
2. **Modal scanning runs on a dedicated sampler thread and exposes a snapshot age.** The request path returns the last known snapshot instead of performing synchronous window-text reads that could block the very liveness response being requested.
3. **Native text reads use SendMessageTimeout and there are explicit duration/window/child caps.** If a native modal is identified but labels cannot be read completely inside the budget, it is marked non-answerable rather than guessing a button.
4. **A modal is only programmatically answerable when a native dialog with buttons was discovered.** Unity-drawn EditorWindow.ShowModal is reported as blocking but not answerable; an unidentifiable modal is also non-answerable.
5. **Answering is serialized and re-identifies the active dialog.** The code confirms the expected title when supplied, selects an exact case-insensitive button label after accelerator stripping, uses PostMessage, and verifies the original window disappears before claiming success.
6. **Normal commands remain serialized on the Unity main thread.** The queue tests check FIFO/no overlap and verify that cancelling the caller does not release the active command slot before the handler completes.
7. **The scene guard distinguishes auto, reload and keep_editor modes.** It blocks ambiguous multi-scene reloads, records successful overwrites, and refuses refresh if saving a scene fails.

These choices are valuable starting points and should be retained unless a reproducer shows a specific flaw.

## 3. High-priority review items

### 3.1 Bind the answer to the exact observed dialog, not just an optional title

The Python API currently makes expect_title optional. On answer, the Editor takes a fresh snapshot; when the expected title is blank/null, it will click a button with that label on whatever answerable modal is currently present.

That is not guaranteed to be the same dialog the model previously inspected. A modal can close and another dialog with a generic button such as “OK”, “Cancel” or “Ignore” can appear between read and answer. A matching title alone can also be ambiguous if two dialogs share the same title.

**Recommendation:** require an expected title for every button press at minimum, and preferably return an opaque short-lived dialog identity bound to window handle plus observed title/button set and a nonce. At answer time, validate the current dialog against that identity and exact button set. Keep the identity short-lived and tied to the same Unity instance; do not assume a raw OS window handle alone is a durable identity.

**Tests:** missing expected identity is rejected; modal changes after reading; same title/different buttons; same generic button on a second modal; stale handle; duplicate answer requests; dialog closes before PostMessage; click queues but original dialog remains open; a second modal appears immediately after the first closes.

### 3.2 Scene external-change detection relies on file modification time alone

SceneExternalChangeGuard records File.GetLastWriteTimeUtc ticks and detects changes when disk mtime is strictly greater than the recorded baseline. This may miss an external rewrite when a tool preserves the timestamp, the filesystem's timestamp resolution collapses rapid writes, or a checkout/restore produces an equal or older timestamp. When no change is detected, RecordBaseline refreshes current timestamps; if a content change had same/older mtime, its evidence may be erased.

This is a **robustness hypothesis derived from the code path**, not a reproduced defect in the PR.

**Recommendation:** store a lightweight file signature such as mtime + length, and use a content hash when those fields are unchanged but reliable detection is required. Hashing scene YAML can cost time on very large scenes, so perform it only for known open-scene paths and consider a size threshold. Keep a conservative unknown state on read/permission failures; don't silently turn an inaccessible file into a clean baseline.

**Tests:** rewrite with unchanged mtime, write twice within coarse timestamp resolution, restore an older mtime, same-size content replacement, a file becoming unreadable/disappearing, multiple open scenes with one change and a baseline update for another scene. Confirm no unrelated baseline entry is advanced when a changed scene remains unreconciled.

### 3.3 A successful click requires stronger identity/intent than “the HWND disappeared”

The probe correctly checks IsWindow(handle) after a button click. The window disappearing is strong evidence but does not by itself prove the intended state transition: a user or another action could close it at the same time, and a new modal might immediately appear. The response should be explicit that success means “the identified original modal closed after the queued button click,” not “the underlying operation succeeded.”

**Recommendation:** retain the original dialog identity throughout verification, return both original-dialog-closed and any-next-modal-present state when practical, and never infer that the underlying operation (scene reload, import, overwrite) finished successfully merely because the prompt closed. Return recovery instructions to inspect Unity's resulting state.

### 3.4 Queue growth and shutdown should remain bounded under a sustained modal stall

The dispatcher holds pending command JSON strings until the main thread can process them. The visible implementation uses a dictionary and FIFO queue but no obvious hard admission cap in the reviewed section. Caller cancellation removes queued work, and the server uses timeouts; nevertheless, a stream of incoming requests can accumulate before those timeouts/cancellations take effect.

**Recommendation:** add a max pending count/serialized-byte budget and return a structured backpressure response before storing oversized or excess requests. Keep one active command semantics; never execute a timed-out active mutation concurrently with the next command just because its caller has cancelled. Record queued count/oldest age without storing command body contents in logs.

**Tests:** queue cap by count and bytes; burst while a modal is open; queued request cancellation removes storage; active request cancellation leaves the slot locked until handler completion; dispose/cancellation registrations on every path; domain reload/shutdown with pending work; no unbounded queue retained after requests time out.

## 4. Test coverage versus what still needs a live Editor

The added EditMode tests exercise the decision logic and routing around liveness. They do **not** actually hold the Editor behind a real native Win32 modal; the test comments explicitly explain why the blocked case cannot be driven from the same Editor test runner. The server-side tests use canned liveness payloads. Therefore:
- a passing unit test cannot prove the modal can be found through Windows window ownership under the actual Unity version;
- it cannot prove that PostMessage(BM_CLICK) closes real DisplayDialog boxes in the target Editor build;
- it cannot prove that Unity-drawn ShowModal is consistently classified as non-answerable;
- it cannot prove thread shutdown/domain reload behaves safely while a Windows call is timing out.

The target PR #1 CI snapshot had compile success, but the actual Unity test job and E2E bridge smoke job were skipped due missing license secrets. **Live Windows Editor + bridge tests remain a merge gate for this feature.**

Recommended manual/automated Windows matrix:
1. Unity EditorUtility.DisplayDialog with one, two and three buttons.
2. Titles/body/button labels with Unicode, accelerator ampersands, empty labels and long text.
3. Unity EditorWindow.ShowModal and unknown native windows.
4. Two nested/rapid sequential dialogs; click closes one and opens another.
5. Editor import/scene-save dialog while another MCP request times out.
6. Domain reload and Editor quit while sampler is active; confirm bounded join and no old-domain sampler remains.
7. Non-Windows platform reports unsupported/unknown instead of all-clear.
8. Server timeout during answer; a retry must not click an unrelated future dialog.

## 5. Scene reconciliation behavior that should be called out in tool documentation

The default auto mode reloads a single changed clean scene from disk, but blocks if the changed scene is dirty or if multiple scenes are open; keep_editor explicitly saves the in-memory scene over disk, and reload explicitly accepts the disk version where it can do so safely. These are destructive/conflict-resolution choices, not transparent housekeeping.

The tool/result schema should clearly list affected scene paths, whether the in-memory or on-disk version wins, which files were overwritten/reloaded, and whether no refresh occurred because a conflict was blocked. Require explicit operator/model intent before selecting reload or keep_editor when unsaved work could be lost. Don't turn mtime mismatch alone into a silent overwrite.

## 6. Interim conclusion

**Assessment: potentially valuable recovery feature, high-risk until live Windows validation.** The approach correctly separates a busy Editor from a modal-blocked Editor and tries to avoid unsafe guesses. Before merge, tighten answer identity, test mtime-preserving external edits, bound request queue admission, and run a real Win32 modal lifecycle test on supported Windows Unity. Compile success and simulated EditMode tests are necessary but insufficient.
